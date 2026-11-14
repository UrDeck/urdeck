## Context

`provider-secrets` stores a provider's declared secrets in Credential Manager (`UrDeck/<provider>/<setting>`) or reads them
from `URDECK_SECRET_<PROVIDER>_<SETTING>`, fills them into the provider's settings before `Start`, and never logs or saves
them. The Providers section (`settings-window`) shows a field per secret and a Test connection button that calls
`DataProvider<TSettings>.ValidateAsync` on a provider instance that is not started. A change to a provider's settings or
secrets restarts it. The editor talks to the host only through `ISettingsHost`.

The Steam spike (`docs/handoff/2026-11-12-steam.md`) went: begin (get a challenge URL, poll interval, expiry), draw the URL
as a QR code, poll every 5 s, see the challenge rotate (new content, the QR must be redrawn) and "scanned" before
"approved", then receive a refresh token (211 days) and an access token (24 h). The device flow of GitHub or Microsoft
has the same shape with a code and an address instead of a QR code.

## Goals / Non-Goals

**Goals:** a provider can get its own credentials with one approval elsewhere; credentials never pass through the
configuration file, the log or the deck; a provider keeps its session alive (renews) without the user; the editor UI is
generic, not per provider.

**Non-Goals:** browser redirect flows with a loopback listener; signing in from the CLI; several accounts; letting a
provider store anything other than its own declared secrets.

## Decisions

1. **Sign-in lives beside the connection test.** `DataProvider<TSettings>` gains `OffersSignIn` and
   `SignInAsync(TSettings, ISignInProgress, CancellationToken)` returning `SignInResult`, and
   `SignOutAsync(TSettings, CancellationToken)`. Like `ValidateAsync`, they run on an instance that is not started, with
   the settings from the form (Steam has none it needs). Rejected: a separate plugin kind or interface found by reflection.
   The base class already carries the optional editor hooks, and a provider needs typed settings anyway.

2. **The provider reports challenges, the host draws them.** `ISignInProgress.Show(SignInChallenge)`: either `QrText`
   (drawn by the host as a QR code) or `Code` plus `Address` (shown as text with a copy and an open button), with
   `Instructions` (one line, "Scan with the Steam app: Steam Guard, then the QR button") and `ExpiresAt`. Calling `Show`
   again replaces it (a rotated challenge). `ISignInProgress.Status(text)` for "Scanned, approve on your phone". The
   provider does the polling with its own interval. Rejected: the host polling a provider-supplied URL, which would push
   each service's protocol into the host.

3. **The result is secrets plus an account name.** `SignInResult.Success(account, secrets)` where `secrets` maps the names
   of the provider's own secret settings marked `SetBySignIn` to values; any other name is refused and logged as a
   provider fault. `Failed(reason)` carries a line to show. Declined and expired are failures with their own reason text.
   The host stores the secrets and the account in one step, then restarts the provider.

4. **The account name rides with the credential.** A generic credential has a user name field; the host stores the
   account name there (`UrDeck/steam/refreshToken`, user name `example_gamer`) rather than in the configuration. So it is
   never in a file a user shares to ask for help, and it disappears with the secret on Disconnect or uninstall. The log never
   carries it. With the environment variable as source there is no account name; the section says "Connected (from
   `URDECK_SECRET_STEAM_REFRESHTOKEN`)".

5. **A running provider may store a new value of its own secret, without a restart.** `IReadingSink.StoreSecret(setting,
   SecretValue)` (default member: no-op, returns false). It is allowed only for a secret the provider's settings type
   declares `SetBySignIn`. The host writes it to Credential Manager and does not treat it as a settings change, so the
   provider is not restarted by its own renewal. When the environment variable is the source, the call writes nothing and
   returns false. The provider keeps using its new value in memory, and the log says once that the renewal could not be
   kept. Rejected: renewing inside the host, which cannot know each service's renewal.

6. **One sign-in at a time per provider, bounded.** Starting a second cancels the first. The host cancels on Cancel, on
   closing the window or section, and at `ExpiresAt` plus 30 s if the provider has not returned. A provider that ignores
   cancellation is abandoned like a hung sample, and its late result is dropped.

7. **Disconnect is best effort at the service, certain at home.** The host calls `SignOutAsync` with the stored secrets
   (10 s), then deletes the credentials whatever the outcome, then restarts the provider. When the service could not be
   told, the section says "Disconnected here; the session may still be listed in your <provider> account" with the
   provider's hint (for Steam: the Steam app's authorized devices).

8. **QR drawing in the host with QRCoder.** QRCoder is MIT, managed-only, and used widely. The host renders the module
   matrix into a `WriteableBitmap` itself, so no System.Drawing is needed. The challenge text never leaves the process: no
   online QR service, no clipboard copy of QR text (a code-and-address challenge may be copied, since that is its use).

9. **What the deck shows.** A provider that needs a connection reports its readings unavailable with the reason
   `Connect <display name> in Settings`. The editor's states (editor-states) already turn a reason into a hint on the tile.
   Sample providers offer no sign-in, and `--demo` never shows Connect.

## Risks / Trade-offs

- [A provider could abuse `StoreSecret` to persist arbitrary data] → only declared `SetBySignIn` secrets, under its own id,
  with Credential Manager's size limit (2,560 bytes) refused with a log line.
- [The user signs in on one copy (portable) and another copy shares the credential] → that is today's rule for secrets
  (every copy shares them) and is what a user expects of "connected on this PC".
- [The QR content is a credential on screen] → it is shown only in the editor while the user asked for it, it expires in
  minutes, and it is useless once approved or expired.
- [Services change their sign-in protocol] → the protocol is in the provider, behind its own seam and tests; the SDK
  shape survives.

## Open Questions

- Should the deck itself be able to show the QR (a tile "Connect Steam" that shows the code on the case display)? It
  would help a deck with no editor open, but it puts a credential on a screen that others see. Not now.
