## ADDED Requirements

### Requirement: Browsing Readings
`urdeck-cli readings browse <provider> [--search <text>] [--domain <prefix>] [--url <address>] [--config <file>]
[--format text|json]` MUST list the concrete readings a provider can enumerate (see data-providers, "Listing Concrete
Readings"), one per line: the reading id, its kind, its current value with its unit, and its label.

- The provider's settings come from `--config` (default: the configuration in the user's data folder), with `--url`
  overriding an address; secrets from the environment and, on Windows, Credential Manager (see provider-secrets). A
  secret is used, never printed
- `--search` keeps readings whose id or label contains the text (case-insensitive); `--domain` keeps ids that start with
  the prefix (`light` keeps `light.*`)
- Values are read by subscribing for at most 5 seconds; a reading with none by then shows its state (pending)
- A provider that cannot list, is not loaded or cannot start ends with exit code 2 and the reason

#### Scenario: Find the basement temperature
- **WHEN** `readings browse homeassistant --search basement --domain sensor` runs with the token in
  `URDECK_SECRET_HOMEASSISTANT_TOKEN`
- **THEN** the output lists `homeassistant:sensor.basement_temperature` with its kind, value and label, and no token

#### Scenario: Provider without a listing
- **WHEN** `readings browse weather` runs
- **THEN** the exit code is 2 and the message says the provider cannot list its readings
