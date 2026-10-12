## Why

`display-recovery` made the deck follow its physical display, but the only way to say which display that is remains
editing `monitorName` or `monitor` in the JSON file, which needs the user to know what "tallest" or `DISPLAY2` means.
The backlog (item 6) and the roadmap (milestone 3) want a monitor picker with friendly names. It also wants the
first window the user can open from the tray icon, which settles the shell (a window that can take focus, a taskbar
button while it is open, one copy at a time) before the configuration editor (item 11) builds on it.

## What Changes

- **A settings window**, opened from the tray icon: a left click, or a new first menu entry "Settings". It is an
  ordinary window that takes focus and has a taskbar button while it is open and none after it is closed; the
  notification-area icon stays for the life of the host. A second request brings the open window forward instead of
  opening another.
- **The monitor picker**, the window's only page for now. It lists every connected display with its friendly name,
  resolution, orientation, a Primary badge, its position relative to the other displays and a number, and marks the
  display the deck is on. A remembered display that is not connected shows as a disabled "waiting" entry.
  The list refreshes while the window is open when displays are plugged, unplugged or rearranged.
- **Choosing a display** moves the deck there at once and remembers it as `display` without a `selector`, so it is
  never replaced by the selectors. **Choose automatically** removes `display`, which makes the host choose again
  with `monitorName` and `monitor`; it is disabled while the display is already chosen that way.
- **Identify** shows the display's number for about two seconds in a corner of every physical display.
- **Where the window opens:** never on the deck's display unless that is the only one: on the display under the
  pointer, otherwise the primary display.
- **Left click on the tray icon opens Settings** (it was inert). The deck window itself still never takes focus; the
  settings window is the one deliberate exception, and the host never activates the deck window to show it.
- **No fallback display.** While the chosen display is absent the deck stays hidden, as today; the picker's waiting
  entry says why.
- **Measured:** opening and closing the window 20 times must leave the host's memory where it started (a gate with a
  recorded number). If it fails, the same window moves to a second process behind the same seam.

Not in this change: an arrangement map, the editor, other settings pages, styling (a later Claude Design pass
restyles the window), a theme picker.

## Capabilities

### New Capabilities
- `settings-window`: the window itself (how it opens, where, its taskbar button, one copy), the monitor picker
  (what the list shows, choosing, choosing automatically, Identify, live refresh) and the memory gate.

### Modified Capabilities
- `tray-icon`: "Tray Icon", replaced by "Notification Area Icon" (a left click opens Settings instead of doing nothing)
  and "Tray Menu" (Settings is the first of three entries).
- `host-shell`: "Host Window" (the focus rule is for the deck window; the settings window is separate).
- `display-targeting`: a new requirement "Explicit Display Choice": a choice made in the settings window and the
  return to automatic choice, with their effect on the remembered display and on placement.

## Impact

- **Code:** `src/UrDeck.Host`: a settings window and its monitor page, the Identify markers (plain Win32 windows, as
  `TrayIcon` is), `App` (the entries, one window at a time), `TrayIcon` (a left-click event), `MainWindow` (applies a
  choice and exposes the displays-changed signal). `src/UrDeck.Engine/Display`: a pure function that turns displays
  into list rows (order, number, orientation, relative position, the waiting entry), unit-tested.
- **No new files on disk, no config field:** `display` and its `selector` already exist.
- **Docs:** `docs/perf/settings-window.md` (the memory gate), BACKLOG item 6 and the roadmap line for the picker.
- **Dependencies:** none.
