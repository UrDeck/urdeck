## MODIFIED Requirements

### Requirement: Clock Configuration
The Clock widget MUST support the following configuration parameters (set as extra properties on its JSON widget object),
each declared as a setting (see settings-metadata):

- `format` — "24h" or "12h" (default: "24h")
- `showDate` — boolean (default: true)
- `style` — "simple" or "flap" (default: "simple"); any other value is treated as "simple"

The Clock has no colour or size setting: the time's size comes from the card's size and its look from the theme. A
`textColor` or `fontSize` key in the file is ignored and kept in the file as an unknown key.

#### Scenario: Clock renders in 12-hour format
- **WHEN** a widget is configured with `format: "12h"` and the current time is 19:30
- **THEN** the displayed time is "7:30" with the unit "PM"

#### Scenario: Clock hides date line
- **WHEN** a widget is configured with `showDate: false`
- **THEN** only the time is displayed (no date line below)

#### Scenario: Old styling keys are ignored
- **WHEN** a widget object has `"textColor": "#ffcc00"` and `"fontSize": 0.5`
- **THEN** the time is drawn in the theme's colours at the size it fits, and both keys are still in the file after a save

#### Scenario: Style not set
- **WHEN** a widget's configuration has no `style`
- **THEN** the Clock looks and behaves as it did before the setting existed and never reports `IsAnimating`

#### Scenario: Unknown style
- **WHEN** a widget is configured with `style: "neon"`
- **THEN** the Clock uses the `simple` style

### Requirement: Clock Visual Appearance
The Clock widget MUST take its whole appearance from the theme and the shared components:

- The card behind the text is drawn by the host (see widget-card); the Clock draws no card of its own, and in the
  `simple` style no background at all
- Fonts, weights, colours, the date's size and the muted date colour come from the theme; the Clock names no font, no
  fixed size and no colour
- The time/date block is centered horizontally and vertically inside the content rectangle
- In the `flap` style the tiles are opaque, so a flipping flap never shows the half behind it: the tile colour is
  derived from the theme (its text colour at a low opacity over the card and background, as a solid colour), the
  digits use the theme's value typeface and text colour, and the hinge, the crease across each half and the shadow of
  a moving flap are black at partial opacity. The tiles do not follow a translucent card: this is a deliberate
  exception to "take its whole appearance from the theme"

#### Scenario: Clock renders with default theme
- **WHEN** the Clock widget is used with the default theme
- **THEN** the host-drawn card is behind the time in the theme's text colour and the date in the theme's muted text colour

#### Scenario: Clock follows a theme change
- **WHEN** the active theme changes to one with a different font and text colour
- **THEN** the Clock shows the new font and colour without any change to the widget

#### Scenario: Flap tiles follow the theme
- **WHEN** the active theme changes while the Clock uses the `flap` style
- **THEN** the tile colour, corner radius and digits follow the new theme, and the tiles are opaque on an opaque and on a translucent card

#### Scenario: Theme value fill is kept
- **WHEN** the Clock is drawn with a theme whose value text has a gradient fill
- **THEN** the time is drawn with that fill
