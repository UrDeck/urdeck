## Why

The theme rework (`theme-format-2` and the changes after it) published its data model as a second version: a
`"format": 2` key in every theme, a `docs/schemas/v2/theme.json` beside a v1 that does not exist for themes, a
`NewerFormat` finding, and a split of the engine's theme code into "format 1" and "format 2" members. The intent was
never a second version. The theme format has not shipped, nothing outside this repository reads it, and the project
keeps no backwards compatibility before release. A version number that means nothing yet is a promise to maintain a
distinction nobody needs, and it would be published in the first public release.

## What Changes

- The theme format is one format, version 1. The schema moves from `docs/schemas/v2/theme.json` to
  `docs/schemas/v1/theme.json` (published at `https://urdeck.app/schemas/v1/theme.json`, beside `shader-manifest.json`).
- **BREAKING** The `format` key leaves the theme file. It is dropped from the built-in themes, the cookbook recipes, the
  sample configuration, the `theme init` starter and every test fixture. A theme that still has one gets the unknown-key
  warning (`THEME002`) like any other key the deck does not read.
- **BREAKING** The `THEME010` finding ("format is newer than the deck reads") is removed with the `NewerFormat` code.
  Its number is not reused.
- A value of the wrong JSON kind (a text where a number goes) costs only that value, for every theme key. Until now that
  held for the format 2 keys only, and a wrong kind in a format 1 key (`card.gap: "wide"`) failed the whole theme.
  `THEME003` is therefore always a warning.
- The engine's "format 2" vocabulary goes: `ThemeFormat2.*` partials become `ThemeDefinition.*` partials, `Format2Attribute`
  is removed, and `SupportedFormat`, `Format` and `SanitizeFormat` are deleted.
- The theme reference, the quick start, `docs/themes.md`, the README and the site's generated reference say "the theme
  format", not "format 1" and "format 2".
- Nothing is drawn differently: every golden image stays byte-identical.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `theme`: the version requirement is removed; the "Format 2 ..." requirements become the theme's ordinary value groups,
  validation and additivity rules; the published schema is `docs/schemas/v1/theme.json`; a value of the wrong kind
  costs only that value.
- `theme-lint`: `THEME003` is always a warning; `THEME010` is removed.
- `urdeck-cli`: the `theme init` starter has no `format` line; `theme schema` prints `docs/schemas/v1/theme.json`.
- `backgrounds`: "Theme Backgrounds" refers to the theme's value groups by their new requirement name.

## Impact

- `src/UrDeck.Engine/Themes`: `ThemeDefinition` (drop `Format`, `SupportedFormat`, `SanitizeFormat`), the `ThemeFormat2.*`
  partials (renamed), `ThemeLenientJson` (no `Format2` distinction), `ThemeFinding`/`ThemeCodes` and `Lint/ThemeLint`
  (remove `NewerFormat`; `THEME003` severity), `Builtin/*.json`.
- `tools/UrDeck.Cli`: `ThemeInitRunner`, `ThemeSchemaRunner`, `Commands.cs`, the csproj's linked schema resource.
- `docs/schemas/v1/theme.json` (moved), `docs/themes.md`, `README.md`, `site/` (reference, quick start, scripts that
  copy the schema, cookbook themes, tests), `src/UrDeck.Host/urdeck-config.json`.
- `tests/`: every fixture and test theme with a `"format"` key, the `THEME010` lint fixtures and the SARIF test.
- A `$schema` line that points at `/schemas/v2/` stops resolving, so completion is lost until it is edited. There are
  no outside users yet.
- Not touched: archived changes under `openspec/changes/archive/`, and `docs/design/look-and-feel/` (the design record).
