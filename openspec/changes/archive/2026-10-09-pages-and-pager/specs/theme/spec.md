## MODIFIED Requirements

### Requirement: Theme Contents
A theme MUST define all of the following values:

- Colours, each with transparency: background, card fill, card border, text, muted text, accent, dimmed accent, good,
  warning and critical
- Card: corner radius, border width, gap between cards and inner padding
- Typography: a font family, a weight for each text role (value, unit, label, body, title), a size for each of the
  three small-text steps (label, body, title), and the ratio of a unit's size to its value's size
- Stroke: line thickness as a ratio of the drawn element's size, and round or square line ends
- Gauge: the default gauge style, one of `ring`, `bar` and `verticalBar` (see components, "Gauge Styles")
- Page indicator: the height of its band, the diameter of a dot, the length of the current page's pill, the space
  between marks, the colour of the current page's mark, the colour of the other marks, and the colour of the
  translucent pill behind a floating indicator (see page-navigation, "Indicator Mode")

A theme MUST NOT contain executable code. The built-in themes use `ring` as the default gauge style. A default gauge
style that is not one of the three is replaced by the default theme's value with a logged warning. A theme that omits
a page indicator value takes the default theme's value for it, so themes written before the indicator existed keep
working.

#### Scenario: Theme provides every value
- **WHEN** a theme is loaded
- **THEN** every value listed above is defined for it

#### Scenario: Colour carries transparency
- **WHEN** a theme sets the card fill to a colour with 80% opacity
- **THEN** cards are drawn with that opacity over the window background

#### Scenario: Theme changes the default gauge style
- **WHEN** a user theme sets the default gauge style to `bar`
- **THEN** a widget that asks for the theme's default gauge style receives `bar`

#### Scenario: Plain is not a default gauge style
- **WHEN** a user theme sets the default gauge style to `plain`
- **THEN** the theme loads with `ring` and a warning is logged

#### Scenario: Theme restyles the indicator
- **WHEN** a user theme sets a longer pill and a different colour for the current page's mark
- **THEN** the indicator is drawn with that pill length and colour

#### Scenario: Older theme without indicator values
- **WHEN** a user theme written before the indicator existed is loaded
- **THEN** it loads without a warning and the indicator uses the default theme's values
