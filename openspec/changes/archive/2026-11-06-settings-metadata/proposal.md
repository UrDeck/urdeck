## Why

The editor (milestone 3, backlog item 11) builds every widget's and provider's form from a declaration, because plugins
add settings the editor cannot know in advance (`docs/handoff/2026-11-06-editor-design.md`, "The control kit"). Today
only providers declare their settings (`[Setting]`, `[Secret]`, four value types); widgets are plain properties with
`[AllowedValues]`, and the meaning of each widget setting lives in the frontmatter of its page on the site. There is
nothing an editor could build a reading picker, a place search, a list of gauge positions or a "only at 4x4" hint from.
This change gives the SDK one way to declare a setting, used by widgets and providers alike, and makes the six
first-party widgets declare every setting they have.

**Order:** the first change of the editor batch, with `config-document` and `deck-session` (no dependencies; the three
can be applied in parallel). `editor-forms` needs it.
**Model:** Sonnet. The public API is pinned in `design.md`; an Opus review of the SDK surface before merging is worth it.

## What Changes

- **Setting kinds in the SDK** (`UrDeck.Sdk.Settings`): Toggle, Choice, Text, Number, Range, Reading, Action, Unit,
  Place, Launch target, Icon, Secret and List of positions, each declared with an attribute on a settings property. The
  kind decides the editor's control; no plugin ships UI. A kind attribute the editor does not know falls back to text.
- **Modifiers** any setting can carry: label and help (plain, English for now), group and order, `Sizes` (only at
  some widget sizes), `ShownWhen` (only when another setting has a value), a fallback (what an empty value means, shown
  as the placeholder: fixed words, the chosen reading, the region or the theme), `AllowsNone` (a text whose empty value
  is meaningful and different from absent), and for numbers a unit fixed or taken from a reading setting.
- **One schema reader in the engine** (`SettingSchema`) used for widget configuration types, their list item types
  (`StatsSlot`) and provider settings types. `ProviderSchema` and its `SettingDescriptor` become a use of it.
  **BREAKING** (SDK): `SettingAttribute` and `SecretAttribute` move from `UrDeck.Sdk.Data` to `UrDeck.Sdk.Settings`;
  `[AllowedValues]` on settings is replaced by `[Option]`.
- **Pickers fed by providers**: a provider may answer suggestion queries (`ISuggestionSource`): Weather answers "place"
  from its existing Open-Meteo geocoding client. The editor never calls a web service itself.
- **Display units are public** in the SDK (`DisplayUnits.ToDisplay` / `FromDisplay`), so a form can show a threshold in
  °F while the file keeps the canonical °C.
- **Every first-party widget declares every setting** (Clock, Stats and its slots, Weather, Shortcut, Now playing,
  Control), and the providers keep theirs on the new attributes.
- **BREAKING** (configuration): Clock's `textColor` and `fontSize` are removed. The clock's size comes from the card's
  size and its look from the theme (`valueScale`, `valueFill`, tracking, slant); `textColor` also switched off a theme's
  `valueFill`.
- **Analyzer rule URDECK006** (warning): a public settable property of a widget configuration type, a provider settings
  type or a list item type that has no setting attribute, no `[SettingPart]` and no `[JsonIgnore]`. The editor can only
  show what is declared.
- **Help text lives in code.** `urdeck-cli catalog export` carries each widget setting's label, help, kind and options;
  the site's widget settings tables read the meaning from the catalog, and the `settings:` maps in the widget pages'
  frontmatter are deleted.

## Capabilities

### New Capabilities

- `settings-metadata`: how a widget or a provider declares its settings (kinds, modifiers, item lists, suggestion
  sources), what the engine reads from the declaration, the fallback for an unknown kind, display unit conversion, and
  the analyzer rule.

### Modified Capabilities

- `data-providers`: "Provider Settings" declares settings with the shared kinds and modifiers instead of its own four
  types.
- `widget-clock`: "Clock Configuration" and "Clock Visual Appearance" lose `textColor` and `fontSize`.
- `widget-stats`: "Stats Widget Identity" declares the size 2x1, which the sample page already uses and the widget
  already draws.
- `website`: "Configuration Pages" takes widget setting descriptions from the catalog instead of the page's frontmatter.

## Impact

- `sdk/UrDeck.Sdk`: new `Settings/` folder (attributes, `ISuggestionSource`, `DisplayUnits`); `Data/SettingAttributes.cs`
  moves there. SDK version bump (minor; breaking changes are allowed before 1.0, see the no-backwards-compatibility note).
- `sdk/UrDeck.Analyzer`: URDECK006 and its tests.
- `src/UrDeck.Engine`: `Settings/SettingSchema.cs` and descriptors; `Data/ProviderSchema.cs` rebuilt on it;
  `ProviderSettingsBinder` unchanged in behaviour; a suggestion route in `ReadingHub` (start the provider, ask, linger).
- `widgets/*`: every `*Config.cs` annotated; `ClockConfig` loses two properties, `ClockWidget` and `FlapDisplay` stop
  reading them.
- `providers/*`: attribute namespace; Weather implements `ISuggestionSource` for `place`.
- `tools/UrDeck.Cli`: `catalog export` writes the richer settings; its tests.
- `site/`: `SettingsTable.astro` reads `help` from the catalog; the six widget pages lose their `settings:` frontmatter;
  the Clock page loses `textColor` and `fontSize`.
- `src/UrDeck.Host/urdeck-config.default.json`: `fontSize` removed.
- Goldens: unchanged (nothing that renders them uses `textColor` or `fontSize`).
