## 1. Identity and selection (engine)

- [x] 1.1 Confirm on the panel machine that `QueryDisplayConfig` + `DisplayConfigGetDeviceInfo` return the name `HYTE Y70Touch`, the EDID ids for `RTD1100` and the device path seen in `design.md`, and that the source name joins to `EnumDisplayMonitors`' device name; note the result in `docs/perf/display-recovery.md`. If the name is missing, adjust decision 3 before going on
- [x] 1.2 `src/UrDeck.Engine/Display`: `DisplayInfo` and `RememberedDisplay` (`name`, `model`, `path`, `selector`, extension data); `UrDeckConfig.Display` (`display`, omitted when null)
- [x] 1.3 `DisplaySelector`: the selector rules moved from `MonitorPlacement.Select`, the current-selector string (`tallest`, `display1`, `index:1`), matching (path, single model, several models with a warning, absent) and the "choose again" rule; the result says what to write back
- [x] 1.4 Tests: each selector, legacy config without `display`, re-enumeration under other device names, moved output (path re-pinned), two identical models, empty model, selector edited, `display` without `selector` kept, absent
- [x] 1.5 Config tests: `display` round-trips with unknown properties, is absent from a default configuration, and an older file loads without a warning

## 2. Placement state machine (engine)

- [x] 2.1 `IDisplayEnvironment` and the owner callbacks (rebuild, release, save remembered display, state changed) as in design decision 4
- [x] 2.2 `PlacementController`: signal handling with the immediate absent check, the 250 ms quiet period capped at 2 s, place and verify (bounds, scaling, laid-out size; nudge when only the layout is stale), the retry delays, the 30 s give-up, the confirm checks at +2 s and +10 s; every method returns the next delay
- [x] 2.3 Rebuild rule: once per settled placement, only when bounds, scaling or display changed, always after a resume and after a return from absent
- [x] 2.4 Log every attempt with what differed, and each state change, through `UrDeckLog`
- [x] 2.5 Tests with a scripted environment: displays returning in each order after a resume, the panel last; scaling that settles late; a burst of signals (one placement, one rebuild); nothing changed (no move, no rebuild); never verifies (warning at 30 s, a new signal starts over); a confirm check that fails; unplug and replug; started absent
- [x] 2.6 Engine line coverage stays above the floor (`dotnet test tests/UrDeck.Engine.Tests -c Release -p:CollectCoverage=true`)

## 3. Host: enumeration and window

- [x] 3.1 `MonitorPlacement`: enumerate through the display configuration API joined to `EnumDisplayMonitors`, add `GetDpiForMonitor`; a display with no target keeps its device name as path. Log each monitor with name, model, device name, bounds and scaling
- [x] 3.2 `MainWindow` implements the window half of `IDisplayEnvironment` (content bounds, scaling from `GetDpiForWindow`, laid-out size, cover with and without nudge, show without activating, hide) and owns the one timer that drives the controller
- [x] 3.3 Route `DisplaySettingsChanged`, the power resume, `XamlRoot.Changed`, `SizeChanged` and the config reload to the controller as signals; remove `ScheduleRetargets`, `Retarget`, `_pendingRetargets` and the nudge from `RebuildLayout`
- [x] 3.4 Hide releases the page (`PagerController.Release`); show is followed by the forced rebuild. Check in the log that providers stop after the linger period while hidden
- [x] 3.5 `HostContext`: nullable target, selection through `DisplaySelector`, saving the remembered display (not for `--snapshot`); `App.OnLaunched` creates the window and tray and lets the controller show the window
- [x] 3.6 `SnapshotCommand`: default size from the target, else the primary monitor, else 1100x3840, logged; never saves the configuration
- [x] 3.7 `TrayIcon`: a settable tooltip, re-applied on `TaskbarCreated`; "UrDeck: waiting for <name>" while absent
- [x] 3.8 `URDECK_ACTIVATABLE=1` still works: Escape closes the window, and the window is still hidden while absent

## 4. Build checks

- [x] 4.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, `dotnet format urdeck.slnx --severity warn`
- [x] 4.2 `--snapshot out.png --size 1100x3840` renders as before and leaves `urdeck-config.json` byte-identical
- [x] 4.3 `openspec validate --all --strict`

## 5. Docs

- [x] 5.1 README: the `display` entry in the configuration table, what the selectors now mean, "remove `display` to choose again", and that the deck hides while its display is absent
- [x] 5.2 `AGENTS.md`: the new log lines (placement attempts, hidden and shown) in the gotchas; `docs/perf/display-recovery.md` with the results of 1.1 and section 6
- [x] 5.3 `docs/BACKLOG.md` (current state, item 6) and `docs/ROADMAP.md` milestone 1 once the panel checks pass

## 6. On the panel (owner)

- [x] 6.1 First start with the existing configuration: the deck is on the Y70, `display` is written with `HYTE Y70Touch` / `RTD1100`, and a second start logs a match by path
- [x] 6.2 Sleep and resume, five times: each time the deck is on the Y70 at the right size, the log shows one rebuild and no placement on another monitor
- [x] 6.3 Unplug the Y70's display cable while running: the deck is not on any other monitor, the tray tooltip says it is waiting; plug it back: the deck returns with current values
- [x] 6.4 Switch the Y70's display off and on (or let the displays sleep without the PC sleeping): record in the perf note whether Windows reports it as a disconnect, and that the deck is right afterwards either way
- [x] 6.5 Start with the Y70 unplugged: no window anywhere, tray icon present; plug it in: the deck appears
- [x] 6.6 Move the Y70 to another output of the graphics card: the deck is on it and `display.path` is updated. Not testable on this hardware (no spare output); covered by `DisplaySelectorTests.MovedToAnotherOutput_IsFoundByModel_AndThePathIsRePinned` and `PlacementControllerTests.Unplugged_TheDeckHidesAtOnce_AndReturnsOnAnotherOutput`
- [x] 6.7 Change the primary monitor's scaling while running: the deck stays 1100x3840 at 100%
- [x] 6.8 Idle check: CPU and GPU of the host at rest are as in `docs/perf/render-host-baseline.md`, and no placement lines appear in the log while nothing changes
- [x] 6.9 Archive the change when everything above is verified
