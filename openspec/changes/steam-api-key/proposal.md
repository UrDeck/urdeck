## Why

The Steam provider's friends readings sign in through Steam's service interface: a QR code approved in the Steam mobile app,
which makes UrDeck register as a companion device on the user's account and call the endpoints the mobile app calls. Steam does
not document that interface for third parties, and the first user to try it was locked out of their account. Whether or not the
lock-out was caused by it, impersonating Steam's own app is not something UrDeck should ask users to risk their account for.
Steam publishes a Web API for exactly this (`ISteamUser`) that a user reaches with their own key. Use that.

## What Changes

- **BREAKING (pre-release, no migration):** the Steam provider no longer offers a sign-in. Its secret `refreshToken` is replaced
  by `apiKey`, a Web API key the user registers at steamcommunity.com/dev/apikey and pastes in Providers, plus an optional
  `steamId` setting (empty: the account Steam is signed in to on this PC).
- The friends and states come from `ISteamUser/GetFriendList` and `GetPlayerSummaries`, with the key. A private friends list
  leaves `me/state` and `me/avatar` working and gives `friends/*` the reason "Your Steam friends list is private; make it
  public to show friends". A key Steam refuses says so and stops the calls until the key changes.
- Removed from the Steam provider: the QR sign-in, access-token renewal, token rotation, sign-out and the JWT reading.
- The site's Steam page, the widget page and the README describe the key.

Not in this change: removing the generic provider sign-in framework from the SDK, engine and editor (`provider-sign-in`). It
stays, with no first-party provider using it; whether to keep it is a separate decision.

## Capabilities

### Modified Capabilities

- `provider-steam`: identity and settings, who I am, connected readings, privacy; the connection lifetime requirement is
  replaced by the API key requirement.

## Impact

- `providers/UrDeck.Providers.Steam`: `SteamSettings`, `ISteamWeb`, `SteamWebClient`, `ConnectedReadings`, `SteamProvider`;
  `SteamJwt` deleted.
- Tests: `SteamConnectedProviderTests`, `FakeSteamWeb`, the opt-in real-Steam test (`URDECK_SECRET_STEAM_APIKEY`,
  `URDECK_STEAM_E2E_STEAMID`), the catalog test in `UrDeck.Cli.Tests`.
- Site: `steam.md`, `widgets/steam.mdx`; `README.md`.
- No SDK change.
