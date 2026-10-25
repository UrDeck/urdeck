# media-provider Specification

## Purpose

Defines the `media` data provider: what it reads from the current Windows media session, the readings and actions it
offers, when it runs, how album art reaches a widget, and what it logs, so that any widget can show what is playing and
control it without referencing the provider.

## Requirements

### Requirement: Media Provider Identity
The product MUST ship a first-party provider with the id `media`, referencing only the widget SDK, that implements
`IDataProvider` and `IActionProvider` (see provider-actions). It follows the one session Windows calls current; it does not
choose between players. It publishes from the session's change events and does not poll: `SampleAsync` does nothing, and
the configured `intervalMs` has no effect.

#### Scenario: Registered
- **WHEN** the host starts with the first-party plugins
- **THEN** the provider `media` is registered and its catalog lists the readings and actions below

#### Scenario: No polling
- **WHEN** a session plays and nothing changes
- **THEN** the provider publishes nothing between the session's events

### Requirement: Media Readings
The catalog MUST offer these readings under `media:`:

| Path | Kind | Meaning |
|---|---|---|
| `status` | text | `playing`, `paused`, `stopped` or `none` (no session) |
| `title`, `artist`, `album` | text | As the player reports them; empty when it reports none |
| `app` | text | The application that owns the session: its application user model id tidied for display (a file extension or package suffix removed); Windows reports no display name |
| `position` | number (seconds) | The position when it was last reported |
| `position/at` | time | The instant `position` was reported |
| `duration` | number (seconds) | The length; 0 when unknown (a stream) |
| `art` | text | The path of the album art file, or empty |
| `can/previous`, `can/next`, `can/playpause` | on/off | Whether the player accepts the command now |

- With no session every reading but `status` is unavailable with the reason "nothing is playing", and `status` is `none`
- A reading the player does not report is empty or unavailable, never a guess
- A change to a session's properties publishes the readings that changed together in one batch, so a widget never draws
  the new title with the old art

#### Scenario: Something plays
- **WHEN** a player reports a track with title, artist, album, position and length
- **THEN** `status` is `playing` and the readings carry those values, with `position/at` set to when the player reported
  the position

#### Scenario: Nothing plays
- **WHEN** no media session exists
- **THEN** `status` is `none` and the other readings are unavailable with the reason

#### Scenario: Track change
- **WHEN** a player moves to the next track
- **THEN** the title, artist, album, duration, position and art are published in one batch

### Requirement: Media Actions
The provider MUST offer the actions `control/previous`, `control/next`, `control/play`, `control/pause` and
`control/playpause`, each without an argument. Each sends the command to the current session and returns done when the
player accepts it, and failed with the reason when there is no session, the player refuses (the matching `can/*` reading
is off) or the call fails. A refused or failed command changes nothing else; the readings follow the player.

#### Scenario: Skip
- **WHEN** a widget invokes `media:control/next` while a player plays
- **THEN** the session receives the command, the result is done, and the title changes when the player reports it

#### Scenario: Nothing to control
- **WHEN** `media:control/playpause` is invoked with no session
- **THEN** the result is failed with the message "nothing is playing"

#### Scenario: Refused
- **WHEN** a player does not support skipping back and `media:control/previous` is invoked
- **THEN** the result is failed with a message that the player does not accept it

### Requirement: Album Art Files
When the session offers a thumbnail, the provider MUST write it to a file and publish the file's path as `art`.

- The folder is a per-run folder under the user's temporary folder, named for the product; leftovers of an earlier run
  are removed when the provider starts, and the folder is removed when it stops
- The file name is derived from the image's content (a hash), so the same art is written once and a new image never
  reuses the path of an older one; a path therefore never changes meaning, which is what lets the icon service cache it
- At most the current art and the previous one are kept; older files are removed
- The thumbnail is read once per change, off the UI thread; while it is read `art` keeps its previous value, and an
  empty value is published when the new track has none, so an old cover is never shown beside a new title
- The title, artist and art are never written to the log

#### Scenario: Same art twice
- **WHEN** two tracks of one album share the cover
- **THEN** one file exists and `art` has the same path for both

#### Scenario: Track without art
- **WHEN** the next track offers no thumbnail
- **THEN** `art` is empty

#### Scenario: Stopped
- **WHEN** the provider stops
- **THEN** the art folder is removed

### Requirement: Platform Seam And Faults
The Windows media session MUST be reached only through an internal interface that tests replace with a fake, so the
provider's logic builds and is tested on Linux. A platform that is unavailable (an older Windows, the session service not
running) or a call that throws makes the readings unavailable with a reason and is logged once per cause, never as a
repeated line; the provider recovers when the service returns. A session that disappears while read ends as "nothing is
playing", not as a fault.

#### Scenario: Service missing
- **WHEN** the platform cannot create the session manager
- **THEN** every reading is unavailable with the reason, one log line names the cause, and the host is unaffected

#### Scenario: Session closes mid-read
- **WHEN** the player closes while the provider reads its properties
- **THEN** `status` becomes `none` and nothing is logged as an error

### Requirement: Media Provider Samples
The provider MUST carry sample data (see data-providers, "First-Party Providers Carry Samples") for a playing track without
art, so that the preview tool, the golden images and `--demo` draw the widget without a player. (The idle state is drawn
from readings the tests supply; a sample that flipped to idle would make `--demo` flip with it.)
The sample provider offers no actions (see provider-actions, "Demo And Sample Providers").

#### Scenario: Preview
- **WHEN** `urdeck-cli preview render --widget urdeck.widgets.nowplaying` runs on Linux
- **THEN** a picture of a playing track is produced from the sample, with no player and no Windows API
