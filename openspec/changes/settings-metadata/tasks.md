Before starting: read `proposal.md`, `design.md` (D1–D11 pin the API and the inventory), the specs in this folder,
`docs/handoff/2026-11-06-editor-design.md` ("The control kit" and "Decisions after review") and
`docs/handoff/editor-design/ControlKit.dc.html`. No other change has to be applied first. Model: Sonnet.

## 1. SDK: attributes and conversions

- [ ] 1.1 Create `sdk/UrDeck.Sdk/Settings/` with `SettingAttribute` (moved from `Data/SettingAttributes.cs`, gaining `Group`, `Order`, `Sizes`, `Fallback`, `FallbackFrom`, `FallbackReading`, `AllowsNone`, `DisabledInRoles`, `DisabledHelp`), `SecretAttribute` (moved, unchanged), `ShownWhenAttribute`, `OptionAttribute`, `SettingPartAttribute`, `PositionsAttribute`, the abstract `SettingKindAttribute` and the kind attributes of D1 (`Number`, `Slider`, `Reading`, `Action`, `Unit`, `Place`, `LaunchTarget`, `Icon`), the enums `FallbackSource`, `ReadingFilter`, `LaunchKinds`; XML docs on every public member (the SDK's docs are the plugin author's reference)
- [ ] 1.2 Update every `using UrDeck.Sdk.Data` that referred to the moved attributes (providers, engine, tests); build with 0 warnings
- [ ] 1.3 `ISuggestionSource` and `Suggestion` (D7) in `UrDeck.Sdk.Settings`
- [ ] 1.4 `DisplayUnits` (D8) in `sdk/UrDeck.Sdk/Data/`: `ToDisplay`, `FromDisplay`, `UnitText`; make `ReadingFormatter.Number`/`Depth` use them; existing formatter tests pass unchanged; new tests for both directions (temperature, speed, depth in cm and mm, a kind with no conversion returns the value)
- [ ] 1.5 Bump the SDK's version as `CONTRIBUTING.md` says for a breaking change

## 2. Engine: one schema reader

- [ ] 2.1 `src/UrDeck.Engine/Settings/SettingSchema.cs` and the descriptor records of D3; kind inference of D2; display order (group, order, declaration order); defaults from a new instance
- [ ] 2.2 Validation errors of D3 (missing referenced keys, kindless `JsonElement`/`List<T>`, `[Option]` on a bool), returned by `TryRead` with a message
- [ ] 2.3 Rebuild `ProviderSchema` on `SettingSchema` (keep the base-key and secret-type checks and the `BoundProperties` it exposes); remove `SettingType`; adapt `ProvidersSection` and `ProviderSettingsEditor` (Text, Number with `WholeNumber`, Toggle, Secret map one to one) with no visible change to the Providers section
- [ ] 2.4 Widget registry: read each widget configuration type's schema when registering (`WidgetDescriptor.Settings`); a schema error rejects the widget with the existing "rejected" log line; also check that `Sizes` and `[Positions]` name sizes the widget declares
- [ ] 2.5 `ReadingHub.SuggestAsync(providerId, kind, text, ct)` routed like `BrowseAsync` (start, ask, linger, 10 s limit, a result with a message on failure; never throws)
- [ ] 2.6 Tests (`tests/UrDeck.Engine.Tests`, Linux-runnable): inference table of D2, every modifier read, validation errors, unknown kind kept as declared, item schema of a list, provider schema unchanged for the Home Assistant settings type, suggestion route with a fake provider (answer, failure, timeout with a fake clock)

## 3. First-party declarations

- [ ] 3.1 Clock: declarations per D9; remove `TextColor` and `FontSize` from `ClockConfig`, and their use in `ClockWidget` (the `ValueColor`/`Scale` options) and `FlapDisplay.Fit` (scale 1); remove `fontSize` from `src/UrDeck.Host/urdeck-config.default.json`; the Clock tests that used them are deleted or rewritten to the spec's "Old styling keys are ignored" scenario
- [ ] 3.2 Stats and `StatsSlot`: declarations per D9, positions per D6; add `[WidgetSize(2, 1)]` to `StatsWidget` (D6) and the 2x1 size to the site's Stats page and its media list if the media generator lists sizes (`site/README.md`, "Media"); a test that, for each declared size, the declared positions and roles match what `Compose()` draws (make the needed part of `Compose` internal and visible to the tests, or test through the rendered positions)
- [ ] 3.3 Weather: declarations per D9 (`latitude`/`longitude` as `[SettingPart("location")]`; `days` and `animateForecast` keep their `JsonElement?` type with a kind attribute); Weather provider implements `ISuggestionSource` for `place` over `GeocodingClient`, returning `location`, `latitude`, `longitude`; tests with the existing fake `IWeatherHttp` (a match, no match, an error)
- [ ] 3.4 Shortcut, Now playing, Control: declarations per D9
- [ ] 3.5 Providers (`system`, `weather`, `media`, `homeassistant`): move to the new namespace; confirm labels and help are present for every declared setting
- [ ] 3.6 Replace `[AllowedValues]`/`[Range]` on settings by `[Option]`/kind attributes; remove the `System.ComponentModel.DataAnnotations` usings that become unused

## 4. Analyzer

- [ ] 4.1 URDECK006 in `sdk/UrDeck.Analyzer` per D10 (both messages); register it in the analyzer's release tracking files as the existing rules are
- [ ] 4.2 Tests in `tests/UrDeck.Analyzer.Tests`: undeclared property warns; `[Setting]`, `[SettingPart]`, `[JsonIgnore]`, `[JsonExtensionData]` do not; inherited SDK base properties do not; list item type checked; kindless `JsonElement` warns
- [ ] 4.3 The whole solution builds with 0 warnings (every first-party setting declared)

## 5. Catalog and site

- [ ] 5.1 `catalog export` writes the fields of D11 for widgets and providers through one writer; update `tests/UrDeck.Cli.Tests` expectations
- [ ] 5.2 `site/src/components/SettingsTable.astro` (and `ProviderSettingsTable.astro` if it differs) read `label` and `help` from the catalog; list Stats' item settings under `slots`; keep the build failure for an empty help
- [ ] 5.3 Delete the `settings:` frontmatter from `site/src/content/docs/widgets/*.mdx`; move longer explanations into the page body where needed; remove `textColor` and `fontSize` from the Clock page and its example
- [ ] 5.4 README: remove any mention of Clock's `textColor`/`fontSize` (grep the repository for both keys, including `site/decks/` and `site/cookbook/`)

## 6. Verify

- [ ] 6.1 `dotnet build urdeck.slnx -c Release` (0 warnings), `dotnet test urdeck.slnx -c Release`, the engine coverage floor (`-p:CollectCoverage=true`), `dotnet format urdeck.slnx --severity warn --verify-no-changes`, and the Linux test projects
- [ ] 6.2 Goldens unchanged (`dotnet test tests/UrDeck.Engine.Tests -c Release --filter Golden` without the update variable)
- [ ] 6.3 `cd site; npm ci; npm run build; npm test` pass
- [ ] 6.4 Host started from the build output: the deck shows the sample page as before; the Providers section looks and works as before (Home Assistant address and token)
- [ ] 6.5 `openspec validate settings-metadata --strict`
