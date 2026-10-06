## Context

`data-providers` (archived 2026-10-05) left the stats widget at one reading per card, drawn with `Readout`. Its config
is already a list of slots, and slots beyond the first are kept in the file. The ingredients for a gauge exist:
`Theme` has `Accent`, `AccentDim`, `Warning`, `Critical`, `StrokeRatio` and `StrokeCap`; `ReadingDescriptor` has `Min`
and `Max`; `IWidget.IsAnimating` and the host's 30 fps frame clock exist since `animation`.

The owner's reference is the HYTE Nexus performance widget: two gauges per row, a row of three text stats, and a
choice of gauge styles (ring, disc, slider, segmented ring, bar, a card-high fill, plain). The goal is the same
features with UrDeck's own look. Decisions below were taken with the owner in an exploration on 2026-10-05.

The readings a full performance card needs (GPU load, temperatures, power, clock) do not exist yet. They come with
`gpu-readings` (unelevated) and a later change (elevated). This change is built and tested with CPU load, core loads
and memory load; a slot whose reading does not exist shows the dash.

## Goals / Non-Goals

**Goals:**

- A 4x2 and a 4x4 stats card that match the reference's structure.
- A gauge any widget can use, themed like the other components.
- Adding a gauge style later costs one drawing function and one name, no contract change.
- A card whose shown values do not change is not repainted, with or without gauges.
- Configurations written for 1x1 and 2x2 stay valid and look the same.

**Non-Goals:**

- New readings of any kind.
- The disc, slider and segmented ring styles.
- Readings where low is bad. The later rule can be "critical below warning means inverted", which needs no new
  property.
- A transition helper in the SDK (see decision 6).
- Thresholds on the readings that exist today.
- Free layout of slots inside a card. Compositions are fixed per size.

## Decisions

### 1. One component with a style, not one component per shape

`UrDeck.Sdk.Components.Gauge` draws a formatted reading with a shape. Names are indicative; the specs fix behaviour.

```
 GaugeStyle     enum: Plain, Ring, Bar, VerticalBar
 GaugeLevel     enum: Normal, Warning, Critical
 GaugeValue     readonly record struct: Fraction (float?, 0..1), Level
 GaugeOptions   Label, Fraction (float?, what to draw now), Level
 Gauge.Draw(canvas, theme, rect, style, ReadingText reading, GaugeOptions options) -> SKRect
 GaugeScale.Resolve(Reading, ReadingDescriptor?, GaugeScaleOptions) -> GaugeValue
 GaugeScaleOptions   Min?, Max?, Warning?, Critical?, Decimals?   (the slot's overrides)
```

- Every style takes the same inputs: a fraction, a level, the formatted reading, a label and a rectangle. A new style
  is a new enum value and a private drawing function.
- The component owns the readout's placement. The readout is inside the ring, below the bar and over the vertical
  bar, so a widget that placed the readout itself would have to know each style's geometry.
- `Plain` is the readout alone, so a widget draws every slot through `Gauge.Draw` and has one code path.
- The component is stateless like the others. The fraction passed in is the one to draw now; easing is the caller's.
- `Gauge.Draw` with a null fraction and a style other than `Plain` draws the track with no fill.

Alternative considered: `GaugeRing`, `GaugeBar`, ... as separate components. Rejected: the widget would switch on the
style and place the readout, and each new style would touch every widget that offers a choice.

### 2. Shapes

All thickness comes from `Theme.StrokeRatio` and all line ends from `Theme.StrokeCap`. Nothing here is a new theme
value except the default style (decision 5).

```
   ring                bar                 verticalBar          plain
  ╭──────╮           ▓▓▓▓▓░░░░░░        ┌──────────┐
 ╱  37%   ╲                             │          │
 │  CPU   │            37%              │▓▓▓▓▓▓▓▓▓▓│            37%
 ╲        ╱            CPU              │▓▓ 37% ▓▓▓│            CPU
  ╰─    ─╯                              │▓▓ CPU ▓▓▓│
                                        └──────────┘
```

- **Ring:** an arc of 270 degrees, open at the bottom, filled clockwise from the lower left. The diameter is the
  smaller side of the rectangle; the stroke is `StrokeRatio` of the diameter. The readout, with its label, is centred
  inside the ring's inner circle.
- **Bar:** a horizontal track across the top of the rectangle, as thick as the ring's stroke would be at that
  rectangle, filled from the left. The readout takes the space below it.
- **Vertical bar:** the whole rectangle is the track (an outline as thick as the theme's stroke at label scale, corners
  following the card radius), filled from the bottom. The readout sits at the bottom, inside the rectangle.
- **Plain:** the readout, centred, exactly as the stats widget draws it today.

The exact proportions (how much of the rectangle the bar leaves to the readout, the readout's inset in the vertical
bar) are tuned on the panel by the owner (task 6.4), as the text sizes were. They are constants in the component, not
theme values, until a theme needs to change one.

### 3. The scale: one rule in the SDK

`GaugeScale.Resolve` turns a reading into a fraction and a level so that every widget agrees.

- The range is the caller's `Min`/`Max`, else the catalog entry's, else 0 to 100 for a percentage. With no usable
  range (an end missing, `Max` not above `Min`, or a value that is not a number) there is no fraction, and the gauge
  falls back to plain: a ring with nothing to measure against would say nothing.
- The fraction is `(value - min) / (max - min)`, limited to 0..1.
- The level is critical at or above the critical value, else warning at or above the warning value, else normal. The
  values are the caller's, else the catalog entry's. Missing values mean that level is never reached.
- **The value is rounded to the decimals the text shows before the fraction and the level are computed.** This keeps
  the roadmap's rule that a card whose text does not change is not repainted: a load moving from 37.2 to 37.4 changes
  neither the text nor the ring.
- A stale or pending reading with a last value keeps its fraction and level. An unavailable reading has no fraction.
- Everything is in the reading's canonical unit. A slot's `warning: 80` on a temperature means 80 degrees Celsius even
  when the slot shows Fahrenheit, because the catalog and the provider speak Celsius.

Alternative considered: thresholds only in the theme (for example "warning at 75% of the range"). Rejected: 75% of the
range is hot for a CPU and normal for memory. Where a value becomes a problem is a fact about the reading.

### 4. Colour

- Fill: `Theme.Accent` at normal, `Theme.Warning` and `Theme.Critical` at those levels.
- Track: the fill's colour with the alpha of `Theme.AccentDim`, so the track of a critical gauge is a dim red, not a
  dim blue next to a red fill.
- A reading that is not current: fill and track in `Theme.TextMuted` (with the dim alpha for the track), matching the
  muted value. Unavailable: track only.
- The number and the label keep their readout colours. Muted keeps meaning "not current" and nothing else.
- In the vertical bar the readout can lie over the fill. There the component draws the value and the label in whichever
  of `Theme.Text` and `Theme.Background` has the higher contrast against the fill colour (WCAG relative luminance).
  White on `Warning` yellow is otherwise unreadable, and this needs no extra theme value. Whether the readout is over
  the fill is decided from the target fraction, not the eased one, so the text colour does not flip mid-ease.
- `Theme.Good` is not used by the gauge. The owner asked for good, warning and critical with good defaulting to the
  accent; using the accent directly is the same result without a second colour that every theme would have to keep
  equal to its accent.

Level colours need no theme format change. They are testable in this change only through a slot's own `warning` and
`critical`, because no existing reading declares levels (a busy CPU or full memory is not a fault). The first readings
with catalog levels are the GPU temperature in `gpu-readings`.

### 5. Where the style comes from

```
 slot "style"        1x1, 2x2                     gauge position (4x2, 4x4)     text position
 ───────────         ─────────                    ─────────────────────────     ─────────────
 absent              plain (as today)             the theme's default           plain
 "gauge"             the theme's default          the theme's default           plain
 "ring" | "bar" |
 "verticalBar"       that style                   that style                    plain
 "plain"             plain                        plain                         plain
 anything else       as absent (widgets have no logger yet; the value stays in the file)
```

- The theme gains `gauge.style` (`ring`, `bar` or `verticalBar`; the built-in themes use `ring`). AGENTS.md says
  styling belongs in the theme; a slot's explicit style is the user's choice for one reading, like its label.
- Absent means plain at 1x1 and 2x2 because existing configurations must look the same.
- The theme cannot name `plain` as the default: a slot that asks for a gauge would then get none.

`Theme` gets a `GaugeStyle` property. `ThemeDefinition` gets a `gauge` section, validated and merged like `stroke`.

### 6. The ease lives in the widget

When a slot's target fraction changes, the fill moves from the fraction currently drawn to the new target over 300 ms
with an ease-out curve. The number, the level colour and the label change in that first frame.

- The widget keeps, per slot, the fraction it eases from, the target and the start time, and computes the drawn
  fraction from `WidgetRenderContext.Time`, as the widget SDK's animation rules require. Progress is limited to 0..1,
  so a wall-clock jump ends the ease instead of breaking it.
- `IsAnimating` is true while any slot is mid-ease. The frame clock therefore runs for 300 ms after a change and not
  otherwise. With the 2000 ms sampling interval a busy card animates about 15% of the time; an idle one never.
- A new target during an ease starts a new ease from the fraction drawn at that moment, so the fill never jumps.
- The first paint, and the first paint after `Configure`, draw the target at rest. `--snapshot` paints once and
  therefore shows the target.
- A reading that gets its first value eases up from an empty track. A reading that becomes unavailable drops to the
  empty track at once: there is no value to ease to.

Alternative considered: a `Transition` helper in the SDK. `animation` left it out on purpose until a widget loops; the
Clock keeps its flip timing private too. One private struct in the stats widget is the smaller commitment. When
weather needs the same thing, the helper is extracted from two real uses.

Alternative considered: ease the number too (count up). Rejected: the digits would repaint the text 9 times per
change and a number that is briefly wrong is worse than one that snaps.

### 7. Compositions

The widget picks a composition from its grid size. Slots fill positions in order.

```
 1x1, 2x2          4x2                              4x4
 ┌────────┐        ┌──────────────────────┐         ┌──────────────────────┐
 │   0    │        │    0     │     1     │         │    0     │     1     │
 └────────┘        │  gauge   │   gauge   │         │  gauge   │   gauge   │
                   ├──────┬───┴───┬───────┤         ├──────────┼───────────┤
                   │  2   │   3   │   4   │         │    2     │     3     │
                   └──────┴───────┴───────┘         │  gauge   │   gauge   │
                                                    ├──────┬───┴───┬───────┤
                                                    │  4   │   5   │   6   │
                                                    └──────┴───────┴───────┘
```

- 4x4 is 4x2 with a second gauge row. The gauge rows share the height that the text row leaves; the text row is the
  same height in both sizes, so the small stats are the same size on a 4x2 and a 4x4.
- Text positions are plain readouts with labels in three equal columns. `style` is ignored there.
- A position with no slot stays empty. With no slots at all, position 0 shows `system:cpu/load`, as today.
- The widget subscribes to the readings of the positions its size shows, not to every slot in the file.
- Gauges in one card are drawn in equal rectangles, so their readouts have the same text size when they declare the
  same widest value. Where they differ (a percentage next to a temperature) the widget draws all gauge readouts at the
  smallest fitted size, using `Readout.Measure`, so one row does not mix sizes.

Trade-off: resizing a 4x2 to a 4x4 turns slots 2 and 3 from text stats into gauges. The alternative (gauges first by
a per-slot marker and a flowing layout) makes the config say what the size already says and lets a user build layouts
no composition was designed for. The editor (item 11) will show the positions.

### 8. What the widget compares to decide on a repaint

Today `NeedsRender` compares the formatted text and the label. It now compares, per shown position: the formatted
text, the label, the target fraction and the level. All four derive from the rounded value (decision 3), so the rule
"no visible change, no repaint" holds. While easing, the frame clock repaints the widget and `NeedsRender` is not
involved.

A 4x4 is one surface: one changed digit repaints about 1100 x 1100 pixels, and an ease repaints it 9 times. Task 6.3
measures this on the panel. If it is too much, the escape is drawing only the changed position's rectangle (clip and
redraw), which needs no SDK change; it is not built until the measurement asks for it.

### 9. SDK compatibility

Everything is additive: a new component, two enums, two optional members on `ReadingDescriptor` (a sealed record with
init-only members), one property on `Theme`. Plugins built against 0.3.0.0 keep loading, so `AssemblyVersion` stays
0.3.0.0 and the package `Version` moves to 0.5.0. `SdkContractTests` keeps guarding the frozen version.

## Risks / Trade-offs

- Thresholds are designed with no real temperature to look at → slot overrides make them testable now; the catalog
  levels and the owner's look at a real temperature are tasks in `gpu-readings`.
- Repainting a 4x4 surface 9 times per change may cost more than the per-core cards did → measured in task 6.3, with
  the clip-and-redraw escape of decision 8.
- A blue-to-yellow colour snap while the fill still eases may look abrupt → owner's check in task 6.4; easing the
  colour is a contained change in the widget.
- The vertical bar's contrast rule picks between two theme colours and may pick a poor one on an unusual theme
  (translucent `Background` in `glass`) → the component treats the background as opaque for the comparison; checked
  under all three built-in themes in task 6.4.
- Fixed compositions will not fit every wish (five gauges, no text row) → accepted; more compositions are more sizes
  or a later change, not a layout language.

## Open Questions

- The share of the height the text row takes at 4x2 and 4x4, and the ring's sweep (270 degrees is the starting point):
  settled on the panel in task 6.4.
- Whether `gauge.style` should later be joined by a ring sweep or a track opacity in the theme: only if a theme
  author asks.
