## Why

With `settings-metadata` every widget and provider declares its settings, and with `editor-shell` a widget can be
selected in the editor; this change builds the form between the two: the properties pane shows the selected widget's
settings as controls generated from the declaration, and every change reaches the preview and the deck as it is made.
This is the most-used part of the editor (`docs/handoff/2026-11-06-editor-design.md`, artboard 1 and "The control kit";
`docs/handoff/editor-design/ControlKit.dc.html`, and `Main.dc.html`'s `field()` and `buildForm()`).

**Order:** after `settings-metadata` and `editor-shell`. Before `editor-reading-picker`, `editor-theme-background` and
`editor-states`, which reuse its controls.
**Model:** Sonnet. Many controls, each simple; the design names every one. The Launch target's "App…" list (Start menu
applications) is the least known part: if it resists, ship File and Folder and record the gap.

## What Changes

- **A form builder** in the host that turns a list of setting descriptors and a JSON object (by path in the
  configuration document) into Fluent controls, one per kind: `ToggleSwitch`, `RadioButtons` or `ComboBox`, `TextBox`
  with the fallback as placeholder (and a "Show a label" switch for three-state text), `NumberBox` with its unit (in
  the reading's display unit, stored canonical), `Slider`, a reading button with the live value, an action `ComboBox`
  filled by the provider, a display unit `ComboBox`, an `AutoSuggestBox` for places (answered by the provider), a launch
  target box with Browse (app, file, folder), an icon drop-down (automatic, the theme's icon set, a file), a
  `PasswordBox` for secrets, and expanders for a list of positions.
- **Modifiers** shown as designed: settings for other sizes dimmed with "Only at 4x4", conditional settings hidden
  until they apply, groups as small headings, more than eight settings folded into expanders, an unknown kind edited as
  text with a line saying so.
- **Writes as you edit**: each control writes through the configuration document (text and numbers merged into one undo
  step per field); an empty value removes the key (or writes `""` for a three-state text set to none).
- **The widget's form replaces the shell's placeholder** in the properties pane, for page widgets and dock slots.
- **The Providers section uses the same builder** (its fields become the kit's controls; it keeps Save and Test
  connection, because a provider restarts on each saved change).
- **A minimal reading chooser** until `editor-reading-picker`: a dialog to type a reading id, validated against the
  readings the providers know.
- The engine lists the glyphs of the theme's icon set (the icon keys its manifest maps) and the actions of a reading.

## Capabilities

### New Capabilities

- `editor-forms`: the generated settings form: the control for each kind, modifiers, fallbacks, units, writing values,
  the list of positions, unknown kinds.

### Modified Capabilities

- `settings-window`: "Providers Section" is built with the same generated controls.

## Impact

- `src/UrDeck.Host/Editor/Forms/`: `FormBuilder`, one small class per control kind, `PositionsList`, `ReadingChooser`
  (minimal), `PlaceBox`, `LaunchTargetBox`, `IconPicker`.
- `src/UrDeck.Host/ProvidersSection.cs`: rebuilt on `FormBuilder` in staged mode.
- `src/UrDeck.Engine`: `IconSets`/`IconService` gain a listing of keys for the resolved theme set; `ReadingHub` gains
  `ActionsFor(readingId)`; a value-coercion helper (`SettingValues`: read a JSON value as the descriptor's type, write
  it back, decide "absent") with tests.
- No configuration format change; no SDK change.
