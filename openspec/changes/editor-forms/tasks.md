Before starting: `settings-metadata` and `editor-shell` must be applied. Read `proposal.md`, `design.md` (D1–D12),
the specs here, `docs/handoff/editor-design/ControlKit.dc.html` and `Main.dc.html` (`field()`, `buildForm()`, the
properties pane markup). Model: Sonnet.

## 1. Engine helpers

- [ ] 1.1 `SettingValues.Read`/`Write` (D12) with tests for every kind, `JsonElement` settings and three-state text
- [ ] 1.2 `ReadingHub.ActionsFor(readingId)` (actions of the reading's provider whose path belongs to the reading, plus the natural action first) with tests using a fake action provider
- [ ] 1.3 `IconSets.KeysOf(set)` for the resolved theme set, with a test on the built-in set
- [ ] 1.4 Fallback resolution (D4) and display-unit conversion for forms (D5) as engine functions with tests

## 2. Form builder and simple controls

- [ ] 2.1 `FormBuilder` (D1): live and staged modes, layout rules (label placement, pairs, groups, expanders over eight), re-read on `Document.Changed` without rebuilding the focused control
- [ ] 2.2 Toggle, Choice (radio ≤ 3, combo ≥ 4, empty value removes), Text (placeholder fallback, merge key), Text with "Show a label", Number (range, step, whole, unit, invalid shows the range), Range (slider + value), Secret (reuse today's provider secret row), Unknown kind
- [ ] 2.3 Modifiers (D3): sizes dimmed with "Only at …", shown-when collapse, disabled-in-role, read-only document

## 3. Pickers

- [ ] 3.1 Reading button with live value from the preview's readings source; `IReadingChooser` and the minimal dialog (D10)
- [ ] 3.2 Action combo (disabled until the owner reading is set) and Unit combo (hidden for single-unit kinds)
- [ ] 3.3 Place `AutoSuggestBox` (D6) over `ReadingHub.SuggestAsync`; one transaction for all values
- [ ] 3.4 Launch target box (D7): File… and Folder… pickers with `InitializeWithWindow`; the kind line; App… dialog over AppsFolder (if it resists, ship without it and record the gap in the PR and `docs/BACKLOG.md`)
- [ ] 3.5 Icon drop-down (D8) with the theme's glyph grid, Automatic and From a file…

## 4. List of positions

- [ ] 4.1 `PositionsList` (D9): expanders per position for the current size, header badge/role/summary/value, item form, Fill, Move up/down, the "more slots stay" note, one open at a time

## 5. Wire into the editor

- [ ] 5.1 Replace the shell's placeholder in the properties pane with the widget's form (page widgets and dock slots with `SizeKey` 1x1); rebuild when the size changes
- [ ] 5.2 Providers section on `BuildStaged` (D11); Save and Test connection unchanged in behaviour

## 6. Verify

- [ ] 6.1 `dotnet build urdeck.slnx -c Release` (0 warnings), `dotnet test`, coverage floor, `dotnet format --verify-no-changes`, Linux tests
- [ ] 6.2 Owner, manual: for each first-party widget, change every setting and watch the preview and the panel; undo each; Stats at 1x1, 2x2, 4x2, 4x4 (positions, Fill, Move, shrink note); a Fahrenheit threshold; Weather place search; Shortcut browse for app, file and folder; Control with a Home Assistant light (actions, dimmer level); Providers section save and test
- [ ] 6.3 `openspec validate editor-forms --strict`
