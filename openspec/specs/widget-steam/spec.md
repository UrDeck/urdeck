# widget-steam Specification

## Purpose
TBD - created by archiving change steam-provider. Update Purpose after archive.
## Requirements
### Requirement: Steam Widget Identity
The system MUST provide a first-party widget `urdeck.widgets.steam` that shows the Steam provider's readings. It
references only the SDK, draws no card, names no font and takes its colours, including the colours of Steam states, from
the theme's status colours (good for online, warning for away and snooze, critical for busy, the accent for playing).

#### Scenario: Theme colours
- **WHEN** the theme changes
- **THEN** the online, away and playing colours of the widget change with it

### Requirement: Steam Widget Settings
The widget MUST declare:

- `friends`: which friends the list shows, `online` (default) or `playing`
- `showDownloads`: on by default; shows a download in progress on the card
- `showRecent`: on by default; at 4x4 shows recent games below the friends

#### Scenario: Only friends playing
- **WHEN** `friends` is `playing`
- **THEN** the list shows `steam:friends/playing`

### Requirement: Steam Widget Layout
The widget MUST draw by size:

- 1x1: an image (the game's art while playing, otherwise my avatar when connected, otherwise the theme's Steam glyph) with
  a state mark, and the number of friends playing
- 2x1: the image, my name, my state or game on the second line, and "N playing · M online"
- 2x2 and 4x2: the 2x1 card above the friends list (avatar, name, and the game or, for a friend not playing, how the friend
  is), as many rows as fit
- 4x4: as 4x2, with recent games (art, name, hours in the last two weeks) below the friends when `showRecent` is on

When `showDownloads` is on and a download is active, the card's second line shows the game and its progress with a
progress bar instead of my state, at 2x1 and larger.

#### Scenario: 2x1 while playing
- **WHEN** the widget is 2x1, I am playing a game, and 3 friends are playing of 7 online
- **THEN** it shows the game's art, my name, the game's name and "3 playing · 7 online"

#### Scenario: Update running
- **WHEN** an update downloads at 43 %
- **THEN** the card's second line shows the game's name with 43 % and a progress bar

### Requirement: Steam Widget States
The widget MUST stay useful before Steam is connected: the card shows what the PC knows (name, game, art, downloads) and
the friends area shows "Connect Steam in Settings" in a muted line. With Steam closed the card shows my name and "Steam is
not running". An empty friends list shows the list's empty text. A stale reading is drawn with the theme's stale
treatment, as elsewhere.

#### Scenario: Not connected at 4x2
- **WHEN** the widget is 4x2 and Steam is not connected
- **THEN** the card shows my name and game, and the friends area shows "Connect Steam in Settings"

#### Scenario: Nobody online
- **WHEN** no friend is online
- **THEN** the friends area shows "No friends online"

### Requirement: Steam Widget Taps
A tap on the card MUST open the Steam client's friends list (`steam://open/friends`); a tap on a friend opens the friend's
profile; a tap on a recent game opens it in the library. Taps go through the launcher, and nothing is sent to Steam's
servers.

#### Scenario: Tap a friend
- **WHEN** the user taps a friend in the list
- **THEN** the launcher opens `steam://url/SteamIDPage/<that friend's SteamID>`

### Requirement: Steam Widget Sample
The widget MUST draw from the Steam provider's sample in the preview tool, the golden images and `--demo`, at every size.

#### Scenario: Golden images
- **WHEN** the golden tests run
- **THEN** the widget is drawn at 1x1, 2x1, 2x2, 4x2 and 4x4 from the sample and matches the stored images

