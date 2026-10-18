## 1. Groundwork

- [x] 1.1 Confirm the goldens (`theme-golden-harness`) pass on this branch before any change; every group below re-runs them and they must not change
- [x] 1.2 Read `docs/design/look-and-feel/02-theme-schema.md` section 4.8 and `04-widgets-and-icons.md` section 1 with the board (`themes-1-...`, `themes-2-...`) beside this design
- [x] 1.3 Keep a copy of the current `Gauge` drawing code in the tests (as `V1Text` does for text) for the byte-for-byte comparison

## 2. Model and SDK surface

- [x] 2.1 `gauge.layout` (`auto|stacked`) in `ThemeGaugeDefinition`: merge, sanitise (unknown value cleared with one warning), the JSON Schema, the schema-vs-model test
- [x] 2.2 `GaugeStyle` appended with `Meter`, `Tach`, `Marker`, `ThickBar`; `ThemeGaugeDefinition.Parse` accepts `meter`, `tach`, `marker`, `thickbar` and `thickBar`; the resolver no longer maps them to `ring`
- [x] 2.3 `GaugeTheme` and the new `Theme` members (`Gauge`, `Accent2`, `Ink`, `Ramp`) with "no effect" defaults; `ThemeResolver` fills them (lengths in pixels from the cell size); API check: new members only, package version up one minor
- [x] 2.4 Tests: a v1 theme gives the empty `GaugeTheme`; defaults for `Accent2`, `Ink` and `Ramp`; sizes in pixels; each drafted theme resolves its gauge values

## 3. The existing styles with the new values

- [x] 3.1 Split `Gauge.cs` into one file per style behind the same entry; the v1 path unchanged; the v1 invariance test (three v1 themes, two cell sizes, ring, bar, vertical bar, with and without a fraction) passes
- [x] 3.2 `sweep` for the ring (including a 360 span with no gap) and `track` for all styles
- [x] 3.3 `segments` on the bar and the ring, partial last block; tests by block count and clipped length
- [x] 3.4 `gradient` (explicit and `ramp`) laid over the whole track, `colorBy: ramp`, `valueTint`, the stale-reading rule; tests by sampled pixel colours
- [x] 3.5 `glow` of the fill; `labelPosition` for the ring (`below`, `above`, `inside`); the readout rectangle `Gauge.Measure` returns follows each

## 4. The new styles

- [x] 4.1 `meter`: stacked layout, thin track, ticks under it, `scale.labels` evenly spaced under the track; `bar` with `gauge.layout` `stacked`
- [x] 4.2 `thickbar`: heavy track, `outline`, stacked layout
- [x] 4.3 `tach`: arc, tick ring outside the track with major ticks and optional numbers, `redline` with `lightsAbove` and its glow, optional needle
- [x] 4.4 `marker`: hairline arc or full circle, `diamond` and `needle` markers with `halo`, ticks and numbers, `crosshair`
- [x] 4.5 Geometry tests per style (the marker's angle at a fraction, tick count and major marks, the redline's state either side of `from`, the needle's end point) and a no-fraction test that draws the track alone
- [x] 4.6 `Gauge.Measure` for each new style; one readout size across the gauges of a Stats card still holds

## 5. Stats and the drafted themes

- [x] 5.1 Stats slot styles `meter`, `tach`, `marker`, `thickbar`; the theme default is drawn as itself; tests for a named style, the theme default, and a text reading with a new style
- [ ] 5.2 Render the sample page under each drafted theme as a fixture (Carbon's bar and stacked layout, Rigline, Prism, Paper, Ember, Blueprint, Pop, Obsidian) and look at each gauge beside the board; note every difference and why in the pull request
- [ ] 5.3 Check Blueprint's sweep against its mock (design, risks) and Ember's and Pop's gauges at 1x1 and 2x2; record the finding for the theme changes
- [ ] 5.4 Measure a repaint of each style against today's ring and with a glow (budget 2x and 3x), and record it in `docs/perf/theme-look.md`; confirm the cache is built once per size and disposed with the theme

## 6. Verification and documentation

- [x] 6.1 The goldens are unchanged; `dotnet build urdeck.slnx -c Release` has 0 warnings; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`; the engine tests with the coverage floor
- [ ] 6.2 On the panel, look at one fixture theme with a tach and one with a thick bar (Stats at 2x2 and 4x2); memory and CPU against the ring baseline
- [x] 6.3 `docs/themes.md` gauge section (remove "drawn by the gauge change", add `layout`), the site's theme and Stats pages, `docs/design/look-and-feel/STATUS.md` (row and the findings of 5.2 and 5.3)
