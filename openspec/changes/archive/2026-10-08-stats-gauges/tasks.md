## 1. SDK: catalog levels and the gauge scale

- [x] 1.1 Add optional `Warning` and `Critical` (canonical unit) to `ReadingDescriptor`, with doc comments
- [x] 1.2 Add `GaugeStyle`, `GaugeLevel`, `GaugeValue` and `GaugeScaleOptions` in `sdk/UrDeck.Sdk/Components`
- [x] 1.3 Add `GaugeScale.Resolve` per design decision 3: the range order (caller, catalog, 0 to 100 for a percentage), no fraction without a usable range or a number, rounding to the shown decimals first, the level rule, and stale and pending readings keeping their value's result
- [x] 1.4 Set the package `Version` to 0.5.0; keep `AssemblyVersion` 0.3.0.0 and note in the csproj comment that the change is additive

## 2. SDK: the gauge component

- [x] 2.1 Add `Gauge.Draw` and `GaugeOptions` (label, fraction, level); `Plain` delegates to `Readout.Draw` for a `ReadingText`
- [x] 2.2 Draw the ring: 270 degree arc open at the bottom, stroke from `Theme.StrokeRatio` and `Theme.StrokeCap`, readout and label centred in the inner circle
- [x] 2.3 Draw the bar: track at the top filled from the left, readout below
- [x] 2.4 Draw the vertical bar: the rectangle as the track, filled from the bottom, readout inside at the bottom
- [x] 2.5 Colours per design decision 4: accent, warning and critical fill; the track at the dim alpha; muted when the reading is not current; track only without a fraction
- [x] 2.6 The vertical bar's text contrast rule: `Theme.Text` or `Theme.Background` by relative luminance against the fill, decided where the readout lies over the fill
- [x] 2.7 Add a way for a widget to learn the readout size a gauge would use (through `Readout.Measure` or a `Gauge.Measure`), so several gauges can share one text size

## 3. Theme: the default gauge style

- [x] 3.1 Add `GaugeStyle` to `Theme` (default `Ring`)
- [x] 3.2 Add the `gauge` section (`style`) to `ThemeDefinition`: merge over the baseline, validate (`ring`, `bar`, `verticalBar`; anything else, including `plain`, falls back with a warning) and resolve
- [x] 3.3 Set `gauge.style` to `ring` in `default-dark.json`; check that `default-light` and `glass` inherit it

## 4. Stats widget

- [x] 4.1 Add `Style`, `Min`, `Max`, `Warning` and `Critical` to `StatsSlot`; unknown properties stay preserved
- [x] 4.2 Add `[WidgetSize(4, 2)]` and `[WidgetSize(4, 4)]` and the composition table of design decision 7 (positions per size, gauge or text)
- [x] 4.3 Make `Subscriptions` the readings of the shown positions; keep `system:cpu/load` for a widget with no slots
- [x] 4.4 Resolve each position's style per the table in design decision 5 (an unknown style name counts as absent), and fall back to plain when the scale gives no fraction for a reading that has a value
- [x] 4.5 Lay out and draw the compositions: equal gauge rectangles, one shared readout size across the gauge positions, the text row of fixed height with three equal columns, empty positions left empty
- [x] 4.6 Replace the single `Shown` record with one per position (text, label, target fraction, level) and compare all shown positions in `NeedsRender`
- [x] 4.7 Implement the ease per design decision 6: a private per-position transition (from, to, start), progress from `WidgetRenderContext.Time` limited to 0..1, ease-out, 300 ms, retargeting from the drawn fraction, at rest on the first paint and after `Configure`, and `IsAnimating` while any position is moving

## 5. Tests

- [x] 5.1 Gauge scale tests: each range source, the percentage default, no fraction without a range or for a text value, clamping, rounding before scaling, the three levels, caller values over catalog values, stale and unavailable readings
- [x] 5.2 Gauge component tests (pixel probes on a bitmap, as `ComponentTests` does): plain equals the readout; the ring's fill covers the expected part of the arc at 0, 0.25 and 1; the bar and the vertical bar fill from the left and from the bottom; fill colour per level; muted when not current; track only without a fraction; the readout's text size is stable
- [x] 5.3 Vertical bar contrast test: text over a light fill uses the background colour, text over a dark fill uses the text colour
- [x] 5.4 Theme tests: `gauge.style` loads, a partial user theme inherits it, `plain` and an unknown name fall back with a warning
- [x] 5.5 Stats widget tests: the four sizes are offered; subscriptions per size; the style table at 1x1 and in gauge and text positions; an old config draws as before; unknown style; a reading that cannot be scaled draws plain; slot levels change the fill colour; all slots survive a config round trip
- [x] 5.6 Stats widget motion tests with a controlled render time: `IsAnimating` false on the first paint, true after a changed fraction, false after 300 ms; no repaint and no animation after an unchanged rounded value; retargeting continues from the drawn fraction; a plain card never animates
- [x] 5.7 `SdkContractTests`: the assembly version is still 0.3.0.0 and a `ReadingDescriptor` built without levels has none

## 6. Verify

- [x] 6.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, and `openspec validate --all --strict`
- [x] 6.2 `UrDeck.Host.exe --snapshot` of a page with a 4x4 and a 4x2 stats card and four 1x1 cards (one per style): gauges at rest at their values, text stats in a row, an unknown reading as a dash with an empty track
- [x] 6.3 On the panel: record private bytes and CPU for a page with a Clock and a full 4x4 card, idle and under load, in `docs/perf/stats-gauges.md`; confirm that the frame clock stops between changes. If the 4x4 repaint cost stands out, apply the clip-and-redraw escape of design decision 8 and measure again
- [x] 6.4 Owner looks at the panel under the dark, light and glass themes: the ring's sweep and thickness, the text row's height, the three styles at 1x1 and in the 4x4, the ease, the colour change at a slot's `warning` and `critical`, and the vertical bar's text over a warning-coloured fill
- [x] 6.5 On the panel: an existing page of 1x1 core cards from `data-providers` looks the same as before this change

## 7. Documentation

- [x] 7.1 `docs/themes.md`: the `gauge` section and which colours a gauge uses
- [x] 7.2 `README.md`: the stats widget's sizes, the new slot properties and an example 4x4 configuration with the readings that exist
- [x] 7.3 `CONTRIBUTING.md`: using the gauge and the gauge scale in a widget, declaring warning and critical values in a provider's catalog, and easing with `IsAnimating`
- [x] 7.4 `docs/ROADMAP.md`: "Current state", item 3, the stats row of the widget table in item 7 and the notes on the larger compositions
