## ADDED Requirements

### Requirement: Providers Section Lists Every Provider With Its Readiness
The Providers section MUST list every loaded provider, whether or not it declares settings, with its readiness (see
provider-readiness): Ready, Needs setup or Not available here. A provider that is not ready shows its guidance, a
sentence and, when it has one, a link. This is how a user finds a provider the pickers hide. Providers that are not
ready MAY be grouped below the ready ones.

#### Scenario: Steam without the client
- **WHEN** the Steam client is not installed and the user opens the Providers section
- **THEN** Steam is listed as not available here, with its sentence and its download link

#### Scenario: A provider that needs a token
- **WHEN** Home Assistant has no token
- **THEN** it is listed as needing setup, with its fields, and saving a token makes it ready
