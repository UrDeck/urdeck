## MODIFIED Requirements

### Requirement: Taps Reach The Widget Under The Pointer
The host MUST deliver a tap to the widget whose card contains the point where the pointer was released, when that
widget accepts taps (see widget-sdk, "Tap Input"). Mouse, touch and pen take the same path.

- A tap is the tap of page-navigation ("Swipe Changes Page"): a press and release that stays within the small distance
  and is short. A gesture that became a swipe, a vertical drag or a long hold is never a tap
- The point handed to the widget is in the widget's own pixels, with `(0, 0)` at the top-left corner of its card, the
  same space as the render context's `PixelSize`
- A tap in the gap between cards, on an empty cell or slot, or on a widget that does not accept taps does nothing
- A visible page indicator takes a tap inside its area before any widget does
- The widgets that can be tapped are those of the dock (see dock, "Dock Input") and those of the page that is shown
  and at rest. A point in the dock's band is looked up in the dock, never in the page
- While a page slide is in progress no tap is delivered, to a page's widget or to the dock's
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

#### Scenario: Tap on a widget in the dock
- **WHEN** the user taps a shortcut in the dock
- **THEN** that widget receives one tap with the point relative to its own, smaller card
