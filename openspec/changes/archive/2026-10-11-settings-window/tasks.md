## 1. Check the assumptions first

- [x] 1.1 Read `PlacementController`'s handling of `ConfigReloaded` and confirm that changing `config.Display` and signalling moves the window to the new display, and that choosing the current display rebuilds nothing. If it does not, add the signal or the rule there with a scripted-sequence test before anything else (design decision 3)
- [x] 1.2 Confirm on the panel that a window created in the host takes the foreground from a tray click while another application is in front, and that the deck window stays unactivated

## 2. Engine: the row model and the choice (unit-tested)

- [x] 2.1 Add the display row model in `UrDeck.Engine.Display`: order by position, number, orientation, nearest-neighbour relative position, "Display N" instead of a device name, the deck's display (through `DisplaySelector.Select`), and the waiting row from the remembered name
- [x] 2.2 Tests for the row model: two displays side by side, stacked, three in a row, a portrait panel, two displays with the same name, a display with no name, one display (no relative position), a remembered display that is not connected, a remembered display found by model at a new path, and the "choose automatically enabled" rule
- [x] 2.3 Add the helper that applies an explicit choice and the return to automatic (no `selector`, unknown properties kept; reports whether the configuration changed) and tests for it, including choosing the display that is already chosen and pinning one the selectors picked
- [x] 2.4 Add a scripted placement test for an explicit choice moving the deck, for choosing automatically (selectors apply and are remembered with their selector), and for the chosen display being unplugged (deck hidden, never on another display)

## 3. Host: tray and the application

- [x] 3.1 `TrayIcon` raises an event for `NIN_SELECT` and `NIN_KEYSELECT`; it still knows nothing about the application
- [x] 3.2 `App`: the Settings entry first in the menu, the left-click event, one settings window at a time (open means restore, bring forward and focus when it exists), all references dropped when it closes, and the window closed when the host quits
- [x] 3.3 `MainWindow` implements the seam: the displays, the remembered display and the placement state, `Choose`, `ChooseAutomatically` (save, then signal placement), and one UI-thread "changed" event from the display-change and placement-change signals; no new `SystemEvents` subscription

## 4. Host: the settings window

- [x] 4.1 The `ISettingsHost` interface, and the settings window built in code against it only: overlapped presenter, title bar, not resizable or maximizable, in the switchers, the application icon, Escape closes it
- [x] 4.2 Placement of the window: the display under the pointer, else the primary display, never the deck's display unless it is the only one; centred, sized by that display's scaling
- [x] 4.3 The monitor page: the list of rows (name, resolution, orientation, Primary badge, relative position, number, deck's display marked, the waiting row disabled), a click on a row is the choice (no selection, so choosing the deck's own row works too)
- [x] 4.4 The **Choose automatically** button, enabled by the rule in the row model
- [x] 4.5 Live refresh: re-read after a short debounce when the host's "changed" event arrives while the window is open; unsubscribe on close
- [x] 4.6 The **Identify** button and the markers: layered, topmost, no-activate tool windows with the number drawn in GDI in the corner of every display, removed together after about two seconds, the timer restarted by a second press, removed when the window closes

## 5. The cost gate

- [x] 5.1 The soak mode (an environment variable in the style of `URDECK_MEMLOG`): open and close the window 20 times at startup, a garbage collection after each, private bytes and working set in `urdeck.log` (`URDECK_SETTINGS_SOAK=1`; `=hold` opens it once and leaves it open)
- [x] 5.2 Run it in Release; record the numbers (before, after 1, after 20, idle CPU and GPU with the window open and closed) in `docs/perf/settings-window.md`. If private bytes are not within 10 MB of the start or keep rising, stop and go back to the design's second-process fallback

## 6. Verify

- [x] 6.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the Engine coverage floor, `dotnet format urdeck.slnx --severity warn --verify-no-changes`, `openspec validate --all --strict`
- [x] 6.2 On the panel (owner): open from the left click and from the menu; a second click brings the same window forward; the taskbar button appears and goes; it opens away from the panel; choose the panel, another display, then back; Choose automatically; Identify numbers match the list; unplug and replug while open; the deck keeps working and never takes focus while Settings is open; open it over a fullscreen application
- [ ] 6.3 With a single display connected (or the panel as the only active display): the window opens in front of the deck (skipped for now by the owner, 2026-10-11: not verified; the deck is not topmost and the window activates, so it is expected to work)

## 7. Documents

- [x] 7.1 README: the tray section (left click, the Settings entry) and the monitor-selection text that says to edit `monitorName`
- [x] 7.2 `docs/BACKLOG.md` (current state and item 6: the picker is done, what remains) and `docs/ROADMAP.md` (the milestone 3 line for the monitor picker)
- [x] 7.3 `AGENTS.md`: the Windows gotchas that changed (the deck is not the only window; the settings window takes focus; how to quit with it open) and the soak mode
