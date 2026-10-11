## Context

See `proposal.md` for the motivation. What the code does today (`src/UrDeck.Host`):

- `MonitorPlacement.GetMonitors` enumerates with `EnumDisplayMonitors` / `GetMonitorInfo` and knows a display only by
  its GDI device name (`\\.\DISPLAY1`), bounds and the primary flag.
- `MonitorPlacement.Select` evaluates `monitorName` / `monitor` against whatever is connected, on every call, and
  falls back to the primary monitor.
- `MainWindow` calls `Retarget` on `SystemEvents.DisplaySettingsChanged`, on a power resume and on a config reload,
  and repeats it after 1.5 s and 5 s. After a resume each of the three forces a page rebuild. `XamlRoot.Changed`
  covers the window again. `RebuildLayout` contains a one-shot "nudge" (resize by a pixel) for the case where XAML
  keeps the content size of the old scaling.
- `HostContext.Target` is a non-nullable monitor; `App.OnLaunched` always activates the window.
- There is no host test project. `tests/UrDeck.Engine.Tests` is where logic is tested, with a line-coverage floor.

Measured on the owner's PC (2026-10-11), which shapes the identity decision:

| Display | Name | Model | Serial | Device path |
|---|---|---|---|---|
| Y70 panel, 1100x3840 | `HYTE Y70Touch` | `RTD1100` | `1` | `\\?\DISPLAY#RTD1100#5&abf4c77&0&UID4353#{e6f07b5f-...}` |
| Primary, 3840x2160 | `LG ULTRAGEAR` | `GSM5CE2` | `509RMQKMM844` | `\\?\DISPLAY#GSM5CE2#5&abf4c77&0&UID4356#{...}` |
| Side, 1080x1920 portrait | `HP Z23n G2` | `HPN347B` | `6CM7360YKH` | `\\?\DISPLAY#HPN347B#5&abf4c77&0&UID4355#{...}` |

The panel has a usable name and model and a serial number that identifies nothing. The side monitor is portrait, so
`tallest` selects it whenever the panel is missing: the failure the proposal describes is real on this machine.

## Goals / Non-Goals

**Goals:**

- The deck follows one physical display through restarts, re-enumeration, sleep and a change of output.
- Placement after a display change is checked, not assumed, and costs one page rebuild.
- The selection and convergence logic is testable without a monitor, with scripted display sequences.
- The configuration shape is one the picker in `settings-window` can write without a migration.

**Non-Goals:**

- Any user interface (picker, Identify, a dialog when the display is missing).
- A "fall back to primary while absent" option. Nobody has asked for it; the picker change can add it with the UI
  that explains it.
- Telling two identical displays apart beyond their device path. See risks.
- Pausing while the display is merely asleep or the session is locked (backlog item 15). Only a display that is no
  longer connected hides the deck.
- Changing how a page is built, or the SDK.

## Decisions

### 1. Identity is the device path, with the model code as the fallback

A display is remembered as `{ name, model, path }`. Matching is: same path, else the only connected display with the
same model, else (several with that model) the first by path order with a warning, else absent. A match by model
re-pins the path.

- The **path** (the monitor's device interface path) is unique per display and stable across restarts and
  re-enumeration, which is what `DISPLAYn` is not. It contains the output's id (`UID4353`), so it changes when the
  cable moves to another output or the graphics card is replaced.
- The **model** (EDID manufacturer and product, `RTD1100`) survives that move. It is what makes "plugged into the
  other port" not look like "absent".
- The **name** is written for whoever reads the file and the log. It is never matched.

*Alternatives considered.* The EDID serial as part of the key: the panel's is `1`, so it adds a registry or WMI
read and distinguishes nothing on the hardware this is built for. Model only: two identical displays (a likely setup
for side panels) would swap at random. Path only: moving the cable would hide the deck with no explanation. The draft's
tie-break by previous position and size: it needs the position persisted, and position is exactly what changes when
displays re-enumerate; left out until somebody has two identical panels.

### 2. Selectors choose, the host remembers, and `selector` detects an edit

`display.selector` records what the choice was made from (`tallest`, `display1`, or `index:1` when only `monitor`
applies). On every evaluation the host computes the current selector string from the configuration:

- no `display` → choose with the selectors, write `display`;
- `display.selector` present and different from the current one → the user edited the selectors: choose again, rewrite;
- `display.selector` absent → the display was picked explicitly (the future picker): never re-chosen from selectors.

*Why not keep evaluating the selector live.* That is today's behaviour and the cause of the jump to the side monitor:
a selector describes a property of the set of displays, and the set changes during every wake. *Why not drop the
selectors.* They are how a hand-written or default configuration says which display to use before any UI exists, and
existing files use them. *Why record the selector instead of comparing with the last loaded value.* The edit may happen
while the host is not running.

### 3. Enumeration moves to the display configuration API

`QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)` with `DisplayConfigGetDeviceInfo` gives, per active display, the GDI
device name (source name), and the friendly name, EDID manufacturer and product ids and device path (target name).
These are joined to the existing `EnumDisplayMonitors` results by GDI device name, which keeps bounds and the primary
flag from the API the host already trusts for physical pixels. `GetDpiForMonitor(MDT_EFFECTIVE_DPI)` adds the scaling.

A display the join finds no target for (a remote session, an indirect display driver) keeps its GDI device name as its
path and an empty model, so the host still works there; it just has no better identity than today.

*Alternative:* `EnumDisplayDevices` with `EDD_GET_DEVICE_INTERFACE_NAME` returns the same path with less code, but its
monitor string is "Generic PnP Monitor" for all three displays here; the name would need WMI.

### 4. The logic lives in the engine, behind a seam; Win32 stays in the host

New folder `src/UrDeck.Engine/Display`:

- `DisplayInfo` (name, model, path, device name, bounds, scaling, primary) and `RememberedDisplay` (the config object).
- `DisplaySelector`: the selector rules moved from `MonitorPlacement.Select`, the matching order of decision 1, and
  the "choose again?" rule of decision 2. Pure functions over a list of `DisplayInfo`. Result: a display plus what to
  write back, or absent.
- `PlacementController`: the state machine of decision 5. It talks to an `IDisplayEnvironment` (enumerate displays;
  read the window's content bounds, scaling and laid-out size; cover a rectangle, with or without the nudge; show;
  hide) and reports to its owner through callbacks (rebuild the page, release the page, save the remembered display,
  state changed). It owns no timer: every method returns the delay after which it wants to be called again, and the
  host arms one `DispatcherQueueTimer` with it.

`MonitorPlacement` implements the Win32 half; `MainWindow` implements the window half and owns the timer. This is
GPL engine code, like `Layout`; nothing reaches the SDK.

*Why no timer inside.* A controller that returns "call me in 500 ms" is driven by a test with no clock, no
`TimeProvider` fake and no new package. *Why the engine and not a host test project.* The host is a WinUI 3
executable; a test project for it would need the Windows App SDK runtime in CI for the sake of code that has no UI
in it.

### 5. Settle, place, verify, confirm

States: `Settling`, `Placing`, `Placed`, `Absent`, `GaveUp`.

1. **Any signal** (display settings changed, resume, XAML root changed, config reloaded, startup) runs the cheap part
   at once: enumerate and match. If the display is absent: hide, release the page, go to `Absent`. This runs before
   any wait, because Windows moves the windows of a disconnected display onto another one and the deck must not sit
   there for the length of a timer.
2. Otherwise go to `Settling` and (re)start a **250 ms** quiet period. Further signals restart it, at most up to
   2 s after the first, so a stream of events cannot postpone placement forever.
3. **Place:** enumerate and match again, cover the display's bounds, then **verify**: content bounds equal the
   display's bounds; the window's scaling equals the display's scaling; the laid-out size times the scaling is within
   2 px of the content bounds. The last check is today's "stale layout" test; when only it fails, the next attempt
   covers with the nudge.
4. Verification fails → retry after 250 ms, 500 ms, 1 s, 2 s, then every 4 s. After **30 s** from the first attempt:
   warn with expected and actual values, go to `GaveUp`. Any signal starts over.
5. Verification passes → `Placed`. Ask for one page rebuild if the bounds, the scaling or the display differ from the
   last `Placed` state, or if the signal was a resume. Then **confirm** at +2 s and +10 s: verify only; a failure goes
   back to step 3, a pass does nothing.
6. In `Absent`, a signal that finds the display shows the window without activating it and continues at step 2.

The confirm checks are what remains of the 1.5 s and 5 s re-checks. They exist because the question "did Windows
report every late change?" has only been answered for one panel and one driver; they cost two enumerations.

The single rebuild after a resume keeps the spec's "Page returns after sleep": the widget surfaces are bitmaps and
survived the spike's sleep test without any recovery code, but a device reset is cheap to cover with one rebuild and
was the reason for the three.

`ScheduleRebuild`'s own coalescing and the `LayoutKey` comparison stay. `XamlRoot.Changed` and `SizeChanged` become
signals to the controller instead of calling `Cover` and `ScheduleRebuild` directly, so there is one place that
decides to move the window.

### 6. Hidden means the page is released

Hiding calls `AppWindow.Hide()` and `PagerController.Release()`. Releasing the views stops their timers and ends their
reading subscriptions through the path a page change already uses, so providers stop after their linger period and the
frame clock stops with its last animating widget. Nothing new has to learn about "paused". Showing uses
`AppWindow.Show(activateWindow: false)` followed by the forced rebuild of step 5.

At startup `App.OnLaunched` creates the window and the tray icon and lets the controller decide whether to show it,
instead of calling `Activate()` unconditionally. The window object exists while hidden: the tray's Quit closes it and
the existing `Closed` handler disposes the host.

*Alternative:* keep the page alive behind a hidden window, for an instant return. It keeps providers sampling for a
screen nobody can see, against "the user pays only for what they switch on". A rebuild takes well under a second.

### 7. The host writes the configuration while running

Remembering a display and re-pinning a path call `ConfigStore.Save()`, which already suppresses the reload its own
write would cause. `display` is a typed property with extension data, so unknown properties inside it survive.
`--snapshot` uses the same selection but with saving switched off: a command that renders a PNG must not edit the
configuration, and CI runs it on machines whose display should not be remembered.

`HostContext.Target` becomes nullable (absent). `SnapshotCommand` falls back to the primary monitor's size, then to
1100x3840 if there is no monitor at all, and logs which it used.

### 8. The old draft is deleted, not archived

`openspec/changes/display-targeting` was never implemented, is written against the WPF host, and would define the
same capability twice. Its remaining requirements (listing with friendly names, Identify, selecting from the list)
move to `docs/BACKLOG.md` item 6 as the brief for `settings-window`.

### 9. Refinements made while implementing

- **The controller is given the time.** `Signal` and `Tick` take the caller's monotonic time and return the next delay;
  the quiet-period cap and the 30 s limit need elapsed time, and a controller that only returned delays could not tell
  how long a burst has lasted. The host passes `Environment.TickCount64`, a test passes a number.
- **Check first, cover only when the check fails.** An attempt verifies, and covers the display only if something
  differs; the result of a cover is verified by the next attempt. This is what makes "nothing changed" move no window.
- **Covered before shown.** When the deck is shown (startup, return from absent) the window is first put on its
  display, so it does not appear where Windows left it.
- **Window events do not restart running attempts.** `XamlRoot.Changed` and `SizeChanged` are mostly the echo of the
  controller's own cover. While attempts run they are ignored (the next attempt looks anyway); otherwise a window that
  never verifies would start over on every nudge and never reach the 30 s warning.
- **Giving up still builds the page.** After the warning the owner is asked for the pending rebuild: a page on a window
  that is a little off is better than an empty deck, which is what a first start that never verifies would show.
- **No page between show and placed.** The host builds no page from the moment the deck is hidden until placement asks
  for one, so a configuration reload that brings the deck back costs one build.
- **A `display` entry with neither `path` nor `model`** identifies nothing and is treated as missing: choose again.

## Risks / Trade-offs

- **The first choice can be the wrong display** (first start with the panel off: `tallest` picks the side monitor and
  remembers it). → The log line that announces the choice names the display and says to remove `display` to choose
  again; README says the same. The picker is the real answer.
- **Two identical displays and a changed path** resolve to the first by path order, which may be the wrong one. → A
  warning names both. Accepted until someone has the hardware; the picker writes an exact path.
- **A display that drops off the bus when it sleeps** (some DisplayPort monitors do) hides the deck and rebuilds the
  page on wake. → That is the intended behaviour, and it is the only correct one when Windows has re-homed the window.
  Task 6.4 checks what the Y70 does.
- **The model code may be empty** (generic or virtual displays). → Matching falls back to the path alone, and to the
  GDI device name when there is no path: no worse than today.
- **A display that never verifies** (a driver that reports a scaling the window never reaches) would retry for 30 s on
  every signal. → The warning says which of the three checks failed, so the check can be relaxed with evidence.
- **Config written while the user has the file open in an editor.** → It happens once per remembered display, the
  editor will offer to reload, and the write is the same serializer as the save on exit. Comments in the file were
  already lost on exit.
- **Hot-plug cannot be simulated in CI.** → The state machine is tested with scripted sequences; the panel checks in
  section 6 of the tasks are the owner's.

## Migration Plan

No migration step. An existing configuration has no `display`; the first start chooses with its selectors, as before,
and writes `display`. A build from before this change ignores `display` (it is not in its model and is dropped on that
build's save), then this build re-creates it. Rollback is reverting the PR.

## Open Questions

- Whether the confirm checks ever fail on real hardware. The log records it; if two weeks of daily use show no
  failure, a later change can remove them.
- Whether `GUID_CONSOLE_DISPLAY_STATE` (display off without disconnect) is worth listening to. It belongs to "pause
  when nobody can see the page" (item 15), not here.
