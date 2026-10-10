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
- Press: the scale and the opacity of a card while it is pressed, each a ratio where 1 means unchanged (see
  widget-input, "Press Feedback")
- Dock: the height of the dock's band, which is also the side of a dock slot, as a fraction of a grid cell (see dock,
  "Dock Slots")

A theme MUST NOT contain executable code. The built-in themes use `ring` as the default gauge style. A default gauge
style that is not one of the three is replaced by the default theme's value with a logged warning. A theme that omits
a page indicator value, a press value or the dock height takes the default theme's value for it, so themes written
before these existed keep working. A press scale outside 0.5 to 1 or a press opacity outside 0.1 to 1 is replaced by
the default theme's value with a logged warning, and so is a dock height outside 0.2 to 1. The built-in themes use a
dock height of 0.71, which leaves the 1100x3840 panel its 13 rows beside the indicator's band.

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

#### Scenario: Theme restyles the press
- **WHEN** a user theme sets the press scale to 0.9 and the press opacity to 1
- **THEN** a pressed card shrinks to nine tenths of its size and does not dim

#### Scenario: Theme switches press feedback off
- **WHEN** a user theme sets the press scale and the press opacity to 1
- **THEN** a pressed card does not change

#### Scenario: Older theme without press values
- **WHEN** a user theme written before press feedback existed is loaded
- **THEN** it loads without a warning and a pressed card uses the default theme's values

#### Scenario: Press value out of range
- **WHEN** a user theme sets the press scale to 2
- **THEN** the theme loads with the default theme's press scale and a warning is logged

#### Scenario: Theme resizes the dock
- **WHEN** a user theme sets the dock height to 0.5
- **THEN** the dock's band and its slots are half a grid cell high

#### Scenario: Older theme without a dock height
- **WHEN** a user theme written before the dock existed is loaded
- **THEN** it loads without a warning and the dock uses the default theme's height

#### Scenario: Dock height out of range
- **WHEN** a user theme sets the dock height to 3
- **THEN** the theme loads with the default theme's dock height and a warning is logged
