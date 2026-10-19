# Widget Weather Specification

## Purpose

Defines the first-party weather widget: the place it shows and how that is configured, what the 4x2 card contains, how
its animated icon is chosen and moves, and the credit it must always show. It shows readings of the `weather`
provider and takes its whole look from the theme and the shared components, except for the icon art.
## Requirements
### Requirement: Weather Widget Identity
UrDeck MUST ship a widget with the type id `urdeck.widgets.weather` that supports the sizes 4x2 and 4x4. It MUST
reference only the widget SDK, MUST NOT reference the weather provider's assembly, and MUST be refreshed by reading
changes and by animation frames only (no timer).

#### Scenario: Size offered
- **WHEN** the registry lists the sizes of `urdeck.widgets.weather`
- **THEN** the sizes are 4x2 and 4x4

#### Scenario: No timer
- **WHEN** only weather widgets are placed
- **THEN** the host schedules no per-widget refresh timer for them

### Requirement: Weather Configuration
The widget's settings MUST be:

- `location`: free text naming a place (see weather-provider, "Locations In Reading Ids"); no default
- `latitude`, `longitude`: optional numbers in decimal degrees; when both are present they replace `location`
- `label`: not present (the card shows the place's resolved name), a text (shown as given), or an empty text (no place
  is shown and no space is reserved for it)
- `unit`: optional display unit for the card, `celsius` or `fahrenheit`; it chooses the unit of temperatures and, with
  it, the system of wind speeds and precipitation depths (`celsius` is kilometres per hour, millimetres and
  centimetres, `fahrenheit` is miles per hour and inches); not present means the region's
- `motion`: `periodic` (the default), `full` or `off`; any other value is treated as `periodic`
- `periodSeconds`: optional seconds between loops in the periodic mode
- `days`: at 4x4, how many forecast days the card shows, a whole number from 1 to 7, 5 when not present; a number
  outside the range is the nearest end of it, and a value that is not a number counts as not present
- `animateForecast`: at 4x4, `true` lets the icons of the forecast days move as `motion` says, `false` or not present
  shows them still

Unknown properties MUST be preserved across load and save. The widget MUST subscribe to the readings of exactly one
location, so two weather widgets with different locations show two places and two with the same location share one
fetch. At 4x2 `days` and `animateForecast` have no effect and are kept in the file.

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
- **THEN** every temperature on the card is in Fahrenheit, wind is in miles per hour and snow and rain are in inches

#### Scenario: Metric override
- **WHEN** the widget sets `"unit": "celsius"` on a system whose region uses Fahrenheit
- **THEN** temperatures are in Celsius, wind is in kilometres per hour, rain is in millimetres and snow is in centimetres

#### Scenario: Two places
- **WHEN** a page has two weather widgets, one for `Zurich` and one for `Tokyo`
- **THEN** each card shows its own place and its own local sunrise and sunset

#### Scenario: Default number of days
- **WHEN** a 4x4 widget has no `days`
- **THEN** it shows five forecast days

#### Scenario: Days out of range
- **WHEN** a 4x4 widget sets `"days": 12`, then `"days": 0`
- **THEN** it shows seven forecast days, then one

#### Scenario: Days is not a number
- **WHEN** a 4x4 widget sets `"days": "many"`
- **THEN** it shows five forecast days and the text is still in the file after a save

#### Scenario: No location
- **WHEN** a widget has no `location` and no coordinates
- **THEN** it subscribes to nothing, the card shows dashes and a neutral icon, and no credit line is drawn because no
  weather data is shown

#### Scenario: Unknown properties survive
- **WHEN** the widget's configuration has a property the widget does not know and is saved
- **THEN** the property is still in the file

#### Scenario: Forecast settings at 4x2
- **WHEN** a 4x2 widget sets `"days": 3` and `"animateForecast": true`
- **THEN** the card is the 4x2 card and both settings are still in the file after a save

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
unit, the place, whether a value is current, the condition or day state that chooses an icon, a forecast day's values,
the snow total, or the credit.

#### Scenario: Value unchanged after rounding
- **WHEN** the temperature goes from 14.2 to 14.4 and the widget shows no decimals
- **THEN** the widget is not repainted

#### Scenario: Condition changes
- **WHEN** the condition goes from `clear` to `cloudy`
- **THEN** the widget is repainted once

#### Scenario: Becomes stale
- **WHEN** the readings go from ok to stale with the same values
- **THEN** the widget is repainted once

#### Scenario: Forecast day changes
- **WHEN** the snow expected on the third forecast day goes from 4.3 centimetres to 6 centimetres on a 4x4 card
- **THEN** the widget is repainted once, and a change on the sixth day is not a repaint when the card shows five days

### Requirement: Weather Forecast Composition
At 4x4 the card MUST show the composition of the 4x2 card (see "Weather Composition") in its upper part, and below it,
drawn with the SDK's shared components and the theme's colours, fonts and sizes:

- a strip of the readings of now: what the temperature feels like, the relative humidity, and the wind (the speed with
  its unit and the compass point it blows from)
- the snow total of the forecast days shown (see "Weather Snow Total")
- one column per forecast day, from today for as many days as `days` says, in this order within a column: the weekday
  of the day at the place, the day's icon (see "Weather Forecast Icons"), the high and the low, the chance of
  precipitation, the rain expected and the snow expected
- the credit line (see "Weather Credit Line"), drawn once for the whole card

A rain amount is drawn in a column only when the rain reading of that day is available and positive, and likewise a snow
amount; a column that has both draws both, one above the other, and a column that has neither draws neither. The space
of a missing amount stays empty, so that the weekdays, icons, temperatures and chances line up across columns. A trace
amount is drawn as the formatter shows it (`<0.1`). A forecast value that is unavailable, or pending with no value, is
shown as a dash, and a stale one in the theme's muted colour, as on the 4x2 card.

The widget MUST NOT draw a card, a title bar or a background of its own, MUST NOT name a font or a fixed size, and MUST
NOT depend on the resolution: every size follows the theme and the card's size. No part of the card MAY be clipped for
any number of days from 1 to 7.

#### Scenario: Full card
- **WHEN** all readings of the location have values and `days` is 5
- **THEN** the card shows the 4x2 content, the strip of now, the snow total, five forecast columns and the credit line,
  none of them clipped

#### Scenario: Seven days
- **WHEN** `days` is 7
- **THEN** seven columns are drawn, none clipped and none overlapping

#### Scenario: One day
- **WHEN** `days` is 1
- **THEN** one column, for today, is drawn

#### Scenario: Mixed rain and snow
- **WHEN** a forecast day expects 7 millimetres of rain and 7.5 centimetres of snow
- **THEN** its column shows both amounts, in the user's units, and the amounts of the other columns stay aligned

#### Scenario: Dry day
- **WHEN** a forecast day expects no rain and no snow
- **THEN** its column shows the weekday, icon, temperatures and chance, and no amount

#### Scenario: Trace of snow
- **WHEN** a forecast day expects 0.1 centimetres of snow and the unit is imperial
- **THEN** its snow amount is drawn as `<0.1"`

#### Scenario: Weekday at the place
- **WHEN** the place is a day ahead of the PC
- **THEN** the first column's weekday is the place's today, not the PC's

#### Scenario: Day without data
- **WHEN** the readings of one forecast day are unavailable
- **THEN** that column shows dashes and the other columns are unchanged

#### Scenario: Theme change
- **WHEN** the active theme changes to one with different fonts and colours
- **THEN** the 4x4 card follows it with no change to the widget

### Requirement: Weather Snow Total
The 4x4 card MUST show how much snow is expected over the forecast days it shows: the sum of the snow readings of
those days, from today, formatted by the SDK's formatter in the user's units. The total is for the days shown, so
changing `days` changes it. If the snow reading of any day shown is unavailable or has no value, the total MUST be shown
as a dash, never as a partial sum; a stale value counts as a value and is drawn muted. A total of zero is shown as `0`,
and a positive total below the trace limit as `<0.1`. The total's label says how many days it covers.

#### Scenario: Total of five days
- **WHEN** the snow of the five days shown is 0, 5.4, 10.9, 7.1 and 0 centimetres and the unit is imperial
- **THEN** the total is 23.4 centimetres, shown as `9.2"`

#### Scenario: Fewer days, smaller total
- **WHEN** `days` goes from 5 to 2 with the values above
- **THEN** the total is 5.4 centimetres, shown as `2.1"`

#### Scenario: A day is missing
- **WHEN** the snow reading of the fourth of five shown days is unavailable
- **THEN** the total is a dash, not the sum of the other four

#### Scenario: A day beyond the shown days is missing
- **WHEN** `days` is 5 and the snow reading of the sixth day is unavailable
- **THEN** the total is the sum of the five days shown

#### Scenario: No snow expected
- **WHEN** every shown day expects zero snow
- **THEN** the total is shown as `0`

#### Scenario: Metric total
- **WHEN** the unit is metric and the total is 23.4 centimetres
- **THEN** the total is shown as `23` with the unit `cm`

### Requirement: Weather Forecast Icons
Each forecast day MUST show an icon chosen from that day's condition class, using the daytime variant of the same
mapping as the main icon (a day's condition is not a night's). A forecast icon is the still poster frame unless
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

### Requirement: Weather Sections
The Weather widget MUST draw its sections with the SDK's parts: the rule between the current conditions and the forecast
at 4x4 with the divider component, each forecast day's column on the theme's well, and the condition text on the theme's
chip. A theme that sets none of the three gets the card exactly as it was before: the same rule, no backing behind a
day, the condition as plain text. A well is drawn for each of the shown days with the day's whole column inside it, and
the column's contents keep their order and alignment.

#### Scenario: Forecast days on a well
- **WHEN** the theme sets a well with a faint fill and the card shows five forecast days
- **THEN** each day's column is drawn on a rounded backing and the weekdays, icons and temperatures line up as before

#### Scenario: Condition on a pill
- **WHEN** the theme sets the chip shape `pill`
- **THEN** the condition text is drawn inside a pill

#### Scenario: Divider from the theme
- **WHEN** the theme sets the divider style `gradient` and a 4x4 card is drawn
- **THEN** the rule between the current conditions and the forecast fades along its length

#### Scenario: A v1 theme
- **WHEN** the card is drawn under `default-dark`
- **THEN** the picture is unchanged from before this requirement

