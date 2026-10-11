## Why

Milestone 1 ("daily driver") is done when the owner's PC runs UrDeck for two weeks, sleep and resume included, and the
host cannot promise that yet. The target monitor is re-selected from `monitorName` on every display change, so on the
owner's PC `tallest` picks the 1080x1920 side monitor whenever the Y70 wakes late or is off, and the deck jumps onto a
screen that is in use. Recovery is a guess (re-check after 1.5 s and 5 s), and it rebuilds the page three times after
every resume.

This is the half of backlog item 6 that needs no user interface. The picker follows in a later `settings-window`
change and builds on the identity defined here.

## What Changes

- **A remembered display.** The first time `monitorName` / `monitor` resolve to a display, the host writes that
  display's identity (name, model, device path) to the configuration as `display`. From then on the deck follows that
  physical display, not whichever display currently is the tallest or is called `DISPLAY1`.
- **Selectors choose once.** `monitorName` and `monitor` keep their meaning, but they are used to make the choice, not
  to re-make it on every display event. Editing them chooses again.
- **Placement that is verified.** After a resume, a hot-plug, a resolution or scaling change, the host waits for the
  displays to settle, places the window, and checks the result (bounds, scaling, layout size). It retries with a
  growing delay until the check passes, and checks again a little later. The fixed 1.5 s and 5 s re-checks go away.
- **One rebuild per change.** The page is rebuilt once when placement has settled, not once per event.
- **An absent display hides the deck.** While the remembered display is not connected, the window is hidden, its
  widgets and their data providers stop, and the tray icon says what it is waiting for. The deck returns by itself
  when the display does. It no longer falls back to the primary monitor in that case.
- **The `display-targeting` draft is replaced.** Its identity, convergence and absent-display parts are this change;
  its picker part is recorded in the backlog for `settings-window`.

## Capabilities

### New Capabilities
- `display-targeting`: which physical display the deck belongs to (a remembered identity and how it is matched),
  converging to a verified placement after display changes, and what happens while that display is absent.

### Modified Capabilities
- `host-shell`: "Monitor Selection" (selectors choose once and are then remembered; primary is the fallback only for
  the first choice), "Host Window" (the "Window survives display changes" scenario no longer names fixed delays),
  "Configuration Management" (the `display` field, written by the host), "Application Entry Point" (startup with an
  absent display; `--snapshot` default size).
- `tray-icon`: the tooltip says which display the deck is waiting for while it is hidden.

## Impact

- **Code:** `src/UrDeck.Engine` gains a `Display` folder (display identity, matching, the placement state machine, all
  without Win32 so they are unit-tested); `UrDeckConfig` gains `display`. `src/UrDeck.Host`: `MonitorPlacement`
  (enumeration through the display configuration API, DPI queries), `MainWindow` (the retarget timers and the nudge
  move into the state machine; hide and show), `HostContext`, `App`, `SnapshotCommand`, `TrayIcon` (a settable tooltip).
- **Config:** a new optional `display` object. Existing files keep working and gain it on first run. The host now
  writes the file while running (once, when it remembers or re-finds a display), not only on exit.
- **SDK:** none. No plugin-visible change.
- **Dependencies:** none new. More P/Invoke (`QueryDisplayConfig`, `DisplayConfigGetDeviceInfo`, `GetDpiForMonitor`).
- **Cost:** idle behaviour is unchanged; while hidden the deck does less than today. Each display change costs a few
  enumerations and one page rebuild instead of three.
- **Docs:** README configuration table, `AGENTS.md` (log lines), `docs/BACKLOG.md` item 6, `docs/ROADMAP.md`.
- **Not in this change:** the picker and Identify, a "fall back to primary" option, pausing while the display is only
  asleep or the session is locked (item 15), several decks on several displays.
