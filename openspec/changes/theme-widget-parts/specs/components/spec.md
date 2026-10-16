## ADDED Requirements

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
