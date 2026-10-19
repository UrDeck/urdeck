## ADDED Requirements

### Requirement: Clock Header And Options
The Clock widget MUST support these further configuration parameters, all absent by default, with which the Clock looks
and behaves as it did before they existed and never animates beyond the flap:

- `header` — `date`, `label` or `none`. Absent keeps the date below the time. When present the Clock draws a header row
  above the time, in the label role, in the theme's letter case, and no date below: `date` puts the date at the left
  and the week or the subtitle at the right, `label` puts the subtitle at the left and the date at the right, `none`
  draws no header and no date
- `subtitle` — text shown in the header (it is the label for `label` and the right-hand text for `date`)
- `showWeek` — boolean; the ISO week (`Wk 42`) is the header's right-hand text for `date`, before the subtitle
- `showSeconds` — boolean; the seconds are drawn small, after the time, in the unit role
- `blinkSeparator` — boolean; the colon blinks once a second. The Clock has no motion setting of its own; the host's motion policy (a later change) will make it steady

With `showSeconds` or `blinkSeparator` the Clock draws once a second, and only then; without them it draws once a minute as
before. The value fill and glow of the theme apply to the digits and not to the flap tiles.

#### Scenario: Header with the date
- **WHEN** a widget is configured with `header: "date"` and `showWeek: true` on a Tuesday in week 42
- **THEN** the date is at the left of the header row, `Wk 42` at the right, the time below, and no date below the time

#### Scenario: Header with a label
- **WHEN** a widget is configured with `header: "label"` and `subtitle: "Local time"`
- **THEN** `Local time` is at the left of the header row and the date at the right

#### Scenario: Seconds
- **WHEN** a widget is configured with `showSeconds: true` at 21:47:32
- **THEN** `32` is drawn small after the time, and the card draws again one second later

#### Scenario: Blinking separator
- **WHEN** a widget is configured with `blinkSeparator: true`
- **THEN** the colon is drawn on one second and left out the next, and the card draws once a second

#### Scenario: Nothing set
- **WHEN** a widget's configuration has none of these parameters
- **THEN** the Clock is drawn exactly as before and draws once a minute
