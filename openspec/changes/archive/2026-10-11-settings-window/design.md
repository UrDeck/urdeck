## Context

See proposal.md for why. What exists, and shapes the approach:

- `TrayIcon` (`src/UrDeck.Host/TrayIcon.cs`) owns a hidden message-only window and a list of `TrayMenuEntry`. It selects
  the version 4 callback, handles only `WM_CONTEXTMENU`, and ignores every other event. It is plain Win32.
- `DisplayInfo` already carries the friendly name (EDID), model, device path, bounds, scaling and the primary flag;
  `MonitorPlacement.GetDisplays()` produces it. `RememberedDisplay` is the `display` object, with `Selector` null meaning
  "chosen explicitly, never chosen again by the selectors" (`DisplaySelector.Select`).
- `PlacementController` is driven by signals (`PlacementSignal.ConfigReloaded` exists) and tested with scripted display
  sequences. `MainWindow` is its only owner; it reports signals and arms one timer.
- `ConfigStore.Save()` suppresses its own file-watcher event for 2 seconds, so a write from inside the process does not
  produce `ConfigChanged`: a choice made in the window has to signal placement directly.
- The deck window is `WS_EX_NOACTIVATE`, not topmost, and has no title bar. It is the only window the host has.
- WinUI windows are built in code here (no XAML page per window; `App.xaml` stays). A removed `SKXamlCanvas` can
  outlive its window (AGENTS.md), which is why the cost gate below is a requirement and not a hope.

## Goals / Non-Goals

**Goals:** the monitor picker, Identify, a window shell that the editor can later grow into (activatable, a taskbar
button while open, one copy), and a recorded memory cost.

**Non-Goals:** an arrangement map, more pages or navigation, styling, a theme picker, a second process now, any change
to how placement decides (only a new way to set `display`), per-display settings such as brightness or scaling.

## Decisions

### 1. In-process now, behind a seam that lets it move out

The window runs in the host process and talks to the host only through a small interface, `ISettingsHost`: the
connected displays, the remembered display and the placement state, `Choose(display)`, `ChooseAutomatically()`, and a
"displays or placement changed" event. `MainWindow` implements it (through a small adapter in `App`). The window class
references nothing else in the host.

*Why in-process:* the XAML framework is already loaded for the deck, so a second `Window` adds objects, not a runtime;
the picker needs live placement state (waiting, placed) that a second process could only infer from the file and the log;
applying a choice is one call instead of a file write, a 1 s debounce and a reload; "bring the open window forward" is a
field check instead of a second mutex and a message; and opening is instant instead of a process start.

*Why not a second process:* its one clear advantage is a zero idle cost while closed, and the cost gate (decision 7)
tests exactly that. It also gives the editor a separate measurement point, but the editor (item 11) will make that
decision on its own terms: it loads plugins and renders previews, and the picker does not. If the gate fails, the same
window class moves to a `--settings` entry point with an `ISettingsHost` that enumerates displays itself and writes
`display` to the file (the host already reloads it, and `display` without a `selector` is the same contract).
That move is the fallback, not a plan.

### 2. A pure row model in the engine

`UrDeck.Engine.Display` gets a function from (connected displays, the remembered display, the deck's display) to a
list of rows: number, name, size, orientation, primary, relative-position text, "is the deck's display", and the
waiting row. Everything the window shows is decided there and unit-tested; the window only draws rows.

- **Order and number:** by `Bounds.X`, then `Bounds.Y`; the number is the 1-based place in that order. A display's
  number can change when displays are rearranged; Identify is how the user reads the current one.
- **Relative position:** against the nearest other display (distance between the centres), by the dominant axis of the
  offset: "Left of", "Right of", "Above" or "Below" plus "Display N". Both of a pair describe each other ("Right of
  Display 1" and "Left of Display 2"), nothing is said for a single display. Ties go to the lower number.
- **Orientation:** portrait when the height is greater than the width, else landscape.
- **Name:** `DisplayInfo.Name` as it is now. When Windows reports none, `MonitorPlacement` already falls back to the
  model, then to `DISPLAYn`; the row model replaces that last fallback with "Display N" so the user never reads a
  device name.
- **The deck's display and the waiting row:** matched with `DisplaySelector.Select` (the same rules the placement
  uses), not by a second matching rule. When `Select` finds no display and `display` names one, the waiting row is
  built from `display.Name`.
- "Choose automatically" is enabled when `display` has a `Selector == null` and is non-empty, as the spec says (disabled
  when a selector is recorded or nothing is remembered).

Rejected: computing rows in the window (untestable), and a map of rectangles (a later change; the same model carries
the bounds it would need).

### 3. Applying a choice goes through `MainWindow`, as a new signal use

`Choose(display)` sets `config.Display` to a `RememberedDisplay` built from the `DisplayInfo` with `Selector = null` and
the previous `ExtensionData` kept (a small helper next to `DisplaySelector`, unit-tested), calls `SaveConfiguration`, and
signals `PlacementSignal.ConfigReloaded`, which the controller already treats as "look again". `ChooseAutomatically()`
sets `config.Display = null`, saves and signals the same way; `DisplaySelector.Select` then chooses with the
selectors and `PlacementController` stores and saves the result with its selector, which is the path that exists for a
`display` deleted from the file today. Choosing the display that is already the deck's compares paths and does
nothing.

First task checks that `ConfigReloaded` really moves the window when `display` changes and rebuilds the page only
when it must; if the controller needs a new signal it is added there, with a scripted test, not in the host.

Rejected: calling the controller's placement methods from the window (it is owned by `MainWindow` and driven by one
timer), and writing the file and waiting for the watcher (a 1 s debounce, and `Save()` suppresses it anyway).

### 4. The tray: a left-click event, an idempotent open

`TrayIcon` handles `NIN_SELECT` and `NIN_KEYSELECT` (the version 4 left click and the keyboard equivalent) and raises
an event; a double click arrives as more of the same and must not open two windows, so "open" is idempotent. The tray
code still knows nothing about the application: `App` subscribes. The Settings entry is added first in the list
(three entries: Settings, Start with Windows, Quit).

### 5. The window: an ordinary top-level window, created per open

`SettingsWindow` is a new WinUI `Window`: overlapped presenter, with a title bar, not resizable or maximizable, shown
in the switchers, with the application icon, and not owned by the deck window. It has a taskbar button because it is an
ordinary window, and loses it when it closes; no code manages the button. A new instance is created for each open and
the host drops every reference on `Closed` (events unsubscribed), so nothing holds it between uses. `App` keeps one
field: while it is non-null, open means "restore, bring forward, focus".

*Focus:* the tray click is user input, so Windows lets the process take the foreground; `Activate` plus, when the
window already exists, `ShowWindow(SW_RESTORE)` and `SetForegroundWindow`. Nothing touches the deck window, which keeps
`WS_EX_NOACTIVATE` (the deck's rule is unchanged; the spec says the settings window is the one exception).

*Where:* the display under the pointer (`GetCursorPos` against the display list), unless that is the deck's display and
another exists, then the primary display, or the first other one. Centred in physical pixels on that display, sized in
DIPs by that display's scaling (the display list has it). One display: it opens on the deck's display, in front.

*Content:* one page, built in code: a heading, a `ListView` with no selection and item clicks enabled (a click on a
row is the choice; the deck's row carries a mark instead of a selection, so clicking it again can pin an automatic
choice, which a selection change could not report; the waiting row is disabled), an **Identify** button and a **Choose
automatically** button. Refreshing rebuilds the rows. Escape closes the window. There is no navigation control until a second page exists; the later design pass
(Claude Design) restyles it and restructures the shell.

### 6. Live refresh from the host's existing signals

`MainWindow` already receives display changes (`SystemEvents`, subscribed from a thread-pool thread because the UI
thread's context never delivered them) and already raises `PlacementChanged`. `ISettingsHost` exposes both as one
"changed" event on the UI thread, and the window re-reads after a short debounce (displays settle in bursts, the same
reason placement waits). No polling and no new `SystemEvents` subscription.

### 7. Identify with plain Win32 windows, one per display

Markers are tiny layered popup windows (`WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED`, the
last two so a click goes nowhere and focus is never taken; `TOOLWINDOW` keeps them out of the taskbar and Alt-Tab)
drawn with GDI: a dark rounded square with the number, in the top-left corner of the display, sized from the display's
scaling. Created together when Identify is pressed, destroyed together after about two seconds by one timer (pressing it
again restarts the timer) and when the settings window closes. This is the pattern `TrayIcon` already uses, and it costs
nothing while no marker shows, where a WinUI window per display would create and tear down a XAML tree per display for a
two-second label.

### 8. The cost gate is a soak mode

An environment variable, in the style of `URDECK_MEMLOG`, makes the host open and close the settings window 20 times at
startup, run a garbage collection after each, and log private bytes and working set, so the gate is a number read from
`urdeck.log` and recorded in `docs/perf/settings-window.md` (before, after 1, after 20; also idle CPU and GPU with the
window open and closed). The gate is 10 MB and no upward trend. Its failure is the trigger for decision 1's fallback.

## Risks / Trade-offs

- **A closed WinUI window may stay in memory** → the gate in decision 8 measures it; the host drops every reference
  on close; if it fails, the second-process fallback is already shaped by the seam.
- **Windows may refuse the foreground to the window** (a game is running) → a tray click is user input and normally
  qualifies; the task list checks it with a fullscreen application in front, and the fallback is the flashing taskbar
  button, which is still the user's way in.
- **Opening above the deck on a single display** → the deck is not topmost and the window activates, so it should be on
  top; checked on the panel by pointing the deck at the only display (or by the picker's own scenario with one display).
- **Choosing a display moves the deck, and the window with it if the window is on that display** → the window is never
  opened on the deck's display unless it is the only one; on the only display there is nothing to choose.
- **A monitor that is only switched off stays in the list** → Windows reports nothing for it (`display-recovery`, check
  6.4), so neither the deck nor the picker can tell; the list is of what Windows says is connected.
- **Numbers change with the arrangement** → Identify exists for this; the numbers are not stored anywhere.
- **Left click now steals focus from the app in front** → a user decision recorded in the proposal; only a deliberate
  click does it, and the deck still never does.
- **An unplugged display is the only one with a given model** → the waiting row uses the remembered name; the row
  model does not guess.
