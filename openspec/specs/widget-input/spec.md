# widget-input Specification

## Purpose
Defines how a press and a tap on the deck reach the widget under the pointer, what the user sees while pressing, and
how this coexists with page swipes and the page indicator, so that widgets can be used by touch without each one
handling pointers itself.
## Requirements
### Requirement: Taps Reach The Widget Under The Pointer
The host MUST deliver a tap to the widget whose card contains the point where the pointer was released, when that
widget accepts taps (see widget-sdk, "Tap Input"). Mouse, touch and pen take the same path.

- A tap is the tap of page-navigation ("Swipe Changes Page"): a press and release that stays within the small distance
  and is short. A gesture that became a swipe, a vertical drag or a long hold is never a tap
- The point handed to the widget is in the widget's own pixels, with `(0, 0)` at the top-left corner of its card, the
  same space as the render context's `PixelSize`
- A tap in the gap between cards, on an empty cell, or on a widget that does not accept taps does nothing
- A visible page indicator takes a tap inside its area before any widget does
- Taps are delivered only to widgets of the page that is shown and at rest; while a page slide is in progress no tap
  is delivered
- After a tap is delivered the host MUST ask the widget whether it needs a repaint and repaint it when it does
- A widget that throws while handling a tap MUST NOT affect the host or other widgets; the failure is logged with the
  widget's name

#### Scenario: Tap on a tappable widget
- **WHEN** the user taps a shortcut card
- **THEN** that widget receives one tap with the point relative to its own card

#### Scenario: Tap on a widget that does not accept taps
- **WHEN** the user taps a clock card
- **THEN** nothing happens and no widget code runs for the tap

#### Scenario: Tap in the gap
- **WHEN** the user taps between two cards
- **THEN** no widget receives a tap

#### Scenario: Swipe that starts on a shortcut
- **WHEN** the user puts a finger down on a shortcut and swipes to the left
- **THEN** the page changes and the shortcut receives no tap

#### Scenario: Indicator first
- **WHEN** a floating indicator is visible over a tappable widget and the user taps a dot
- **THEN** the page changes and the widget receives no tap

#### Scenario: Widget fails in its tap handler
- **WHEN** a widget throws while handling a tap
- **THEN** the failure is logged, the widget stays on the page and later taps are still delivered

### Requirement: Press Feedback
While the pointer is down on a widget that accepts a tap at that point, the host MUST show that widget as pressed, and
MUST return it to rest when the press ends for any reason.

- The pressed look is a change of the whole card's scale and opacity, taken from the theme (see theme, "Theme
  Contents"), applied to the widget's layer by the compositor
- The widget is not repainted for a press or a release, and the widget does not draw a pressed state
- The press starts when the pointer goes down, not when the tap is recognised
- The press ends, without a tap, when the gesture becomes a swipe or a vertical drag, when the pointer is cancelled or
  lost, and when the pointer is released after too long for a tap
- A press on a widget that does not accept taps, or that declines the point, shows nothing
- With the theme's pressed scale and opacity both at 1 nothing visible happens and no animation is started

#### Scenario: Finger down on a shortcut
- **WHEN** the user puts a finger down on a shortcut and holds it still
- **THEN** the card shrinks and dims slightly, and the widget's surface is not repainted

#### Scenario: Press turns into a swipe
- **WHEN** the user puts a finger down on a shortcut and then drags sideways
- **THEN** the card returns to its normal look as the page starts to follow the finger

#### Scenario: Long hold
- **WHEN** the user holds a finger on a shortcut for two seconds and lifts it
- **THEN** the card returns to its normal look and no tap is delivered

#### Scenario: Press on a clock
- **WHEN** the user puts a finger down on a clock card
- **THEN** the card does not change

### Requirement: Input Costs Nothing At Rest
Routing input to widgets MUST NOT add recurring work. With no pointer down there are no timers, no frames and no
polling for it, and a page of widgets that do not accept taps costs the same as before.

#### Scenario: Idle page with shortcuts
- **WHEN** a page of shortcut widgets is shown and nobody touches it
- **THEN** no frames are produced and CPU and GPU use are not above those of the same page without shortcuts

### Requirement: Input Decisions Are Testable Without A Window
Whether a pointer sequence is a press, a tap, a cancelled press or a swipe, and which widget a point falls on, MUST be
decided by engine code that takes pointer samples and a page layout and needs no window, so that the rules are covered
by unit tests.

#### Scenario: Recorded pointer sequence
- **WHEN** a test feeds a down sample and an up sample at the same place 100 milliseconds apart
- **THEN** a press, then a tap, are reported at that place

#### Scenario: Point to widget
- **WHEN** a test asks which widget of a laid-out page contains a point
- **THEN** it receives that widget's index and the point relative to its card, or nothing for a point in a gap

