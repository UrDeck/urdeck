## ADDED Requirements

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
