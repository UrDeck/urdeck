## MODIFIED Requirements

### Requirement: Readout Size Is Stable
A readout's text size MUST NOT change as its value changes. The widget MAY state the widest value it expects, and the
readout is sized so that this widest value and the unit fit the rectangle. Digits MUST occupy equal widths so that a
changing number does not shift the characters around it. The digits of the unit MUST occupy equal widths too, so that a
unit such as the seconds does not change the readout's size as it changes. The widget MAY state the widest unit it
expects, and the readout reserves that width.

#### Scenario: Short and long values side by side
- **WHEN** two readouts in equal rectangles both declare `100` as their widest value and show `8` and `100`
- **THEN** both values are drawn at the same text size

#### Scenario: Digit changes
- **WHEN** a readout showing `11:11` changes to `10:00`
- **THEN** the text size is unchanged and the colon stays in the same position

#### Scenario: Unit digits change
- **WHEN** a readout's unit changes from `11` to `00` in a font whose digits differ in width
- **THEN** the text size and the width of the readout are unchanged

#### Scenario: Widest unit
- **WHEN** a readout declares `MM` as its widest unit and its unit is `ii`
- **THEN** the readout is as wide as with the unit `MM`
