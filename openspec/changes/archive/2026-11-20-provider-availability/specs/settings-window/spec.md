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

### Requirement: Providers Are Described, Collapsible And Scrollable
The Providers section MUST scroll when its content is taller than the window. Each provider MUST be one collapsible entry
whose header always shows its name, a one-sentence description of what it gives (declared by the provider), a "Built-in"
badge when it ships with UrDeck, and a badge for its readiness (Ready, Needs setup or Not available here). A provider that
is not ready starts expanded and the others collapsed; what the user opens or closes is kept while the section is open. The
body holds the provider's guidance and its settings, or says there is nothing to set up.

#### Scenario: A small window
- **WHEN** the editor window is too short to show every provider
- **THEN** the Providers section scrolls and every provider can be reached

#### Scenario: A provider with nothing to set up
- **WHEN** the System provider is listed
- **THEN** its header shows its description with the Built-in and Ready badges, and its body says there is nothing to set up
