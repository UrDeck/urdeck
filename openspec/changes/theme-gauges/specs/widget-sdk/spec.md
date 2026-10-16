## ADDED Requirements

### Requirement: Theme Carries Colours And Gauge Values
The `Theme` a widget receives MUST carry, as read-only data resolved for the surface: the theme's second accent colour
(the accent when the theme has none), its ink colour (the colour for text on an accent fill; whichever of the text and
background colours contrasts more with the accent when the theme has none), its colour ramp (good, warning and critical
when the theme has none), and the gauge values of the theme in pixels and fractions. A theme that sets none of them
gives values that change nothing. The gauge style enumeration gains `Meter`, `Tach`, `Marker` and `ThickBar` after the
existing members, which keep their values. Adding these MUST NOT remove or change any member a widget built against the
earlier SDK uses.

#### Scenario: A widget built before the new styles
- **WHEN** a widget compiled against the earlier SDK draws a gauge under a theme whose default style is `tach`
- **THEN** it draws a tach without being rebuilt

#### Scenario: No second accent
- **WHEN** a theme sets no `colors.accent2`
- **THEN** the theme's second accent is its accent colour

#### Scenario: No ramp
- **WHEN** a theme sets no `colors.ramp`
- **THEN** the ramp is the theme's good, warning and critical colours

#### Scenario: Sizes are pixels
- **WHEN** a theme sets `gauge.ticks.length` to 0.06 and the surface's grid cell is 275 pixels
- **THEN** the tick length the theme reports is 16.5 pixels
