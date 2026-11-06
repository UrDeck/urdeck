# Handoff: the editor's design (milestone 3, backlog item 11)

**For:** whoever writes the OpenSpec proposals for the editor, and then builds them. **Date:** 2026-11-06.
**Answers:** [the design brief](2026-11-05-editor-design-brief.md). Read that first; this page does not repeat it.
**Mock-ups:** the Claude Design canvas "UrDeck Configuration Editor" (ask the owner for the link; it is private until
shared). Its source is in [`editor-design/`](editor-design/), one `.dc.html` file per artboard (see "The files" below).

This is the baseline the proposals start from, not the spec. Where a proposal finds a better answer, the proposal wins
and this page gets a line saying so.

## The answers to the brief's open questions

| Question | Answer in the design |
|---|---|
| A section of the settings window or a window of its own? | **The settings window grows into the editor.** One window, a `NavigationView` with Pages, Theme and background, Readings, Providers and Display. A left click on the tray icon still opens it. Pages opens first once there is a configuration. |
| Where does the monitor picker go? | Display, as it is today (name, orientation, number, Identify, Choose automatically). No resolutions shown. |
| Live readings or sample data in the preview? | **Live by default**, with a Live / Sample switch under the preview. Sample is the providers' `sample.json`, as `PreviewRenderer` already uses. A provider that is not connected shows its sample values in the preview, and the form says why. |
| Apply as you edit, or on Save? Undo? | **As you edit.** Each change writes `urdeck-config.json` (debounced) and the deck reloads as today. Undo and Redo in the window's header, covering every edit in the session. There is no Save button; a small "Deck is up to date" status in the navigation pane says where the deck is shown. |
| A file edited by hand while the editor is open | The editor reloads it and says so in an `InfoBar` ("changed outside the editor"), keeping the undo history so the reload itself can be undone. |
| How a save keeps the user's JSON | Not designed visually; the design assumes the editor edits the JSON tree in place: key order kept, keys it does not know (from plugins, or newer versions) kept untouched, only changed values rewritten. **Superseded** (see "Decisions after review"): unknown keys are kept, but the file is written whole and hand formatting is not a goal. |
| How a 1100x3840 portrait page is shown on a landscape desktop | **A scrolling column at "Fit width"** (cells about 92 px), with "Whole page" as the other zoom. The page sits in a dark bezel. A cell grid overlay (toggle) shows the four columns and twelve rows. |
| Landscape monitor (item 15) | Suggested: **cells stay square, the page gets more columns** (7 by 4 at 16:9), the dock moves to the right edge. The alternative (four columns, huge cells, scrolling) is shown as the second option. Still open. |

## The six artboards

1. **Edit a widget, with a live preview** (`Main.dc.html`, interactive). The whole editing loop: page tabs, the preview,
   selection, a properties pane whose form is generated from a per-widget schema, sizes, the "does not fit" `InfoBar`,
   the widget palette, the dock, page settings, the reading picker dialog, Undo / Redo, the theme quick-switch.
2. **Theme and background** (`ThemeBackground.dc.html`, interactive). Themes as pictures from the real renderer
   (the goldens). Backgrounds in two rows: "Suggested for <theme>" (the theme's `background.default` first, then the
   shaders whose manifest `pairsWith` names the theme, which is what `PairingIndex` reads) and "All backgrounds". Above
   them: Theme's choice, None, Picture…. Motion (Moving / Still; still is `speed` 0), the shader's parameters generated
   from its manifest, and "Dim behind cards" (the scrim). None and Picture hide Motion and the shader options; Picture shows a
   drop area, then the chosen file with Change…. The shader tiles stay visible and selectable in every state.
3. **Arrange** (`Arrange.dc.html`). Moving snaps to cells with a green target outline; over a taken space it turns orange
   and names what is in the way, and same-size widgets swap. Resizing shows the declared sizes as dashed stops and snaps to
   the nearest that fits. Pages as a reorderable list. The dock: drag on, along, off, plus menu equivalents for keyboard.
4. **Reading picker and provider states** (`ReadingPicker.dc.html`). A `ContentDialog` with search, provider tabs,
   area and kind filters for Home Assistant, the live value per row, an "action" tag on readings that can be switched, and
   "type an id by hand". The Home Assistant "not connected" state on the widget form and in Providers (address, token in
   a `PasswordBox`, Test connection; the token lives in Credential Manager as `SecretStore` does today).
5. **The control kit** (`ControlKit.dc.html`, light theme). The twelve controls every generated form is built from. This
   is the input to the SDK's settings metadata; see the next section.
6. **States** (`States.dc.html`). First run (sample page or empty page, with a `TeachingTip`), a widget whose plugin
   failed to load, a widget that is not installed, a size the widget does not declare, the file changed outside the
   editor, and the landscape monitor.

## The control kit: what the settings metadata must express

A widget or provider declares each setting's kind; the editor picks the control. No plugin ships UI.

| Kind | Control | The declaration carries | Used by |
|---|---|---|---|
| Toggle | `ToggleSwitch` | default | Clock, Weather, Now playing |
| Choice | `RadioButtons` (up to three options), `ComboBox` (four or more) | options with labels, default | Clock style, Stats gauge (9), Weather unit |
| Text | `TextBox` | where an empty value comes from (shown as the placeholder) | labels, header, subtitle |
| Number | `NumberBox` | min, max, unit (fixed, or from a reading setting) | decimals, min/max, warning/critical |
| Range | `Slider` | min, max, step | forecast days |
| Reading | button opening the reading picker | filter: has a range, has an action, provider | Stats slots, Control |
| Action | `ComboBox` filled by the provider | the reading setting it belongs to | Control |
| Place | `AutoSuggestBox` | stores the name and the coordinates | Weather |
| Launch target | `TextBox` + Browse (app, file, folder) | which kinds are allowed | Shortcut |
| Icon | glyph grid, Automatic, From a file | the theme's icon set | Shortcut, Control |
| Secret | `PasswordBox` | the setting it belongs with (Home Assistant address) | providers only |
| List of positions | one `Expander` per position | the item's settings, and the positions per size with their role (gauge or text) | Stats slots |

Modifiers any setting can carry:

- **sizes**: shown at other sizes but dimmed with "Only at 4x4", never hidden (Weather's forecast days).
- **when**: hidden until another setting has a value (Control's level reading needs `style: dimmer`).
- **default from**: the region, the chosen reading, or the theme; the editor shows where an unset value comes from.
- **label, help, group, order**: plain, localisable words from the plugin; more than eight settings fold into `Expander`s.

Deliberately absent: colour, font, font size, padding, any pixel value. Those belong to the theme. A setting kind the
editor does not know is edited as text, with a line saying the plugin uses a newer kind of setting.

`Main.dc.html` has this as data, which is the quickest way to see the shape: `SCHEMA`, `SLOT_SCHEMA` and `POS` in its
script are the six built-in widgets' settings written in these kinds, and `field()` is the generator.

## Rules the mock-ups follow

- Fluent (WinUI 3) vocabulary only, following the Windows light and dark setting (both are in the canvas: the Main
  artboard has an "appearance" tweak).
- The preview is drawn by the deck's renderer in the real editor. The mock-ups imitate it in HTML from the theme files'
  tokens, and the background preview blends two pictures; neither is a reference for how anything looks.
- The user never sees pixels or scaling. Sizes are cells (`4x2`), never pixels.
- A widget does not move because another one moved. Nothing is placed where it does not fit; the user is told what is in the way and
  offered the first free space.
- A widget that cannot be shown (plugin failed, not installed, undeclared size) keeps its settings in the file untouched,
  and the deck leaves its space empty.
- The palette has a disabled "Web page" entry for milestone 4.

## Found while designing

- **The sample page has Stats at 2x1** (`src/UrDeck.Preview/SamplePage/urdeck-config.json`, the CPU and GPU cards), but
  `StatsWidget` declares only 1x1, 2x2, 4x2 and 4x4. The editor would flag it ("2x1 is not a Stats size"). Either the
  widget gains 2x1 or the sample page changes; that is a separate change.
- The goldens are 1100x1920, half the page. The theme pictures in the canvas are their top 1380 pixels.

## Suggested order of proposals

Each is useful on its own, in the brief's priority order:

1. **Settings metadata in the SDK** (the kinds and modifiers above), with the built-in widgets declaring theirs.
2. **Editor shell**: the settings window becomes the editor (navigation, Pages section, the preview through
   `PreviewRenderer`, selection, Live / Sample), writing the configuration as you edit, with undo. **Superseded:** the
   preview is a second presenter of `deck-session`, not `PreviewRenderer` (see "Decisions after review").
3. **Generated forms** from (1), including the list of positions and the size buttons.
4. **Reading picker** over `IReadingBrowser`, with the live values.
5. **Theme and background** over `ThemeCatalog`, `BackgroundCatalog` and `PairingIndex`.
6. **Arrange**: drag to move and resize, pages, the dock.
7. **States**: first run, broken and missing plugins, undeclared sizes, external edits.

## The files

`editor-design/` holds the canvas as it was on 2026-11-06: `canvas.json` (the layout of the artboards) and one
`.dc.html` per artboard. They are Claude Design components: HTML with `{{holes}}`, `<sc-for>` / `<sc-if>` and a
`class Component extends DCLogic` script. They do not run outside the canvas (they need its runtime and its uploaded
pictures, the `/_blob/...` addresses), but they read as plain source. The pictures they show are the repository's own:
`tests/UrDeck.Engine.Tests/Goldens/<theme>-page.png` and `src/UrDeck.Engine/Backgrounds/Builtin/<name>/sample.png`.

## Decisions after review (2026-11-06)

An exploration session reviewed this page and the six artboards against the code and wrote the OpenSpec changes. Where
this section and the text above disagree, this section (and the changes) win.

**The editor is the product's front door.** Hand-edited JSON is not a design constraint: no effort goes into keeping
comments, formatting or key order. Data is still never lost: keys of plugins that are not installed and of newer versions
survive every write.

| Topic | Decision | Change |
|---|---|---|
| Preview | A second presenter of a deck session (the deck's own widgets, readings, theme, background and layout for the deck's display), not `PreviewRenderer`, which draws stills with statically referenced widgets. `deck-session` is no longer gated on the specialized-display spikes. | `deck-session` (amended), `editor-shell` |
| Saving | A configuration document in the engine: a JSON tree in memory, edits by path, snapshot undo (100 steps), changes applied in the process first, the whole file written 0.5 s after the last change, own writes recognised by content (not a 2 s window), no write on exit. | `config-document` |
| Settings metadata | One attribute family for widgets and providers (`UrDeck.Sdk.Settings`); help text lives in the code and the site reads it from the catalog; analyzer rule URDECK006 for undeclared settings; providers answer suggestion queries (Weather: places), so the editor never calls a web service itself. | `settings-metadata` |
| Clock `textColor` / `fontSize` | Removed. `fontSize` could only shrink the time; `textColor` switched off a theme's value fill. The size comes from the card, the look from the theme. | `settings-metadata` |
| Three-state text (Stats label: absent, empty, text) | An "allows none" modifier, shown as a "Show a label" switch above the box. | `settings-metadata`, `editor-forms` |
| Thresholds in °F | Shown and typed in the display unit, stored canonical; the SDK exposes the conversion. | `settings-metadata`, `editor-forms` |
| Stats 2x1 | Declared (the sample page uses it; the widget already draws it). | `settings-metadata` |
| Mock-up keys | The mock-up's `SCHEMA` keys drifted from the code (`hour` vs `format`, a free-text `header`); the code's keys win. | `settings-metadata` (inventory, design D9) |
| Unit of a slot | A 13th kind, "Unit": a choice filled from the chosen reading's display units, hidden for single-unit readings. | `settings-metadata` |
| Start page | Pages get stable ids; `startPage` (an id) replaces `activePage` (an index); a missing start page means the first page and the editor says so; deleting the start page moves it in the same undo step. The deck follows pages by id. | `editor-shell` |
| Deck and editor | While the editor is open the deck shows the editor's page (never written); a swipe on the panel moves the editor's tab; closing leaves the deck where it is. | `editor-shell` |
| Navigation | Pages, Theme and background, Readings, Providers, Display, General (version, updates, start with Windows, show the file, open the log). "Open the file" moves into General. | `editor-shell`, `editor-reading-picker`, `editor-theme-background` |
| Display section | No resolution shown (the monitor picker spec changes accordingly). | `editor-shell` |
| Unused `icon` key | Removed from the configuration model: nothing reads it. | `editor-shell` |
| Reading picker | Each provider's listing loaded once per editor session and filtered in memory (no search request per keystroke); live values only for rows on screen; readings carry an area and a category (Home Assistant from its registries); `system` lists its readings; a cache of the readings in use for providers that are not connected. | `editor-reading-picker` |
| "Dim behind cards" | Had no setting behind it (the scrim is the theme's). New `background.dim` (0–0.7) overrides the scrim's strength. | `editor-theme-background` |
| Theme pictures | Rendered in the app from the user's own page in each theme and cached (the goldens are test assets and show the sample page). | `editor-theme-background` |
| Motion level | In Theme and background. | `editor-theme-background` |
| Broken plugins | A plugin load report and a memory of which plugin provided which widget type tell "did not load" from "not installed". "Find the plugin" is left out until there is a marketplace. | `editor-states` |
| First run | Opens the editor with the welcome (sample page or empty page). | `editor-states` |
| Landscape grids | Left to backlog item 15; the editor reads the grid's size from the engine and never assumes 4 by 12. | `editor-shell` |
| Localisation | English only for now. | `settings-metadata` |

The order the changes are applied in, their dependencies and the model for each session are in
[`docs/BACKLOG.md`](../BACKLOG.md), "Suggested order".
