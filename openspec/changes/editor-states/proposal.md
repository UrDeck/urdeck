## Why

The editor must stay calm and helpful when the file and the installed plugins disagree, and it must greet a new user
instead of leaving them with a sample page they did not choose (`docs/handoff/2026-11-06-editor-design.md`, artboard 6;
`docs/handoff/editor-design/States.dc.html`). Today the first run silently copies the sample page and nothing opens; a
widget whose plugin failed or is not installed is an empty cell and a log line; a widget at a size its type does not
declare is drawn anyway with no word; two widgets that overlap in a hand-edited file are drawn on top of each other.

**Order:** after `editor-forms` (the states replace or extend the properties pane's form). Independent of
`editor-reading-picker`, `editor-theme-background` and `editor-arrange`.
**Model:** Sonnet.

## What Changes

- **First run opens the editor** with a welcome: the deck already shows the sample page on its display (named);
  the user chooses "Start with the sample page" or "Start with an empty page" (one empty page, an empty dock; one undo
  step). An empty page shows a `TeachingTip` "Add your first widget" pointing at the first row, with "Add a clock".
- **A widget whose plugin did not load**: the preview draws a placeholder card in its cells ("Focus timer did not
  load"); the properties pane says which plugin failed and why, with "Show in the log" and "Remove". Its settings stay
  in the file untouched; the deck leaves its space empty, as today.
- **A widget that is not installed**: the placeholder says "<id> is not installed"; the pane shows the widget's JSON
  read-only and "Remove". Its settings stay in the file.
- **A size the widget does not declare**: the card is marked; the pane says "2x1 is not a <type> size", lists the
  declared sizes, and offers the nearest declared sizes that fit as buttons. Nothing changes until the user chooses; the
  deck keeps drawing the widget as today.
- **Overlapping or off-grid widgets** from a hand-edited file: the page shows a warning naming them, with "Move <name>
  to the first free space".
- The engine keeps a **plugin load report** (each plugin file: loaded, failed with the reason, rejected types with the
  reason) and remembers which widget types each plugin provided (`cache/widget-types.json`), so the editor can tell "did
  not load" from "not installed".

## Capabilities

### New Capabilities

- `editor-states`: the editor's first run, missing and failed plugins, undeclared sizes and invalid placements.

### Modified Capabilities

- `data-folder`: "First Run" opens the editor with the welcome after creating the configuration.

## Impact

- `src/UrDeck.Engine/Plugin/WidgetPluginLoader.cs`: `LoadReport`, the widget-type memory file; tests.
- `src/UrDeck.Engine/Editor/PageEditing.cs`: `PlacementIssues(page)` (undeclared size, overlap, off-grid) and the nearest
  declared sizes; tests.
- `src/UrDeck.Host/Editor/`: `WelcomeDialog`, empty-page `TeachingTip`, placeholder cards in the preview overlay, the
  pane's state views; `App` opens the editor on first run.
