Before starting: `config-document` and `deck-session` must be applied (and archived, so that
`openspec/specs/host-shell/spec.md` has config-document's "Configuration Management"; this change's delta of it builds on
that text). Read `proposal.md`, `design.md` (D1–D12), the specs here, `docs/handoff/2026-11-06-editor-design.md`
(including "Decisions after review") and `docs/handoff/editor-design/Main.dc.html` (read as source: markup = layout,
script = behaviour; it does not render in a browser). Model: **Opus**. Ask the owner for screenshots of the canvas if a
layout detail is unclear.

## 1. Page identity and the start page (engine)

- [ ] 1.1 `PageConfig.Id`, `UrDeckConfig.StartPage`; remove `ActivePage` and `CurrentPage`'s use of it; one `StartPageResolver` used by `MainWindow`, `SnapshotCommand` and `PreviewRenderer` (`PageSelection.TryChoose` takes the resolved index)
- [ ] 1.2 Assign missing or duplicate ids on load as a Host transaction (D4); id format `p-` + 6 base-36 characters
- [ ] 1.3 `PageNavigator` follows by id on `Reload` (fallback: clamp the previous index)
- [ ] 1.4 Ids in `src/UrDeck.Host/urdeck-config.default.json` and `src/UrDeck.Preview/SamplePage/urdeck-config.json`; `site/decks/*` if they carry pages
- [ ] 1.5 Remove `UrDeckConfig.Icon` (`icon`): nothing reads it (checked 2026-11-06; grep again), so it would be a setting with no home in the editor; an `icon` key in a file stays as an unknown key
- [ ] 1.6 Tests: resolution (found, missing, absent), ids assigned once and written once, reload by id after a rename and after a reorder, `page-navigation` scenarios

## 2. Editor model (engine)

- [ ] 2.1 `src/UrDeck.Engine/Editor/PageEditing.cs` per D3: grid size from the layout, `Fits`, `Blocker`, `FirstFree`, `AddWidget` (default size order and default configuration; move `PreviewWidgets.DefaultConfig`'s logic into the engine if needed and make `UrDeck.Preview` call it), `Resize` with `FitProblem`, `MoveTo`, `RemoveWidget`, page operations including start-page handling on delete, display names for messages
- [ ] 2.2 `WidgetRef` with a runtime key for selection (survives undo when the widget still exists)
- [ ] 2.3 Tests on Linux for every operation, including "no free space", resize past the bottom, delete the start page (next, or previous for the last), duplicate gives new ids, undo of each restores the exact JSON

## 3. Deck session for the preview

- [ ] 3.1 Confirm the `deck-session` amendment items exist (several sessions in a process, injected readings source, page set from outside, input off, layout rectangles exposed, shown/hidden); add what is missing in the engine with tests (two sessions with a fake presenter, plugin reload releases both)
- [ ] 3.2 Composite readings source for the preview: Live = host hub, with readings of providers missing required settings served from a sample hub; Sample = sample hub only (build the sample hub as `--demo` does); test with fakes
- [ ] 3.3 `ISettingsHost`: `Document`, `CreatePreviewSession(options)`, `DeckSurface`, `ShowPageOnDeck(pageId)`, `DeckPageChanged` event, deck status (shown on / waiting for), widget catalog (descriptors with name, description, sizes, plugin name and version, first-party flag)

## 4. Window and sections (host)

- [ ] 4.1 `EditorWindow` replacing `SettingsWindow` (D1): NavigationView with Pages, Providers (InfoBadge), Display, General; size and placement rules; minimum size; Escape rule; one window per opening (keep `App`'s lifetime handling and the soak hook working)
- [ ] 4.2 Move the monitor picker to the Display section and drop the resolution text; move `ProvidersSection` unchanged into Providers
- [ ] 4.3 General section (D11)
- [ ] 4.4 Deck status card (D9) bound to document state and `ISettingsHost` status

## 5. Pages section (host)

- [ ] 5.1 `PreviewPresenter` (D2): one `SKXamlCanvas` in a `ScrollViewer`, painting the session's slots and background scaled from the deck surface; repaint on dirty slots; frames only while animating; stops when minimised or when another section is shown
- [ ] 5.2 Zoom (Fit width, Whole page), Grid overlay with "+" on empty cells, bezel, Live/Sample switch, selection outline and label as XAML overlays
- [ ] 5.3 Page tabs (start page marked ⌂), "+" add page, a ⋯ menu per tab: Rename, Duplicate, Move earlier, Move later, Make this the start page, Delete (with the confirmation `ContentDialog`)
- [ ] 5.4 Properties pane (D6): widget view (type, sizes, fit InfoBar with "Move it to the first free space" / "Keep the size", placeholder line for settings, Remove), dock slot view, page view (name, start page, order, indicator mode, dock count, delete), unknown type view
- [ ] 5.5 Palette (D7) opened by "Add widget" and by a cell's "+"
- [ ] 5.6 Undo/Redo buttons, tooltips and shortcuts (D8); re-read from `Root` on every change; selection kept by runtime key
- [ ] 5.7 File InfoBars (D10) from `Document.Problem`, outside changes and `IsReadOnly`; read-only disables editing
- [ ] 5.8 Keyboard (D12) and automation names
- [ ] 5.9 The deck follows the editor (D5) in `App`/`MainWindow` (or the window presenter); the editor follows a swipe

## 6. Docs

- [ ] 6.1 `AGENTS.md` (the settings window paragraph and the soak note), README (the editor first), `site/src/content/docs/pages-and-dock.md` and the getting-started page: describe the editor first and the file second; `startPage` and page ids replace `activePage` in the configuration reference
- [ ] 6.2 `docs/perf/settings-window.md`: re-run `URDECK_SETTINGS_SOAK=1`, measure idle with the window on Display, the preview's CPU on the sample page (Live, still background), and the deck's rebuild rate while typing a page name; record the numbers

## 7. Verify

- [ ] 7.1 `dotnet build urdeck.slnx -c Release` (0 warnings), `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format --verify-no-changes`, the Linux test projects, goldens unchanged, site build
- [ ] 7.2 Owner on the panel (manual, as `verification-preference` asks): open the editor; the preview matches the panel; add each widget type, resize with and without room, remove, undo/redo each; add, rename, reorder, duplicate and delete pages; change the start page and restart; swipe on the panel while the editor is open and watch the tab follow; switch Live/Sample; change the file in a text editor (InfoBar, Undo the reload); save invalid JSON; close and reopen 20 times (soak)
- [ ] 7.3 `openspec validate editor-shell --strict`
