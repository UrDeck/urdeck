## Context

- Providers: `sdk/UrDeck.Sdk/Data/SettingAttributes.cs` has `[Setting(label) { Help, Required }]` and
  `[Secret(ContextSetting)]`. The engine's `src/UrDeck.Engine/Data/ProviderSchema.cs` reads them into
  `SettingDescriptor(Key, SettingType, Label, Help, Required, ContextKey, Default)` with `SettingType` Text, WholeNumber,
  Number, Toggle, Secret. `src/UrDeck.Host/ProvidersSection.cs` builds a form from it; `catalog export`
  (`tools/UrDeck.Cli/CatalogExportRunner.cs`, `ProviderSettingsOf`) writes it for the site.
- Widgets: `widgets/*/*Config.cs` are plain properties on a `WidgetConfig` subclass, some with
  `System.ComponentModel.DataAnnotations` `[AllowedValues]` / `[Range]`. `CatalogExportRunner.Settings(Type)` reflects
  name, type, allowed values and default; the meaning is in `site/src/content/docs/widgets/<widget>.mdx` frontmatter
  (`settings:`), joined by `site/src/components/SettingsTable.astro`, whose build fails on a missing or extra meaning.
- The editor's design: `docs/handoff/2026-11-06-editor-design.md` ("The control kit") and
  `docs/handoff/editor-design/ControlKit.dc.html`; the mock-up's `SCHEMA` / `SLOT_SCHEMA` / `POS` in `Main.dc.html` are
  the six widgets written in these kinds, but their keys drifted from the code (for example the mock's `hour` is the
  code's `format`). **The code's keys win**; the mock-up only shows the controls.
- Decisions taken in the exploration (handoff, "Decisions after review"): help text in code; three-state text shown as
  a "Show a label" toggle; numbers in a reading's unit are shown in the display unit and stored canonical; Clock's
  `textColor` and `fontSize` removed; English only for now; the editor is the product's front door, so every setting
  needs a home in it.

## Goals / Non-Goals

**Goals:**
- One declaration per setting that is enough for the editor to build its control, validate a value, explain an empty
  value, and for the site to document it.
- Every first-party widget and provider setting declared; nothing reachable only by editing the file.
- The declaration lives in the SDK (MIT) and needs no reference to the engine.

**Non-Goals:**
- The editor's controls (`editor-forms`), the reading picker (`editor-reading-picker`).
- Localisation. Labels and help are plain English strings; a resource mechanism can come later without changing the
  attribute shapes (a later `LabelKey` property, for example).
- Theme or background manifest parameters: those are declared in JSON manifests, not in code. `editor-forms` maps
  them onto the same engine descriptor.
- Validation beyond what the declaration states (ranges, options). Widgets keep tolerating odd values at runtime as
  they do today.

## Decisions

### D1. Attributes, one per kind, deriving from a common base

```csharp
namespace UrDeck.Sdk.Settings;

// Every setting has exactly one [Setting]. It carries the modifiers common to all kinds.
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute(string label) : Attribute
{
    public string Label { get; } = label;
    public string Help { get; set; } = "";          // one sentence under the control and in the reference
    public string? Group { get; set; }               // a small heading; settings without a group come first
    public int Order { get; set; }                   // within the group; ties keep declaration order
    public bool Required { get; set; }               // providers: not started while missing (as today)
    public string[]? Sizes { get; set; }             // widget sizes it applies at, e.g. ["4x4"]; null = all
    public string? Fallback { get; set; }            // words shown when empty, e.g. "None", "Automatic", "Off"
    public FallbackSource FallbackFrom { get; set; } // None | Reading | Region | Theme (see D4)
    public string? FallbackReading { get; set; }     // the reading setting's key when FallbackFrom = Reading
    public bool AllowsNone { get; set; }             // Text only: empty ("") differs from absent (see D5)
}

// Hidden until another setting has one of the given values.
[AttributeUsage(AttributeTargets.Property)]
public sealed class ShownWhenAttribute(string key, params string[] values) : Attribute { ... }

// Kind attributes. Optional where the property type already says it (see D2).
public abstract class SettingKindAttribute : Attribute { public abstract string Kind { get; } }

[Option("simple", "Simple")]                      // AllowMultiple; order = display order; makes it a Choice
public sealed class OptionAttribute(string value, string label) : Attribute { ... }  // not a kind attribute itself

public sealed class NumberAttribute : SettingKindAttribute   // Kind "number"
{ public double Min, Max (NaN = none); public double Step; public string? Unit; public string? UnitFromReading; }
public sealed class SliderAttribute(double min, double max, double step = 1) : SettingKindAttribute // Kind "range"
{ public string? Unit; }
public sealed class ReadingAttribute : SettingKindAttribute  // Kind "reading"
{ public ReadingFilter Filter; public string? Provider; }    // Filter: None | HasRange | HasAction (flags)
public sealed class ActionAttribute(string readingSetting) : SettingKindAttribute   // Kind "action"
public sealed class UnitAttribute(string readingSetting) : SettingKindAttribute     // Kind "unit"
public sealed class PlaceAttribute(string source) : SettingKindAttribute            // Kind "place"
{ public string? LatitudeSetting, LongitudeSetting; }
public sealed class LaunchTargetAttribute : SettingKindAttribute                    // Kind "launchTarget"
{ public LaunchKinds Allowed = LaunchKinds.All; }           // App | File | Folder | Address (flags)
public sealed class IconAttribute : SettingKindAttribute     // Kind "icon"
{ public bool AllowFile = true; }
public sealed class PositionsAttribute(string size, params string[] roles) : Attribute // AllowMultiple, see D6
public sealed class SecretAttribute : Attribute { public string? ContextSetting { get; set; } } // moved, unchanged

// A property written by another setting's control and never shown by itself (Weather's latitude, longitude).
public sealed class SettingPartAttribute(string ownerSetting) : Attribute { ... }
```

Rationale: attributes keep a setting's declaration next to the property that holds it, which is how providers already
work and how plugin authors expect .NET settings to look. A single `[Setting]` for the common part plus an optional
kind attribute avoids a dozen attributes each repeating `Label`, `Help`, `Sizes`.

Alternatives considered: a fluent builder in the widget (`DescribeSettings(SettingsBuilder b)`): more flexible
(computed options) but separates the declaration from the property and needs an instance; a JSON schema file per
plugin: one more file to keep in step with the code. Neither is needed by the six widgets.

### D2. Kind from the property type when unambiguous

| Property type | No kind attribute | With `[Option]`s |
|---|---|---|
| `bool`, `bool?` | Toggle | n/a |
| `string`, `string?` | Text | Choice |
| `int`, `double` (nullable or not) | Number (no range) | Choice (values compared as text) |
| `JsonElement?` | must carry a kind attribute (URDECK006 says so) | Choice |
| `List<T>` | must carry `[Positions]` (the only list kind) | n/a |
| `SecretValue` | Secret (needs `[Secret]`, as today) | n/a |

A kind attribute always wins over the type. Choice: up to three options show as radio buttons, four or more as a
combo box (`editor-forms` decides; the declaration does not).

### D3. The engine's reader

`src/UrDeck.Engine/Settings/SettingSchema.cs`:

```csharp
public sealed record SettingDescriptor(
    string Key, string Kind, string Label, string Help, string? Group, int Order, bool Required,
    IReadOnlyList<string>? Sizes, ShownWhen? When, Fallback Fallback, bool AllowsNone,
    IReadOnlyList<SettingOption> Options, NumberSpec? Number, ReadingSpec? Reading, string? OwnerReading,
    PlaceSpec? Place, LaunchKinds? Launch, bool IconAllowsFile, string? SecretContext,
    PositionsSpec? Positions, SettingSchema? Item, object? Default);

public sealed class SettingSchema
{
    public Type Type { get; }
    public IReadOnlyList<SettingDescriptor> Settings { get; }       // in display order (group, order, declaration)
    public IReadOnlyList<string> Parts { get; }                     // [SettingPart] keys
    public static SettingSchema Read(Type type, Type? stopAt = null); // stopAt: WidgetConfig / ProviderSettings
    public static bool TryRead(Type type, Type? stopAt, out SettingSchema? schema, out string? error);
}
```

- Key = `[JsonPropertyName]` or camelCase name (as `ProviderSchema.KeyOf` does today).
- `Default` = the property's value on `Activator.CreateInstance(type)` (as today), `null` when it cannot be created.
- `Kind` is a string (`"toggle"`, `"choice"`, `"text"`, `"number"`, `"range"`, `"reading"`, `"action"`, `"unit"`,
  `"place"`, `"launchTarget"`, `"icon"`, `"secret"`, `"positions"`). A `SettingKindAttribute` subclass from a newer SDK
  or another plugin keeps its own `Kind` string; the editor treats a kind it does not know as text and says so
  (`editor-forms`). That is the "a plugin uses a newer kind of setting" fallback.
- Errors (the plugin is rejected with a logged reason, as `ProviderSchema.TryRead` does today): a `[ShownWhen]`,
  `FallbackReading`, `ActionAttribute`, `UnitAttribute` or `[SettingPart]` naming a key that does not exist; a
  `JsonElement` or `List<T>` property without a kind; `Sizes` naming a size the widget does not declare (checked where
  the widget descriptor is known, in the registry); `[Option]` on a bool.
- Undeclared public settable properties are **not** an error at runtime (URDECK006 is a warning; the editor simply
  does not show them), so a plugin built against this SDK without the analyzer still loads.
- `ProviderSchema` becomes `SettingSchema.Read(settingsType, typeof(ProviderSettings))` plus its existing checks (base
  keys, secret types); the old `SettingType` enum is removed and `ProvidersSection` is adapted (Text, Number with or
  without decimals, Toggle, Secret map one to one). `WholeNumber` vs `Number`: `NumberSpec.WholeNumber` is true for an
  `int` property.
- The widget registry reads each widget's schema when it registers the type (`WidgetDescriptor.Settings`), so a schema
  error rejects the widget with the existing "rejected" log line.

### D4. Fallback: what an empty value means

`Fallback` is literal words. `FallbackFrom` tells the editor to compute the placeholder:

| `FallbackFrom` | Placeholder the editor shows | Example |
|---|---|---|
| `None` | `Fallback` or nothing | Shortcut arguments: "None" |
| `Reading` | the chosen reading's label, unit, min, max or levels (by the setting's key: `label` → label, `min` → min, ...) | Stats slot label "GPU temperature"; min "0" |
| `Region` | what the region gives (`°C` or `°F`) | Weather unit |
| `Theme` | `Fallback` words (the theme decides at render time) | none yet |

This only explains; the widget still decides at runtime, as it does today.

### D5. Text with three states (`AllowsNone`)

`StatsSlot.Label`: absent → the reading's label; `""` → no label; text → the text. With `AllowsNone = true` the editor
shows a "Show a label" switch above the box: off writes `""`; on with an empty box removes the key; on with text writes
it. The declaration only says the three states exist; the control is `editor-forms`'.

### D6. List of positions (Stats slots)

```csharp
[Setting("Readings")]
[Positions("1x1", "reading")]
[Positions("2x2", "reading")]
[Positions("4x2", "gauge", "gauge", "text", "text", "text")]
[Positions("4x4", "gauge", "gauge", "gauge", "gauge", "text", "text", "text")]
public List<StatsSlot> Slots { get; set; } = [];
```

- The item type's schema (`StatsSlot`) is read the same way (stopAt `object`) and is `SettingDescriptor.Item`.
- Role names are the widget's words; the editor shows them capitalised ("Gauge position"). An item setting can be
  disabled in a role: `[Setting("Gauge", DisabledInRoles = ["text"], DisabledHelp = "Text positions are always plain.")]`
  (two more `SettingAttribute` properties: `DisabledInRoles`, `DisabledHelp`).
- Slots beyond the size's positions stay in the file (as today); the editor says how many.
- The positions match `StatsWidget.Compose()` today (1x1, 2x1 and 2x2: one; 4x2: two gauges and three texts; 4x4: four
  and three).
- **Stats gains `[WidgetSize(2, 1)]`** with `[Positions("2x1", "reading")]`. The sample page
  (`src/UrDeck.Preview/SamplePage/urdeck-config.json`) has two Stats at 2x1 (CPU and GPU), and the widget already draws
  them (one position below 4 columns), but the size was never declared, so the editor would flag the sample page itself.
  Declaring it changes no pixel (the goldens draw the sample page). Found in the design handoff ("Found while designing"). A unit test asserts that the declared positions match what `Compose` builds for each declared size, so the
  two cannot drift.

### D7. Suggestion sources (Place)

```csharp
namespace UrDeck.Sdk.Settings;
public interface ISuggestionSource            // implemented by a provider, next to IDataProvider
{
    IReadOnlyList<string> SuggestionKinds { get; }           // e.g. ["place"]
    Task<IReadOnlyList<Suggestion>> SuggestAsync(string kind, string text, CancellationToken cancellationToken);
}
public sealed record Suggestion(string Label, string? Detail, IReadOnlyDictionary<string, JsonNode?> Values);
```

- `[Place("weather", LatitudeSetting = "latitude", LongitudeSetting = "longitude")]` on `WeatherConfig.Location`. A
  chosen suggestion writes `Values` into the widget object: Weather returns `{ "location": "Portland, Oregon",
  "latitude": 45.52, "longitude": -122.68 }`.
- The engine routes it like a listing (`ReadingHub.BrowseAsync`): start the provider if it is not running, ask, linger
  it, time out after 10 s, never throw into the caller (`SuggestionResult` with a message on failure). Route:
  `ReadingHub.SuggestAsync(providerId, kind, text, ct)`.
- Weather wraps its existing `GeocodingClient` (`providers/UrDeck.Providers.Weather/WeatherHttp.cs`). The editor never
  calls Open-Meteo directly, keeping the rule that UrDeck talks only to services the user configured or a provider
  already uses.
- Readings and actions are not suggestion sources: they already have `IReadingBrowser` and `IActionProvider`.

### D8. Display units

`sdk/UrDeck.Sdk/Data/DisplayUnits.cs` (public static): `double ToDisplay(ReadingKind kind, double canonical,
DisplayUnit unit, ReadingDescriptor? descriptor)`, `double FromDisplay(...)` and `string UnitText(kind, unit,
descriptor)`. `ReadingFormatter.Number` and `Depth` call them (no output change; the formatter tests prove it). The
editor uses them for a `NumberSpec.UnitFromReading` setting: it shows the value in the slot's display unit (`StatsSlot.Unit`,
else the region's) and writes the canonical value.

### D9. The first-party declarations

The inventory, by the code's keys. "≈" marks a setting whose label/help the implementer writes from today's site text.

| Widget | Key | Kind and modifiers |
|---|---|---|
| Clock | `format` | Choice: `24h` "24-hour", `12h` "12-hour" |
| | `style` | Choice: `simple` "Simple", `flap` "Split-flap" |
| | `showDate` | Toggle |
| | `header` | Choice: (absent) "Under the time", `date` "Date row", `label` "Label row", `none` "None" — absent is an option (see note) |
| | `subtitle` | Text, `ShownWhen("header", "date", "label")`, Fallback "None" |
| | `showWeek` | Toggle |
| | `showSeconds` | Toggle, help: draws once a second |
| | `blinkSeparator` | Toggle, help: draws once a second |
| Stats | `slots` | Positions as D6; item `StatsSlot`: |
| | `reading` | Reading |
| | `label` | Text, `AllowsNone`, FallbackFrom Reading |
| | `unit` | Unit("reading") — shown only for readings with more than one display unit |
| | `decimals` | Number 0–3 whole, Fallback "Automatic" |
| | `style` | Choice (10: absent "Automatic", `plain`, `gauge`, `ring`, `bar`, `verticalBar`, `meter`, `tach`, `marker`, `thickbar`), DisabledInRoles text |
| | `min`, `max` | Number, UnitFromReading "reading", FallbackFrom Reading, Group "Range" |
| | `warning`, `critical` | Number, UnitFromReading "reading", FallbackFrom Reading (the reading's own levels, else "Off"), Group "Levels" |
| Weather | `location` | Place("weather"), parts `latitude`, `longitude` |
| | `label` | Text, Fallback "The place name" |
| | `unit` | Choice `celsius`/`fahrenheit`, FallbackFrom Region |
| | `days` | Slider 1–7, Sizes 4x4 |
| | `animateForecast` | Toggle (`JsonElement?` + kind attribute), Sizes 4x4 |
| | `motion` | Choice `periodic` "Now and then", `full` "Always", `off` "Off" |
| | `periodSeconds` | Number, unit "s", Min 1, Fallback "60", `ShownWhen("motion", "periodic")`, Group "Motion" |
| Shortcut | `target` | LaunchTarget (all) |
| | `arguments` | Text, Fallback "None", help: for a file target only |
| | `icon` | Icon, Fallback "From the target" |
| | `label` | Text, Fallback "None" |
| Now playing | `showAlbum`, `showApp` | Toggle |
| | `idleLabel` | Text |
| Control | `reading` | Reading, Filter HasAction |
| | `action` | Action("reading"), Fallback "The reading's own action" |
| | `style` | Choice `switch`, `dimmer` |
| | `level` | Reading, `ShownWhen("style", "dimmer")` |
| | `levelAction` | Action("level"), `ShownWhen("style", "dimmer")` |
| | `label` | Text, FallbackFrom Reading |
| | `icon` | Icon, Fallback "The reading's own icon" |

Note on "absent" as a Choice option: `[Option("", "Under the time")]` with an empty value means "remove the key". The
editor writes nothing for it. The same for Stats' `style` "Automatic".

`WidgetConfig`'s own properties (`typeId`, `col`, `row`, `width`, `height`, `parameters`, `isVisible`) are not
settings; the analyzer and the schema skip the `WidgetConfig` base (stopAt). `parameters` and `isVisible` are unused by
the first-party widgets; leave them alone in this change.

Providers keep their settings as they are, with `[Setting]` from the new namespace; `system`'s and `weather`'s base
settings (`intervalMs` and so on) stay base keys and are not shown (as today).

### D10. Analyzer rule URDECK006

- Severity warning (the repo builds with warnings as errors, so first-party code must comply; third parties get a
  warning).
- Applies to: types deriving from `UrDeck.Sdk.WidgetConfig` or `UrDeck.Sdk.Data.ProviderSettings`, and the element type
  `T` of a `List<T>` property that carries `[Positions]`.
- Reports: each public instance property with a public setter declared in such a type (not inherited from the SDK base)
  that has none of `[Setting]`, `[SettingPart]`, `[JsonIgnore]`, `[JsonExtensionData]`.
- Also reports (same ID, other message): a `JsonElement`/`List<T>` setting without a kind attribute.
- Message: "'{0}' is not a declared setting: the editor cannot show it. Add [Setting], or [JsonIgnore] if it is not
  user-facing."
- Tests in `tests/UrDeck.Analyzer.Tests` in the style of the existing URDECK001–005 tests.

### D11. Catalog and site

- `catalog export`: each widget's `settings` entries gain `label`, `help`, `kind`, `group`, `sizes`, `options` (value
  and label), `item` (for positions, the item's settings) and keep `name`, `type`, `values`, `default`. Providers'
  `settings` use the same writer.
- `site/src/components/SettingsTable.astro`: meaning = `help` from the catalog; the build still fails on a setting with
  an empty `help` (the "Configuration Pages" rule). Item settings (Stats slots) are listed under their list, as the
  Stats page does by hand today; read `site/src/content/docs/widgets/stats.mdx` before changing it.
- Delete the `settings:` frontmatter of the six widget pages. Move any longer explanation that does not fit one sentence
  into the page body (most already is).
- `site` checks: `npm run build` and `npm test` pass.

## Risks / Trade-offs

- [The declarations drift from what the widget does at runtime] → the positions test (D6), the catalog-vs-site build
  check, and the editor's preview (users see the result immediately).
- [Attribute API too narrow for a future widget] → the kind is an open string and the base is abstract: a later kind is
  additive. Breaking changes are allowed before 1.0.
- [Help text in English only] → accepted for now; recorded as a non-goal.
- [Removing `textColor` breaks someone's file] → the product has no users relying on it (pre-release, no backwards
  compatibility); the key is simply ignored if present (kept in the file as an unknown key).

## Migration Plan

No configuration migration. Files with `textColor` or `fontSize` keep the keys as unknown keys; nothing reads them.
Third-party plugins (none exist) would need the namespace change.
