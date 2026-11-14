# provider-steam Specification

## Purpose
TBD - created by archiving change steam-provider. Update Purpose after archive.
## Requirements
### Requirement: Steam Provider Identity
The system MUST provide a first-party provider with the id `steam` and the display name Steam. It references only the
SDK, offers a sign-in (see provider-sign-in) and declares one secret, `refreshToken`, set by sign-in. It has no other
setting the user must fill in; its sampling interval defaults to 60 seconds with a minimum of 30.

#### Scenario: Nothing to fill in
- **WHEN** the user opens Steam in the Providers section
- **THEN** it shows Connect and the sampling interval, and no field for a key or a SteamID

### Requirement: Local Steam Readings
On Windows, the provider MUST offer these readings from Steam's registry values and files on this PC, with no sign-in and
no network:

- `client/running` (on/off): Steam is running
- `me/name` (text): the persona name of the account signed in to Steam on this PC, or of the most recent one
- `me/playing` (on/off) and `me/game` (text, empty when not playing): the game being played
- `me/game/art` (text): the path of the game's art in Steam's library cache, empty when there is none
- `games/recent` (list): the ten most recently played games of the last 30 days, keyed by app id, with `name` (title),
  `lastPlayed` (date, detail), `hours2wks` (hours in the last two weeks, one decimal, status), `hours` (total hours),
  `art` (image) and `link` (`steam://nav/games/details/<appid>`); empty text "No games played recently"
- `downloads` (list): games downloading or updating, keyed by app id, with `name` (title), `state` (Downloading,
  Updating, Queued or Paused; detail), `progress` (percent, status), `art` (image); unit "downloads", empty text "Nothing
  downloading"
- `download/progress` (percent) and `download/name` (text): the first active download, for a gauge; unavailable with the
  reason "Nothing downloading" when there is none

#### Scenario: Start a game
- **WHEN** a widget shows `steam:me/game` and the user starts a game in Steam
- **THEN** the reading changes to the game's name within two seconds, without waiting for the sampling interval

#### Scenario: An update downloads
- **WHEN** Steam updates a game while `steam:downloads` is shown
- **THEN** the list holds the game with the state Updating and its progress, and the progress rises as the download runs

#### Scenario: Offline
- **WHEN** the PC has no network and Steam runs in offline mode
- **THEN** every local reading works

### Requirement: Who I Am
The provider MUST take the account from this PC: the account signed in to the running Steam client, or, while Steam does
not run, the most recent account in Steam's login file. When the connected account (from the sign-in) differs from the
client's account, the connected readings are those of the connected account, the local ones those of the client, and the
log says once that the two differ, without naming either.

#### Scenario: Steam closed
- **WHEN** Steam is not running
- **THEN** `client/running` is off, `me/game` is empty and `me/name` is the most recent account's persona name

### Requirement: Connected Steam Readings
After the user connects Steam, the provider MUST offer:

- `me/state` (text): Online, Away, Busy, Snooze, Looking to trade, Looking to play or Offline
- `me/avatar` (text): the path of my avatar in the cache, empty when there is none
- `friends/online` (list): friends who are not offline, those playing first, then by name; keyed by SteamID, with `name`
  (title), `game` (detail, absent when not playing), `state` (status), `avatar` (image) and `link`
  (`steam://url/SteamIDPage/<steamid>`); unit "online", empty text "No friends online"
- `friends/playing` (list): the same rows, only friends playing a game; unit "playing", empty text "Nobody is playing"

Before connecting, these readings are unavailable with the reason `Connect Steam in Settings`. Friends Steam does not
report on are treated as offline. A game's name is taken from this PC when the game is installed, otherwise from Steam's
store by its id once and cached; until it is known the row has no game.

#### Scenario: Friends playing in the stats widget
- **WHEN** a stats slot shows `steam:friends/playing` and three friends are in a game
- **THEN** it shows 3 playing

#### Scenario: Not connected
- **WHEN** the List widget shows `steam:friends/online` before Steam is connected
- **THEN** it shows "Connect Steam in Settings"

#### Scenario: A friend starts a game
- **WHEN** a friend starts a game
- **THEN** within one sampling interval the friend moves to the top of `friends/online` with the game's name, and appears
  in `friends/playing`

### Requirement: Steam Connection Lifetime
The provider MUST keep its connection without the user: it renews its access before it expires and stores a renewed
sign-in token itself (see provider-secrets, "A Provider Renews Its Own Secret"). When Steam rejects the sign-in token,
the connected readings become unavailable with `Steam sign-in expired; connect Steam again in Settings`, and the provider
calls Steam no more until the token changes. When Steam cannot be reached, the connected readings are stale with their
last values and the provider retries with the usual back-off. On Disconnect the provider ends the session at Steam when
it can (see provider-sign-in, "Sign-Out").

#### Scenario: A week later
- **WHEN** the deck has run for seven days after the user connected Steam once
- **THEN** friends' readings still arrive and the user was never asked to connect again

#### Scenario: Session removed in the Steam app
- **WHEN** the user removes UrDeck from the authorized devices in the Steam app
- **THEN** the connected readings say the sign-in expired, and local readings keep working

### Requirement: Steam Provider Cost
The provider MUST do only the work its subscribed readings need. It MUST NOT watch the registry while no `me/` or
`client/` reading is subscribed, read `localconfig.vdf` while `games/recent` is not, read manifests while no download
reading is, or call Steam's servers while no connected reading is. The friend list is fetched at most every 10 minutes and
friends' states at the sampling interval. Avatars are downloaded only for friends in a subscribed list and kept in the data
folder's cache (at most 300, least recently used removed); game names looked up at Steam's store are cached for good, at
most 5 lookups per sample.

#### Scenario: Only my game is shown
- **WHEN** the only Steam reading shown is `me/game`
- **THEN** the provider makes no network request and reads no file except when the registry says the game changed

#### Scenario: Deck hidden
- **WHEN** the deck is hidden and nothing shows a Steam reading
- **THEN** the provider makes no requests

### Requirement: Steam Files And Faults
Steam's text files MUST be read with a parser that rejects what it does not understand rather than guessing. A file that is
missing, locked or cut off while Steam writes it leaves the readings that depend on it with their last values (stale) and
is logged once per file per cause; the next change of the file is read again. Steam not installed, or not on Windows,
makes the local readings unavailable with the reason `Steam is not installed on this PC`.

#### Scenario: Manifest written while read
- **WHEN** an `appmanifest` file is cut off because Steam was writing it
- **THEN** `downloads` keeps its last value and is updated on the next change of the file

#### Scenario: No Steam
- **WHEN** Steam is not installed
- **THEN** `steam:me/game` is unavailable with the reason `Steam is not installed on this PC`

### Requirement: Steam Privacy
The provider MUST send nothing before the user connects Steam. After connecting it sends requests only to
`api.steampowered.com`, `store.steampowered.com` (a game's name by its id) and Steam's avatar host, and only while a
connected reading is shown. It MUST NOT write persona names, account names, SteamIDs, game names or tokens to the log.

#### Scenario: The log of a busy evening
- **WHEN** friends come online and start games while the deck shows them
- **THEN** the log has no name, game or SteamID, only state changes, counts and errors

### Requirement: Steam Provider Samples
The provider MUST carry sample data covering every reading: a fictional persona playing a fictional game, fictional
friends online and playing with sample avatars that travel in the assembly, recent games and one update in progress, so
that previews, the golden images, the site and `--demo` draw Steam without Steam, a sign-in or the network. The sample
names no real game or person.

#### Scenario: Preview on Linux
- **WHEN** `urdeck-cli preview render --widget urdeck.widgets.steam --widget-size 4x2` runs on Linux
- **THEN** a picture of the card and a friends list is produced from the sample

