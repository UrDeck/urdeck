## ADDED Requirements

### Requirement: Connecting An Account
For a provider that offers a sign-in (see provider-sign-in), the Providers section MUST show, in place of fields for its
secrets set by sign-in:

- Not connected: a Connect button, with the provider's one-line description of what connecting gives
- Signing in: the challenge (a QR code drawn on this PC, or a code with Copy and Open), its instructions, the latest
  status, the time left, and Cancel
- Connected: "Connected as" and the account name (or the environment variable it comes from), and Disconnect
- Failed: the reason, and Connect again

Connecting saves nothing else in the form: unsaved changes to the provider's other settings stay unsaved. Approval
restarts the provider as a settings change does. The deck never shows a challenge.

#### Scenario: Connect Steam
- **WHEN** the user presses Connect under Steam
- **THEN** a QR code with "Scan with the Steam app" appears in the section, and after approval on the phone the section
  shows "Connected as" with the account name

#### Scenario: Cancel
- **WHEN** the user presses Cancel while the QR code is shown
- **THEN** the QR code disappears, nothing is stored and Connect is offered again

#### Scenario: Disconnect
- **WHEN** the user presses Disconnect under a connected Steam
- **THEN** the section shows Connect, and the stored token is gone from Credential Manager
