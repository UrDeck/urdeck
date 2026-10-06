## ADDED Requirements

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
The gauge MUST offer the styles `plain`, `ring`, `bar` and `verticalBar`. Every style MUST accept the same inputs, so
that a widget needs no code per style.

- `plain`: the readout alone, centred in the rectangle, drawn exactly as the readout component draws a formatted
  reading
- `ring`: an arc open at the bottom, filled clockwise from its lower left end, with the readout and its label centred
  inside it
- `bar`: a horizontal track at the top of the rectangle, filled from the left, with the readout below it
- `verticalBar`: the rectangle itself is the track, filled from the bottom, with the readout inside it at the bottom

#### Scenario: Plain equals the readout
- **WHEN** a gauge is drawn with the style `plain`
- **THEN** the result is the same as drawing the formatted reading with the readout component in that rectangle

#### Scenario: Ring at a quarter
- **WHEN** a ring gauge is drawn with a fraction of 0.25
- **THEN** the first quarter of the arc, starting at its lower left end, is filled

#### Scenario: Vertical bar at full
- **WHEN** a vertical bar gauge is drawn with a fraction of 1
- **THEN** the whole rectangle is filled and the readout is drawn over the fill

### Requirement: Gauge Colours
The gauge MUST take its colours from the theme and from the level and the state of the reading.

- At the normal level the fill is the theme's accent colour; at the warning level the theme's warning colour; at the
  critical level the theme's critical colour
- The track is the fill's colour at the opacity of the theme's dimmed accent
- For a formatted reading that is not current, the fill and the track use the theme's muted text colour
- The value, the unit and the label keep the colours the readout gives them, with one exception: where the readout
  lies over the fill of a vertical bar, the text is drawn in whichever of the theme's text colour and background
  colour contrasts more with the fill

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
