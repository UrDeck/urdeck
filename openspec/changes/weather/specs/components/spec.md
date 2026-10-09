## MODIFIED Requirements

### Requirement: Components Are Available Through The SDK
Shared components MUST be usable by any widget that references only the widget SDK. A component draws into a rectangle
the widget chooses, using the theme from the render context and the values the widget passes. A component MUST NOT
hold state between paints that a widget has to manage, with one exception: a component whose description says that the
widget owns it (the Lottie animation) is created by the widget, used on the UI thread and released by the widget.

#### Scenario: Community widget uses a component
- **WHEN** a widget that references only the SDK draws a readout
- **THEN** the readout is drawn with the active theme's fonts, colours and sizes

#### Scenario: Widget owns a stateful component
- **WHEN** a widget creates a Lottie animation and is later disposed
- **THEN** the widget releases the animation, and the component needs no other call to free what it holds

## ADDED Requirements

### Requirement: Lottie Animation
The SDK MUST provide a component that plays a Lottie animation, so that any widget that references only the SDK can
show one. It MUST NOT expose the types of the library it is built on.

- The widget creates the component from a stream that holds the animation (Lottie JSON). When the stream is not a
  readable animation, creating it MUST fail without throwing, so a widget can fall back to drawing nothing
- The component reports the animation's duration
- Drawing takes a time in seconds from the start of the animation and a rectangle. The frame at that time is drawn
  into the rectangle, scaled to fit with its aspect ratio kept and centred; a time before the start or after the end
  is clamped to the first or last frame, and looping is the widget's choice
- The animation keeps its own colours; the theme does not recolour it
- The component is released by disposing it; disposing twice is allowed, and drawing a disposed component is a
  programming error that is reported, not ignored
- A drawn frame MUST NOT depend on earlier draws, so any time can be drawn in any order (a seek, a snapshot)

#### Scenario: Widget plays an animation
- **WHEN** a widget creates the component from a valid animation and draws it at times 0, 0.5 and 1.0 seconds
- **THEN** each call draws that moment of the animation into the rectangle

#### Scenario: Not an animation
- **WHEN** a widget creates the component from a stream that holds text that is not Lottie JSON
- **THEN** creating it reports failure and does not throw

#### Scenario: Fits the rectangle
- **WHEN** the animation is square and the rectangle is twice as wide as it is high
- **THEN** the animation is drawn as large as the height allows, centred, with nothing outside the rectangle

#### Scenario: Time out of range
- **WHEN** the widget draws at a time past the animation's duration
- **THEN** the last frame is drawn

#### Scenario: Any order
- **WHEN** a widget draws at 2.0 seconds and then at 0.0 seconds
- **THEN** the second draw is identical to drawing at 0.0 seconds first

#### Scenario: Release
- **WHEN** a widget disposes the component twice
- **THEN** neither call fails

### Requirement: Attribution Line
The SDK MUST provide a component that draws the credit of the data a widget shows, so that every widget credits
sources the same way.

- It takes the attributions of the readings the widget shows (see data-providers, "Reading Catalog") and a width, and
  draws each distinct attribution text once, as a text line at the label step (see "Text Line") in the muted text colour
- It reports the height it needs for a width, so a widget can reserve the space before laying out its content; with no
  attribution it needs no height and draws nothing
- It uses the attribution's shorter text when the full text does not fit the width on one line at the label step's
  size; when the shorter text does not fit either, it shrinks to fit like any text line
- The component draws text only; it opens no link

#### Scenario: One credit
- **WHEN** a widget passes two descriptions with the same attribution text
- **THEN** the text is drawn once

#### Scenario: Different credits
- **WHEN** a widget passes two descriptions with different attribution texts
- **THEN** both are drawn, one per line, and the reported height covers both lines

#### Scenario: Narrow width
- **WHEN** the full text does not fit the width and the shorter text does
- **THEN** the shorter text is drawn

#### Scenario: No attribution
- **WHEN** a widget passes descriptions that carry no attribution
- **THEN** the reported height is zero and nothing is drawn

#### Scenario: Theme decides the look
- **WHEN** the active theme changes
- **THEN** the credit uses the new theme's label size and muted colour
