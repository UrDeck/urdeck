## Purpose

Defines the now-playing widget: a card that shows what the current media session plays and lets the user skip back,
play or pause, and skip forward by touch.

## ADDED Requirements

### Requirement: Now Playing Widget Identity
The product MUST ship a first-party widget with the type id `urdeck.widgets.nowplaying`, the size 4x2 and the category
"Media". It references only the widget SDK, reads the `media` provider through reading ids, names no provider type, uses
the `[RefreshOnData]` strategy and accepts taps. It draws no card, names no font and takes its whole look from the theme.

#### Scenario: Registered
- **WHEN** the host starts with the first-party plugins
- **THEN** `urdeck.widgets.nowplaying` is registered with the size 4x2

#### Scenario: Idle costs nothing
- **WHEN** nothing plays and nothing is touched
- **THEN** the widget is not repainted

### Requirement: Now Playing Configuration
The settings MUST be flat, typed properties on the widget object, each with a default:

| Setting | Type | Default | Meaning |
|---|---|---|---|
| `showAlbum` | on/off | on | Show the album line when there is room |
| `showApp` | on/off | off | Show the name of the player |
| `idleLabel` | text | `Nothing playing` | What the card says when no session exists |

The widget subscribes to the `media` readings it draws and to nothing else. The provider is not a setting.

#### Scenario: Minimal
- **WHEN** the configuration is only the type id
- **THEN** the widget draws with every default

### Requirement: Now Playing Composition
At 4x2 the widget MUST draw, from the card's content rectangle only and with no resolution assumption:

- The album art in a square on the left through the image tile, with the themed placeholder while it loads and when there is
  none; the widget releases the art source it no longer shows (see icons, "A Widget Can Release A Source")
- Beside it the title, the artist and, when `showAlbum` is on and the height allows, the album, each shrinking to fit and
  none overflowing; with `showApp` the player's name as a small status line
- A progress bar from the position and the duration, absent when the duration is 0
- Below, a row of three buttons laid out by `ButtonRow`: previous, play-or-pause (the glyph follows `status`: pause while
  playing, play otherwise) and next, drawn with the theme's media glyphs, with a text fallback when the theme has none
- A button whose `can/*` reading is off is drawn dimmed and does not react to a tap
- With `status` `none` the card shows the themed placeholder and `idleLabel`, no progress and no buttons

#### Scenario: Playing
- **WHEN** a track plays with art
- **THEN** the card shows the art, title, artist, a progress bar and the buttons with the pause glyph in the middle

#### Scenario: Long title
- **WHEN** the title is longer than the line
- **THEN** it shrinks to fit and does not overflow into the art or the card's edge

#### Scenario: Nothing playing
- **WHEN** `status` is `none`
- **THEN** the card shows the placeholder and `Nothing playing` and no buttons

#### Scenario: Previous unavailable
- **WHEN** `can/previous` is off
- **THEN** the previous button is dimmed and a tap on it does nothing

### Requirement: Progress And Repaint
The widget MUST compute the shown position as `position` plus the time since `position/at` while `status` is `playing`, and
as `position` otherwise, clamped to the duration. It repaints once a second only while `status` is `playing` and the
progress bar is shown; otherwise it repaints only when a reading it draws changes. It never polls the provider.

#### Scenario: Playing
- **WHEN** a track plays for ten seconds with no other change
- **THEN** the progress advanced ten seconds and the widget was repainted about once a second

#### Scenario: Paused
- **WHEN** the player pauses
- **THEN** the progress holds its value and the widget is repainted once, not every second

### Requirement: Now Playing Taps
The widget MUST implement `ITapTarget`. `CanTap` is true only for a point inside a button whose command is available. A tap
on a button invokes the matching action (`media:control/previous`, `media:control/playpause` or `media:control/next`)
through the host's action service and returns at once; the widget keeps no optimistic state, so the card changes when the
provider publishes the new state. A tap anywhere else does nothing. The result is not awaited; a failure is already in the
log.

#### Scenario: Tap play-pause
- **WHEN** the user taps the middle button while a track plays
- **THEN** `media:control/playpause` is invoked once, and the glyph changes when `status` changes

#### Scenario: Tap on the art
- **WHEN** the user taps the album art
- **THEN** no action is invoked and the card shows no press feedback

#### Scenario: Tap in the gap between buttons
- **WHEN** the point lies between two buttons
- **THEN** the nearer button is hit and not both; a point outside the group hits none
