## ADDED Requirements

### Requirement: Steam Is Available When The Client Is Installed
The Steam provider MUST declare itself available on a PC where the Steam client is installed (a Steam path is recorded)
and not available otherwise. It is ready whenever it is available: the Connect step is not needed for it to be offered,
since its local readings work without it, and its friends readings keep saying they need the sign-in. When it is not
available its guidance says Steam is not installed and links to Steam's download page. Whether Steam is installed is
decided again when readiness is next asked, so installing Steam is noticed without restarting the host.

#### Scenario: Not installed
- **WHEN** the Steam client is not installed
- **THEN** the pickers offer no Steam readings and the Providers section lists Steam as not available, with "Get Steam"

#### Scenario: Installed, not signed in
- **WHEN** the Steam client is installed and the user has not connected Steam
- **THEN** Steam's readings are offered in the pickers, its local readings work, and its friends readings say Steam needs setup (see provider-sign-in)

#### Scenario: Installed after the host started
- **WHEN** the user installs Steam while UrDeck runs and then opens the picker
- **THEN** Steam is offered without restarting UrDeck
