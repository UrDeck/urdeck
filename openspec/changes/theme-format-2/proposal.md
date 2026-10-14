## Why

UrDeck's three built-in themes are rounded translucent cards, which is what the program it replaces looks like. A
design pass produced eight themes (Carbon, Rigline, Prism, Paper, Ember, Blueprint, Pop, Obsidian) that differ in
structure, not only colour: chamfered cards, dashed outlines with corner marks, hard shadows, gradient borders, other
typefaces, other gauges, a pattern on the page, a background each. The theme file cannot say any of that today.

Everything else in the look-and-feel work (the card chrome, the typography, the gauges, the icons, the themes
themselves) reads values this change defines, so the data model comes first: the new keys, how they merge, how they are
checked and how they are published, with no change to anything drawn. Landing it alone gives the later changes one
agreed, tested vocabulary and keeps every existing theme pixel-identical while it does.

## What Changes

- A theme may declare `"format": 2`. Absent means 1. A host that supports format 2 loads both; a theme that declares a
  newer format than the host knows logs one warning and still loads.
- Optional new groups in `theme.json`, each key with a range, a default and a validation rule, all absent by default
  (absent means the feature is off): `meta`, new `colors` (`accent2`, `ramp`, `cardTints`, `ink`), card chrome
  (`shape`, `cut`, `cutCorners`, `fillGradient`, `borderStyle`, `borderDash`, `borderGradient`, `borderAnimate`,
  `cornerMarks`, `shadow`, `glow`, `highlight`, `sheen`, `accentStrip`, `tint`, `compact`), `pattern`, `divider`, `chip`,
  `well`, typography roles (`fonts`, `valueScale`, `case`, `tracking`, `slant`, `valueFill`, `valueGlow`), `gauge`
  modifiers and the styles `meter`, `tach`, `marker` and `thickbar`, `icons`, `background` (`default`, `static`, `scrim`)
  and `motion`.
- The keys no drafted theme uses are **not** in this change. They are recorded with their definitions and the reason in
  `docs/design/theme-format-2-deferred-keys.md`, so a later change can restore one. Those keys, and anything else
  unknown, are ignored as unknown keys always have been.
- Merge and validation follow the existing rules: an invalid value is replaced by the default with one logged warning
  naming the theme and the key; arrays and colour arrays are dropped whole; a few groups are never inherited from the
  default theme; the cross-field rules that can be decided from the file alone are checked.
- The theme's published schema, `docs/schemas/v2/theme.json`, and a test that fails when the schema and the model
  disagree.
- The eight drafted themes as test fixtures that must load with no warning. They are not built-in themes yet and cannot
  be selected by name.

Nothing is drawn differently. The new keys are read, checked, merged and kept on the loaded theme; the changes that draw
them follow. The gauge styles `meter`, `tach`, `marker` and `thickbar` are accepted in a theme and a widget asked for one
receives `ring` until the gauge change; the SDK is not touched.

## Capabilities

### New Capabilities

None. Everything here extends the theme.

### Modified Capabilities

- `theme`: "Theme Contents" (the default gauge style may also be one of the four new styles). New requirements for the
  format version, additive and optional format 2 values, theme metadata, the value groups, their validation and merge
  rules, and the published schema.

## Impact

- `src/UrDeck.Engine/Themes`: `ThemeDefinition` gains the new nullable members (split into partial files by group),
  `MergeOver` and `Sanitize` for each, and a small set of `ValueCheck` helpers. `ThemeStore` and `ThemeResolver` keep
  their behaviour; the resolver does not read the new keys yet.
- `docs/schemas/v2/theme.json` (new), `docs/themes.md` (the reference gains the new groups).
- `tests/UrDeck.Engine.Tests`: definition tests per group, the schema-versus-model test, the draft theme fixtures (new
  `Fixtures/themes/<name>/theme.json`). A test-only JSON Schema validator package in `Directory.Packages.props`.
- No SDK, widget, host or rendering change. `UrDeck.Engine` coverage floor still applies.
