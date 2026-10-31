## ADDED Requirements

### Requirement: Providers Section
The settings window MUST have a Providers section listing each loaded provider that declares settings beyond the base
ones, with one field per declared setting, generated from the declaration: a text box for text, a password box for a
secret, a number box for a number and a switch for on/off, each with the setting's label and help text.

- A secret's field never shows a stored value. It says whether a value is stored and where it comes from (Credential
  Manager or the environment variable, named), and offers to save a new value or remove the stored one. While the
  environment variable is set, the field says it overrides the stored value
- Nothing is applied until the user saves the provider's settings. Saving writes non-secret settings to the configuration
  file and secrets to the secret store (see provider-secrets), and restarts that provider (see data-providers, "A
  Settings Change Restarts Only Its Provider")
- A provider MAY offer a connection test. The section then shows a Test connection button that runs it with the values
  in the form, without saving them, and shows its one-line result or failure under the button; the test is cancelled
  after 10 seconds or when the window closes
- The deck never shows a secret

#### Scenario: Entering a token
- **WHEN** the user types a Home Assistant address and token into the Providers section and saves
- **THEN** the address is in `urdeck-config.json`, the token is in Credential Manager, and Home Assistant readings on the
  deck start arriving

#### Scenario: Testing before saving
- **WHEN** the user presses Test connection with a wrong token
- **THEN** the section shows that the token was rejected, and nothing is saved

#### Scenario: No configurable providers
- **WHEN** no loaded provider declares settings beyond the base ones
- **THEN** the Providers section says so and shows no fields
