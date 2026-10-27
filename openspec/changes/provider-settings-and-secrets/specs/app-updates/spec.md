## MODIFIED Requirements

### Requirement: Uninstall
Uninstalling UrDeck from Windows' installed apps MUST remove the app and its secrets, and leave the user's files:

- The deck is closed first
- The Start with Windows registration of this copy is removed
- Every Credential Manager credential whose target starts with `UrDeck/` is deleted (see provider-secrets), so nothing
  UrDeck stored for a service stays behind
- The application folder and the Start menu shortcut are removed
- The data folder (`%APPDATA%\UrDeck`) is left as it was, so a later install shows the same page

#### Scenario: Uninstall and reinstall
- **WHEN** an installed copy with Start with Windows on is uninstalled and then installed again
- **THEN** after the uninstall nothing of UrDeck starts at sign-in, and after the new install the deck shows the page
  the user had

#### Scenario: Secrets removed
- **WHEN** an installed copy with a Home Assistant token stored is uninstalled
- **THEN** Credential Manager no longer lists `UrDeck/homeassistant/token`, and after a new install the provider reports
  the missing token
