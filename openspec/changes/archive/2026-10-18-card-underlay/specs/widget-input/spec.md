## MODIFIED Requirements

### Requirement: Press Feedback
While the pointer is down on a widget that accepts a tap at that point, the host MUST show that widget as pressed, and
MUST return it to rest when the press ends for any reason.

- The pressed look is a change of the whole card's scale and opacity, taken from the theme (see theme, "Theme
  Contents"), applied to the widget's layer by the compositor
- What the theme draws outside the card (its shadow, glow and outside corner marks) takes the same scale, about the card's
  centre, and the same opacity, at the same time
- The widget is not repainted for a press or a release, and the widget does not draw a pressed state
- The press starts when the pointer goes down, not when the tap is recognised
- The press ends, without a tap, when the gesture becomes a swipe or a vertical drag, when the pointer is cancelled or
  lost, and when the pointer is released after too long for a tap
- A press on a widget that does not accept taps, or that declines the point, shows nothing
- With the theme's pressed scale and opacity both at 1 nothing visible happens and no animation is started

#### Scenario: Finger down on a shortcut
- **WHEN** the user puts a finger down on a shortcut and holds it still
- **THEN** the card shrinks and dims slightly, and the widget's surface is not repainted

#### Scenario: Pressed card with a shadow
- **WHEN** the user puts a finger down on a shortcut under a theme with a card shadow
- **THEN** the shadow shrinks and dims with the card, and no band of uncovered background appears round the card

#### Scenario: Press turns into a swipe
- **WHEN** the user puts a finger down on a shortcut and then drags sideways
- **THEN** the card returns to its normal look as the page starts to follow the finger

#### Scenario: Long hold
- **WHEN** the user holds a finger on a shortcut for two seconds and lifts it
- **THEN** the card returns to its normal look and no tap is delivered

#### Scenario: Press on a clock
- **WHEN** the user puts a finger down on a clock card
- **THEN** the card does not change
