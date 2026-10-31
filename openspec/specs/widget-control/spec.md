# widget-control Specification

## Purpose
Defines the control widget: a tile that shows a reading's state and performs an action when tapped, for any data provider,
with its defaults taken from the reading's description, its styles and sizes, and the feedback it gives while an action
runs or after it fails.
## Requirements
### Requirement: Control Widget Identity
The product MUST ship a first-party widget with the type id `urdeck.widgets.control`, the sizes 1x1 and 2x1 and the
category "Control". It references only the widget SDK, names no provider and no provider type, uses the
`[RefreshOnData]` strategy and accepts taps. It can be placed in the dock. It draws no card, names no font and takes its
whole look from the theme.

#### Scenario: Registered
- **WHEN** the host starts with the first-party plugins
- **THEN** `urdeck.widgets.control` is registered with the sizes 1x1 and 2x1

#### Scenario: Idle costs nothing
- **WHEN** the reading does not change and nothing is touched
- **THEN** the widget is not repainted

### Requirement: Control Configuration
The settings MUST be flat, typed properties on the widget object, each with a default:

| Setting | Type | Default | Meaning |
|---|---|---|---|
| `reading` | text (reading id) | none | The state shown |
| `action` | text (action id) | from the reading | What a tap does |
| `label` | text | from the reading | The name shown |
| `icon` | text (icon key or image file) | from the reading | The icon shown |
| `style` | `switch` or `dimmer` | `switch` | How it is drawn |
| `level` | text (reading id) | none | `dimmer` only: the level shown on the bar, 0 to 100 |
| `levelAction` | text (action id) | from `level` | `dimmer` only: what a tap on the bar does, with the level as argument |

`action`, `label`, `icon` and `levelAction` follow one rule: absent means the value from the reading's description (its
natural action, its label, its icon; `levelAction` from the `level` reading's description), a text is used as given, and
an empty text means none. An action written without a provider id (`light.desk/toggle`) is taken under the reading's
provider. An unknown `style` counts as `switch`.

#### Scenario: Only a reading
- **WHEN** the configuration is `{"typeId": "urdeck.widgets.control", "reading": "homeassistant:light.desk"}`
- **THEN** the tile shows the light's friendly name, the `home/light` icon and its state, and a tap toggles it

#### Scenario: Shortened label
- **WHEN** `label` is `Stair Underglow` and the reading's label is `Basement stairs underglow light switch`
- **THEN** the tile shows `Stair Underglow`, also after the reading is described again

#### Scenario: Display-only
- **WHEN** `action` is `""`, or the reading's description names no action
- **THEN** the tile accepts no tap and the host shows no press on it

#### Scenario: Action without a reading
- **WHEN** the configuration has `action` `homeassistant:scene.movie_night/toggle`, a `label` and no `reading`
- **THEN** the tile is neutral (neither on nor off) and a tap runs the scene

### Requirement: State
The tile MUST show the reading as on, off or neutral:

- An on/off value is on when on
- A text value is on when it is `on`, `open`, `unlocked`, `playing` or `home` (ignoring case), and off otherwise
- A number is on when it is above zero
- No reading configured: neutral. A pending reading keeps the last state it showed; a stale reading is drawn as the theme
  draws stale values; an unavailable reading is drawn neutral with the reason in the state line

The icon and the state text use the theme's accent colour when on, its muted text colour when off and its text colour
when neutral. The state line says the reading formatted as any widget formats it (`On`, `Off`, `22.5°`, `Playing`).

#### Scenario: Light off
- **WHEN** `homeassistant:light.desk` is off
- **THEN** the icon and `Off` are drawn in the theme's muted text colour

#### Scenario: Media
- **WHEN** the reading is `media:status` and its value is `playing`
- **THEN** the tile is drawn on

### Requirement: Tap And Pending
A tap MUST invoke the action (see provider-actions) and put the tile in a pending look until one of these happens:

- the reading's value changes: the tile shows the new state
- the action completes done and the value does not change within 1 second: the tile returns to its state
- the action completes failed, or the provider's action time limit passes: the tile shows a failed look in the theme's
  critical colour for 2 seconds, then returns to its state

While pending the tile accepts no tap, so taps do not queue. The pending look uses the theme's dimmed accent and a small
mark; with motion reduced the mark does not move.

#### Scenario: Toggle
- **WHEN** the user taps an off light and the provider reports it on 200 milliseconds later
- **THEN** the tile is pending for those 200 milliseconds and then shows on

#### Scenario: Tapping fast
- **WHEN** the user taps a tile five times within half a second
- **THEN** one action is invoked

#### Scenario: Failure
- **WHEN** the action fails
- **THEN** the tile shows the failed look for 2 seconds and the host's log has the action's failure line

### Requirement: Switch Style
The `switch` style MUST draw, from the card's content rectangle only, the icon, the label and the state line: stacked at
1x1, in a row at 2x1. The label shrinks to fit and is dropped before the state line when there is no room; without an
icon the label and state take the space; the icon is drawn with the theme's icon values (see widget-sdk, "Theme Carries
The Icon Values").

#### Scenario: Long label at 1x1
- **WHEN** the label is longer than the tile
- **THEN** it shrinks to fit and does not overflow the card

### Requirement: Dimmer Style
At 2x1 with `level` set, the `dimmer` style MUST draw an on/off button on the left (the `switch` drawing of `reading`,
with its action) and a level bar on the rest, drawn with the theme's bar gauge from the `level` reading (0 to 100). A tap
on the bar at a fraction f of its length invokes `levelAction` with the argument round(100·f), with taps in the outer 4 %
at each end giving 0 and 100. While pending, a tap on the bar is kept (only the latest) and sent when pending ends. At
1x1, or without `level`, `dimmer` draws as `switch`; a missing `level` is logged once.

#### Scenario: Set brightness
- **WHEN** the user taps the bar of a `dimmer` on `homeassistant:light.desk` with `level`
  `homeassistant:light.desk/brightness` at 70 % of its length
- **THEN** `homeassistant:light.desk/turn_on` is invoked with the argument 70, and the bar shows the new level when the
  provider reports it

#### Scenario: Taps on the bar while pending
- **WHEN** the user taps the bar at 30 %, 50 % and 80 % within one pending period
- **THEN** two actions are invoked, with 30 and then 80

#### Scenario: Dimmer at 1x1
- **WHEN** a `dimmer` is placed at 1x1
- **THEN** it draws and behaves as a `switch`

### Requirement: Control Repaint Rules
The tile MUST be repainted only when what it shows changes: on its first paint, when its configuration changes, when the
value or description of a reading it shows changes, when it enters or leaves the pending look, when the failed look ends,
and when an icon it asked for becomes ready. A tap alone causes no repaint until the tile enters the pending look.

#### Scenario: A reading of another tile changes
- **WHEN** a reading the tile does not show changes
- **THEN** the tile is not repainted

