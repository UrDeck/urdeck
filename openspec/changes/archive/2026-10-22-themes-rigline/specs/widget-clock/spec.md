## ADDED Requirements

### Requirement: Clock Seconds Do Not Move The Time
With `showSeconds` the Clock MUST keep the time at the same size and position as the seconds change, whatever the
digits' widths in the theme's font. It states its widest suffix (`88`, or `88 AM` in the 12-hour format) to the readout.

#### Scenario: Seconds change width
- **WHEN** a Clock with `showSeconds: true` in a theme whose font has digits of different widths draws 21:47:41 and then 21:47:44
- **THEN** the hours and minutes are drawn at the same size and in the same place
