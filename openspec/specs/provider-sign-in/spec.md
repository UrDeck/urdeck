# provider-sign-in Specification

## Purpose
TBD - created by archiving change provider-sign-in. Update Purpose after archive.
## Requirements
### Requirement: Providers Offer A Sign-In
A provider MAY offer a sign-in, by which it obtains its own secrets after the user approves it elsewhere (a phone app, a
web page). A provider that offers one MUST declare at least one secret setting as set by sign-in. The sign-in MUST run on a
provider instance that is not started, with the settings from the form, as the connection test does.

#### Scenario: A provider with a sign-in
- **WHEN** a loaded provider offers a sign-in and declares `refreshToken` as a secret set by sign-in
- **THEN** the editor offers to connect it, and offers no text field for `refreshToken`

#### Scenario: A provider without one
- **WHEN** a provider offers no sign-in
- **THEN** its secrets are entered as today and no Connect button is shown

### Requirement: Sign-In Challenges
While signing in, a provider MUST report what the user has to do as a challenge: either a text to be shown as a QR code,
or a code with an address to open; each with one line of instructions and the time it expires. It MAY replace the
challenge (the service rotated it) and MAY report a one-line status ("Scanned; approve on your phone"). The host shows the
latest challenge and status. A QR code is drawn on this PC from the text; the text is never sent anywhere, never copied to
the clipboard and never written to the log.

#### Scenario: QR challenge
- **WHEN** the Steam provider reports a QR challenge with the instructions "Scan with the Steam app"
- **THEN** the editor shows a QR code drawn from the text, with that line

#### Scenario: Rotated challenge
- **WHEN** the provider reports a new challenge while the first is shown
- **THEN** the new QR code replaces the old one

#### Scenario: Code and address
- **WHEN** a provider reports the code `WDJB-MJHT` and an address
- **THEN** the editor shows the code with a way to copy it and a button that opens the address

### Requirement: Sign-In Result
A sign-in MUST end in one of: approved, with an account name to show and a value for each secret set by sign-in; or
failed, with a reason to show (declined, expired, the service's refusal, no network). On approval the host stores the
secrets and the account name together and restarts the provider with them. A result that names a secret the provider
does not declare as set by sign-in is refused: nothing is stored, the sign-in fails with "The provider returned an
unexpected secret", and the provider is named in the log.

#### Scenario: Approved
- **WHEN** the user approves the sign-in on the phone
- **THEN** the secrets are stored, the section says "Connected as" and the account name, and the provider's readings
  start arriving on the deck

#### Scenario: Declined
- **WHEN** the user declines on the phone
- **THEN** nothing is stored and the section shows the reason and Connect again

### Requirement: Sign-In Lifetime
At most one sign-in per provider MUST run at a time; starting another cancels the first. The host MUST cancel a sign-in
when the user presses Cancel, when the section or the window closes, and when the challenge's expiry has passed by
30 seconds without a result. A provider that does not return after cancellation is abandoned and its late result is
dropped; nothing it returns afterwards is stored.

#### Scenario: Window closed mid-sign-in
- **WHEN** the user closes the editor while the QR code is shown
- **THEN** the sign-in is cancelled and nothing is stored, even if the phone approves later

#### Scenario: Expired
- **WHEN** nobody scans the code and it expires
- **THEN** the section says the code expired and offers Connect again

### Requirement: Sign-Out
A provider that offers a sign-in MAY end its session at the service when the user disconnects. The host MUST call it with
the stored secrets, wait at most 10 seconds, and then delete the stored secrets and account name whatever the outcome, and
restart the provider without them. When the service could not be told, the section MUST say the session may still be
listed at the service, with the provider's hint on where to remove it.

#### Scenario: Disconnect while offline
- **WHEN** the user disconnects Steam with no network
- **THEN** the stored token is deleted, Steam's friends readings report that Steam is not connected, and the section says
  the session may still be listed in the Steam app's authorized devices

### Requirement: Sign-In In The Log
The log MUST have one line per step of a sign-in: started, approved, failed with its reason, cancelled, and one line per
sign-out with whether the service was told. It MUST NOT contain a challenge, a code, a secret or an account name.

#### Scenario: Approved sign-in in the log
- **WHEN** a Steam sign-in is approved
- **THEN** the log says `Sign-in steam: started` and `Sign-in steam: approved`, and neither line names the account

### Requirement: Not Connected Readings
A provider whose readings need a connection that has not been made MUST report them unavailable with the reason
`Connect <provider display name> in Settings`. A sample provider offers no sign-in, and `--demo` never asks to connect.

#### Scenario: Friends before connecting
- **WHEN** the deck shows Steam's friends list and Steam is not connected
- **THEN** the tile shows "Connect Steam in Settings"

