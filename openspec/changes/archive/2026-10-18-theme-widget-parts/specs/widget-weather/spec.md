## ADDED Requirements

### Requirement: Weather Sections
The Weather widget MUST draw its sections with the SDK's parts: the rule between the current conditions and the forecast
at 4x4 with the divider component, each forecast day's column on the theme's well, and the condition text on the theme's
chip. A theme that sets none of the three gets the card exactly as it was before: the same rule, no backing behind a
day, the condition as plain text. A well is drawn for each of the shown days with the day's whole column inside it, and
the column's contents keep their order and alignment.

#### Scenario: Forecast days on a well
- **WHEN** the theme sets a well with a faint fill and the card shows five forecast days
- **THEN** each day's column is drawn on a rounded backing and the weekdays, icons and temperatures line up as before

#### Scenario: Condition on a pill
- **WHEN** the theme sets the chip shape `pill`
- **THEN** the condition text is drawn inside a pill

#### Scenario: Divider from the theme
- **WHEN** the theme sets the divider style `gradient` and a 4x4 card is drawn
- **THEN** the rule between the current conditions and the forecast fades along its length

#### Scenario: A v1 theme
- **WHEN** the card is drawn under `default-dark`
- **THEN** the picture is unchanged from before this requirement
