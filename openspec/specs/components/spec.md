# Components Specification

## Purpose

Defines the shared drawing components widgets compose their content from, and the sizing rules that make text look
consistent across every widget on a page under any theme.

## Requirements

### Requirement: Components Are Available Through The SDK
Shared components MUST be usable by any widget that references only the widget SDK. A component draws into a rectangle
the widget chooses, using the theme from the render context and the values the widget passes. A component MUST NOT
hold state between paints that a widget has to manage.

#### Scenario: Community widget uses a component
- **WHEN** a widget that references only the SDK draws a readout
- **THEN** the readout is drawn with the active theme's fonts, colours and sizes

### Requirement: Readout
The SDK MUST provide a readout component that draws a value, an optional unit and an optional label.

- The value is drawn in the theme's value role and text colour, as large as fits the rectangle
- The unit is drawn in the theme's unit role at the theme's unit ratio of the value's size, either raised to the top
  of the value or on the value's baseline, as the widget chooses
- The label is drawn below the value in the theme's label role, label size and muted text colour
- The widget MAY override the value colour
- The widget chooses horizontal and vertical alignment of the readout inside the rectangle

#### Scenario: Value with a raised unit and a label
- **WHEN** a widget draws a readout with value `41`, unit `°` raised, and label `CPU`
- **THEN** `41` is drawn large, `°` is drawn smaller and aligned to the top of the value, and `CPU` is drawn below in
  the muted colour at the theme's label size

#### Scenario: Value with a baseline unit and no label
- **WHEN** a widget draws a readout with value `03:59`, unit `PM` on the baseline, and no label
- **THEN** `PM` shares the value's baseline and no space is reserved for a label

### Requirement: Readout Size Is Stable
A readout's text size MUST NOT change as its value changes. The widget MAY state the widest value it expects, and the
readout is sized so that this widest value and the unit fit the rectangle. Digits MUST occupy equal widths so that a
changing number does not shift the characters around it.

#### Scenario: Short and long values side by side
- **WHEN** two readouts in equal rectangles both declare `100` as their widest value and show `8` and `100`
- **THEN** both values are drawn at the same text size

#### Scenario: Digit changes
- **WHEN** a readout showing `11:11` changes to `10:00`
- **THEN** the text size is unchanged and the colon stays in the same position

### Requirement: Text Line
The SDK MUST provide a text line component that draws one line of text at one of three steps: label, body or title.

- Each step uses the theme's size and font weight for that step
- The widget chooses primary or muted colour and the alignment inside the rectangle
- Text that is wider than the rectangle shrinks to fit; text never grows beyond the step's size

#### Scenario: Text fits
- **WHEN** a body text line fits its rectangle
- **THEN** it is drawn at exactly the theme's body size

#### Scenario: Text is too wide
- **WHEN** a title text line is wider than its rectangle at the theme's title size
- **THEN** it is drawn smaller so that it fits the rectangle's width

### Requirement: Small Text Is The Same Size Across The Page
A given step (label, body, title) MUST have the same pixel size in every widget on a page, whatever the widget's grid
size, unless a particular text had to shrink to fit.

#### Scenario: Labels in different card sizes
- **WHEN** a 1x1 widget and a 4x4 widget each draw a label that fits
- **THEN** both labels are drawn at the same pixel size

### Requirement: Readout Draws A Reading
The readout MUST accept a formatted reading (see data-providers, "Reading Formatting") in place of a value text, so
that a widget does not decide how a reading's state looks.

- The value text, the unit, the unit's placement and the widest value come from the formatted reading
- A formatted reading that is marked as not current is drawn with its value and unit in the theme's muted text
  colour; one that is current is drawn in the theme's text colour
- The widget still chooses the label, the alignment and the rectangle
- The text size is the same whether the reading is current, not current or shown as a dash

#### Scenario: Current reading
- **WHEN** a readout draws a formatted percentage reading of `37` that is current
- **THEN** `37` is drawn in the theme's text colour with a raised `%`

#### Scenario: Stale reading
- **WHEN** a readout draws a formatted reading that is not current
- **THEN** the value and the unit are drawn in the theme's muted text colour

#### Scenario: Dash keeps the layout
- **WHEN** a readout that showed `37` with a raised `%` draws the same reading as unavailable
- **THEN** a dash is drawn at the same text size and the unit and the label stay where they were
