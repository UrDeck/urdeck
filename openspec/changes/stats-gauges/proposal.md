## Why

The stats widget shows one number per card, so the owner's performance page (four gauges and three text stats in one
4x4 card, as in HYTE Nexus) cannot be built. This is the second of the three changes of roadmap item 3. It brings the
gauge, the first shared component that draws a shape from a reading, and it is settled now because it adds public SDK
API that the weather and media widgets will build on.

## What Changes

- **A gauge component in the SDK.** It draws a reading as a shape with its readout: `ring`, `bar` and `verticalBar`,
  plus `plain` (the readout alone). The component places the readout itself, because it sits inside the ring, below
  the bar and over the vertical bar. Every style takes the same inputs, so more styles can follow without a contract
  change.
- **One scale rule for all widgets.** The SDK turns a reading into a fraction of its range and a level (normal,
  warning, critical). The range and the levels come from the reading's catalog entry and can be overridden per slot.
- **Colour by level.** The shape is drawn in the theme's accent, and in the theme's warning or critical colour once
  the value reaches that level. The number keeps the text colour. A reading with no levels is always accent.
- **Catalog entries gain optional warning and critical levels**, so a provider can say when its reading runs hot.
  The `system` readings that exist today declare none: a busy CPU is not a fault.
- **The theme names the default gauge style** (`gauge.style`, `ring` in the built-in themes). A slot can override it.
- **The stats widget grows to 4x2 and 4x4.** 4x2 is two gauges above three text stats; 4x4 is four gauges above three
  text stats. Slots fill the positions in order. At 1x1 and 2x2 a slot can now ask for a gauge.
- **Slots gain `style`, `min`, `max`, `warning` and `critical`.** A slot without `style` looks as it does today at
  1x1 and 2x2, so existing configurations do not change.
- **The fill eases** to a new value over about 300 ms; the number and the colour change at once. The widget asks for
  animation frames only during that time, and only when the value it shows has changed.

Deliberately not in this change: GPU readings (the `gpu-readings` change, which also brings the first readings with
levels); sensors that need elevation; more gauge styles (disc, slider, segmented ring); readings where low is bad
(battery, fan speed); a shared transition helper in the SDK; history and charts; a per-widget editor.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `components`: adds the gauge component, its styles, the scale rule and the level colours.
- `data-providers`: a catalog entry may carry a warning and a critical level.
- `theme`: a theme names the default gauge style.
- `widget-stats`: the sizes 4x2 and 4x4 and their compositions, the new slot properties, gauge presentation, the
  eased fill and the repaint rule that goes with it.

## Impact

- `sdk/UrDeck.Sdk`: new `Components/Gauge` (component, style, options, scale), `ReadingDescriptor` (two optional
  members), `Theme` (default gauge style). All additive: `AssemblyVersion` stays 0.3.0.0, package `Version` 0.5.0.
- `src/UrDeck.Engine/Themes`: `gauge.style` in the theme definition, validation and resolver; the three built-in themes.
- `widgets/UrDeck.Widgets.Stats`: `StatsConfig`, `StatsWidget` (compositions, several subscriptions, the ease).
- Tests: component tests for the gauge and the scale, theme tests, stats widget tests.
- Docs: `docs/themes.md`, `README.md` (slots and sizes), `CONTRIBUTING.md` (using the gauge), `docs/ROADMAP.md`,
  a perf note `docs/perf/stats-gauges.md`.
- No new package dependency. No change to the engine's reading hub or to the host.
