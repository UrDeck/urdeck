## ADDED Requirements

### Requirement: Button Row
The SDK MUST offer `ButtonRow`, a pure layout: given a rectangle, a button count and optional weights (a middle button
may be larger), it returns one rectangle per button, in order, left to right, and draws nothing. A widget uses the same
call to draw each glyph and to decide which button a tap hit, so what is drawn and what is tapped cannot differ.

- The buttons are centred as a group in the rectangle, share the height, are square-bounded by it, and are separated by a
  gap that is a fixed share of the button size; sizes follow from the rectangle only, never from a resolution or a scale
- A button is never smaller than a minimum touch size that is a share of the rectangle's height; a rectangle too small for
  the count lays the buttons out at the largest size that fits, not overlapping
- `HitTest(point)` returns the index of the button whose rectangle, grown by half the gap on every side, contains the point,
  and none otherwise; the grown rectangles of neighbours never overlap
- An empty or inverted rectangle, or a count of zero, gives no buttons

#### Scenario: Three buttons
- **WHEN** a wide rectangle is laid out for three buttons with the middle weighted larger
- **THEN** three rectangles come back in order, centred as a group, the middle one larger, none overlapping

#### Scenario: Hit between buttons
- **WHEN** a point lies in the gap between the first and second button
- **THEN** it hits exactly one of them, the nearer, and never both

#### Scenario: Outside
- **WHEN** a point lies beyond the group
- **THEN** no button is hit

#### Scenario: Too small
- **WHEN** the rectangle is narrower than three minimum touch sizes
- **THEN** the buttons shrink to fit and still do not overlap
