# Components Specification

## Purpose

Defines the shared drawing components widgets compose their content from, and the sizing rules that make text look
consistent across every widget on a page under any theme.
## Requirements
### Requirement: Components Are Available Through The SDK
Shared components MUST be usable by any widget that references only the widget SDK. A component draws into a rectangle
the widget chooses, using the theme from the render context and the values the widget passes. A component MUST NOT
hold state between paints that a widget has to manage, with one exception: a component whose description says that the
widget owns it (the Lottie animation) is created by the widget, used on the UI thread and released by the widget.

#### Scenario: Community widget uses a component
- **WHEN** a widget that references only the SDK draws a readout
- **THEN** the readout is drawn with the active theme's fonts, colours and sizes

#### Scenario: Widget owns a stateful component
- **WHEN** a widget creates a Lottie animation and is later disposed
- **THEN** the widget releases the animation, and the component needs no other call to free what it holds

### Requirement: Readout
The SDK MUST provide a readout component that draws a value, an optional unit and an optional label.

- The value is drawn in the theme's value role and text colour, as large as fits the rectangle
- The unit is drawn in the theme's unit role at the theme's unit ratio of the value's size, either raised to the top
  of the value or on the value's baseline, as the widget chooses
- The label is drawn below the value in the theme's label role, label size and muted text colour
- The widget MAY override the value colour
- The widget chooses horizontal and vertical alignment of the readout inside the rectangle

#### Scenario: Value with a raised unit and a label
- **WHEN** a widget draws a readout with value `41`, unit `°` raised, and label `CPU`
- **THEN** `41` is drawn large, `°` is drawn smaller and aligned to the top of the value, and `CPU` is drawn below in
  the muted colour at the theme's label size

#### Scenario: Value with a baseline unit and no label
- **WHEN** a widget draws a readout with value `03:59`, unit `PM` on the baseline, and no label
- **THEN** `PM` shares the value's baseline and no space is reserved for a label

### Requirement: Readout Size Is Stable
A readout's text size MUST NOT change as its value changes. The widget MAY state the widest value it expects, and the
readout is sized so that this widest value and the unit fit the rectangle. Digits MUST occupy equal widths so that a
changing number does not shift the characters around it.

#### Scenario: Short and long values side by side
- **WHEN** two readouts in equal rectangles both declare `100` as their widest value and show `8` and `100`
- **THEN** both values are drawn at the same text size

#### Scenario: Digit changes
- **WHEN** a readout showing `11:11` changes to `10:00`
- **THEN** the text size is unchanged and the colon stays in the same position

### Requirement: Text Line
The SDK MUST provide a text line component that draws one line of text at one of three steps: label, body or title.

- Each step uses the theme's size and font weight for that step
- The widget chooses primary or muted colour and the alignment inside the rectangle
- Text that is wider than the rectangle shrinks to fit; text never grows beyond the step's size

#### Scenario: Text fits
- **WHEN** a body text line fits its rectangle
- **THEN** it is drawn at exactly the theme's body size

#### Scenario: Text is too wide
- **WHEN** a title text line is wider than its rectangle at the theme's title size
- **THEN** it is drawn smaller so that it fits the rectangle's width

### Requirement: Small Text Is The Same Size Across The Page
A given step (label, body, title) MUST have the same pixel size in every widget on a page, whatever the widget's grid
size, unless a particular text had to shrink to fit.

#### Scenario: Labels in different card sizes
- **WHEN** a 1x1 widget and a 4x4 widget each draw a label that fits
- **THEN** both labels are drawn at the same pixel size

### Requirement: Readout Draws A Reading
The readout MUST accept a formatted reading (see data-providers, "Reading Formatting") in place of a value text, so
that a widget does not decide how a reading's state looks.

- The value text, the unit, the unit's placement and the widest value come from the formatted reading
- A formatted reading that is marked as not current is drawn with its value and unit in the theme's muted text
  colour; one that is current is drawn in the theme's text colour
- The widget still chooses the label, the alignment and the rectangle
- The text size is the same whether the reading is current, not current or shown as a dash

#### Scenario: Current reading
- **WHEN** a readout draws a formatted percentage reading of `37` that is current
- **THEN** `37` is drawn in the theme's text colour with a raised `%`

#### Scenario: Stale reading
- **WHEN** a readout draws a formatted reading that is not current
- **THEN** the value and the unit are drawn in the theme's muted text colour

#### Scenario: Dash keeps the layout
- **WHEN** a readout that showed `37` with a raised `%` draws the same reading as unavailable
- **THEN** a dash is drawn at the same text size and the unit and the label stay where they were

### Requirement: Gauge
The SDK MUST provide a gauge component that draws a formatted reading (see data-providers, "Reading Formatting")
together with a shape that shows how far the value is along its range.

- The widget passes the rectangle, the style, the formatted reading, an optional label, the fraction to draw (0 to 1,
  or none) and the level (normal, warning or critical)
- The component draws the readout itself and decides where it goes for each style; the widget does not place it
- The shape consists of a track (the whole range) and a fill (the fraction)
- With no fraction and a style other than plain, the track is drawn without a fill
- The component holds no state between paints: it draws the fraction it is given, and any motion between two values
  is the widget's
- Line thickness and line ends come from the theme's stroke values; the gauge names no colour, font or size of its own

#### Scenario: Community widget draws a gauge
- **WHEN** a widget that references only the SDK draws a gauge with a fraction of 0.5
- **THEN** half of the track is filled and the readout is drawn with the active theme's fonts, colours and sizes

#### Scenario: No fraction
- **WHEN** a ring gauge is drawn for a reading that has no value
- **THEN** the track is drawn empty and the readout shows the dash

#### Scenario: Readout size is stable inside a gauge
- **WHEN** a gauge's value goes from `8` to `100` and the reading's widest value is `100`
- **THEN** the text size inside the gauge is unchanged

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

### Requirement: Gauge Scale
The SDK MUST provide one rule that turns a reading into the fraction and the level a gauge shows, so that all widgets
scale alike.

- The range is the minimum and maximum the widget passes, else those of the reading's description, else 0 to 100 for
  a percentage
- There is no fraction when the range is incomplete, when the maximum is not above the minimum, when the value is not
  a number, or when the reading is unavailable or pending with no last value
- The fraction is the value's position in the range, limited to 0 and 1
- The level is critical when the value is at or above the critical value, else warning when it is at or above the
  warning value, else normal. The values are those the widget passes, else those of the reading's description; a
  missing value means that level is not reached
- The value MUST be rounded to the number of decimals the formatter shows before the fraction and the level are
  computed
- The range and the level values are in the reading's canonical unit
- A stale reading, and a pending reading with a last value, keep the fraction and level of that value

#### Scenario: Percentage without a declared range
- **WHEN** a percentage reading of `37` whose description declares no range is scaled
- **THEN** the fraction is 0.37

#### Scenario: Value outside the range
- **WHEN** a reading of `120` with a range of 0 to 100 is scaled
- **THEN** the fraction is 1

#### Scenario: No range
- **WHEN** a plain number whose description declares no maximum is scaled and the widget passes none
- **THEN** there is no fraction

#### Scenario: Rounding comes first
- **WHEN** a reading shown without decimals goes from 37.2 to 37.4
- **THEN** the fraction and the level are the same for both values

#### Scenario: Levels
- **WHEN** a reading with a warning value of 80 and a critical value of 90 has the values 79, 80 and 90
- **THEN** the levels are normal, warning and critical

#### Scenario: Widget overrides the description
- **WHEN** the description declares a warning value of 80 and the widget passes 70
- **THEN** a value of 75 is at the warning level

### Requirement: Lottie Animation
The SDK MUST provide a component that plays a Lottie animation, so that any widget that references only the SDK can
show one. It MUST NOT expose the types of the library it is built on.

- The widget creates the component from a stream that holds the animation (Lottie JSON). When the stream is not a
  readable animation, creating it MUST fail without throwing, so a widget can fall back to drawing nothing
- The component reports the animation's duration
- Drawing takes a time in seconds from the start of the animation and a rectangle. The frame at that time is drawn
  into the rectangle, scaled to fit with its aspect ratio kept and centred; a time before the start or after the end
  is clamped to the first or last frame, and looping is the widget's choice
- The animation keeps its own colours; the theme does not recolour it
- The component is released by disposing it; disposing twice is allowed, and drawing a disposed component is a
  programming error that is reported, not ignored
- A drawn frame MUST NOT depend on earlier draws, so any time can be drawn in any order (a seek, a snapshot)

#### Scenario: Widget plays an animation
- **WHEN** a widget creates the component from a valid animation and draws it at times 0, 0.5 and 1.0 seconds
- **THEN** each call draws that moment of the animation into the rectangle

#### Scenario: Not an animation
- **WHEN** a widget creates the component from a stream that holds text that is not Lottie JSON
- **THEN** creating it reports failure and does not throw

#### Scenario: Fits the rectangle
- **WHEN** the animation is square and the rectangle is twice as wide as it is high
- **THEN** the animation is drawn as large as the height allows, centred, with nothing outside the rectangle

#### Scenario: Time out of range
- **WHEN** the widget draws at a time past the animation's duration
- **THEN** the last frame is drawn

#### Scenario: Any order
- **WHEN** a widget draws at 2.0 seconds and then at 0.0 seconds
- **THEN** the second draw is identical to drawing at 0.0 seconds first

#### Scenario: Release
- **WHEN** a widget disposes the component twice
- **THEN** neither call fails

### Requirement: Attribution Line
The SDK MUST provide a component that draws the credit of the data a widget shows, so that every widget credits
sources the same way.

- It takes the attributions of the readings the widget shows (see data-providers, "Reading Catalog") and a width, and
  draws each distinct attribution text once, as a text line at the label step (see "Text Line") in the muted text colour
- It reports the height it needs for a width, so a widget can reserve the space before laying out its content; with no
  attribution it needs no height and draws nothing
- It uses the attribution's shorter text when the full text does not fit the width on one line at the label step's
  size; when the shorter text does not fit either, it shrinks to fit like any text line
- The component draws text only; it opens no link

#### Scenario: One credit
- **WHEN** a widget passes two descriptions with the same attribution text
- **THEN** the text is drawn once

#### Scenario: Different credits
- **WHEN** a widget passes two descriptions with different attribution texts
- **THEN** both are drawn, one per line, and the reported height covers both lines

#### Scenario: Narrow width
- **WHEN** the full text does not fit the width and the shorter text does
- **THEN** the shorter text is drawn

#### Scenario: No attribution
- **WHEN** a widget passes descriptions that carry no attribution
- **THEN** the reported height is zero and nothing is drawn

#### Scenario: Theme decides the look
- **WHEN** the active theme changes
- **THEN** the credit uses the new theme's label size and muted colour

### Requirement: Image Tile
The SDK MUST provide an image tile component that draws an image, an optional label and, when there is no image, a
placeholder, so that every widget that shows an icon or a picture does it alike.

- The widget passes the rectangle, the image or none, an optional label text and an optional placeholder text
- The image is scaled to fit the rectangle with its aspect ratio kept and is centred; it is drawn with smooth
  sampling and keeps its own colours and transparency (the theme does not recolour it)
- An image is never enlarged to more than twice its own pixel size; a smaller image is drawn at twice its size,
  centred
- With a label, the label is drawn as a text line at the label step in the muted text colour, centred below the
  image, and the image takes the space that remains above it; without one no space is reserved
- With no image, a placeholder is drawn where the image would be: a rounded square in the theme's dimmed accent
  colour, with the first letter or digit of the placeholder text, upper-cased, in the theme's value role and text
  colour; with no placeholder text the square is drawn empty
- The component holds no state between paints and does not own the image
- The component names no colour, font or size of its own, and draws no card and no pressed state

#### Scenario: Square icon in a square rectangle
- **WHEN** a widget draws a 256 pixel icon into a 200 pixel square
- **THEN** the icon fills the square, drawn smoothly, with its transparent areas left untouched

#### Scenario: Wide image
- **WHEN** a widget draws an image twice as wide as it is high into a square rectangle
- **THEN** the image spans the width, is centred vertically, and nothing is drawn outside the rectangle

#### Scenario: Small image
- **WHEN** a widget draws a 48 pixel image into a 240 pixel square
- **THEN** the image is drawn 96 pixels wide in the centre

#### Scenario: With a label
- **WHEN** a widget draws an image with the label `Steam`
- **THEN** `Steam` is drawn below the image at the theme's label size in the muted colour and the two do not overlap

#### Scenario: Placeholder
- **WHEN** a widget draws the tile with no image and the placeholder text `homeassistant`
- **THEN** a rounded square in the dimmed accent colour with the letter `H` is drawn

#### Scenario: Theme decides the look
- **WHEN** the active theme changes
- **THEN** the placeholder and the label use the new theme's colours, font and label size

### Requirement: Text Case
Readout labels and text lines MUST be drawn in the letter case the theme asks for. `typography.case.label` applies to
a readout's label and to a text line at the label step; `typography.case.title` applies to a text line at the title
step. A text line at the body step and a readout's value and unit are never recased. The change is made with invariant
casing, so it does not depend on the machine's language, and it is made before the text is measured, so shrinking to fit
and alignment act on the text as drawn. With `none`, or no value, the text is drawn as given.

#### Scenario: Upper-case labels
- **WHEN** the theme sets `typography.case.label` to `upper` and a widget draws a readout with the label `Cpu`
- **THEN** the label is drawn as `CPU`

#### Scenario: Body is never recased
- **WHEN** the theme sets `typography.case.label` and `typography.case.title` to `upper` and a widget draws a body text line `Sunny`
- **THEN** the line is drawn as `Sunny`

#### Scenario: Recased text still fits
- **WHEN** a title line is wider than its rectangle once it is upper-cased
- **THEN** it shrinks to fit the rectangle's width

### Requirement: Letter Spacing
Readout values and labels and text lines at the label and title steps MUST be drawn with the letter spacing the theme
asks for in `typography.tracking` (`value`, `label`, `title`), in em of the text's size, added after each character
except the last. A body line and a readout's unit are not spaced. A readout's digits keep equal widths with spacing
added; the readout's size is chosen with the spacing in the measured width, so the widest value still fits and the size
does not change as the value changes. With no spacing, or 0, the text is drawn and measured exactly as without this
requirement.

#### Scenario: Spaced label
- **WHEN** the theme sets `typography.tracking.label` to 0.2 and a widget draws the label `CPU` at 20 pixels
- **THEN** each pair of neighbouring letters is 4 pixels further apart than without the key, and the text is 8 pixels wider

#### Scenario: Digits stay equal
- **WHEN** the theme sets `typography.tracking.value` to 0.1 and a readout changes from `11:11` to `10:00`
- **THEN** the text size is unchanged and the colon stays in the same position

#### Scenario: Spacing is part of the fit
- **WHEN** the theme adds spacing to the value and the widest value no longer fits the rectangle at the old size
- **THEN** the value is drawn smaller so the widest value fits

#### Scenario: No spacing
- **WHEN** the theme sets no spacing
- **THEN** the drawn pixels are identical to those drawn before this requirement existed

### Requirement: Slanted Value
A readout's value MUST be drawn sheared by the angle in `typography.slant` (0 to 20 degrees), leaning to the right,
about the value's baseline. The unit and the label are not slanted. The readout's rectangle, alignment and size are
those of the unslanted value. With no slant, or 0, the value is drawn as without this requirement.

#### Scenario: Slanted digits
- **WHEN** the theme sets `typography.slant` to 10 and a readout draws `41`
- **THEN** the top of each digit is further right than its foot, the label below is upright, and the readout occupies the same rectangle as without the slant

### Requirement: Value Scale
A readout MUST multiply its size, as the widget asks for it, by `typography.valueScale` (0.5 to 2). The size is never
larger than the size at which the value, its unit and its label fill the rectangle, so a value above 1 enlarges only a
readout that a widget draws smaller than the rectangle allows, and a value below 1 shrinks every readout.

#### Scenario: Smaller than the rectangle
- **WHEN** the theme sets `typography.valueScale` to 0.8 and a widget draws a readout at full size
- **THEN** the value is drawn at 80 % of the size at which it fills the rectangle

#### Scenario: Restoring a reduced readout
- **WHEN** a widget draws a readout at 0.6 of the fitted size and the theme sets `typography.valueScale` to 1.25
- **THEN** the value is drawn at 0.75 of the fitted size

#### Scenario: Never beyond the rectangle
- **WHEN** a widget draws a readout at full size and the theme sets `typography.valueScale` to 1.35
- **THEN** the value is drawn at the size at which it fills the rectangle

### Requirement: Value Fill
A readout's value MUST be filled with the gradient in `typography.valueFill` (two to six colours, evenly spaced, at the
given angle in degrees across the value's bounding box) instead of the text colour. A value that is not current is
drawn in the muted colour without the gradient, and a colour the widget gives the value explicitly replaces the
gradient. The unit and the label are not filled. The fill does not move.

#### Scenario: Gradient digits
- **WHEN** the theme sets `typography.valueFill` to a gradient from magenta to cyan at 0 degrees and a readout draws `41`
- **THEN** the left of the digits is magenta, the right is cyan, and the unit and the label use their usual colours

#### Scenario: Stale reading
- **WHEN** a readout draws a formatted reading that is not current while a value fill is set
- **THEN** the value is drawn in the muted text colour with no gradient

#### Scenario: Widget colour wins
- **WHEN** a widget gives its readout an explicit value colour while a value fill is set
- **THEN** the value is drawn in that colour

### Requirement: Value Glow
A readout's value MUST be drawn over a blurred copy of itself when the theme sets `typography.valueGlow`: the copy is
blurred by `blur` (a fraction of a grid cell, resolved to pixels for the surface), in `color` (absent: the value's own
colour, or the gradient when there is one), at `alpha` strength. The glow is drawn behind the value and does not change
the readout's rectangle. A blurred copy is kept for as long as the text, size and colour do not change, so a card that
repaints without a new value does not blur again.

#### Scenario: Glowing digits
- **WHEN** the theme sets `typography.valueGlow` with a blur of 0.1 and an alpha of 0.4 and a readout draws `41`
- **THEN** a soft halo of the value's colour surrounds the digits, and the digits are drawn sharp over it

#### Scenario: Same value, no new blur
- **WHEN** a readout is drawn twice with the same value, size and colour
- **THEN** the glow is blurred once

#### Scenario: Glow does not move the layout
- **WHEN** a glow is set
- **THEN** the readout's rectangle is the same as without it

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

### Requirement: Chip
The SDK MUST provide a chip component that draws a short status text in a rectangle, as the theme's `chip` says: with
the shape `none` (the default) the text alone, in the label role and the case the theme sets; with the shape `pill` a
rounded outline and fill round the text, in the theme's fill, border and text colours. With the theme's `chip.colorBy`
set to `status` the text, and the pill's fill, take the colour of the level the widget gives (good for normal, warning,
critical), instead of the theme's. The chip draws no larger than its rectangle: the text shrinks to fit.

#### Scenario: Plain text
- **WHEN** a chip is drawn with the text `WARM` at the warning level under a theme with the shape `none` and `colorBy` `status`
- **THEN** `WARM` is drawn without an outline in the theme's warning colour

#### Scenario: Pill
- **WHEN** a chip is drawn with the text `PARTLY CLOUDY` under a theme with the shape `pill`, a yellow fill and a black border
- **THEN** a rounded pill with a yellow fill and a black outline surrounds the text, which is drawn in the theme's chip text colour

#### Scenario: No chip values
- **WHEN** a chip is drawn under a theme that sets no chip values
- **THEN** the text is drawn plain in the theme's text colour

### Requirement: Divider
The SDK MUST provide a divider component that draws the rule between two sections of a widget across a rectangle, as the
theme's `divider` says: `line` (one rule in the theme's colour and thickness), `gradient` (the colour fading to the
theme's second colour along the rule) or `dimension` (a rule with a short tick at each end and room for a centred label).
`none` draws nothing. A theme that sets no divider gets the rule the Weather card has always drawn: the muted text colour
at about a third of its opacity, at least one pixel thick.

#### Scenario: Gradient rule
- **WHEN** a divider is drawn under a theme with the style `gradient`, the colour champagne and the second colour transparent
- **THEN** the rule starts champagne at its left end and has faded to transparent at its right end

#### Scenario: Dimension rule
- **WHEN** a divider is drawn under a theme with the style `dimension`
- **THEN** the rule has a tick at each end and nothing else is drawn when no label is given

#### Scenario: No divider values
- **WHEN** a divider is drawn under a theme that sets none
- **THEN** the rule is the one the Weather card drew before this requirement existed

### Requirement: Well
The SDK MUST provide a well component that draws a recessed rounded rectangle behind a group of content, as the theme's
`well` says: its fill, corner radius (a fraction of a cell, at most half the smaller side) and border. A theme with no
well draws nothing, and the content is drawn on the card as before.

#### Scenario: Well behind a day
- **WHEN** a well is drawn under a theme with a faint white fill and a radius of 0.09
- **THEN** a rounded rectangle of that fill and radius fills the rectangle

#### Scenario: No well
- **WHEN** a well is drawn under a theme that sets none
- **THEN** nothing is drawn

### Requirement: Sparkline
The SDK MUST provide a sparkline component that draws a polyline of recent values across a rectangle, oldest at the
left, each value as a fraction of the range the widget gives, in the colour and the stroke width the widget or the theme
gives. Fewer than two values draw nothing.

#### Scenario: Rising values
- **WHEN** a sparkline is drawn for the fractions 0.1, 0.5 and 0.9
- **THEN** a line rises from the lower left to the upper right of the rectangle

#### Scenario: Too few values
- **WHEN** a sparkline is drawn for one value
- **THEN** nothing is drawn

### Requirement: Gauge Sparkline And Status
The gauge MUST draw, when the widget gives them and the theme asks for them, a sparkline of the recent fractions under
the readout (`gauge.sparkline`: its height in cells and its stroke width) in the colour of the fill, and a status chip
(`gauge.status`) with the status text and level the widget gives, on the label's row at the right with the label at
the left. A gauge without them is drawn as before. The sparkline is drawn only when the rectangle is tall enough
for it, at least 0.9 of a grid cell, and not otherwise.

#### Scenario: Status beside the label
- **WHEN** a gauge with the label `CPU` is drawn with the status text `OK` under a theme with the status enabled
- **THEN** `CPU` is at the left and `OK` at the right of the label's row, above the readout

#### Scenario: Sparkline under the value
- **WHEN** a gauge is drawn with a history under a theme with the sparkline enabled
- **THEN** a line of the history is drawn under the readout in the colour of the fill

#### Scenario: Too small for a sparkline
- **WHEN** the same gauge is drawn in a rectangle 0.6 of a grid cell tall
- **THEN** no sparkline is drawn

### Requirement: Gauge Readout Gap
A theme MAY set `gauge.readoutGap` and the gauge MUST apply it: the space between the track of a bar, meter or thick bar and its readout, in
thicknesses of the track. The default is 0.5; the value is a number from 0 to 4. A theme that does not set it draws
the gap it drew before the key existed.

#### Scenario: A wider gap
- **WHEN** a stacked bar is drawn under a theme whose `gauge.readoutGap` is 1.5, and again with the default
- **THEN** the readout ends one track thickness higher than with the default, and the track is drawn where it was

#### Scenario: A value out of range
- **WHEN** a theme sets `gauge.readoutGap` to 40
- **THEN** the value is ignored with one logged warning and the default gap applies
