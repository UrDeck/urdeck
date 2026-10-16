## 1. Groundwork

- [ ] 1.1 Confirm `theme-golden-harness` is merged and the goldens pass on this branch before any change; every later group re-runs them and they must not change
- [ ] 1.2 Read `docs/perf/theme-look.md` (S3 findings) and the typography part of `docs/design/look-and-feel/02-theme-schema.md` section 4.6 and `03-engine-work.md` section 1.5 beside this design

## 2. Model

- [ ] 2.1 `typography.opticalSize` (`off|auto`) in `ThemeTypographyDefinition` with merge, sanitise (unknown value cleared with one warning), the JSON Schema `docs/schemas/v2/theme.json`, and the schema-vs-model test
- [ ] 2.2 Tests: valid, other case, unknown value, wrong type, absent; and that merging a v1 theme over `default-dark` still produces no format 2 group

## 3. Resolver

- [ ] 3.1 `LoadedTheme` carries the per-role font specs from `typography.fonts` (same `Open` function and path-escape rule per role); `ThemeStore` fills them for built-in and user themes
- [ ] 3.2 `ThemeResolver.LoadTypefaces`: resolve each role through role font, `typography.font`, default theme, system font; share typefaces between roles that agree; dispose exactly the typefaces it created
- [ ] 3.3 `opsz` for `auto`: top of the axis for value and unit, the role's pixel size (clamped) for label, body and title; one `Clone` with `wght` and `opsz`
- [ ] 3.4 Warnings once per loaded theme: missing font per role, weight outside a variable font's range, weight a static font does not have; installed families are not checked
- [ ] 3.5 Tests: each role resolves; path escape rejected per role; missing file falls back along the chain with one warning across several resolves; clamped weight warns once; a static font at another weight warns; `default-dark`, `default-light` and `glass` log nothing new and create the typefaces they did before; no typeface leaks after 20 resolve and dispose rounds
- [ ] 3.6 Opsz tests with the embedded Inter (`opsz` 14 to 32): `auto` changes the digits' ink, `off` does not

## 4. SDK Theme

- [ ] 4.1 `TextStyle` and `TextCase` in `sdk/UrDeck.Sdk`, `Theme.Text` (default: nothing set), the glow cache disposed with the theme
- [ ] 4.2 `ThemeResolver` fills `Theme.Text` from the definition (glow blur in pixels)
- [ ] 4.3 API check: only new members; the SDK's package version moves up one minor; the analyzer tests pass
- [ ] 4.4 Tests: a theme with no text style gives the "nothing set" style; sizes are in pixels for the surface; the style is the same for the page and the dock theme except sizes

## 5. Components

- [ ] 5.1 Case in `TextLine` (label, title) and `Readout` labels, invariant, before measuring; tests including shrink-to-fit
- [ ] 5.2 Letter spacing in `Readout` (value, label) and `TextLine` (label, title): added after each character but the last, digits stay equal, counted in the fit; tests for width, for stable size across values, and that spacing 0 is byte-identical
- [ ] 5.3 Slant for the value (shear about the baseline, layout unchanged), value scale (limited to the fit), value fill (gradient across the value box, stale and explicit colour skip it)
- [ ] 5.4 Glow with the cached blurred copy; test it blurs once for repeated draws; measure the cost against a plain readout (budget: at most 2x) and record it in `docs/perf/theme-look.md`
- [ ] 5.5 A test with no style set that compares `Readout` and `TextLine` output byte for byte with the output of the untouched path (kept as a copy in the test or as the golden)
- [ ] 5.6 Pictures of the specimens, looked at by eye: Obsidian-like (Bodoni numerals via a fixture font, spaced capitals), Ember-like (slant and glow), Rigline-like (gradient), Carbon-like (two families); attach to the pull request

## 6. Verification

- [ ] 6.1 The goldens of `theme-golden-harness` are unchanged (v1 invariance)
- [ ] 6.2 `dotnet build urdeck.slnx -c Release` 0 warnings; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`; engine tests with the coverage floor
- [ ] 6.3 Check `valueScale` against the mocks (Pop 1.35, Obsidian 1.25, Ember 1.2) on the sample page; if the limit to the fit leaves them visibly short of the mock, record what the theme change should do instead (padding, a different base) in this change's design before archive
- [ ] 6.4 On the panel, look at one fixture theme with a per-role font and `auto` optical size; record the memory with a second font family against the one-family baseline

## 7. Documentation

- [ ] 7.1 `docs/themes.md` typography section: remove "Drawn by: the typography change", add `opticalSize`, the fallback chain, the weight warnings, what `valueScale` does and does not do
- [ ] 7.2 The site's theme page (`site/src/content/docs/`) and `docs/design/look-and-feel/STATUS.md` (the row for this change and the validator findings it implements)
- [ ] 7.3 `docs/design/theme-format-2-deferred-keys.md`: no key restored; add the line that `typography.opticalSize` is new in this change
