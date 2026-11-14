## 1. Spike: keeping the connection (gate)

- [x] 1.1 Sign in by QR with `platform_type` 1 (Steam client), then 3 (mobile app) if needed; renew with `GenerateAccessTokenForApp` (with and without `renewal_type`) before any revoke; record which audiences and parameters work in `docs/handoff/2026-11-12-steam.md`
- [x] 1.2 Find `RevokeRefreshToken`'s working parameters (compare SteamKit2 and node `steam-session`); confirm the device disappears from the Steam app
- [x] 1.3 Poll `GetPlayerLinkDetails` for all friends through the provider against a real account (the opt-in `SteamEndToEndTests`; 78 of 88 friends answered, the others count as offline). Re-scoped: the day-long run for rate limits and renewal at the 24-hour mark was not done and is a follow-up in `docs/BACKLOG.md` (the sampling interval is at least 30 s, one call a minute by default)
- [x] 1.4 Decide: Web API (as designed) or SteamKit2 behind the same seam; record it in `design.md` decision 6 before 3.x starts

## 2. Local half

- [x] 2.1 Project `providers/UrDeck.Providers.Steam` (SDK only), identity, `refreshToken` secret set by sign-in, added to the host's `UrDeckPlugin` list
- [x] 2.2 KeyValues (VDF/ACF) text parser: strict, escapes, comments, nesting; tests with fictional fixtures, truncated and garbled inputs
- [x] 2.3 `ISteamLocal` seam and its Windows implementation: Steam path, libraries, active user and pid, `RunningAppID`, `Apps\<id>`, `loginusers.vdf`, manifests, `localconfig.vdf`, library cache art
- [x] 2.4 Registry change notification for `me/` and `client/` while subscribed; file watching with a 2 s debounce and the 5 s manifest read during a download; all stopped when unsubscribed
- [x] 2.5 Readings `client/running`, `me/name`, `me/playing`, `me/game`, `me/game/art`, `games/recent`, `downloads`, `download/progress`, `download/name`; states and reasons (not installed, not running, file faults logged once)
- [x] 2.6 Fake-seam tests on Linux for every local scenario

## 3. Connected half

- [x] 3.1 Sign-in by QR through `provider-sign-in` (begin, rotate, scanned status, approved, declined, expired) and sign-out; `ISteamWeb` seam with a scripted fake in the captured shapes
- [x] 3.2 Access renewal before expiry, rotated refresh token stored with `StoreSecret`, rejection to "sign-in expired" and silence until the secret changes
- [x] 3.3 Friend list every 10 minutes, states at the sampling interval in batches of 100, missing friends as offline, `me/state`, `friends/online`, `friends/playing` ordering
- [x] 3.4 App-name cache (`cache/steam/apps.json`, local names first, 5 lookups per sample) and avatar cache (300 files, LRU), both only for subscribed readings
- [x] 3.5 Account mismatch rule; interruption on network loss; nothing logged that names a person, game or id (a test greps the captured log)
- [x] 3.6 Fake-clock tests for renewal timing, back-off, demand, caches; an opt-in end-to-end test (`URDECK_STEAM_E2E=1` and `URDECK_SECRET_STEAM_REFRESHTOKEN`) never run in CI

## 4. Sample

- [x] 4.1 `sample.json` covering every reading with fictional persona, friends, games, an update in progress, and avatars embedded in the assembly; the sample scenarios pass in `preview render` on Linux

## 5. Widget

- [x] 5.1 Project `widgets/UrDeck.Widgets.Steam` (SDK only), settings, added to the host's `UrDeckPlugin` list; the state mark uses the theme's existing status colours (good, warning, critical, accent): nothing to add to the theme
- [x] 5.2 Layout at 1x1, 2x1, 2x2, 4x2, 4x4 with `ImageTile`, `TextLine`, `Chip`, `Gauge` and `TextList.SetItems`; download line; states (not connected, not running, empty, stale)
- [x] 5.3 Taps through the launcher: card, friend, recent game
- [x] 5.4 Golden images on the built-in themes; widget tests for states and taps

## 6. Site and docs

- [x] 6.1 Site: a Steam provider page (what works without connecting, connecting with the Steam app, what is sent where, removing the device), the Steam widget's page with pictures (`npm run media`), the privacy page's hosts, generated reference complete
- [x] 6.2 README provider and widget lists; `docs/BACKLOG.md`; close #162 from the PR
