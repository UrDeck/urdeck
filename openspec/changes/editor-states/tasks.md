Before starting: `editor-forms` must be applied (and therefore `editor-shell` and its prerequisites). Read
`proposal.md`, `design.md` (D1–D6), the specs here and `docs/handoff/editor-design/States.dc.html`. Model: Sonnet.

## 1. Engine

- [ ] 1.1 `ConfigStore.CreatedOnFirstRun`; test with an empty temp data folder (true) and an existing file (false)
- [ ] 1.2 `WidgetPluginLoader.LoadReport` and `cache/widget-types.json` (D3); classification function (did not load, removed, not installed) with tests using fake plugin folders (a broken DLL, a missing file, an unknown id)
- [ ] 1.3 `PageEditing.PlacementIssues` (D5) and the nearest declared sizes; tests: undeclared size with two, one and no fitting suggestions, overlap, off-grid

## 2. Host

- [ ] 2.1 First run (D1): `App` opens the editor and `WelcomeDialog` after the deck starts; the sample picture from a preview session; empty start as one User transaction
- [ ] 2.2 Empty page `TeachingTip` (D2)
- [ ] 2.3 Placeholder cards in the preview overlay and the two panes (D4), page widgets and dock slots
- [ ] 2.4 Undeclared size badge and pane InfoBar; overlap/off-grid page InfoBar (D5)

## 3. Docs

- [ ] 3.1 The site's getting-started page describes the welcome; the troubleshooting page describes "did not load" and "not installed" as the editor shows them

## 4. Verify

- [ ] 4.1 `dotnet build urdeck.slnx -c Release` (0 warnings), `dotnet test`, coverage floor, `dotnet format --verify-no-changes`, Linux tests
- [ ] 4.2 Owner, manual: a portable copy with an empty data folder (welcome, both choices, Undo); a page with an unknown type id; a plugin DLL replaced by a broken file; a Stats widget edited to 3x1 in the file; two overlapping widgets in the file
- [ ] 4.3 `openspec validate editor-states --strict`
