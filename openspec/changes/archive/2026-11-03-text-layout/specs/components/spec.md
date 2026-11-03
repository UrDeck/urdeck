## MODIFIED Requirements

### Requirement: Components Are Available Through The SDK
Shared components MUST be usable by any widget that references only the widget SDK. A component draws into a rectangle
the widget chooses, using the theme from the render context and the values the widget passes. A component MUST NOT
hold state between paints that a widget has to manage, with two exceptions: a component whose description says that the
widget owns it (the Lottie animation and the text list) is created by the widget, used on the UI thread and released by
the widget.

#### Scenario: Community widget uses a component
- **WHEN** a widget that references only the SDK draws a readout
- **THEN** the readout is drawn with the active theme's fonts, colours and sizes

#### Scenario: Widget owns a stateful component
- **WHEN** a widget creates a Lottie animation or a text list and is later disposed
- **THEN** the widget releases it, and the component needs no other call to free what it holds

### Requirement: Text Line
The SDK MUST provide a text line component that draws one line of text at one of three steps: label, body or title.

- Each step uses the theme's size and font weight for that step
- The widget chooses primary or muted colour and the alignment inside the rectangle
- The widget MAY choose what happens to text wider than the rectangle (see text-layout): shrink, ellipsis, clip or marquee.
  Shrink is the default and is what a widget that says nothing gets; text drawn with it is unchanged from before the
  choice existed
- With shrink, text that is wider than the rectangle shrinks to fit; text never grows beyond the step's size

#### Scenario: Text fits
- **WHEN** a body text line fits its rectangle
- **THEN** it is drawn at exactly the theme's body size

#### Scenario: Text is too wide
- **WHEN** a title text line is wider than its rectangle at the theme's title size
- **THEN** it is drawn smaller so that it fits the rectangle's width

#### Scenario: Overflow chosen
- **WHEN** a widget draws a text line wider than its rectangle with the ellipsis overflow
- **THEN** it is drawn at the theme's size and ends in `…`, as text-layout says

#### Scenario: Overflow not chosen
- **WHEN** a widget built before the choice existed draws a text line wider than its rectangle
- **THEN** it shrinks to fit, byte for byte as before
