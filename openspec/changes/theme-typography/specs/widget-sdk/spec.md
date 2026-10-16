## ADDED Requirements

### Requirement: Theme Carries The Text Style
The `Theme` a widget receives MUST carry the theme's text style, resolved for the surface and in pixels, as read-only
data: the letter case of labels and titles, the letter spacing of values, labels and titles, the slant and size
multiplier of values, the value's gradient fill and the value's glow. A theme that sets none of them gives a style that
changes nothing. The components apply the style; a widget that draws with the components needs no code of its own to
honour it, and a widget that draws text itself MAY read it. Adding the style MUST NOT remove or change any member a
widget built against the earlier SDK uses.

#### Scenario: A widget that uses the components
- **WHEN** a widget built before the text style existed draws a readout under a theme that sets a slant
- **THEN** the value is slanted without the widget being rebuilt

#### Scenario: No style
- **WHEN** a theme sets no text style value
- **THEN** every member of the style reports "no effect" and the components draw as they did before

#### Scenario: Sizes are pixels
- **WHEN** a theme sets `typography.valueGlow.blur` to 0.1 and the surface's grid cell is 275 pixels
- **THEN** the style reports a blur of 27.5 pixels
