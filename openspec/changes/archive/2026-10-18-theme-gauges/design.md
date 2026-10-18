## Context

`Gauge.Draw` (`sdk/UrDeck.Sdk/Components/Gauge.cs`, 207 lines) draws `Ring`, `Bar` and `VerticalBar` with constants for
the ring's sweep (135, 270), the stroke from `Theme.StrokeRatio` and the fill from the level (`accent`, `warning`,
`critical`), and places the readout itself with `Readout`. It keeps no state. `ThemeGaugeDefinition` holds every format 2
gauge value (merged, sanitised) and `ThemeResolver` sets `Theme.GaugeStyle` from the theme, mapping `meter`, `tach`,
`marker` and `thickbar` to `ring` because the enum has only four members. The Stats widget resolves a slot's style by
name (`ring`, `bar`, `verticalbar`, `gauge`, `plain`), measures one readout size for all gauges of a card and eases the
fraction itself. `Theme` has the colours of the v1 format and, from `theme-typography`, a `TextStyle`.

The look the drafts want is on the design board (`themes-1-...png`, `themes-2-...png` in the original package, outside
the repository): Ember's tachometer with a tick ring and a lit redline wedge, Blueprint's protractor dial with a needle,
Obsidian's hairline ring with a jewel, Pop's heavy outlined bar, Paper's thin meter with a printed scale, Carbon's
segmented bar, Rigline's gradient ring whose hue follows the load, Prism's full ring with the label below.

## Goals / Non-Goals

**Goals:**
- Every gauge value a draft uses is drawn, for the styles it applies to, and a style is never replaced by another.
- A ring, bar or vertical bar under a v1 theme is byte-identical.
- Widgets that use the component need no code; widgets built against the earlier SDK keep running.

**Non-Goals:**
- Sparkline and status chip (`theme-widget-parts`), the animated fill ease (stays in the widget), `marker.shape` `dot` and
  `line`, `sparkline.fillAlpha`.
- Card chrome, and the layout of the Stats card (where the label sits on a card, the clock's header).
- Per-widget gauge overrides in `card.compact`.

## Decisions

### 1. One entry point, one file per style, v1 path untouched

`Gauge.Draw` keeps its signature and return value. The body becomes a dispatch to `RingGauge`, `BarGauge`,
`VerticalBarGauge`, `MeterGauge`, `TachGauge`, `MarkerGauge` and `ThickBarGauge` (internal static classes next to it) and
shared helpers for the track, the fill colour, ticks and the glow. The existing three styles keep their current drawing
code as the path taken when the theme's gauge values are all absent, so their output cannot change; with a value
present they use the same code with the value applied (a sweep, a segmented fill, a gradient). A test keeps a copy of
the old code (as `V1Text` does for text) and compares pixels on the three v1 themes at two sizes.

*Alternative: rewrite all styles on one generic path.* Rejected: it risks a pixel of v1 for no gain.

### 2. The theme's values reach the component as data on `Theme`

`Theme.Gauge` is a `GaugeTheme` (sealed, read-only), resolved by `ThemeResolver` for the surface: lengths in pixels, the
rest as given, each group `null` when the theme did not set it. `Theme` also gets `Accent2`, `Ink` and `Ramp`
(`IReadOnlyList<SKColor>`), defaulting as the schema says (accent; the more contrasting of text and background; good,
warning, critical). Everything is additive. `GaugeStyle` gets `Meter = 4, Tach = 5, Marker = 6, ThickBar = 7`; the
assembly version stays and the package version moves up one minor, as `theme-typography` did. `ThemeGaugeDefinition.Parse`
accepts the four names (`thickbar` and `thickBar`) and `Theme.GaugeStyle` carries them; the resolver's downgrade to
`ring` is removed.

### 3. The readout sits above a bar in the mocks: one new key, `gauge.layout`

The board draws Carbon's, Paper's and Pop's bars with the value large and left-aligned **above** the bar, which sits at the
bottom of the card. The v1 `bar` puts the track at the top and the readout below, centred. The typography change found
the same thing for the clock. Without a way to ask for the board's layout the three themes cannot match their mocks.
So: `meter` and `thickbar` are drawn stacked (readout above, left-aligned, track below) by definition, and `bar` is
drawn stacked when `gauge.layout` is `stacked` (Carbon sets it); `auto`, the default, is the v1 layout. The key is a
format 2 value: validated as an enumeration, in the schema, ignored by older hosts.

*Alternative: make `bar` itself stacked under a format 2 theme.* Rejected: a format 2 theme that only wants a gradient
must not change its layout.
*Alternative: a general `readoutAlign` and `readoutPosition` for every style.* Rejected for now: three combinations are
used, and arcs hold the readout inside by nature.

### 4. Geometry in one place

Arcs (`ring`, `tach`, `marker`) share a helper that maps a fraction to an angle from `sweep` (start and span, 135 and
270 by default) and builds the oval; a span of 360 closes the track. The tick ring of a `tach` and of a `marker` is
drawn outside the track with `ticks.length` of the diameter; a needle runs from the centre to the track at the angle of
the fraction; a `marker`'s diamond sits on the track. Segments on an arc are laid along the arc at the track's middle
radius (as the schema says) and the last block is clipped, so a partial block is a partial block. Meter ticks sit under
the bar, its labels under them. Layout numbers (a label's offset, the thick bar's three times the thickness, a
marker's halo width) are constants of the component, named, and checked on the sample page by eye.

### 5. Colour: ramp, gradient and tint

`colorBy: ramp` replaces the level steps with the ramp's colour at the fraction (piecewise linear in sRGB between its
colours, evenly spaced). A `gradient` (explicit stops, or `ramp`) is laid over the **whole** track with a sweep shader for
arcs (rotated to the sweep's start) and a linear shader for bars, and the fill shows the part up to the fraction, so the
hue at the end of the fill is the hue at that load. A stale reading uses the muted colour and neither. `valueTint`
colours the value like the fill, but the vertical bar's contrast rule still wins. The track is the fill colour at the
dimmed accent's alpha unless the theme sets one.

### 6. Caches, because a shader and a blur are not free

A gradient shader depends only on the size and the colours, and a blurred glow only on the shape, size and colour at a
fraction. The component keeps a small cache on the `Theme` instance, disposed with it (the typography change put the
text glow cache there for the same reason: the SDK must not hold static caches that outlive a plugin's load context). A
gradient shader is cached per (rect size, style); the glow is drawn as a blurred stroke and re-blurred only when the
fraction changes, which Stats already limits to its 300 ms ease. Apply measures a repaint of each style against today's
ring (budget: at most 2x for a plain gauge, 3x with a glow), as the typography change did for the glow.

### 7. What the Stats widget changes

Only its style names: `meter`, `tach`, `marker`, `thickbar` become `StyleRequest` values and map to the enum. The
fraction ease, the one-readout-size rule across the gauges of a card (`ReadoutScales`) and the fallback to a plain
readout for a reading with no fraction stay. `ReadoutScales` calls `Gauge.Measure`, which must give the readout's
rectangle for each new style.

## Risks / Trade-offs

- [The mocks are hand-drawn and the component is code] → the sample page is rendered for each drafted gauge (fixtures) and
  compared with the board by eye before the task is ticked; the numbers (tick length, marker size, halo) are the drafts',
  so a tuned look is a theme edit, not a component edit.
- [A new key, `gauge.layout`, widens the schema] → one enumeration, and a theme that omits it is unchanged; the
  alternative was to leave three themes unable to match.
- [Blueprint's draft sweeps 180 degrees (`start` 180, `span` 180) while its mock draws a full dial] → not decided here; the
  Blueprint theme change checks it on the sample page and edits the draft if needed.
- [`marker` and `tach` readouts inside a small ring get crowded] → the ring's readout rectangle shrinks only when ticks
  are on, by the tick length; checked at 1x1 and 2x2.
- [Piecewise-linear sRGB between ramp colours can look muddy between hues] → it is what a CSS gradient does and what the
  mock shows; changing the interpolation is a one-function change.
- [Per-style files make the SDK larger] → they are internal, and the entry point is the one the specs describe.

## Migration Plan

None for users. Existing configurations, themes and widgets draw as before; a theme or a slot that named a new style
now receives it instead of a ring. This is the only visible change for a user theme that already set `meter`, `tach`,
`marker` or `thickbar`, and it is the point.

## Findings during apply

- **One file for the arcs.** `ring`, `tach` and `marker` share `ArcGauge` (geometry, track, ticks, redline, marker); `meter` is a stacked
  `BarGauge` with a scale. The v1 drawing stays as `LegacyGauge`, used when the theme sets no gauge value (the invariance test compares it
  with a copy kept in the tests, three themes, two sizes, three styles).
- **A tach has ticks and a redline of its own** (28 marks, every fourth longer, a redline from 0.85) when the theme sets none: the spec
  says a widget that asks for `tach` gets a ring of ticks. The track is cut into blocks as well as the fill, so a segmented bar shows
  its unlit blocks (Carbon).
- **Room for an inside label.** With ticks the circle is small (Ember's 0.07 ticks leave a 49 px inner radius at a 1x1 card), and the
  value shrank to one pixel beside a 32 px label. An arc that is open at the bottom now gives an inside label extra readout height
  below its middle.
- **A sweep gradient wraps.** The round cap before the start of the arc sampled the end of the sweep and drew a magenta dot on Rigline;
  the gap now holds the last colour to its middle and then the first.
- **Blueprint's 180 degree sweep** leaves the lower half of the cell empty and puts the value on the needle's hub. Its theme change should
  sweep 270 degrees (or the component should centre a half dial lower); not decided here.
- **Colours in the drafts are `#AARRGGBB`:** Ember's tick colour `#ffffff1c` is opaque yellow, and Blueprint's track `#559fd0ff` is a
  yellow-green; the theme changes should write them alpha first.
- **Pop's thick bar** shows a hairline seam between the outline and the fill (the clip's anti-aliasing); Prism's white values need its
  translucent cards. Neither is the component's.
- **Cost.** See `docs/perf/theme-look.md`, "Gauge styles": every style is inside 2x without a glow; the glow themes are 3.5x to 5.1x
  (0.18 to 0.25 ms a repaint) and need acceptance.

## Open Questions

- Whether `ticks.labels` should use the reading's formatter (units) or plain numbers. The drafts print plain numbers
  (Blueprint) or none (Ember); plain numbers are used, and a formatted scale can be added later without changing the
  requirement.
