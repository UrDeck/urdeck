## MODIFIED Requirements

### Requirement: Gauge Styles
The gauge MUST offer the styles `plain`, `ring`, `bar`, `verticalBar`, `meter`, `tach`, `marker` and `thickbar`. Every
style MUST accept the same inputs, so that a widget needs no code per style. A style a theme or a widget asks for is
drawn as that style; nothing falls back to another.

- `plain`: the readout alone, centred in the rectangle, drawn exactly as the readout component draws a formatted
  reading
- `ring`: an arc open at the bottom, filled clockwise from its lower left end, with the readout and its label centred
  inside it
- `bar`: a horizontal track at the top of the rectangle, filled from the left, with the readout below it
- `verticalBar`: the rectangle itself is the track, filled from the bottom, with the readout inside it at the bottom
- `meter`: the readout at the top, left-aligned; below it a thin horizontal track, filled from the left; below the track
  the theme's scale labels, evenly spaced, and tick marks if the theme asks for them
- `tach`: an arc like the ring's, with a ring of tick marks outside the track, a redline zone and, if the theme asks
  for one, a needle; the readout and its label inside it
- `marker`: a hairline arc (or full circle) with a marker at the value instead of a filled arc, optional tick marks and
  centre cross; the readout inside it
- `thickbar`: a heavy horizontal track, three times as thick as a bar and outlined if the theme asks, at the bottom of
  the rectangle, with the readout above it, left-aligned

With the theme's `gauge.layout` set to `stacked` a `bar` is drawn like a `meter` without a scale: the readout above,
left-aligned, the track below.

#### Scenario: Plain equals the readout
- **WHEN** a gauge is drawn with the style `plain`
- **THEN** the result is the same as drawing the formatted reading with the readout component in that rectangle

#### Scenario: Ring at a quarter
- **WHEN** a ring gauge is drawn with a fraction of 0.25
- **THEN** the first quarter of the arc, starting at its lower left end, is filled

#### Scenario: Vertical bar at full
- **WHEN** a vertical bar gauge is drawn with a fraction of 1
- **THEN** the whole rectangle is filled and the readout is drawn over the fill

#### Scenario: Meter with a scale
- **WHEN** a meter is drawn under a theme whose `gauge.scale.labels` are `0`, `50` and `100`
- **THEN** the labels sit under the track at its left end, middle and right end, in the label role, and the readout is above the track

#### Scenario: Thick bar
- **WHEN** a thick bar is drawn at a fraction of 0.5 under a theme with an outline
- **THEN** the left half of the track is filled and an outline of the theme's width surrounds the whole track

#### Scenario: Marker at the value
- **WHEN** a marker gauge is drawn at a fraction of 0.5 with a sweep of 270 degrees
- **THEN** the marker sits on the arc 135 degrees from its start and the arc is not filled

#### Scenario: Tach style is drawn as asked
- **WHEN** a widget draws a gauge in the style `tach`
- **THEN** a ring of tick marks surrounds the arc and the style is not replaced by `ring`

#### Scenario: Stacked bar
- **WHEN** a bar is drawn under a theme whose `gauge.layout` is `stacked`
- **THEN** the readout is above the track and left-aligned, and the track is at the bottom of the rectangle

#### Scenario: A theme with no gauge values
- **WHEN** a ring, a bar or a vertical bar is drawn under a theme that sets none of the format 2 gauge values
- **THEN** the result is exactly what it was before the new styles existed

### Requirement: Gauge Colours
The gauge MUST take its colours from the theme and from the level and the state of the reading.

- At the normal level the fill is the theme's accent colour; at the warning level the theme's warning colour; at the
  critical level the theme's critical colour
- When the theme's `gauge.colorBy` is `ramp`, the fill is instead the colour at the fraction along the theme's colour
  ramp, and the level does not change it
- When the theme gives a fill gradient, the gradient is laid over the whole track and the fill shows the part of it up to
  the fraction
- The track is the fill's colour at the opacity of the theme's dimmed accent, or the theme's track colour when it has one
- For a formatted reading that is not current, the fill and the track use the theme's muted text colour and the gradient
  and the ramp are not used
- The value, the unit and the label keep the colours the readout gives them, with two exceptions: where the readout
  lies over the fill of a vertical bar, the text is drawn in whichever of the theme's text colour and background
  colour contrasts more with the fill; and when the theme's `gauge.valueTint` is true the value is drawn in the colour
  of the fill (the contrast rule wins over it)

#### Scenario: Normal level
- **WHEN** a gauge is drawn at the normal level for a current reading
- **THEN** the fill is the theme's accent colour and the value is the theme's text colour

#### Scenario: Critical level
- **WHEN** a gauge is drawn at the critical level
- **THEN** the fill is the theme's critical colour, the track is a dimmed critical colour, and the value keeps the
  theme's text colour

#### Scenario: Stale reading
- **WHEN** a gauge is drawn for a formatted reading that is not current
- **THEN** the fill, the value and the unit are drawn in the theme's muted text colour

#### Scenario: Text over a light fill
- **WHEN** a vertical bar's readout lies over a fill in a light warning colour under a theme with white text
- **THEN** the value and the label are drawn in the theme's background colour

#### Scenario: Colour by ramp
- **WHEN** a theme's ramp is cyan, violet, magenta and `colorBy` is `ramp`, and a gauge is drawn at a fraction of 0.5
- **THEN** the fill is violet whatever the level

#### Scenario: Gradient over the whole track
- **WHEN** a gauge with a two-colour gradient is drawn at a fraction of 0.5
- **THEN** the end of the fill is the colour halfway along the gradient, not the gradient's last colour

#### Scenario: Value takes the fill's colour
- **WHEN** the theme sets `gauge.valueTint` to true and a gauge is drawn at the warning level
- **THEN** the value is drawn in the warning colour

## ADDED Requirements

### Requirement: Gauge Geometry Values
The gauge MUST honour the theme's geometry values for the styles they apply to and ignore them, without a warning, for
the others.

- `sweep` (`start`, `span`): where an arc (`ring`, `tach`, `marker`) starts, in degrees clockwise from the three o'clock
  position, and how far it runs. The defaults are the ring's own, 135 and 270, so a theme without them draws what it
  did. A span of 360 has no gap in the track
- `segments` (`length`, `gap`): the fill of a bar, ring or tach is cut into blocks of that length with that gap between
  them, a length in grid cells measured along the bar or along the arc at its middle; the last block is drawn partially
  when the fraction ends inside it
- `track` (`color`, `widthRatio`): the track's colour, and its width relative to the stroke thickness
- `outline` (`width`, `color`): an outline round the track of a `thickbar`
- `ticks` (`count`, `majorEvery`, `length`, `thickness`, `color`, `labels`): `count` marks evenly spaced over the sweep
  (arcs, radial and outside the track) or along the bar (meter, under it); every `majorEvery` th mark is longer; with
  `labels` the numbers from the range's start to its end are printed at the major marks, in the label role
- `scale` (`labels`, `size`): the labels printed under a `meter` at even spacing
- `redline` (`from`, `color`, `glow`, `lightsAbove`): the part of the track from the fraction `from` to the end is drawn
  in `color` at low opacity; with `lightsAbove` it is drawn at full strength, with its glow, only while the fraction
  drawn is at or above `from`
- `marker` (`shape`, `size`, `color`, `halo`): a `diamond` or a `needle` at the value; a needle runs from the centre
  of an arc to the track; `halo` draws a ring in the theme's background colour round the marker
- `crosshair`: a small cross at the centre of an arc
- `glow` (`blur`, `color`, `alpha`): a blurred copy of the fill is drawn under the fill, in `color` or the fill's colour

#### Scenario: Full ring from the top
- **WHEN** a ring is drawn under a theme with a sweep that starts at -90 and spans 360, at a fraction of 0.25
- **THEN** the fill covers the arc from 12 o'clock to 3 o'clock and the track has no gap

#### Scenario: Segments
- **WHEN** a bar with segments is drawn at a fraction that ends in the middle of the fourth block
- **THEN** three blocks are filled and the fourth is filled to half its length

#### Scenario: Ticks with numbers
- **WHEN** a marker gauge is drawn under a theme with 19 ticks, a major mark every third and `labels` true
- **THEN** 19 marks are drawn over the sweep, the 1st, 4th, 7th and so on are longer, and the numbers are printed at those

#### Scenario: Redline lights only above its start
- **WHEN** a tach has a redline from 0.8 with `lightsAbove`, and is drawn at fractions of 0.6 and 0.9
- **THEN** the zone is drawn dim and without glow at 0.6, and at full strength with its glow at 0.9

#### Scenario: No fraction
- **WHEN** a gauge has no fraction
- **THEN** no marker, needle or redline highlight is drawn and the track is drawn alone

#### Scenario: A value that does not apply
- **WHEN** a theme sets `ticks` and a `verticalBar` is drawn
- **THEN** no ticks are drawn and no warning is logged

### Requirement: Gauge Label Position
The gauge MUST place the readout's label as the theme's `gauge.labelPosition` says: `auto` is the placement the style
has, `inside` puts the label with the value, and `below` and `above` put it outside the shape, so that a ring holds only
the value.

#### Scenario: Label below the ring
- **WHEN** a ring is drawn under a theme whose `labelPosition` is `below`
- **THEN** the label is drawn under the ring and the ring's centre holds only the value
