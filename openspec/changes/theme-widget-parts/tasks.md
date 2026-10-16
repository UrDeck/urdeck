## 1. Groundwork

- [ ] 1.1 Confirm `theme-gauges` is merged and the goldens pass on this branch before any change; every group re-runs them and they must not change
- [ ] 1.2 Read `docs/design/look-and-feel/04-widgets-and-icons.md` sections 1.4 and 2, `02-theme-schema.md` sections 4.5 and 4.8, and the board beside this design

## 2. SDK surface

- [ ] 2.1 `Theme.Chip`, `Theme.Divider`, `Theme.Well` and the sparkline and status values on the gauge values, with "no effect" defaults; `ThemeResolver` fills them in pixels; the divider default equals Weather's current rule
- [ ] 2.2 `GaugeOptions.History` and `GaugeOptions.Status`; API check: new members only; package version up one minor
- [ ] 2.3 Tests: a v1 theme gives the empty values; the divider default equals the Weather rule on the three v1 themes; sizes are in pixels

## 3. Components

- [ ] 3.1 `Chip.Draw`: plain and `pill`, `colorBy: status`, shrink to fit, theme case; tests by pixels and the text rectangle
- [ ] 3.2 `Divider.Draw`: `line`, `gradient`, `dimension` (end ticks, room for a label), the default; tests by sampled pixels along the rule
- [ ] 3.3 `Well.Draw`: fill, radius capped at half the smaller side, border; nothing without a well
- [ ] 3.4 `Sparkline.Draw`: polyline of fractions, fewer than two draw nothing; tests
- [ ] 3.5 The gauge's status row (label left, chip right) and sparkline under the readout, only when given and asked for; a gauge without them is unchanged; the sparkline is skipped under 0.9 of a cell
- [ ] 3.6 `Gauge.Measure` includes the status row and the sparkline so the one-readout-size rule across a card still holds

## 4. Stats

- [ ] 4.1 History ring per gauge slot: one value per second, `historySeconds` long, allocated only while the sparkline is enabled and the position is tall enough, cleared when the slot's reading or the configuration changes
- [ ] 4.2 Status from the theme's labels and thresholds, the slot's own warning and critical winning; no fraction, no status
- [ ] 4.3 Tests: sparkline enabled and not, too small, reading replaced; status thresholds, slot levels, no fraction; memory of the buffers (an allocation test)

## 5. Clock

- [ ] 5.1 Options `header`, `subtitle`, `showWeek`, `showSeconds`, `blinkSeparator` in `ClockConfig` (all optional, unknown values ignored, kept across load and save); header layout for both styles
- [ ] 5.2 Per-second repaint only with `showSeconds` or `blinkSeparator`; `IsAnimating` unchanged; a configuration with none of them draws once a minute and byte-identically (golden)
- [ ] 5.3 Measure the per-second repaint of one clock card against the once-a-minute baseline, and record it in `docs/perf/theme-look.md`
- [ ] 5.4 Tests: header modes, week number, seconds, blink on and off ticks, nothing set

## 6. Weather

- [ ] 6.1 The 4x4 rule drawn by `Divider.Draw`; v1 themes byte-identical (compare with the old paint)
- [ ] 6.2 A well behind each forecast column, the condition on `Chip.Draw`; the card is unchanged under a theme with no well and no chip
- [ ] 6.3 Tests: well present and absent, pill, gradient rule, a v1 theme

## 7. The drafted themes and documentation

- [ ] 7.1 Render the sample page under each drafted theme as a fixture (Carbon's header, status and history; Rigline's well; Pop's pill and tiles; Paper's rule and week; Blueprint's dimension rule; Obsidian's gradient rule) and look at each beside the board; note every difference and why in the pull request
- [ ] 7.2 The goldens are unchanged; build with 0 warnings; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`; the engine tests with the coverage floor
- [ ] 7.3 `docs/themes.md` (remove "drawn by the widget parts change"), the widgets' option tables on the site (`header`, `subtitle`, `showWeek`, `showSeconds`, `blinkSeparator`), and `docs/design/look-and-feel/STATUS.md` (the row and the findings of 7.1)
