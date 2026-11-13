## Why

Today a provider that needs an account asks the user to make a token somewhere else and paste it into the Providers
section (Home Assistant's long-lived token). For Steam that would mean registering a Web API key on Valve's developer
page and finding a 17-digit SteamID, which is exactly what issue #162's discussion set out to avoid. The exploration
(`docs/handoff/2026-11-12-steam.md`) showed a better way: the provider starts a sign-in, the editor shows a QR code, the
user approves it in the Steam app, and the provider gets a token it can renew for months. The same shape (show
something, wait for the user to approve it elsewhere, receive a token) is the OAuth device flow used by GitHub,
Microsoft, Twitch, Google and others, so it belongs in the SDK rather than in the Steam provider.

## What Changes

- **A provider can offer a sign-in.** It starts a sign-in on request and reports what to show: a QR code (text the host
  draws), or a code with an address to open, with one line of instructions. It replaces that as the challenge rotates,
  and finishes with the secrets to store and an account name to show, or a reason it failed.
- **Secrets set by sign-in.** A secret setting can be marked as set by sign-in. The Providers section then shows no text
  field for it. It shows "Connect", or "Connected as <account>" with "Disconnect". A secret the user may also paste
  (an environment variable, as today) still works.
- **A provider can renew its own secret.** A running provider can store a new value of a secret it declares (a rotated
  refresh token) without being restarted. Only its own declared secrets can be stored, as for reading them.
- **Disconnect** asks the provider to end the session at the service (best effort), deletes the stored secrets and
  restarts the provider without them.
- **The editor shows the sign-in** in the provider's part of the Providers section. The QR code is drawn locally, because
  its content is a credential. It shows the instructions, the progress ("Scanned, approve on your phone"), and the result,
  with Cancel. Nothing about it reaches the deck or the log beyond one line per step.
- **The deck says what to do.** A reading that needs a connection says "Connect <provider> in Settings".

Not in this change: sign-in by a browser redirect to a local address (OAuth authorization-code flow with a loopback
listener); signing in from `urdeck-cli` or on Linux (the environment variable stays the way there); several accounts per
provider; the Steam provider itself (`steam-provider`).

## Capabilities

### New Capabilities

- `provider-sign-in`: the SDK contract for a provider's sign-in, its challenges, progress, result, cancellation and
  time-out; sign-out; what is logged; samples and demo.

### Modified Capabilities

- `provider-secrets`: secrets set by sign-in; a running provider storing a new value of its own secret without a restart;
  the account name kept with the secret.
- `settings-window`: Connect, the sign-in panel with its QR code or code, Connected as, Disconnect.

## Impact

- `sdk/UrDeck.Sdk` (MIT): `SignInChallenge` (QR text or code and address, instructions, expiry), `ISignInProgress`,
  `SignInResult`; virtual `OffersSignIn`, `SignInAsync` and `SignOutAsync` on `DataProvider<TSettings>` beside the
  connection test; `SecretAttribute.SetBySignIn`; `IReadingSink.StoreSecret(setting, value)` as a default member.
  Additive; one minor version.
- `src/UrDeck.Engine`: the secret store keeps an account name with a secret (the credential's user name field); stores a
  provider's renewed secret without restarting it; the sign-in session (one at a time per provider, cancellation,
  time-out) behind `ISettingsHost`, tested on Linux with a fake provider.
- `src/UrDeck.Host`: the Providers section's Connect panel and the QR drawing. QR encoding needs a library:
  `QRCoder` (MIT, no native code) in `Directory.Packages.props`, used by the host only.
- Site: the Providers page explains connecting an account; the privacy page says sign-in tokens live in Credential
  Manager.
