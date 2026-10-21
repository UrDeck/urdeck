## ADDED Requirements

### Requirement: Weather Themed Icons
When the theme names an icon set (see widget-sdk, "Theme Carries The Icon Values"), the widget MUST draw the icon of the
current condition and of each forecast day as a themed glyph (see components, "Themed Icon") and MUST NOT use the animations
of "Weather Icon" for any of them.

- The key is `weather/` plus the same name the animations use: the condition class and day or night for the main icon, the
  daytime variant for a forecast day, and `not-available` for a condition that is unavailable or `unknown`
- The glyph is drawn into the rectangle the animation would take, in the theme's tint and stroke, with the theme's glow when it
  has one
- With the tint `ramp`, the position along the ramp is the temperature mapped from -10 to 40 degrees Celsius and clamped: the
  current temperature for the main icon and the day's high for a forecast day, whatever unit the card shows; with no
  temperature the position is 0.5
- The glyphs are pictures: the widget loads no animation, keeps no poster image, never reports `IsAnimating`, reports no time for
  a next loop, and asks the host for no animation frames; the `motion`, `periodSeconds` and `animateForecast` settings have no
  effect
- The card is repainted only when what it shows changes, as for any other content
- A glyph the lookup cannot answer draws no icon, is logged once and leaves the rest of the card unchanged
- A snapshot shows the same glyphs as the window

#### Scenario: Outline icon
- **WHEN** the theme names `outline` and the condition is `rain`
- **THEN** the `weather/rain` glyph is drawn in the theme's tint and no animation is loaded

#### Scenario: Night variant
- **WHEN** the condition is `clear` and `is-day` is off
- **THEN** the `weather/clear-night` glyph is drawn

#### Scenario: Forecast uses the daytime variant
- **WHEN** a forecast day is `clear` and it is night at the place now
- **THEN** that column shows `weather/clear-day`

#### Scenario: Ramp by temperature
- **WHEN** the tint is `ramp` and the temperature is 15 degrees Celsius, or 59 degrees Fahrenheit
- **THEN** the glyph is drawn at the position 0.5 along the theme's ramp

#### Scenario: No animation work
- **WHEN** the card is painted with motion set to `full`
- **THEN** `IsAnimating` is `false`, no next animation time is reported and no poster image is held

#### Scenario: Unavailable condition
- **WHEN** the condition is unavailable
- **THEN** the `weather/not-available` glyph is drawn

### Requirement: Weather Forecast Icon Size
The icon of a forecast day MUST keep a readable size: at least 0.6 of its column's width, or all the card leaves it. The five
text rows of a column keep their usual pitch (1.5 times the title text size) while the icon still gets that much, and are
tightened, to no less than 1.2 times the title text size, when they would leave it less. This applies to every icon, animated or
themed.

#### Scenario: A large title size
- **WHEN** the theme's title size leaves less than 0.6 of a column for the icon at the usual row pitch
- **THEN** the rows are tightened and the icon is drawn larger than it would be at the usual pitch

#### Scenario: Room to spare
- **WHEN** the usual row pitch leaves the icon 0.6 of a column or more
- **THEN** the rows keep their usual pitch and the icon is as before

## MODIFIED Requirements

### Requirement: Weather Icon
When the theme names no icon set, the widget MUST show an icon chosen from the condition class and whether it is day, taken
from the readings `current/condition` and `current/is-day`. (When the theme names a set, "Weather Themed Icons" applies and
this requirement does not.) Each pair of class and day or night MUST map to one animation; classes that
look the same by day and by night MAY map to the same animation. The animations MUST be shipped inside the widget's own
assembly, MUST be drawn with the SDK's Lottie component, and MUST be loaded only when needed.

- A condition that is unavailable, pending with no value or `unknown` shows a neutral icon
- When the condition changes the icon changes on the next paint, and the previous animation is released
- A missing or unreadable animation draws no icon, is logged once, and leaves the rest of the card unchanged
- The widget MUST NOT keep more animations loaded than it shows

#### Scenario: Clear day and clear night
- **WHEN** the condition is `clear` and `is-day` is on, then off
- **THEN** a day icon is shown, then a night icon

#### Scenario: Condition changes
- **WHEN** the condition goes from `cloudy` to `rain`
- **THEN** the rain icon is shown and the cloud icon is released

#### Scenario: Unknown condition
- **WHEN** the condition is `unknown` or unavailable
- **THEN** the neutral icon is shown

#### Scenario: Animation missing
- **WHEN** the animation for a condition cannot be read
- **THEN** no icon is drawn, one line is logged, and the other content is unaffected

#### Scenario: Theme without a set
- **WHEN** the theme names no icon set
- **THEN** the animations are shown exactly as before the themed icons existed

### Requirement: Weather Motion
When the theme names no icon set, the icon MUST move according to the `motion` setting. (With a set the icons are pictures
and "Weather Themed Icons" applies.) With `periodic` (the default, and the meaning of any value the widget
does not know) it plays its loop once at the start and then once every period (the `periodSeconds` setting, 60 seconds by
default, never less than a loop and a second), and between loops it shows the poster frame, a fixed frame of the animation,
and the widget MUST NOT report `IsAnimating`; it reports when the next loop starts so that the host paints it then. With
`full` the icon loops continuously and the widget reports `IsAnimating` while an icon is shown. With `off` the widget draws the
poster frame and never animates. While it animates the widget MUST ask for a frame interval of about 1/15 second.

- The loop is timed from the icon's first paint and computed from the render time; a render time before that start (a clock
  change) is drawn as the poster frame and the timing restarts there
- The first paint, the paint of a new icon, and `--snapshot` MUST draw the poster frame, so a snapshot is repeatable
- Frames do not refresh data, and a page with no weather widget MUST pay nothing for any of this

#### Scenario: Periodic loop
- **WHEN** `motion` is absent and the icon is shown
- **THEN** the widget animates for one loop, then reports it is not animating and the time of the next loop, and the icon is
  the poster frame until then

#### Scenario: Full motion
- **WHEN** `motion` is `full` and the icon is shown
- **THEN** the widget reports `IsAnimating` and the icon advances with the render time and repeats at the end of the animation

#### Scenario: Motion off
- **WHEN** `motion` is `off`
- **THEN** `IsAnimating` returns `false`, no next start is reported and the icon is the poster frame in every paint

#### Scenario: First paint
- **WHEN** the widget is painted for the first time or in `--snapshot`
- **THEN** the icon is the poster frame

#### Scenario: Clock change
- **WHEN** the render time jumps back by an hour while the icon loops
- **THEN** the icon is drawn at the poster frame and then continues

#### Scenario: Unknown motion value
- **WHEN** `motion` is `"slow"`
- **THEN** the widget behaves as `periodic`, and `slow` is still in the file after a save

#### Scenario: Theme with a set
- **WHEN** the theme names an icon set and `motion` is `full`
- **THEN** the widget does not report `IsAnimating`

### Requirement: Weather Forecast Icons
When the theme names no icon set, each forecast day MUST show an icon chosen from that day's condition class, using the daytime
variant of the same mapping as the main icon (a day's condition is not a night's). (With a set, "Weather Themed Icons"
applies.) A forecast icon is the still poster frame unless
`animateForecast` is `true`; then it moves as `motion` says, in step with the main icon (the same loop timing, the
same frame interval and, in the periodic mode, the same loop start), and the widget MUST report that it is animating
while any of its icons is. The widget MUST NOT keep more animations loaded than it shows, a missing or unreadable
forecast animation draws no icon, is logged once and leaves the rest of the column unchanged, and a day whose condition
is unavailable shows the neutral icon.

#### Scenario: Forecast icons still by default
- **WHEN** the 4x4 card has `motion` set to `full` and no `animateForecast`
- **THEN** the main icon animates and the forecast icons are the poster frame in every paint

#### Scenario: Forecast icons animate when asked
- **WHEN** the 4x4 card sets `"animateForecast": true` and `motion` is `full`
- **THEN** the forecast icons loop together with the main icon

#### Scenario: Motion off wins
- **WHEN** the 4x4 card sets `"animateForecast": true` and `"motion": "off"`
- **THEN** every icon is the poster frame and the widget never animates

#### Scenario: Daytime variant
- **WHEN** a forecast day's condition is `clear` and it is night at the place now
- **THEN** the forecast icon is the clear day icon

#### Scenario: Forecast day unavailable
- **WHEN** a forecast day's condition is unavailable
- **THEN** the neutral icon is shown in its column

#### Scenario: First paint and snapshot
- **WHEN** the card is painted for the first time or in `--snapshot` with `animateForecast` set
- **THEN** every icon is the poster frame
