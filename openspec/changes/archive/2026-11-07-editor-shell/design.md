## Context

- Design: `docs/handoff/2026-11-06-editor-design.md` and `docs/handoff/editor-design/Main.dc.html` (the Pages section;
  its markup is the layout, its `class Component` script the behaviour: `fits`, `blocker`, `firstFree`, `setSize`,
  `addWidget`, `remove`, `undo`, the tabs, the palette, the page pane). The `.dc.html` files need the Claude Design
  runtime and do not render in a browser; read them as source (the markup with `{{holes}}`, `<sc-if>`, `<sc-for>`).
  `States.dc.html` shows the file InfoBar; `Arrange.dc.html` the page list (drag is `editor-arrange`'s).
- Today: `src/UrDeck.Host/SettingsWindow.cs` (271 lines, fixed 560×720 DIP, not resizable, one page: monitor picker,
  then `ProvidersSection`), talking to the host through `ISettingsHost`; `App` keeps one reference and drops it on
  close; `URDECK_SETTINGS_SOAK=1` opens and closes it 20 times (`docs/perf/settings-window.md`).
- `config-document` (applied before this): `ConfigStore.Document` with `Edit`, `History`, `Changed`, `Problem`,
  `IsReadOnly`; `ConfigStore.ConfigChanged` coalesced for the deck.
- `deck-session` (applied before this, amended by this batch): `DeckSession` (engine) with `IDeckPresenter`,
  `WidgetSlot`, `PageSlots`, `PagerState`, `IDeckScheduler`; `MainWindow` is the window presenter.
- `page-navigation` spec: `activePage` index at startup, follow by name after reload.
- The deck and the settings window run on the same UI thread.

## Goals / Non-Goals

**Goals:**
- A first-class editor window that a user who has never seen the JSON can use to build a page from installed widgets.
- The preview is the deck's real rendering (not an imitation), for the deck's display, with live or sample data.
- Every edit is a document transaction: undoable, applied to the deck at once, written behind.
- The shell is the frame later changes fill without reworking it.

**Non-Goals:**
- Generated settings forms (`editor-forms`; until then the pane shows type, size, Remove).
- Drag to move or resize, the dock editing, the page list view (`editor-arrange`).
- Theme and background section, the theme quick-switch in the header (`editor-theme-background`).
- Readings section and picker (`editor-reading-picker`).
- First run, broken or missing plugins, undeclared sizes (`editor-states`). The shell must not crash on them: an
  unknown widget type is drawn as an empty card in the preview and selecting it shows its type id and Remove.
- Landscape grids (backlog item 15). The shell reads the grid's size from the engine; it does not decide it.

## Decisions

### D1. Window and navigation

```
┌ UrDeck ─ urdeck-config.json ──────────────────────────────────────────── _ □ × ┐
│ ┌──────────────┐ ┌───────────────────────────────────────────────────────────┐ │
│ │ ▣ Pages      │ │ Pages                               ↶ Undo  ↷              │ │
│ │ ⚙ Providers ①│ │ [Main ⌂][Home][+]                         [+ Add widget]   │ │
│ │ ▭ Display    │ │ ┌──────── preview stage ────────┐ ┌── properties pane ──┐ │ │
│ │ ⓘ General    │ │ │   ┌─ bezel ─┐                 │ │ Stats                │ │ │
│ │              │ │ │   │ page    │  (scrolls)      │ │ urdeck.widgets.stats │ │ │
│ │              │ │ │   │ dock    │                 │ │ Size [1x1][2x2][4x2] │ │ │
│ │ ● Deck is up │ │ │   └─────────┘                 │ │ (form: editor-forms) │ │ │
│ │   to date    │ │ │ [Fit width|Whole page] Grid   │ │ [Remove]             │ │ │
│ │   Y70 Touch  │ │ │ Readings [Live|Sample]        │ │                      │ │ │
│ └──────────────┘ │ └───────────────────────────────┘ └──────────────────────┘ │ │
└────────────────────────────────────────────────────────────────────────────────┘
```

- `Microsoft.UI.Xaml.Controls.NavigationView` (left mode, always expanded at ≥ 1000 DIP width, compact below),
  items Pages, Providers (with an `InfoBadge` counting providers whose required settings are missing), Display, General;
  footer: the deck status (D9). Later changes insert "Theme and background" after Pages and "Readings" after it.
- Window: resizable; opens at 1280×860 DIP or 90 % of the work area, whichever is smaller, centred as today's rule
  (away from the deck's display); minimum 900×600 DIP. Title "UrDeck". Mica backdrop if available, else the default.
  Light/dark follows Windows (default WinUI behaviour; no custom colours).
- Sections are `UserControl`-free C# classes building XAML in code, as today's window does (`App.xaml` must stay; no
  new XAML files are required, but adding `.xaml` pages is allowed if the implementer finds it clearer: check that the
  unpackaged build still finds them).
- Escape closes the window only when no dialog or flyout is open.
- One window, created per opening and dropped on close, as today (`App` holds one reference).

### D2. The preview is a deck session with a preview presenter

- `ISettingsHost.CreatePreviewSession(PreviewOptions)` returns a `DeckSession` built like the deck's but:
  - **Surface** = the deck display's surface (its size in pixels and scale, from `PlacementController`/`HostContext`);
    when no deck display is connected, the remembered display's last known size, else 1100×3840 at scale 1. Layout,
    text fitting and wrapping are therefore identical to the deck.
  - **Page** chosen by the editor (`session.ShowPage(pageId)`), no swipe, no taps (input off).
  - **Readings**: Live = the host's `ReadingHub`; Sample = a second hub fed only by sample providers built from the
    loaded providers' sample files, as `--demo` builds the deck's (`WidgetPluginLoader(demo: …)`, `SampleProvider`,
    `SampleFile`). In Live, a reading whose provider is not started because a required setting is missing is read from
    the sample hub instead (composite source), so the preview is never a page of dashes.
  - **Theme, background, motion**: the deck's, from the same configuration; the background is drawn (moving if the
    deck's is) so the preview shows the real look; it stops when the window is minimised or hidden (presenter
    `IsShown` false).
- The canvas is a software one: painting the moving background's shader, the pattern and the shadows at 30 fps made the
  editor unusable (measured on the panel, 2026-11-07). The preview therefore paints the background, pattern and underlays
  once into a picture (`DeckFramePainter.PaintCached`) and shows the background and moving borders **still**; a frame paints
  only the cards and the indicator. This answers the open question below.
- `PreviewPresenter` (host): one `SKXamlCanvas` for the whole page (not one per widget: the preview is small and one
  canvas is cheaper), inside a `ScrollViewer`. It paints the session's slots in z-order through a canvas scaled from
  the surface to the canvas size (`canvas.Scale(previewPx / surfacePx)`); the background is painted by the same
  `DeckBackground.PaintAtTime` the snapshot path uses, into the same canvas. Repaint when the session says slots are
  dirty or a frame is due (session's frame source at 30 fps only while something animates).
- Overlays are XAML on top of the canvas, positioned from the session's layout rectangles (converted to DIPs): the
  selection outline with a label ("Stats · 4x2"), the grid overlay (cell lines and a "+" button per empty cell when the
  grid is on), the bezel. The overlays are not drawn into the page, so the page pixels equal the deck's.
- Zoom (opens on **Whole page**, the owner's choice on the panel, 2026-11-07): **Fit width**: the page is scaled to the stage width minus margins, with a cell at most 120 DIP; the stage
  scrolls vertically. **Whole page**: scaled so the whole page (dock and indicator included) fits the stage. The
  choice and the Grid toggle are remembered for the session only.
- Selection hit test: `WidgetHitTest` (engine) on the session's layout, in surface coordinates.
- **Why a second session and not the deck's own:** the preview needs another page, other readings (sample), no input,
  another size and selection overlays; sharing the deck's session would couple the editor to the panel's state. The
  cost is a second set of widget instances while the editor is open; widgets are small and `ReadingHub`
  subscriptions are shared per reading. Alternative considered: `PreviewRenderer` (`src/UrDeck.Preview`): it draws
  still frames with sample data, statically references the first-party widgets (the host loads them as plugins in a
  collectible context), and has no live readings or animation, so it cannot be the editor's preview.

### D3. The editor model in the engine

`src/UrDeck.Engine/Editor/PageEditing.cs` (engine, so it is tested on Linux), all operations returning document
transactions (labels in quotes are the undo labels):

- `GridSize For(surface)`: columns and rows the engine's layout gives for the deck's surface (`GridLayoutManager` /
  `ChromeLayout`; today 4 by about 12 on the Y70). The editor never hard-codes 4 or 12.
- `bool Fits(page, widgetIndex?, col, row, w, h)`, `WidgetRef? Blocker(...)`, `Cell? FirstFree(page, w, h, except?)`:
  as `Main.dc.html`'s `fits`, `blocker`, `firstFree` (row-major scan).
- `AddWidget(pageId, typeId, at?)` "Add <name>": default size = the type's first declared size in the order 2x2, 4x2,
  1x1, 2x1, 4x1, 4x4 that it supports (Clock 4x2, Stats 2x2, Weather 4x2, Shortcut 1x1, Control 1x1, Now playing
  4x2); position = `at` clamped to fit, else first free; configuration = the type's default configuration as the
  catalog writes it (`PreviewWidgets.DefaultConfig` logic: move the needed part into the engine if it is in
  `UrDeck.Preview`). No space: no transaction; the caller shows "This page has no free <size> space".
- `Resize(widget, w, h)` "Make <name> <w>x<h>": keeps the row, clamps the column so it fits the width; if it does not
  fit, no transaction and a `FitProblem(blocker name or "past the bottom", firstFree)`; `MoveTo(widget, cell, w, h)`
  for the InfoBar's "Move it to the first free space".
- `RemoveWidget(widget)` "Remove <name>".
- Pages: `AddPage` "Add a page" (name "Page N", empty, new id, inserted after the current page), `RenamePage`,
  `DuplicatePage` (new ids, " copy" suffix), `MovePage(from, to)`, `DeletePage` (if it is the start page, the same
  transaction sets `startPage` to the page that follows, else the one before), `SetStartPage`. The last page cannot be
  deleted.
- `WidgetRef` = (page id or dock, index) resolved fresh against the document each time; the editor's selection keeps
  a `WidgetRef` plus a runtime key (D5) so it survives undo and outside changes when the widget still exists.
- Display names for messages ("Stats \"CPU load\" is in the way"): type name plus the first label-ish value
  (`label`, first slot's reading label, `target` file name), as `describe()` does in the mock-up.

### D4. Page ids and the start page

- `PageConfig.Id` (`id`): a short random string (`p-` + 6 base-36 characters), unique in the file. On load, pages
  without an id, or with a duplicate, get a new one through a **Host** transaction (so it is written once and never an
  undo step). `urdeck-config.default.json` and `src/UrDeck.Preview/SamplePage/urdeck-config.json` get ids in the repo.
- `UrDeckConfig.StartPage` (`startPage`): a page id. `ActivePage` is removed (no migration: an old `activePage` key is
  an unknown key and stays in the file, unread).
- Resolution (engine, one function used by the deck, `--snapshot` and `preview render`): the page whose id equals
  `startPage`; else the first page, logging `Start page '<id>' not found; starting on the first page` once per load.
- `PageNavigator` keeps the current page's **id**; `Reload` finds it by id, else clamps the previous index (the
  existing fallback). Duplicate names no longer matter.
- `--page` on `--snapshot` and `preview render` keeps taking a 1-based number (unchanged CLI).

### D5. The deck follows the editor

- `ISettingsHost.ShowPageOnDeck(pageId)` → the deck session's navigator moves to that page with the normal settle
  animation (no swipe needed). Called when the editor's page tab changes and when the editor opens (the deck jumps to
  the editor's first shown page: the page the deck is on, so opening changes nothing).
- The editor opens on the page the deck is currently showing.
- Nothing is written. Closing the editor leaves the deck where it is.
- While the editor is open, a swipe on the panel still works; the editor's tab follows the deck
  (`ISettingsHost.DeckPageChanged`), so the two never disagree.

### D6. Properties pane (shell scope)

- Widget selected: icon letter, type name, type id (small, monospace), a size `RadioButtons`-like row of the type's
  declared sizes (1x1 first), the fit `InfoBar` (warning: "<size> does not fit here. <blocker> is in the way." with
  "Move it to the first free space" and "Keep the size"), then a placeholder line ("Settings for this widget appear here
  in a later version") that `editor-forms` replaces, then Remove (a danger-styled button at the bottom). A dock slot:
  "In the dock: the same on every page, drawn smaller, always 1x1", no size row.
- Nothing selected: the page's Name (`TextBox`, writes on each change, merge key = path), "Start page" (shows ⌂ "This
  is the start page" or a "Make this the start page" button), Order (Move earlier / Move later), "Page dots on the deck"
  (`RadioButtons` for `pager.indicator`: Automatic, Always, Show then fade, Off; "Applies to every page"), Dock
  ("N of 4 slots"), Delete this page (disabled with one page; a `ContentDialog` to confirm when it has widgets).
- Unknown widget type (plugin missing): type id and Remove only (full state handling is `editor-states`').

### D7. Palette

A pane that replaces the properties pane ("Add a widget", close button): one row per installed widget type from the
widget registry (name, `[Widget]` description, sizes as chips, "Plugin: <assembly name> <version>" for non-first-party),
sorted by name; a final disabled "Web page · coming later" row. Opened by "Add widget" (position: first free) or a "+"
on an empty cell (position: that cell). Escape closes it.

### D8. Undo and the document in the editor

- Header buttons bound to `Document.History` (`CanUndo`, `UndoLabel` as tooltip "Undo Make Stats 4x2"); Ctrl+Z,
  Ctrl+Y and Ctrl+Shift+Z.
- Every editor action is one `Document.Edit` with origin User. Text boxes pass the setting's path as `mergeKey`.
- After any `Document.Changed`, the editor re-reads what it shows from `Root` (no separate editor model to keep in step).
  Selection is kept if its runtime key still resolves; else cleared to the page pane.
- Read-only document: every editing control disabled, the InfoBar of D10.

### D9. Deck status (navigation footer)

A small card: a dot and one line, and a second line:

| State | Line | Second line |
|---|---|---|
| Shown, nothing pending | Deck is up to date | Shown on <display name>. Changes reach it as you make them. |
| Write pending | Saving… | (same) |
| Deck display absent | Deck is waiting | Waiting for <remembered display name>. Changes are saved. |
| Write failed | Could not save | The error, and "Changes are kept and saved again with the next one." |
| Read-only | Read only | Written by a newer UrDeck. |

### D10. File InfoBars (top of the Pages section)

- Outside change (Informational): "urdeck-config.json changed outside the editor. The editor took the new file." with
  "Undo the reload" (undoes that history step). Closes on the next edit.
- Invalid file (Error): "urdeck-config.json is not valid (line L, column C). The deck keeps the last good version."
  Stays until a valid file is read.
- Newer version (Warning): "This configuration was written by a newer version of UrDeck. It is shown but cannot be
  changed here."

### D11. General section

- Version (`UrDeckAppVersion`, "0.0.0-dev" for a build), "Check for updates automatically" (`ToggleSwitch` on
  `updates`: on = absent/`auto`, off = `off`; a document edit), "Start with Windows" (`ToggleSwitch` over
  `RunKeyAutostart`, as the tray entry does; not in the file), "Show the configuration file" (opens Explorer with the
  file selected), "Open the log" (opens `urdeck.log`).

### D12. Keyboard

The preview is a focusable element: arrow keys move the selection to the nearest widget in that direction (by card
centre), Home/End to the first/last widget, Enter moves focus to the properties pane, Delete removes the selected widget
(undoable), Escape clears the selection. Page tabs are a `TabView`-like row reachable by Tab; Ctrl+PageUp/PageDown
switch pages. Each widget card has an automation name ("Stats, 4x2, row 7").

## Risks / Trade-offs

- [Two sessions double widget work while the editor is open] → measured in the cost requirement; the preview presenter
  stops painting and the background stops when the window is minimised; everything is released on close.
- [Painting at the deck's surface size scaled down blurs fine text at small zoom] → acceptable for a preview; Fit width
  keeps cells near 92 DIP, which is legible. A crisper preview (layout at preview scale) would no longer equal the deck.
- [The deck rebuilds on every coalesced change] → `config-document` D6; measure while typing in a page name and record
  it in `docs/perf/settings-window.md`.
- [Plugin hot reload while the editor is open] → both sessions release their slots on `PluginsChanged` (session
  behaviour from `deck-session`); a test with a fake presenter for two sessions.
- [Following the editor fights a user swiping the panel] → the tab follows the deck (D5), last action wins.

## Migration Plan

No migration (pre-release). `activePage` in an existing file is ignored; the start page becomes the first page until
the user picks one.

## Open Questions

- (Answered: the preview shows a still background; see D2.)
