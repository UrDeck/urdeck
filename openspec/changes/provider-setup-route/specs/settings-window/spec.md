## ADDED Requirements

### Requirement: Opening The Providers Section On A Provider
Every "Set up" in the editor (the picker's bar, a reading in a form) and the settings launch target (see launcher) MUST
show the Providers section with the named provider focused: scrolled into view, expanded, and its first field or its
Connect button ready. It is the same behavior whether the editor was closed or open.

#### Scenario: Set up from the picker
- **WHEN** the user chooses Set up on the Home Assistant bar in the picker
- **THEN** the Providers section is shown with Home Assistant expanded and in view

#### Scenario: A provider that is not available
- **WHEN** the focused provider is not available on this PC
- **THEN** its guidance is shown (see provider-readiness) instead of fields it cannot use
