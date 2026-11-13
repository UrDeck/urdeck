## Context

The measurements are in `docs/handoff/2026-11-12-steam.md`: which registry values and files hold what, the QR sign-in
exchange, which Web API methods accept the sign-in's access token (the service methods `IFriendsListService`,
`IPlayerService`; not the old `ISteamUser` ones), the shapes they return, and what was left open (renewal and revoke).
`list-readings` gives list values with fields and roles; `provider-sign-in` gives a sign-in with a QR challenge,
secrets set by sign-in, and renewal by the running provider. The media provider is the model for a provider that pushes
local changes and writes image files for widgets.

## Goals / Non-Goals

**Goals:** a useful Steam tile with no setup at all; friends with one QR scan, never a key; nothing running while no Steam
tile is shown; no names, games or ids in the log; works offline for everything local.

**Non-Goals:** see the proposal; also no reading of Steam's binary caches (`appinfo.vdf`), no use of `steamclient.dll`, the
Steamworks SDK or the client's debug port.

## Decisions

1. **Two halves behind two seams.** `ISteamLocal` (registry and files; Windows implementation, fake in tests) and
   `ISteamWeb` (HTTP; real `HttpClient` implementation, scripted fake). The provider's logic (what to publish, when, which
   state) is plain code over both, tested on Linux. The local half never touches the network, and the connected half never
   reads Steam's files.

2. **Who I am comes from the PC.** The SteamID is `ActiveProcess\ActiveUser` + 76561197960265728, or, with Steam not
   running, the `MostRecent` account in `loginusers.vdf`; the persona name comes from the same file. When the connected
   account differs from the client's account, friends come from the connected account, *me* from the client, and the log
   says once that they differ (no ids).

3. **My game is pushed, not polled.** A thread waits on `RegNotifyChangeKeyValue` for `HKCU\Software\Valve\Steam`
   (`RunningAppID`, `ActiveProcess`) while a reading that needs it is subscribed, and publishes on change. The game's name
   comes from its `appmanifest_<appid>.acf` in any library of `libraryfolders.vdf`, then from the registry's
   `Apps\<appid>\Name`, then from the app-name cache (decision 7). Its art is the first that exists of
   `appcache\librarycache\<appid>\header.jpg` and the other capsule files, published as a path (the icon service reads
   it). A game with no art publishes an empty path.

4. **Recent games and downloads are read from files on change.** `localconfig.vdf` (`LastPlayed`, `Playtime`,
   `Playtime2wks`, minutes) for `games/recent` (the ten most recent, kept to the last 30 days); `appmanifest_*.acf`
   (`StateFlags`, `BytesToDownload`, `BytesDownloaded`) and `Apps\<appid>\Updating` for `downloads`. A
   `FileSystemWatcher` on the files' folders, debounced 2 s, triggers a re-read; while a download is active, its manifest
   is also read every 5 s, because Steam rewrites it lazily. All of this happens only while the reading is subscribed.
   The VDF parser is a small, strict reader of Valve's text KeyValues: quoted keys and values, nested blocks, `//`
   comments, escapes. It is fuzzed in tests with truncated files, because Steam writes these files while we read them. A
   file that does not parse leaves the last value stale and is logged once.

5. **Friends: one list call, one state call, once a minute.** `IFriendsListService/GetFriendsList` (relationship 3) every
   10 minutes or on a start; `IPlayerService/GetPlayerLinkDetails` with all friend ids (in batches of 100 if more) at the
   provider's sampling interval (default 60 s, minimum 30 s), only while a connected reading is subscribed. From it:
   `persona_state` (absent = offline), `game_id`, `persona_name`, `sha_digest_avatar`. Friends missing from the answer
   (9 of 88 in the spike) are treated as offline, and the count of them is logged once per change. `friends/online` holds
   everyone not offline, playing first then by name; `friends/playing` holds those with a game.

6. **Renewal is the gate (task 1).** The access token lives about 24 h; the provider renews it from the refresh token
   before expiry (`IAuthenticationService/GenerateAccessTokenForApp`, with `renewal_type` so a rotated refresh token
   comes back) and stores a rotated refresh token with `StoreSecret`. The spike must show this working for a sign-in
   with the right `platform_type` (1, Steam client, is the first candidate), and find `RevokeRefreshToken`'s parameters for
   Disconnect. If no Web API renewal works: the connected half moves to SteamKit2 over the same `ISteamWeb`-level seam
   (a CM connection signed in with the refresh token, persona state pushed). That is larger but changes no requirement.
   A refresh token Steam rejects makes the connected readings unavailable with `Steam sign-in expired; connect Steam
   again in Settings` and stops calling Steam until the secret changes.

7. **Caches in the data folder.** `cache/steam/apps.json`: app id to name, filled from `store.steampowered.com/api/appdetails`
   (`filters=basic`) only for games not installed here, at most 5 lookups per sample, kept forever (names do not change
   meaningfully). `cache/steam/avatars/<sha>_medium.jpg`: downloaded for friends in a subscribed list, published as paths,
   at most 300 files, least recently used removed. The name of a game not yet looked up is empty, so the row shows no game
   until the next sample.

8. **States and reasons.** Not on Windows, or Steam not installed: local readings unavailable, `Steam is not installed on
   this PC`. Steam installed but not running: `client/running` off, `me/game` empty, `me/name` from `loginusers.vdf`,
   `me/state` from the connection if any. Not connected: connected readings unavailable with `Connect Steam in Settings`.
   Network down: connected readings stale (interruption) with their last values, retried with the hub's back-off.

9. **The widget composes components, it does not draw a card.** Card: `ImageTile` (my avatar when connected, else the
   game's art, else the theme's Steam glyph), `TextLine`s for name, state and game, a `Chip` for "3 playing", a `Gauge`
   bar for a download. List: `TextList.SetItems` from `friends/online` or `friends/playing`. Status colours come from
   the theme's status palette (online, away, busy, playing), never from the widget.

10. **What leaves the PC.** Only after Connect, only while a connected reading is shown, and only to Steam's hosts listed in
    the proposal. Never logged: persona names, account names, SteamIDs, game names, tokens. Logged: state changes,
    counts, errors with Steam's `x-eresult`.

## Risks / Trade-offs

- [Valve changes or closes the service methods, which are undocumented for third parties] → the seam isolates it; the
  local half is unaffected; SteamKit2 is the fallback; the site says the connected half is unofficial.
- [Valve's terms about automated access] → the provider acts as the signed-in user, for that user's own friends, at a
  human rate (one call a minute), the way Steam's own web and mobile clients do; no scraping, no key sharing.
- [Steam's file formats change] → strict parser, stale and logged once on failure, fixtures from real shapes.
- [Rate limits unknown for a call every minute over hours] → part of the spike (run it for a day); interval minimum 30 s.
- [A shared PC: another user's Steam] → the registry is per Windows user (`HKCU`), so each Windows user sees their own.

## Open Questions

- Rich presence (`rich_presence_kv`, "Ranked match, 12 - 9") as a second detail line: nice, but its keys are per game and
  often localisation tokens. Revisit after the first release of the widget.
