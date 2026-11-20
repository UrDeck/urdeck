## MODIFIED Requirements

### Requirement: Not Connected Readings
A provider whose readings need a connection that has not been made MUST report them unavailable with the reason
`<provider display name> needs setup`. A sample provider offers no sign-in, and `--demo` never asks to connect.

#### Scenario: Friends before connecting
- **WHEN** the deck shows Steam's friends list and Steam is not connected
- **THEN** the tile shows "Steam needs setup"
