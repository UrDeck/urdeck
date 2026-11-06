## Context

- First run (`data-folder` spec, "First Run"; `ConfigStore.LoadMissing`): copy `urdeck-config.default.json` (the sample
  page) or write an empty configuration; log it. `--snapshot` and `--demo` never create it.
- Unregistered widget type on a page: `PageHost` logs "Widget type '…' is not registered; leaving its cell empty" and
  draws nothing; in the dock `DockLayout` does the same.
- Undeclared size on a page: drawn anyway (the widget lays itself out in whatever cells it gets). Grid validation
  (`grid-layout`, "Layout Validation") clamps widths and columns, but overlaps are not detected.
- `WidgetPluginLoader` logs load failures (`Failed to load plugin <file>: <message>`) and rejections but keeps no report.
- `editor-shell`: the preview overlays, the properties pane, `PageEditing`; `editor-forms`: the form.

## Goals / Non-Goals

**Goals:** a user never loses settings because something is broken; every broken state says what happened and offers
the obvious next step; a first-time user is greeted.

**Non-Goals:** finding or installing plugins (no marketplace yet: "Find the plugin" from the mock-up is left out);
landscape monitors (backlog item 15); repairing invalid JSON (the shell's InfoBar covers it).

## Decisions

### D1. First run

- `ConfigStore` reports `CreatedOnFirstRun` (true when it copied the sample or wrote the empty configuration this
  start). `App`, after the deck window exists, opens the editor when it is true and shows `WelcomeDialog` (a
  `ContentDialog`): title "Welcome to UrDeck", text "Your deck is showing on <display name>. Pick a start; you can change
  everything afterwards." (or "Your deck will appear on <name> when it is connected."), two large buttons with a
  picture: "Start with the sample page" (a rendered still of the sample page from a preview session, cached) and "Start
  with an empty page", and a line "Prefer the file? It is urdeck-config.json." linking to General's "Show the
  configuration file".
- Sample: nothing changes. Empty: one User transaction "Start with an empty page": `pages` = one page `{ id, name:
  "Main", widgets: [] }`, `startPage` = its id, `dock` = `[]`. Undoable (back to the sample).
- Closing the dialog with Escape = keep the sample.
- A first run with no display connected: the dialog still opens; the text says where the deck will appear.

### D2. Empty page tip

When the selected page has no widgets and the grid is shown (or not), a `TeachingTip` targets the first row's first
cell: "Add your first widget" / "Click any empty cell, or Add widget. Each widget takes a few cells; the page has room
for about N rows." (N from the grid size), action "Add a clock" (adds Clock at its default size at row 1) and close
"Got it". Shown once per page per editor session.

### D3. Plugin load report and widget-type memory

- `WidgetPluginLoader.LoadReport`: list of `PluginFileReport(path, file name, Loaded | Failed(reason) | Skipped(reason),
  widget types provided, provider ids provided, rejected types with reasons)`, rebuilt on each (re)load.
- `cache/widget-types.json`: `{ "<typeId>": { "plugin": "<file name>", "name": "<display name>", "sizes": ["1x1", …] } }`,
  updated after each load with every registered type (types are added and refreshed, never removed automatically, so
  a plugin that fails later is still known).
- Classification of a widget object whose type id is not registered:
  - known in the memory and its plugin file is in the report as Failed/Skipped → **did not load** (with the reason);
  - known but its file is not present any more → **removed** (treated as not installed, with "It was provided by <file>");
  - unknown → **not installed**.

### D4. Placeholder cards and panes

- Preview overlay (editor only, never drawn on the deck): a card at the widget's cells with a hatched outline, a
  warning glyph and two lines ("Focus timer / did not load", "Ticker / not installed"). Selectable like any widget.
- Pane for **did not load**: InfoBar (error) "<Name> did not load. Its plugin, <file>, failed to start: <reason>. Its
  settings are kept and the deck leaves the space empty." Buttons "Show in the log" (opens `urdeck.log`) and "Remove".
- Pane for **not installed**: InfoBar (warning) "<id> is not installed. It stays in the file untouched." Then the
  widget's JSON, formatted, in a read-only monospace `TextBox` (selectable, so the user can copy it), and "Remove".
- In the dock: the same, for dock slots.
- `editor-arrange` (if applied) treats these cards like any widget for moving; `PageEditing` already works on JSON.

### D5. Placement issues

`PageEditing.PlacementIssues(page)` returns, per widget: `UndeclaredSize(declared sizes)` (registered type only),
`Overlaps(other)`, `OffGrid` (column + width beyond the grid, or row beyond the grid's rows: what the layout clamps or
cannot show). Ordered by row.

- Undeclared size: a small ⚠ badge on the card (overlay); the pane's InfoBar (warning) "<w>x<h> is not a <Name> size.
  <Name> comes in <sizes>. Nothing changes until you choose." with buttons for up to two declared sizes that fit at the
  widget's position, nearest first (by the difference in cell count, then preferring the same width): "Make it 2x2",
  "Make it 1x1"; when none fits, "Move it to the first free space" at the nearest declared size.
- Overlap / off-grid: a page-level InfoBar above the preview (warning): "Two widgets overlap: Weather and Stats." / "Stats
  is outside the page." with "Move <the later one> to the first free space".

### D6. Remove

Remove in these panes is the shell's `RemoveWidget` (undoable). Nothing in this change ever edits an unknown widget's
object except removing it whole.

## Risks / Trade-offs

- [The widget-type memory names a plugin the user deleted on purpose] → it says "removed" rather than "failed"; Remove
  clears the widget; the memory file is small and harmless.
- [A first-run welcome on a machine where the user only wanted the deck] → one dialog, Escape keeps the sample, never
  shown again (it depends on the configuration having just been created).
