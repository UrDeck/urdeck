## Why

Issue #162 asks for a small Steam panel on the case display: my status and the game I am playing, how many friends are
online or playing, and who is playing what. The issue planned the Steam Web API with a key the user registers on
Valve's developer page and a pasted SteamID. The exploration (`docs/handoff/2026-11-12-steam.md`) found that neither is
needed:

- Everything about *me* (who I am, the game I am playing, its art, recent games and hours, downloads and updates) is on
  this PC, in Steam's registry keys and files, readable with no setup and no network.
- Friends' live state comes from Steam's newer service API with a token from a QR sign-in approved in the Steam mobile
  app. That was tested on a real account: the friends list, each friend's state and current game, and avatars, all
  without a key.

## What Changes

- **A Steam provider** (`steam`) in two halves:
  - **Local, always on, no setup** (Windows): whether Steam runs, my persona name, the game I am playing (from the
    registry, as it changes), its art from Steam's library cache, recently played games with hours, and downloads and
    updates with progress.
  - **Connected, after "Connect Steam"** (QR sign-in, `provider-sign-in`): my online state, friends online and friends
    playing, as list readings (`list-readings`) with name, game, state, avatar and a link to the profile. Polled once a
    minute while shown; nothing runs while nothing shows it.
- **A Steam widget** (`urdeck.widgets.steam`): my status card (avatar or game art, name, state, game, friends playing and
  online) at 1x1 and 2x1, plus the friends list at 2x2 and 4x2, plus recent games at 4x4; a download in progress shows on
  the card. A tap opens the Steam client's friends list, the game, or a friend's profile.
- **The counts need no readings of their own:** the stats widget shows "friends playing" from the list itself, and the
  List widget shows any of the lists.
- **The first task is a spike on keeping the connection alive**: renewing the 24-hour access token from the 211-day
  refresh token without the user, and revoking it on Disconnect. Both were left open by the exploration. If renewal
  cannot work over the Web API, the connected half moves to a SteamKit2 session (LGPL-2.1) behind the same seam, and
  the specs do not change.

Not in this change: chat, trading, the store, achievements, launching a friend's game or joining it, more than one Steam
account, game art for friends' games (another download per game), Steam on Linux (the host is Windows; previews use
the sample), the Steam Web API key route.

## Capabilities

### New Capabilities

- `provider-steam`: the Steam provider: local and connected readings, where each comes from, sign-in and renewal,
  polling and demand, caches, states and reasons, what is sent where, sample data.
- `widget-steam`: the Steam widget: settings, layout per size, states (not connected, Steam not running, nobody online),
  taps, sample.

### Modified Capabilities

(none; the SDK work is in `list-readings` and `provider-sign-in`, which this change depends on)

## Impact

- `providers/UrDeck.Providers.Steam` (new, GPL, SDK only): local reader (registry, VDF and ACF files) and Web API client
  behind internal seams; a small KeyValues (VDF) text parser; caches for avatars and app names in the data folder's
  `cache/steam/`; `sample.json` with fictional friends and games. Added to the host's `UrDeckPlugin` list.
- `widgets/UrDeck.Widgets.Steam` (new, GPL, SDK only), with golden images.
- Network, only after Connect and only while a connected reading is shown: `api.steampowered.com` (sign-in, renewal,
  friends, states), `store.steampowered.com/api/appdetails` (a game's name by its id, once per game, cached),
  `avatars.fastly.steamstatic.com` (avatars of the friends shown, cached). The local half sends nothing. Nothing to
  urdeck.app.
- Tests: hand-written fixtures (fictional VDF, ACF, registry snapshots, JSON shapes captured in the handoff) and fakes;
  nothing contacts Steam in CI. An opt-in end-to-end test with a token from the environment.
- Site: a Steam page (what works without connecting, connecting, privacy), the Steam widget's page, the privacy page's
  hosts, the reference tables. Closes #162.
- Depends on `list-readings` and `provider-sign-in`; lands after both.
