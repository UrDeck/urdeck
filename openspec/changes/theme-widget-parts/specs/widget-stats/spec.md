## ADDED Requirements

### Requirement: Stats History
When the theme's `gauge.sparkline` is enabled, the widget MUST keep a short history of each gauge slot's fraction and give
it to the gauge, so that a line of the recent values is drawn under the readout. The history is kept only while the
sparkline is enabled and only for gauge positions tall enough to show it (at least 0.9 of a grid cell); otherwise nothing is
kept. It holds at most one value for each second, the latest, covering the theme's `historySeconds` (at most 600), and is
cleared when the slot's reading changes or the configuration does. A value is added when the card is drawn for a
changed reading, so a card that was not on screen has a gap.

#### Scenario: Sparkline enabled
- **WHEN** the theme enables the sparkline and a 2x2 card draws a CPU gauge for sixty seconds of changing values
- **THEN** a line of those values is drawn under the readout

#### Scenario: Sparkline not enabled
- **WHEN** the theme does not enable the sparkline
- **THEN** no history is kept and the card is drawn as before

#### Scenario: Position too small
- **WHEN** the theme enables the sparkline and a gauge position is 0.6 of a grid cell tall
- **THEN** no history is kept for it and no sparkline is drawn

#### Scenario: Reading replaced
- **WHEN** a slot's `reading` is changed in the configuration
- **THEN** the old history is dropped and the line starts again

### Requirement: Stats Status
When the theme's `gauge.status` is enabled, the widget MUST give each gauge position a status text and the matching level:
the first of the theme's three labels while the fraction is below the first threshold, the second below the second
threshold, the third from there on, at the good, warning and critical levels. A slot that sets its own `warning` or
`critical` uses the levels it gets from them (as fractions of its range) in place of the thresholds. A position with no
fraction has no status.

#### Scenario: Thresholds
- **WHEN** the theme's status labels are `OK`, `WARM` and `HOT` with the thresholds 0.7 and 0.9, and the load is 68 percent
- **THEN** the status is `OK` at the good level, and at 82 percent it is `WARM` at the warning level

#### Scenario: Slot levels win
- **WHEN** a slot sets `"warning": 50` and the load is 60 percent
- **THEN** the status is the second label at the warning level whatever the thresholds are

#### Scenario: No fraction
- **WHEN** a gauge position has a reading with no fraction
- **THEN** no status is drawn
