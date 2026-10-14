## 1. Groundwork

- [x] 1.1 Read `docs/design/look-and-feel/02-theme-schema.md` and `assets/schema/theme.schema.json` there with `docs/design/theme-format-2-deferred-keys.md` beside them; write the list of keys this change carries (the drafts' keys minus the deferred ones) as the checklist for groups 3 to 10
- [x] 1.2 Split `ThemeDefinition` into partial files by group without changing behaviour; the existing tests pass unchanged
- [x] 1.3 Add the `ValueCheck` helpers (`Enum`, `Colors(path, list, min, max)`, `Angle`, `Array`) and move the v1 checks onto them; the warning text stays byte-identical, asserted by the existing tests
- [x] 1.4 Add `format` (optional 1 or 2; a larger value logs one warning and still loads; a non-integer is dropped with a warning)

## 2. Merge rules

- [x] 2.1 Recursive `MergeOver` for nested groups, with the never-inherited list (background palette, `colors.ramp`, `colors.cardTints`, `meta`, `pattern`, `background`, `icons`, card chrome, typography effects, gauge modifiers)
- [x] 2.2 A feature that is off stays absent after the merge; test that merging a v1 theme over `default-dark` produces no format 2 group
- [x] 2.3 Tests: the exceptions, partial groups (`card.shadow` with only `blur`), and that default-dark, default-light and glass merge to the same values as before

## 3. Colours and card chrome

- [x] 3.1 `colors.accent2`, `ramp` (2..5), `cardTints` (1..8), `ink`
- [x] 3.2 `card.shape`, `cut`, `cutCorners` (unknown names dropped with a warning, duplicates removed), `fillGradient`, `tint`
- [x] 3.3 `card.borderStyle` (`solid|dashed|none`), `borderDash`, `borderGradient`, `borderAnimate`
- [x] 3.4 `card.cornerMarks` (`bracket|tick`), `shadow`, `glow`, `highlight`, `sheen`, `accentStrip`
- [x] 3.5 `card.compact` (radius, borderWidth, shape, accentStrip, cornerMarks) as a partial of the same shapes
- [x] 3.6 Per-key tests: valid, out of range, wrong type, absent; the warning text for each

## 4. Page, parts and typography

- [x] 4.1 `pattern` (kinds `dots|grid|hatch|scanlines`, spacing, thickness, colour, angle, `majorEvery`, `major`)
- [x] 4.2 `divider`, `chip`, `well`
- [x] 4.3 `typography.fonts` (`value`, `unit`, `label`), `valueScale`, `case`, `tracking`, `slant`, `valueFill`, `valueGlow`; the values are read and kept, the resolver does not use them yet
- [x] 4.4 Per-key tests as in 3.6

## 5. Gauge, icons, background, motion, meta

- [x] 5.1 `gauge.style` accepts `meter|tach|marker|thickbar`; the resolver maps them to `ring` for the SDK theme (SDK untouched); test both halves
- [x] 5.2 Gauge modifiers: `sweep`, `segments`, `outline`, `ticks`, `scale`, `redline`, `gradient`, `glow`, `marker`, `crosshair`, `track`, `labelPosition`, `colorBy`, `valueTint`, `sparkline`, `status`
- [x] 5.3 `icons` (`set`, `stroke`, `tint`, `animated`, `glow`)
- [x] 5.4 `background` (`default.name`, `static` gradient linear and radial, colour and `image`, `scrim`) as data only; the plan that uses it is the `theme-backgrounds` change
- [x] 5.5 `motion.level` (`full|reduced|off`)
- [x] 5.6 `meta` (`name`, `description`, `author`, `license`, `version`, `mode`, `tags`, `pairsWith.backgrounds`, `samples`); a theme without `meta.name` reports its folder name
- [x] 5.7 Per-key tests as in 3.6

## 6. Cross-field rules

- [x] 6.1 Border style, gradient or animation with border width 0: one warning
- [x] 6.2 `borderAnimate.spin` without a conic gradient: animation cleared, one warning
- [x] 6.3 Pattern too dense (`spacing / thickness` below 3): one warning
- [x] 6.4 Outside corner marks with a gap smaller than the inset: one warning
- [x] 6.5 Chamfer with a circle compact shape: allowed, no warning
- [x] 6.6 More than one continuous animation: one information line (the only continuous animation among the carried keys is `borderAnimate`, so the line cannot fire from a file yet; the check itself is tested directly)
- [x] 6.7 A theme with every format 2 value invalid loads with the default theme's values and one warning per key (the fuzz-style test of the spec)

## 7. Schema and reference

- [x] 7.1 Copy `docs/design/look-and-feel/assets/schema/theme.schema.json` to `docs/schemas/v2/theme.json`; remove the deferred keys and the enum values the deferred document lists; keep `additionalProperties: true`
- [x] 7.2 Add the test-only JSON Schema package to `Directory.Packages.props` and the test project; confirm it is not referenced by any shipped project
- [x] 7.3 The schema-versus-model test (reflection over the model, walk of the schema), failing with the name of the key
- [x] 7.4 `docs/themes.md`: the reference for every new key (range, default, which later change draws it); the three groups of the shipped themes unchanged
- [x] 7.5 Update `docs/design/theme-format-2-deferred-keys.md` if a key had to be added or removed on the way

## 8. The drafted themes as fixtures

- [x] 8.1 Copy the eight drafts from `docs/design/look-and-feel/assets/themes/` to `tests/UrDeck.Engine.Tests/Fixtures/themes/<name>/theme.json` and make the four edits: remove Prism's `card.blur`, Rigline's `typography.valueFill.animate` and `pattern.scroll`, and point Pop's value role at Archivo (weight 900)
- [x] 8.2 Each draft validates against `docs/schemas/v2/theme.json`
- [x] 8.3 Each draft loads with no warning (a test that captures the log)
- [x] 8.4 Confirm `ThemeStore` still lists only `default-dark`, `default-light` and `glass`, and that `"theme": "carbon"` falls back with the missing-theme warning

## 9. Verification

- [x] 9.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; `dotnet test tests/UrDeck.Engine.Tests -c Release -p:CollectCoverage=true` meets the floor; `dotnet format urdeck.slnx --severity warn` is clean; `openspec validate --all --strict`
- [x] 9.2 Render a snapshot of each of the three built-in themes before and after (`--snapshot ... --size 1100x3840`) and compare the PNGs byte for byte
- [x] 9.3 Run the host once on the panel with a user theme that uses format 2 values and read `urdeck.log`: no warning, no change in what is drawn (checked on the Y70 panel with a user theme carrying format 2 values: no warning, looks as before)
