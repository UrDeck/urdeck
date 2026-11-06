## Context

- Descriptors: `SettingSchema`/`SettingDescriptor` (`settings-metadata`, design D3): key, kind (string), label, help,
  group, order, sizes, shown-when, fallback, allows-none, options, number spec (min, max, step, whole, unit,
  unit-from-reading), reading spec (filter, provider), owner reading (action, unit), place spec, launch kinds, icon
  file allowed, secret context, positions spec and item schema, default.
- The shell (`editor-shell`): properties pane with a placeholder line where the form goes; `Document.Edit(label, edit,
  origin, mergeKey)`; the preview's readings source (Live or Sample) for live values; the selected `WidgetRef` and the
  widget's current size.
- Design sources: `ControlKit.dc.html` (each card = a kind: the control, what the declaration carries, the rule);
  `Main.dc.html` `field()` and `buildForm()` (the generator; its `SCHEMA` keys are outdated, the code's keys win).
- `ProvidersSection.cs` builds a provider form from the old four types with Save and Test connection.

## Goals / Non-Goals

**Goals:** every first-party setting editable; any plugin's declared settings editable without plugin UI; identical
look for widgets and providers; values written exactly as the widget reads them.

**Non-Goals:** the full reading picker (`editor-reading-picker`); shader parameters and the Theme and background
section (`editor-theme-background` reuses the builder); validation beyond the declaration.

## Decisions

### D1. FormBuilder

```csharp
sealed class FormBuilder
{
    // Live mode: each control writes to the document at `objectPath` (e.g. "/pages/0/widgets/3").
    public FrameworkElement Build(SettingSchema schema, string objectPath, FormContext context);
    // Staged mode (providers): edits go to a detached JsonObject; Commit() returns it.
    public FrameworkElement BuildStaged(SettingSchema schema, JsonObject staging, FormContext context);
}
sealed record FormContext(
    ConfigDocument Document, string? SizeKey,                 // "4x2"; null for providers
    IEditorReadings Readings,                                  // describe, read live value, actions, units, suggest
    IIconCatalog Icons, Window Owner, IReadingChooser Chooser);
```

- The form re-reads values from the document on `Document.Changed` (only when a path under `objectPath` or a reading
  setting it depends on changed) and never keeps its own copy of values.
- Layout: a `StackPanel` of rows; label above the control (except Toggle: label left, switch right, as the kit shows);
  help as a `TextBlock` in the secondary text style below; settings marked as pairs in the kit (min/max,
  warning/critical, label/unit) are laid out two per row when they are consecutive Numbers or Texts in the same group
  and the pane is wider than 320 DIP.
- Groups: settings without a group first, then a small `TextBlock` heading per group. More than eight visible settings:
  each group becomes an `Expander` (the first expanded).

### D2. Controls per kind

| Kind | Control | Reads / writes |
|---|---|---|
| toggle | `ToggleSwitch` (On/Off text) | bool; writes `true`/`false` |
| choice ≤ 3 options | `RadioButtons` (horizontal) | option value; empty value → remove the key |
| choice ≥ 4 | `ComboBox` | same |
| text | `TextBox`, `PlaceholderText` = fallback (D4) | empty → remove; merge key = path |
| text + allows none | `ToggleSwitch` "Show a label" above the `TextBox` (box hidden when off) | off → `""`; on + empty → remove; on + text → text |
| number | `NumberBox` (`SpinButtonPlacementMode.Compact`, `Minimum`/`Maximum`, `SmallChange` = step or 1, whole-number rounding), unit as a `TextBlock` after it | empty/NaN → remove; merge key = path |
| number + unit from reading | as number, shown and accepted in the display unit (D5) | converts to canonical |
| range | `Slider` + value text ("5 days" style: value + unit) | number; merge key = path |
| reading | `Button` (full width): label, id (monospace, small), live value right-aligned; opens `IReadingChooser` with the filter | reading id |
| action | `ComboBox` of the owner reading's actions (label), first item "The reading's own action" when the reading has a natural action | action path; disabled until the owner reading is set |
| unit | `ComboBox`: "Automatic (from your region)", then the display units of the owner reading's kind (Celsius/Fahrenheit for temperature; metric/imperial for speed and depth) | `celsius`/`fahrenheit` or remove; **hidden** when the reading's kind has one unit |
| place | `AutoSuggestBox` (D6) + a resolved line ("45.52° N, 122.68° W · Oregon, United States") | the suggestion's values |
| launchTarget | `TextBox` + `DropDownButton` "Browse…" (App…, File…, Folder…; only the allowed kinds) + a line saying what the target is (D7) | text |
| icon | `DropDownButton` showing the current icon; flyout: "Automatic", a `GridView` of the theme's icon set glyphs, "From a file…" (when allowed) (D8) | absent / icon key / file path |
| secret | `PasswordBox` + state line + Replace/Remove (as `ProvidersSection` today) | secret store, never the file |
| positions | `PositionsList` (D9) | the list |
| unknown kind | `TextBox` + help line "Edited as text: this plugin uses a newer kind of setting." | raw text (a JSON string) |

Disabled state (`IsEnabled = false`) follows the rules of D3. All controls get `AutomationProperties.Name` = label.

### D3. Modifiers

- **Sizes**: when `SizeKey` is not in `Sizes`, the control is disabled and the help line reads "Only at 4x4." (sizes
  joined with ", "). Never hidden. For a dock slot `SizeKey` is `1x1`.
- **Shown when**: the row is collapsed until the named setting's current value (from the document, absent counting as
  its default) is one of the values.
- **Disabled in a role** (item settings in a position): disabled with the declared help text.
- **Read-only document**: everything disabled.

### D4. Fallback placeholders

`Fallback` words verbatim. `FallbackFrom = Reading`: by the setting's key on the item: `label` → the reading's label;
`unit` → its unit; `min`/`max` → its min/max in the display unit ("From the reading" when unknown); `warning`/`critical`
→ its levels, else the `Fallback` words ("Off"). `Region` → "°C (your region)" or "°F (your region)" from
`RegionUsesFahrenheit`. `Theme` → `Fallback` words.

### D5. Units

`DisplayUnits` (SDK, `settings-metadata` D8). The display unit of a number with unit-from-reading = the item's `unit`
setting when set, else the region's. Show `ToDisplay(...)` rounded to the step's decimals; on input, write
`FromDisplay(...)` rounded to 4 decimals. The unit text is `DisplayUnits.UnitText`.

### D6. Place

`AutoSuggestBox`: after 2 characters and 300 ms without typing, call `Readings.SuggestAsync(placeSpec.Source, "place",
text)`; show `Label` with `Detail` as a second line; on choose, one transaction writes every value of the suggestion into
the object (`location`, `latitude`, `longitude`). A failure shows its message as the only (disabled) suggestion. The
resolved line is computed from the stored latitude/longitude ("N/S", "E/W", two decimals) plus the stored detail if the
widget keeps none (Weather does not: show only the coordinates).

### D7. Launch target

- File… / Folder…: `FileOpenPicker` / `FolderPicker` with `InitializeWithWindow` on the editor's HWND (unpackaged app).
- App…: a `ContentDialog` listing the Start menu's applications from the shell's AppsFolder (`shell:AppsFolder`,
  enumerated with `IShellItem`/`SHGetKnownFolderItem(FOLDERID_AppsFolder)` on the STA worker the engine already has
  for shell icons, `src/UrDeck.Engine/Icons/StaWorker.cs`), with icons and a search box; choosing writes
  `shell:AppsFolder\<AppUserModelID>`. Check that `Launcher` opens that form (it opens `shell:` items today); if not,
  add it there with a test.
- The line under the box uses the engine's `LaunchTarget` parsing: "An application.", "A folder. Opens in File
  Explorer.", "A web address. Opens in your default browser.", "A file. Opens with its app.", "Not found." in warning
  colour when a rooted path does not exist.

### D8. Icons

- `IIconCatalog.Glyphs(theme)`: the keys of the theme's resolved icon set (`IconSets`), each with a rendered 24 DIP
  preview (through `IconService`, cached), grouped by the key's first segment (`home/`, `weather/`, `app/`). Engine
  method `IconSets.KeysOf(set)`; test it.
- The drop-down's own content shows the chosen glyph, "Automatic" or the file name.

### D9. List of positions

- One `Expander` per position of the current size (`PositionsSpec` for `SizeKey`). Header: a number badge (accent tint
  for the first role in the list's declared order, neutral for others), role ("Gauge position"; a single position reads
  "Reading"), summary (item label, else the reading's label, else "Empty"), the live value at the right.
- Content: the item form (`Build(itemSchema, "<listPath>/<i>", context)` with the role for disabled-in-role rules).
- An empty position (index ≥ list length): header "Empty" and a "Fill" button, which appends items with default
  values up to that index (one transaction).
- Reorder: each expander's header has a ⋯ menu "Move up" / "Move down" (one transaction moving the item); dragging the
  headers to reorder is a nice-to-have, not required.
- Items beyond the positions: a note under the list "N more slots stay in the file and come back at a larger size."
- Only one expander open at a time (opening one closes the others); the first opens when the widget is selected.

### D10. Reading chooser (minimal)

`IReadingChooser.ChooseAsync(filter, current)`; the shell's default: a `ContentDialog` "Choose a reading" with a
`TextBox` for the id, a line showing the reading's label and live value as the id resolves (via `Readings.Describe`), an
error line for an id no provider knows ("No provider has this reading"), OK/Cancel. `editor-reading-picker` replaces
it with the full picker.

### D11. Providers section

Rebuilt with `BuildStaged`: the same controls; Save commits the staging object as one User transaction (as
`ProviderSettingsEditor` does after `config-document`), secrets go to the secret store; Test connection runs with the
staged values. Visible changes: the controls look like the widget form's; behaviour is unchanged.

### D12. Value coercion (engine, tested)

`SettingValues.Read(JsonNode?, descriptor)` → typed value or "absent", tolerant as widgets are (a number written as a
string reads as the number; an unknown option shows as the raw text in the ComboBox's text); `Write(value, descriptor)`
→ `JsonNode?` (`null` = remove). Tests for every kind, including `JsonElement` settings (Weather `days`), and the
three-state text.

## Risks / Trade-offs

- [Rebuilding the form on every document change flickers or loses focus] → re-read values into existing controls; only
  rebuild when the schema, size or shown-when visibility changes; never rebuild the control that has focus.
- [AppsFolder enumeration is fiddly] → isolated behind `App…`; File and Folder cover every target meanwhile.
- [Unit conversion rounding round-trips badly] → write 4 decimals canonical; show with the step's decimals; tests.
