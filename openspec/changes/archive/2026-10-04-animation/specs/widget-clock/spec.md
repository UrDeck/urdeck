## ADDED Requirements

### Requirement: Split-Flap Style
With `style: "flap"` the Clock MUST show the time as a split-flap display:

- Four digit tiles, two for the hour and two for the minute, with a colon between the pairs. In the 12-hour format
  the first tile is blank for hours 1 to 9, and `AM`/`PM` is drawn as text beside the tiles, not on a tile
- All tiles have the same size, so the layout does not change as digits change; the tiles are as large as fit the
  content rectangle (scaled by `fontSize`), and the date line, when shown, is below them as in the `simple` style
- When the displayed time changes, each tile whose character changed flips from the old character to the new one over
  about half a second; tiles whose character did not change do not move
- The Clock reports `IsAnimating` only while a flip is in progress, and its last frame is the resting state
- The first paint, and the first paint after a configuration change, show the current time at rest with no flip
- A flip's progress is computed from the render time; if the render time is before the flip's start or past its end
  (a clock change, a resume from sleep), the tiles are drawn at rest
- The flap style is available at every supported size

#### Scenario: Minute changes
- **WHEN** the displayed time changes from `10:41` to `10:42` in the flap style
- **THEN** only the last tile flips, the Clock reports `IsAnimating` until the flip ends, and then shows `10:42` at rest

#### Scenario: Several digits change
- **WHEN** the displayed time changes from `09:59` to `10:00`
- **THEN** all four tiles flip

#### Scenario: Flip finished
- **WHEN** the Clock is painted with a time more than the flip duration after the change
- **THEN** it draws the new time at rest and `IsAnimating` returns `false`

#### Scenario: First paint
- **WHEN** a flap-style Clock is painted for the first time (when placed, or in `--snapshot`)
- **THEN** it shows the current time at rest and `IsAnimating` returns `false`

#### Scenario: Idle between minutes
- **WHEN** a flap-style Clock ticks again within the same displayed minute and no flip is in progress
- **THEN** `NeedsRender` returns `false` and the widget is not repainted

#### Scenario: Single-digit hour in 12-hour format
- **WHEN** the format is `12h`, the style is `flap` and the time is 19:30
- **THEN** the tiles show a blank, `7`, `3`, `0` and `PM` is drawn as text

## MODIFIED Requirements

### Requirement: Clock Display
The Clock widget (`typeId` `urdeck.widgets.clock`) MUST display the current time and date on its SkiaSharp surface:

- Primary display: `HH:mm` (24-hour clock by default, configurable to 12-hour `h:mm` with `AM`/`PM` shown as a unit)
- Secondary display: Date line, formatted `ddd MMM d, yyyy`
- In the `simple` style the time is drawn with the shared readout component; in the `flap` style it is drawn as
  split-flap tiles (see "Split-Flap Style"). In both styles the date is drawn with the shared text line component
  (see the `components` capability) and everything is centered horizontally
- Color: the config `TextColor` override if valid, otherwise the theme's text colour
- Supported sizes: 4×2, 4×1, 2×1 and 1×1

#### Scenario: Clock ticks every second but repaints once a minute
- **WHEN** the Clock widget is configured with `[RefreshOnTick(1, TimeUnit.Seconds)]`
- **THEN** it refreshes every second so the minute rollover is timely, but `NeedsRender` returns `true` only when the displayed minute changes, so its refresh policy repaints it at most once a minute

#### Scenario: Clock renders at different sizes
- **WHEN** the Clock widget is placed at different grid sizes (e.g., 4×2, 4×1, 2×1, 1×1)
- **THEN** the time is as large as fits the content rectangle, the date uses the theme's title size, and neither is clipped on narrow sizes

#### Scenario: Time does not resize as digits change
- **WHEN** the displayed time changes from `11:11` to `20:00`
- **THEN** the time is drawn at the same text size

### Requirement: Clock Configuration
The Clock widget MUST support the following configuration parameters (set as extra properties on its JSON widget object):

- `format` — "24h" or "12h" (default: "24h")
- `showDate` — boolean (default: true)
- `textColor` — hex color override (default: use theme color)
- `fontSize` — float multiplier applied to the time's fitted size (default: 1.0); the time never exceeds the content rectangle, so values above 1.0 have no further effect
- `style` — "simple" or "flap" (default: "simple"); any other value is treated as "simple"

#### Scenario: Clock renders in 12-hour format
- **WHEN** a widget is configured with `format: "12h"` and the current time is 19:30
- **THEN** the displayed time is "7:30" with the unit "PM"

#### Scenario: Clock hides date line
- **WHEN** a widget is configured with `showDate: false`
- **THEN** only the time is displayed (no date line below)

#### Scenario: Font size multiplier below one
- **WHEN** a widget is configured with `fontSize: 0.5`
- **THEN** the time is drawn at half the size it would otherwise fit at

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
- Fonts, weights, the date's size and the muted date colour come from the theme; the Clock names no font and no fixed
  size
- The time/date block is centered horizontally and vertically inside the content rectangle
- In the `flap` style the tiles are opaque, so a flipping flap never shows the half behind it: the tile colour is
  derived from the theme (its text colour at a low opacity over the card and background, as a solid colour), the
  digits use the theme's value typeface and text colour (or the `textColor` override), and the hinge, the crease
  across each half and the shadow of a moving flap are black at partial opacity. The tiles do not follow a translucent
  card: this is a deliberate exception to "take its whole appearance from the theme"

#### Scenario: Clock renders with default theme
- **WHEN** the Clock widget is used with the default theme
- **THEN** the host-drawn card is behind the time in the theme's text colour and the date in the theme's muted text colour

#### Scenario: Clock follows a theme change
- **WHEN** the active theme changes to one with a different font and text colour
- **THEN** the Clock shows the new font and colour without any change to the widget

#### Scenario: Flap tiles follow the theme
- **WHEN** the active theme changes while the Clock uses the `flap` style
- **THEN** the tile colour, corner radius and digits follow the new theme, and the tiles are opaque on an opaque and on a translucent card
