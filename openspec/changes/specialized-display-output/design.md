## Context

See `proposal.md` for why and `docs/handoff/2026-10-31-specialized-displays.md` for the decisions taken with the
owner. This change builds on two others:

- `specialized-display-spikes` measured the facts on hardware; `docs/perf/specialized-display.md` holds the answers
  to S1 to S7 and the go. **Where this design says "per S<n>", the spike's answer wins**: if it differs, update this
  file in the spike's pull request (its task 5.2) or before starting here.
- `deck-session` gave the host a `DeckSession` and an `IDeckPresenter` seam with one presenter, the window.

What exists: `PageRenderer` draws a whole page on one canvas; `GestureRecognizer` and `WidgetHitTest` decide input
from samples; `PlacementController` is the model for a tested, retrying controller driven by signals and one timer;
`DisplayPowerWatcher` already reads `GUID_CONSOLE_DISPLAY_STATE` (from the deck window, which direct output does not
have).

## Goals / Non-Goals

**Goals:**
- The requirements in `specs/direct-output`, and the deltas to `display-targeting` and `settings-window`.
- Decisions about acquiring the display, power and touch that run on Linux in tests, through scripted fakes.
- The idle cost of a still page drawn directly is no higher than in the window on the same PC.

**Non-Goals:**
- Hosted views (web view, media element) on direct output; only their placeholder.
- HDR, high refresh rates, more than one specialized display at once, head-mounted displays, indirect displays (USB
  display adapters).
- Driving a display that is not specialized (a desktop monitor is always the window's).
- Any change to the window path beyond choosing it.

## Decisions

### D1. Shape

```
 App (XAML: tray, settings, later the editor)
  └─ DeckHost ── chooses the presenter from the deck display's kind; swaps on a kind change
       ├─ DeckSession (engine; deck-session)
       ├─ WindowPresenter (MainWindow + PlacementController)          ← desktop monitor
       └─ DirectPresenter (host/Direct)                               ← specialized display
            ├─ DirectOutputController (engine): acquire/apply/verify/retry/release, dark/lit, reasons
            ├─ DisplayCoreOutput: DisplayManager, target, path, mode, rotation, device, source, 2 primaries, fence
            ├─ GpuRenderer: D3D12 + Skia GRContext over the primaries (per S3), or RasterRenderer (D3D11 upload)
            ├─ FramePacer thread: WaitForVBlank → posts "frame due" to the UI thread at 30 per second while moving
            ├─ TouchReader: Raw Input on a message-only window → TouchMapper (engine) → GestureRecognizer samples
            └─ SessionWatcher: message-only window: GUID_CONSOLE_DISPLAY_STATE, WTS lock/unlock, power resume
```

`DeckHost` is the small piece that today is "create `MainWindow`" in `App`. It asks the display model (desktop
monitors plus Display.Core targets, D3) which display is the deck's and of what kind, creates the presenter for that
kind, and on a kind change disposes one presenter and creates the other, handing the session over (the session's
current page and slots survive; only the surfaces change).

### D2. Threads

Everything that touches the session or a widget runs on the UI thread, as in the window, so widgets keep their
one-thread contract. The pacer thread only waits for the vertical blank and posts. A frame is: the UI thread draws the
page (the session's slots, the pager offset, the background at the frame's time) into the next primary through Skia,
flushes, signals the fence, and queues a `DisplayTask` with `SetScanout` and `SetWait(fence, value)`; `ExecuteTask`
does not block. With two primaries, a frame is drawn into the one not on screen; if the previous task has not yet
scanned out, the frame is skipped (the next one is due in 33 ms), never queued. *Alternative:* a render thread that
calls widgets. Rejected: plugin widgets were never asked to be thread-safe.

Idle: no frame due, nothing drawn, nothing queued, the pacer thread is parked (it only waits for a vertical blank
while something moves). A change (a reading, a page, a press) asks for one frame.

### D3. Finding the specialized displays

The display model is the union of `MonitorPlacement.GetDisplays()` (desktop) and Display.Core targets with
`UsageKind == SpecialDesign` and `IsConnected`. A target's identity for matching is the one S5 found to equal the
remembered `display.path` (expected: the monitor's `DeviceId` from `TryGetMonitor()`, the same device interface path
`MonitorPlacement` reports), and its model code from the EDID (manufacturer and product, the same code
`DisplayInfo.Model` has). `display.kind` (`desktop` / `specialized`) is written on change. Matching itself stays in
`DisplaySelector` / the existing matcher, now given both kinds. Display.Core's `DisplayManager.Changed` (after
`Start()`) and the existing `DisplaySettingsChanged` both signal the model to re-read; the controller debounces like
placement (250 ms quiet period).

### D4. `DirectOutputController` (engine)

Modelled on `PlacementController`: an `IDirectOutputEnvironment` (list targets, try acquire, try apply mode, create
output, release, is stale) and an owner (show, hide, dark, lit, rebuild, state changed with reason), signals
(`TargetsChanged`, `DeviceLost`, `DisplayOff/On`, `Locked/Unlocked`, `Resumed`, `ConfigReloaded`, `Quit`) and one timer
(`Tick(now)` returns the next wake). States: `Starting`, `Acquiring`, `Showing`, `Dark(reason)`, `Waiting(reason)`,
`Released`. Retries: quiet period 250 ms, then attempts at 0.5, 1, 2, 5, 10, 30 s, then every 30 s while a reason
holds (no spinning). Mode application uses `TryApply(FailIfStateChanged)` in the retry loop the documentation
prescribes. Tests drive it with scripted target sequences on Linux: start specialized; start desktop then toggle;
toggle back; unplug and replug; held by another app then released; remote session; device lost during a frame;
display off; lock during display off; resume with a stale target; quit while waiting.

### D5. Mode and rotation

Preferred resolution, refresh closest to 60 Hz, `B8G8R8A8UIntNormalized`, `DisplayPathScaling.Identity`, not
interlaced (the sample's choice). Rotation per S4: if the driver accepts `DisplayPath.Rotation`, use it (the primary
is then allocated in the rotated size and Skia draws upright); otherwise allocate the native size and rotate the
Skia canvas, and rotate touch the same way in `TouchMapper`. The default for an absent `display.rotation` is the
orientation `MonitorPlacement` last reported for that display while it was on the desktop, kept in `display` (the
host already writes `display`); otherwise 0. The deck's surface for the session is the rotated size at scale 1: the
theme resolves against the cell size as everywhere else, so nothing about resolution reaches the user.

### D6. Drawing

Per S3: the GPU path (D3D12 device on the target's adapter, primaries opened as D3D12 resources, Skia
`GRContext.CreateDirect3D`, `GRBackendRenderTarget` per primary) when it works; the raster path (Skia into an
`SKBitmap`, `UpdateSubresource` to the D3D11-opened primary) when it does not, with moving backgrounds drawn still
(a log line says so). The frame composes what the window composes, in the same order: `PageRenderer`'s background,
pattern, page, dock and indicator, driven by the session's slots instead of fresh widgets, plus press feedback, the
pager offset (the current and neighbour page translated), border motion and the Identify marker. To avoid a second
drawing of the same thing, factor `PageRenderer` into steps the direct presenter calls with the session's state
(goldens unchanged); widgets paint through `WidgetSlot.Paint` exactly as into their window canvas.

A widget whose descriptor says it needs a hosted view (an SDK flag added when the first such widget exists; none
today) is painted as a placeholder card with the theme's text style. Until such a flag exists this is one code path
and a test with a fake descriptor.

### D7. Touch

Per S1 and S2. The reader registers Raw Input for touch screens (`UsagePage 0x0D`, `Usage 0x04`, `RIDEV_INPUTSINK |
RIDEV_DEVNOTIFY`) on a message-only window only while the deck is shown directly, and unregisters while dark or
hidden (input costs nothing at rest). It picks the digitizer of the deck's display by the rule S2 found (expected:
the container id shared by the HID device and the monitor), else the single touch screen not mapped to a desktop
monitor, else `display.touch` (a HID device path the user can copy from the log), else none, logged. Reports are
parsed with the preparsed data (contact id, tip, X, Y, contact count). `TouchMapper` (engine, tested) maps logical
ranges and rotation to deck pixels and turns contacts into the same samples `PagerController` makes from pointer
events (first contact only, as today), so taps, presses, swipes and dot taps reuse `GestureRecognizer` and
`WidgetHitTest` unchanged. Whatever one-time Windows setting S1 found necessary is documented on the site page and
checked, if it can be read, with a warning in the log when it is missing.

### D8. Power and session

The `SessionWatcher` window registers `GUID_CONSOLE_DISPLAY_STATE` (`RegisterPowerSettingNotification`),
`WTSRegisterSessionNotification` and listens for resume; it feeds the controller's signals and the session's motion
gate (display off) like `DisplayPowerWatcher` does for the window. Dark is, per S7, disconnecting the path
(`TryApply` without it, so the panel enters standby) where that works, else one black frame. Lit re-applies the mode
and draws a fresh frame. While dark the presenter reports the deck as hidden to the session, so slots unsubscribe and
providers stop. After resume the controller checks `IsStale` and re-acquires as S6/S7 found necessary.

### D9. Diagnostics

`URDECK_DIRECT_DUMP=<folder>` writes `direct-frame.png` at most once a second while frames change (read back from the
GPU surface, or the raster bitmap). Log lines (prefix `Direct output:`), used by the site's troubleshooting page and
the manual checks:

```
Direct output: target 'HYTE Y70Touch' specialized, path \\?\DISPLAY#..., adapter 'NVIDIA GeForce ...'
Direct output: acquired; mode 3840x1100 @ 60.00 Hz, rotation 90, deck 1100x3840, drawing on the GPU (Direct3D 12)
Direct output: drawing on the processor: <reason>; moving backgrounds are drawn still
Direct output: touch from '<product>' (<device path>), contacts up to 10
Direct output: touch unavailable: <reason>
Direct output: frames started (<reason>) / stopped (<reason>): N frames in S s
Direct output: dark (display off | session locked | sleep), lit (<reason>)
Direct output: waiting (<reason>) / released (<reason>)
Deck: presenter window -> direct (display 'HYTE Y70Touch' removed from the desktop)
```

### D10. Where the pieces live

Engine (Linux-testable, no Windows types): `DirectOutputController`, the display model's union and matching,
`TouchMapper`, rotation maths, and the `PageRenderer` factoring. Host: everything that calls Display.Core, Direct3D,
Raw Input, Win32 notifications, Skia GPU contexts. The probe's code from the spikes is the starting point and is
replaced, not kept beside.

## Risks / Trade-offs

- [The spike answers differ from the expectations written here] → the spike's pull request updates this design; the
  tasks refer to D-numbers, not to the expected answers.
- [A GPU driver resets (TDR) or the adapter changes] → `DeviceLost` signal: release, recreate the device and Skia
  context, re-acquire; tested in the controller with a scripted loss; checked by hand with a driver update if possible.
- [Burn-in if the host hangs with the panel lit] → the controller's watchdog: if no frame or tick happened for 60 s
  while lit and the session is locked or the displays are off, the pacer thread disconnects the path itself; the
  process ending releases the display.
- [The hand-declared COM interop is wrong in a way that only shows on some drivers] → keep it in one file with the
  header's IID and vtable order quoted; log the HRESULT of every failing call.
- [Two desktop-only features drift: Identify markers and the settings window's placement assume HMONITORs] → the
  settings window still opens on a desktop monitor ("away from the deck's display" is trivially true); Identify draws
  through the presenter on the specialized display (specs).
- [The user specializes their only display] → the host never asks for it; the site page warns and explains how to undo
  it.

## Migration Plan

Additive. Configurations gain `display.kind`, `display.rotation` and optionally `display.touch`, written by the host;
older hosts keep unknown `display` properties (display-targeting, "Explicit Display Choice"). Rollback: revert the
pull request; a specialized panel then shows nothing, as before this change.

## Open Questions

- Whether a second specialized display (not the deck's) should ever be offered for something; not needed now.
- Whether the frame dump should become a user-facing "save a picture of the deck" in the editor; later.
