# Widget Weather Specification

## Purpose

Defines the first-party weather widget: the place it shows and how that is configured, what the 4x2 card contains, how
its animated icon is chosen and moves, and the credit it must always show. It shows readings of the `weather`
provider and takes its whole look from the theme and the shared components, except for the icon art.

## Requirements
### Requirement: Weather Widget Identity
UrDeck MUST ship a widget with the type id `urdeck.widgets.weather` that supports the size 4x2 only. It MUST reference
only the widget SDK, MUST NOT reference the weather provider's assembly, and MUST be refreshed by reading changes and
by animation frames only (no timer).

#### Scenario: Size offered
- **WHEN** the registry lists the sizes of `urdeck.widgets.weather`
- **THEN** the only size is 4x2

#### Scenario: No timer
- **WHEN** only weather widgets are placed
- **THEN** the host schedules no per-widget refresh timer for them

### Requirement: Weather Configuration
The widget's settings MUST be:

- `location`: free text naming a place (see weather-provider, "Locations In Reading Ids"); no default
- `latitude`, `longitude`: optional numbers in decimal degrees; when both are present they replace `location`
- `label`: not present (the card shows the place's resolved name), a text (shown as given), or an empty text (no place
  is shown and no space is reserved for it)
- `unit`: optional display unit for temperatures (`celsius`, `fahrenheit`); not present means the region's unit
- `motion`: `periodic` (the default), `full` or `off`; any other value is treated as `periodic`
- `periodSeconds`: optional seconds between loops in the periodic mode

Unknown properties MUST be preserved across load and save. The widget MUST subscribe to the readings of exactly one
location, so two weather widgets with different locations show two places and two with the same location share one
fetch.

#### Scenario: Place typed as text
- **WHEN** a widget has `"location": "Portland, OR"`
- **THEN** it shows the weather of Portland, Oregon, with the place `Portland, Oregon`

#### Scenario: Coordinates win
- **WHEN** a widget has `"location": "Zurich"`, `"latitude": 47.37` and `"longitude": 8.54`
- **THEN** it shows the weather for those coordinates and subscribes to no reading of `Zurich`

#### Scenario: Place name override
- **WHEN** the widget sets `"label": "Home"`
- **THEN** `Home` is shown as the place

#### Scenario: Place hidden
- **WHEN** the widget sets `"label": ""`
- **THEN** no place is drawn and the other content uses the space

#### Scenario: Unit override
- **WHEN** the widget sets `"unit": "fahrenheit"` on a system whose region uses Celsius
- **THEN** every temperature on the card is in Fahrenheit

#### Scenario: Two places
- **WHEN** a page has two weather widgets, one for `Zurich` and one for `Tokyo`
- **THEN** each card shows its own place and its own local sunrise and sunset

#### Scenario: No location
- **WHEN** a widget has no `location` and no coordinates
- **THEN** it subscribes to nothing, the card shows dashes and a neutral icon, and no credit line is drawn because no
  weather data is shown

#### Scenario: Unknown properties survive
- **WHEN** the widget's configuration has a property the widget does not know and is saved
- **THEN** the property is still in the file

### Requirement: Weather Composition
At 4x2 the card MUST show, drawn with the SDK's shared components and the theme's colours, fonts and sizes:

- the animated icon (see "Weather Icon")
- the temperature now, large, with the degree sign raised and the unit the user's setting or region chooses
- the condition as words (`Clear`, `Partly cloudy`, `Cloudy`, `Fog`, `Drizzle`, `Rain`, `Sleet`, `Snow`,
  `Thunderstorm`)
- today's high and low
- the place (see `label` above)
- the sunrise and the sunset, each as a time of day in the place's own local time, formatted by the SDK's formatter
- the credit line (see "Weather Credit Line")

The widget MUST NOT draw a card, a title bar or a background of its own, and MUST NOT name a font or a fixed size. The
temperature text size MUST NOT change as the value changes. A reading that is unavailable, or pending with no value, is
shown as a dash; a stale reading, and a pending reading with a last value, are shown with that value in the theme's
muted colour.

#### Scenario: Full card
- **WHEN** all readings of the location have values
- **THEN** the card shows the icon, the temperature, the condition, high and low, the place, sunrise and sunset, and
  the credit line, none of them clipped

#### Scenario: Temperature changes width
- **WHEN** the temperature goes from `9` to `-12`
- **THEN** the text size is unchanged

#### Scenario: Readings unavailable
- **WHEN** the location matches no place
- **THEN** the temperature, high, low, sunrise and sunset are dashes, the condition and place are empty or dashes,
  the icon is the neutral one and the layout is otherwise unchanged

#### Scenario: Provider stops reporting
- **WHEN** the weather provider fails after earlier samples succeeded
- **THEN** the last values stay on the card in the muted colour

#### Scenario: Region uses a 12 hour clock
- **WHEN** the region uses a 12 hour clock
- **THEN** sunrise and sunset are shown like `6:42` with `AM` or `PM` as a unit

#### Scenario: Theme change
- **WHEN** the active theme changes to one with different fonts and colours
- **THEN** the card follows it with no change to the widget; the icon art is the one exception and does not change

### Requirement: Weather Icon
The widget MUST show an icon chosen from the condition class and whether it is day, taken from the readings
`current/condition` and `current/is-day`. Each pair of class and day or night MUST map to one animation; classes that
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

### Requirement: Weather Motion
The icon MUST move according to the `motion` setting. With `periodic` (the default, and the meaning of any value the widget
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

### Requirement: Weather Credit Line
The card MUST always draw the credit of the data it shows, taken from the attribution in the descriptions of the
readings it subscribes to (see weather-provider, "Weather Attribution"), with the SDK's attribution component. No
setting MAY hide it.

- The credit is drawn even when every reading is unavailable
- Hiding the place with an empty `label`, or setting `motion` to `off`, does not hide the credit
- The shorter attribution text is used only when the full text does not fit the width

#### Scenario: Credit always present
- **WHEN** the widget is drawn with all readings, with none, and with `"label": ""`
- **THEN** the text `Weather data by Open-Meteo.com` is drawn every time

#### Scenario: Description without attribution
- **WHEN** the readings' descriptions carry no attribution (a provider other than `weather` fills in the same
  readings)
- **THEN** no credit line is drawn and the space is used by the content

### Requirement: Weather Repaint
When the widget is not animating it MUST ask to be repainted only when what it would draw has changed: a value text, a
unit, the place, whether a value is current, the condition or day state that chooses the icon, or the credit.

#### Scenario: Value unchanged after rounding
- **WHEN** the temperature goes from 14.2 to 14.4 and the widget shows no decimals
- **THEN** the widget is not repainted

#### Scenario: Condition changes
- **WHEN** the condition goes from `clear` to `cloudy`
- **THEN** the widget is repainted once

#### Scenario: Becomes stale
- **WHEN** the readings go from ok to stale with the same values
- **THEN** the widget is repainted once
