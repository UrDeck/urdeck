## 1. Engine: theme and configuration

- [x] 1.1 Add `Dock { Height }` to `ThemeDefinition` (merge, sanitise to 0.2..1 with the usual warning), and `"dock": { "height": 0.71 }` to `default-dark`, `default-light` and `glass`
- [x] 1.2 Theme tests: every built-in theme has a dock height, an older theme without one takes the default's without a warning, an out-of-range value warns and falls back, a user theme overrides it
- [x] 1.3 Change `UrDeckConfig.Dock` to a list of widget objects and remove `DockItemConfig`; tests: two shortcut entries round-trip with their settings and an unknown property, an entry in the old `type`/`command`/`url` shape loads and round-trips, an absent `dock` is an empty list

## 2. Engine: layout

- [x] 2.1 Extend `ChromeLayout.Compute` with the dock's height and its result with the dock rectangle (dock at the bottom, the indicator's band above it, `fade` floating at the bottom of the grid area, `auto` counting rows after both); keep the result without a dock identical to today's and the existing `ChromeLayoutTests` unchanged
- [x] 2.2 Chrome layout tests with a dock: the 1100x3840 panel keeps 13 rows with the built-in dock height in `always` and in `auto`; the rectangles of dock, indicator and grid do not overlap and add up to the screen; `fade` puts the indicator directly above the dock; `off` reserves the dock alone; a 1000x1000 screen loses rows and `auto` becomes `fade`; a dock height larger than the screen is clamped
- [x] 2.3 Add the dock layout function: slot side, at most four slots, the group centred in the band, cards inset by half the gap with rounded edges, as `WidgetLayoutItem`s; tests for one, two and four entries, equal card sizes, the gap between neighbours, a band wider than tall and a screen narrower than four slots
- [x] 2.4 Add the step that decides which entries are shown: more than four (one warning, the rest dropped), a missing or unregistered `typeId`, a type without the size 1x1, `isVisible` false (each an empty slot, with a warning where the spec says so), and the 1x1 copy of each entry's configuration that leaves the loaded configuration untouched; tests for each case with a registry holding a 1x1 widget and a 4x2-only widget
- [x] 2.5 Hit test over the dock's layout items: a point on a card, between two cards, in the band beside the group, on an empty slot

## 3. Engine: snapshot rendering

- [x] 3.1 `PageRenderer.Render` and `RenderToBitmap` take the dock's widgets, reserve the dock through `ChromeLayout`, resolve the theme for the slot size and paint each placed slot; without a dock the output is byte-identical to today's
- [x] 3.2 Tests: a snapshot with two docked widgets shows two cards of the expected size and place in the bottom band with the indicator's pill above them and nothing of the page under them; a docked widget's card radius and padding are the theme's fractions of the slot; a page widget that no longer fits beside the dock is not drawn and an error is logged

## 4. Host

- [x] 4.1 `MainWindow.RebuildLayout`: pass the theme's dock height to `ChromeLayout` when `dock` has entries, resolve and own a second `Theme` for the slot size (disposed with the first, not created without a dock), and give `PagerSetup` a `BuildDock` that builds a `PageHost` from the dock layout
- [x] 4.2 `PagerController`: build the dock once per rebuild into the overlay below the indicator, dispose it in `Release`, leave it alone on page changes, yield it from `LivePages`, and log one line with the number of slots shown
- [x] 4.3 `PagerController` input: `Press` and `Tap` look at the indicator, then the dock, then the current page; a point in the dock band that hits no card goes nowhere
- [x] 4.4 `SnapshotCommand`: create the dock's widgets, add their subscriptions to the readings waited for, and pass them to the renderer
- [x] 4.5 Move the sample `urdeck-config.json`'s shortcut from the page into `dock`

## 5. Verify (automated)

- [x] 5.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn --verify-no-changes`, and `openspec validate --all --strict`
- [x] 5.2 `UrDeck.Host.exe --snapshot --size 1100x3840` of a two-page configuration with four docked shortcuts: 13 rows, the indicator above the dock, four centred cards with icons; and of the same configuration with an empty dock: identical to a snapshot taken before this change

## 6. Measure

- [x] 6.1 Create `docs/perf/dock.md`: the host with a dock of four shortcuts against the same configuration with the four shortcuts on the page and no dock (private bytes, CPU and GPU at rest, the cost of the second theme), and private bytes over twenty page changes with the dock shown; the idle cost must not rise

## 7. Verify on the panel (with the owner)

- [x] 7.1 Look: four shortcuts in the dock as a centred group under the indicator, cards of equal size, all 13 rows still placed; a clock and a stat in a slot are readable; the owner approves the dock height (record the value in `docs/perf/dock.md` and `docs/themes.md`)
- [x] 7.2 Behaviour: the dock does not move during a swipe and is not repainted by one; a tap on a docked shortcut shows the press and launches it to the front; a sideways swipe that starts on the dock changes the page and launches nothing; a tap on a dot still changes the page; the `fade` mode shows the indicator above the dock
- [x] 7.3 Editing `dock` in the file while the host runs adds and removes slots without a restart and keeps the page that is shown; a fifth entry is ignored with a warning in the log

## 8. Documentation and delivery

- [x] 8.1 `README.md`: a "Dock" section (what it is, the `dock` list with an example, four slots, any 1x1 widget as a miniature, what happens on a display without spare space), the `dock` row of the configuration table, the status paragraph
- [x] 8.2 `docs/themes.md`: the `dock.height` value; `AGENTS.md` and `CONTRIBUTING.md` only where they describe what changed
- [x] 8.3 `docs/BACKLOG.md` ("Current state", items 8 and 10, "Suggested order") and `docs/ROADMAP.md` (milestone 1); delete `docs/handoff/2026-10-09-shortcut-and-dock.md`
- [x] 8.4 Open the pull request for `feat/shortcut-and-dock` covering `shortcut-and-tap` and `dock`, with a conventional title and a description that becomes the squash commit body
