## ADDED Requirements

### Requirement: Findings Are Structured
Everything the deck reports about a theme when it loads one MUST be available as a finding as well as a log line:

- A finding has a code of the `theme-lint` capability, a severity, the key it is about when it is about one, and the
  message
- The log line for a finding is the line the deck has always written for it, at the level it has always had
- A finding is reported once for each load of a theme, as the log line is
- Findings that the deck acts on (a value cleared, an animation switched off) are acted on as before; reporting them in
  another form changes nothing the deck draws

#### Scenario: Log unchanged
- **WHEN** a theme with an out-of-range value is loaded
- **THEN** the deck's log has the same single warning as before, and the finding of the same message exists

#### Scenario: Once per load
- **WHEN** a theme is resolved for a page, a dock and a display change after one load
- **THEN** each finding is reported once

#### Scenario: Drawing unchanged
- **WHEN** the built-in themes are drawn
- **THEN** the pictures are those of the reference pictures
