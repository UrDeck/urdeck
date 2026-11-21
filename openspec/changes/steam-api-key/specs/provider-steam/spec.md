## MODIFIED Requirements

### Requirement: Steam Provider Identity
The system MUST provide a first-party provider with the id `steam` and the display name Steam. It references only the
SDK and offers no sign-in. It declares one secret, `apiKey` (the user's own Steam Web API key), and one optional setting,
`steamId` (a SteamID64; empty means the account Steam is signed in to on this PC). Neither is required: the provider is ready
without them. Its sampling interval defaults to 60 seconds with a minimum of 30.

#### Scenario: Nothing required
- **WHEN** the user opens Steam in the Providers section
- **THEN** it shows a Web API key field, an optional SteamID field and the sampling interval, and no Connect button

### Requirement: Who I Am
The provider MUST take the local account from this PC: the account signed in to the running Steam client, or, while Steam does
not run, the most recent account in Steam's login file. The connected readings are those of the configured `steamId`, or of the
local account when it is empty. When the configured account differs from the client's account, the local readings stay the
client's and the log says once that the two differ, without naming either. With no `steamId` and no local account, the
connected readings are unavailable with `Enter your SteamID in Settings`.

#### Scenario: Steam closed
- **WHEN** Steam is not running
- **THEN** `client/running` is off, `me/game` is empty and `me/name` is the most recent account's persona name

#### Scenario: No account to ask about
- **WHEN** a Web API key is set, `steamId` is empty and Steam is not installed
- **THEN** the connected readings say `Enter your SteamID in Settings` and Steam's servers are not called

### Requirement: Connected Steam Readings
When a Web API key is set, the provider MUST offer:

- `me/state` (text): Online, Away, Busy, Snooze, Looking to trade, Looking to play or Offline
- `me/avatar` (text): the path of my avatar in the cache, empty when there is none
- `friends/online` (list): friends who are not offline, those playing first, then by name; keyed by SteamID, with `name`
  (title), `game` (detail, absent when not playing), `state` (status), `avatar` (image) and `link`
  (`steam://url/SteamIDPage/<steamid>`); unit "online", empty text "No friends online"
- `friends/playing` (list): the same rows, only friends playing a game; unit "playing", empty text "Nobody is playing"

They come from Steam's documented Web API (`ISteamUser/GetFriendList`, `GetPlayerSummaries`) with the user's key. Without a
key, these readings are unavailable with the reason `Connect Steam in Settings`. When the account's friends list is private,
`friends/online` and `friends/playing` are unavailable with `Your Steam friends list is private; make it public to show friends`
and `me/state` and `me/avatar` still work. Friends Steam does not report on are treated as offline. A game's name is taken from
this PC when the game is installed, otherwise from Steam's store by its id once and cached; until it is known the row has no
game.

#### Scenario: Friends playing in the stats widget
- **WHEN** a stats slot shows `steam:friends/playing` and three friends are in a game
- **THEN** it shows 3 playing

#### Scenario: No key
- **WHEN** the List widget shows `steam:friends/online` before a key is set
- **THEN** it shows "Connect Steam in Settings"

#### Scenario: Private friends list
- **WHEN** the key is valid and the account's friends list is private
- **THEN** `me/state` shows my state and the friends readings say the friends list is private

#### Scenario: A friend starts a game
- **WHEN** a friend starts a game
- **THEN** within one sampling interval the friend moves to the top of `friends/online` with the game's name, and appears
  in `friends/playing`

### Requirement: Steam Privacy
The provider MUST send nothing before the user sets a Web API key. After that it sends requests only to
`api.steampowered.com` (the friend list and states, with the key), `store.steampowered.com` (a game's name by its id) and
Steam's avatar host, and only while a connected reading is shown. It MUST NOT write persona names, account names, SteamIDs,
game names or the key to the log.

#### Scenario: The log of a busy evening
- **WHEN** friends come online and start games while the deck shows them
- **THEN** the log has no name, game, SteamID or key, only state changes, counts and errors

## REMOVED Requirements

### Requirement: Steam Connection Lifetime
**Reason**: There is no sign-in session to keep alive. The provider uses the user's own Web API key, which does not expire.
**Migration**: None (pre-release). A user who connected through the QR sign-in adds a Web API key instead; the stale stored
token is ignored.

## ADDED Requirements

### Requirement: Steam Web API Key
When Steam refuses the Web API key (a wrong, revoked or never-activated key), the connected readings MUST become unavailable
with `Steam rejected the API key; check it in Settings` and the provider MUST call Steam no more until the key changes. When
Steam cannot be reached, the connected readings are stale with their last values and the provider retries with the usual
back-off. The key lives in Windows Credential Manager or the environment variable, never in the configuration file or the log.

#### Scenario: A wrong key
- **WHEN** the user pastes a key Steam does not accept
- **THEN** the connected readings say Steam rejected the key, local readings keep working, and Steam is not called again until the key is changed
