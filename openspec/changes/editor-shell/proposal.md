## Why

Milestone 3's goal is that **a new user builds and changes a page without opening a file** (`docs/ROADMAP.md`,
backlog item 11). The design is done (`docs/handoff/2026-11-06-editor-design.md`, the canvas sources in
`docs/handoff/editor-design/`), and the decisions after its review are recorded in that handoff's "Decisions after
review" section. This change is the editor's frame: the settings window grows into the editor, with a live preview of
the page drawn by the deck's own renderer, page management, and the simple widget operations (add, size, remove),
writing the configuration as the user edits, with undo. The later changes (`editor-forms`, `editor-reading-picker`,
`editor-theme-background`, `editor-arrange`, `editor-states`) fill its sections.

It also fixes what an editor makes visible in today's page model: the start page is an index (`activePage`), so
reordering or deleting pages silently changes it, and the deck follows the current page by name, which a rename breaks.

**Order:** after `config-document` (the editor edits through it) and `deck-session` (the preview is a second
presenter of a deck session; its gate on the specialized-display spikes is removed by this batch). Before every other
`editor-*` change.
**Model:** **Opus.** A second presenter of the deck session in another window, render scale and composition in WinUI 3,
plugin hot reload with two sessions alive, and the deck following the editor are the hard parts of the milestone.

## What Changes

- **The settings window becomes the editor**: a resizable window titled "UrDeck" with a `NavigationView`: **Pages**
  (the editor), **Providers** (today's section, moved), **Display** (today's monitor picker, moved, without the
  resolution) and **General** (updates, start with Windows, the version, show the configuration file, open the log).
  Later changes add Theme and background and Readings. Pages opens first. A left click on the tray icon still opens it.
- **The page preview**: the current page, the dock and the page indicator drawn by a deck session of its own (the
  same renderer, theme, background, widgets and layout as the deck, laid out for the deck's display), in a bezel,
  scrolling at "Fit width" or scaled to "Whole page", with a cell grid overlay. **Live** readings by default, **Sample**
  readings (the providers' sample files) on a switch; a provider that is not connected shows its sample values in Live.
- **Selection**: a click (or the keyboard) selects a widget or a dock slot; the properties pane shows its type, its
  sizes as buttons, Remove, and (until `editor-forms`) a line saying its settings come next. Nothing selected shows the
  page's settings: name, start page, page indicator mode, delete.
- **Add a widget**: a palette of the installed widget types (name, sizes, a line of description, the plugin that
  provides it, a disabled "Web page" entry for milestone 4); a new widget goes to the clicked empty cell or the first
  free space, with the type's default configuration. A size that does not fit says what is in the way and offers the
  first free space.
- **Pages**: tabs above the preview; add, rename, duplicate, move earlier or later, delete (asks first, undoable), and
  "Make this the start page".
- **Page identity and the start page**: each page gets a stable `id`; `startPage` names the page shown at startup
  **BREAKING** (configuration: replaces `activePage`; no migration, the product is pre-release). A missing or orphaned
  `startPage` means the first page, and the editor says so. Deleting the start page through the editor moves it in the
  same undo step.
- **The deck follows the editor**: while the editor is open, the deck shows the page selected in the editor (never
  written to the file); on close the deck stays where it is. After any reload the deck follows its page by id.
- **Undo and Redo** in the header (the document's history), and a **deck status** in the navigation pane ("Deck is up
  to date · Shown on <display>", waiting for a display, could not save).
- **File states** as `InfoBar`s: changed outside the editor (with "Undo the reload"), not valid JSON (line and column),
  written by a newer UrDeck (read-only).
- The grid's columns and rows come from the engine's layout for the deck's display, never assumed to be 4 by 12.

## Capabilities

### New Capabilities

- `editor`: the editor window's sections, the page preview, Live and Sample readings, selection, adding, sizing and
  removing widgets, page management, undo and redo, the deck status, the file states, the General section, keyboard use.

### Modified Capabilities

- `settings-window`: the window is resizable, has sections, opens on Pages; the monitor picker moves to Display and
  shows no resolution; its cost requirement covers the preview.
- `page-navigation`: pages have ids; `startPage` replaces `activePage`; the deck follows pages by id and follows the
  editor while it is open.
- `host-shell`: "Configuration Management" documents `startPage` and page ids instead of `activePage`.

## Impact

- `src/UrDeck.Host`: `SettingsWindow` rebuilt as the editor shell (`Editor/` folder: `EditorWindow`, `PagesSection`,
  `PreviewPresenter`, `PropertiesPane`, `WidgetPalette`, `GeneralSection`, `DeckStatus`); `ProvidersSection` and the
  monitor picker become sections; `ISettingsHost` grows (document, session factory, show page, deck status, widget
  catalog); `App`/`MainWindow` wire the deck to follow the editor.
- `src/UrDeck.Engine`: `PageConfig.Id`, `UrDeckConfig.StartPage` (and `ActivePage` removed), `PageNavigator` by id,
  `PageSelection` for `--snapshot` and `preview render`; an editor model (`Editor/PageEditing.cs`: fits, blocker, first
  free space, add, resize, remove, page operations as document transactions) with tests; the deck session gains what
  `deck-session`'s amendment lists for a second presenter.
- `src/UrDeck.Preview`, `tools/UrDeck.Cli`: `--page` and the sample page use `startPage`; the sample page and
  `urdeck-config.default.json` get page ids.
- Docs: `docs/perf/settings-window.md` re-measured; the site's "pages and dock" page and the README describe the
  editor first; `AGENTS.md`'s settings window notes updated.
