## Why

Format 2 lets a theme name a font per role, a letter case, letter spacing, a slant, a value size, a gradient fill and a
glow for its numbers (`typography.fonts`, `case`, `tracking`, `slant`, `valueScale`, `valueFill`, `valueGlow`). They are
read, checked and kept, and nothing draws them: the text components and the resolver still use one font for all roles
and one look for every theme. Five of the eight drafted themes depend on them (Obsidian's Didone numerals and spaced
capitals, Ember's slanted glowing digits, Rigline's gradient clock, Carbon's monospaced values, Pop's heavy numerals).

The font spike (S3) also found three things that make a theme silently wrong if the loader stays as it is: the optical
size axis of Bodoni Moda and Bricolage Grotesque is never driven, a weight a font cannot make is clamped or ignored
without a word, and the text components have no fallback for a missing glyph.

## What Changes

- **Per-role fonts.** `typography.fonts.value`, `unit` and `label` select a family or a file in the theme folder per
  role; a role without one uses `typography.font`. The fallback chain per role is the role's font, `typography.font`,
  the default theme's font, the system font, with one warning per missing font per loaded theme. A path that leaves the
  theme folder is refused for each role.
- **Weight honesty.** A weight outside a variable font's range, or a weight a static file does not have, logs one
  warning per role per loaded theme. Built-in v1 themes log nothing new.
- **Optical size.** A new `typography.opticalSize` (`off` or `auto`, default `off`) drives a font's `opsz` axis: the
  display end for numerals and units, the role's pixel size for labels, body and titles. Off, which every existing
  theme is, leaves the axis where it is today, so no existing picture moves.
- **Drawing.** The SDK's `Readout` and `TextLine` draw `case` (label, title), `tracking` (value, label, title), `slant`
  (value), `valueScale`, `valueFill` and `valueGlow`. With none of them set the output is byte-identical to today.
- **SDK.** `Theme` gains a read-only text style that carries these values in pixels, so a widget that uses the
  components gets them without code of its own. The SDK stays backward compatible (new members only) and its minor
  version moves.
- **Docs.** `docs/themes.md` and the site's theme page document the keys; the drafts' unused or deferred keys stay in
  `docs/design/theme-format-2-deferred-keys.md`.

Not in this change: the animated value fill, `typography.fonts.body` and `title`, `case.unit`, `tracking.body` (deferred
keys), bundling any new font family (each theme change brings its own), the gauge styles, and the split-flap clock's own
digit drawing, which does not go through `Readout`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `theme`: the Fonts requirement gains per-role fonts, the fallback chain, the warning for weights a font cannot make;
  a new Optical Size requirement.
- `components`: Readout and Text Line draw case, tracking, slant, value scale, value fill and value glow.
- `widget-sdk`: the Theme a widget receives carries the text style.

## Impact

- `sdk/UrDeck.Sdk`: `Theme` (new `Text` member and a `TextStyle` type), `Readout`, `TextLine`, `TextMetrics`.
- `src/UrDeck.Engine/Themes`: `ThemeResolver` (per-role typefaces, `opsz`, warnings), `ThemeDefinition.Typography`
  and `ThemeFormat2.Typography` (the new `opticalSize` key, the schema `docs/schemas/v2/theme.json`).
- `tests/UrDeck.Engine.Tests`: resolver, component and sample-page tests; goldens must stay unchanged (this change
  depends on `theme-golden-harness`).
- `docs/themes.md`, `site/src/content/docs/` theme page.
- The memory cost of an extra typeface per distinct (font, weight, optical size) is about 1 MB private per further
  family and almost nothing per clone (`docs/perf/theme-look.md`, S3).
