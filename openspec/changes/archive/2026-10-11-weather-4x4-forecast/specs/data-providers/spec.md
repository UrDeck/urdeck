## MODIFIED Requirements

### Requirement: Reading Values
A reading's value MUST be one of: a number, a text, an on/off state, or a time. A time is an instant together with the
UTC offset it is to be read in, so that a sunrise at another place can be shown in that place's local time; a date is
the same value, read as the day that instant falls on in that offset. Values MUST be of types defined by the SDK, so
that a widget can read a value from any provider without referencing that provider's assembly. Two time values are
equal only when both the instant and the offset are equal.

#### Scenario: Widget reads a value from an unrelated provider
- **WHEN** a widget built without any reference to a provider's assembly reads one of that provider's readings
- **THEN** it receives the value as a number, a text, an on/off state or a time

#### Scenario: Time keeps its offset
- **WHEN** a provider publishes the instant 2026-10-08 06:42 at UTC offset +09:00
- **THEN** a widget that reads it gets that instant and the offset +09:00

#### Scenario: Same instant, other offset
- **WHEN** a provider publishes the same instant first with offset +09:00 and then with offset +02:00
- **THEN** the second publication counts as a change and widgets that show the reading are repainted

### Requirement: Reading Catalog
A provider MUST describe every reading it offers. A description MUST contain the reading's path and kind, a short
default label, and a full name; it MAY contain the name of the device the reading belongs to, a range (minimum and
maximum), a warning value and a critical value, a unit text for plain numbers, a canonical unit for depths, a default
display unit, a number of decimals and an attribution.

- The kinds are: plain number, percentage, temperature, text, on/off, time, date, speed and depth; a temperature is
  always reported in degrees Celsius and a speed in kilometres per hour
- A date is a time value shown as a day (see "Reading Formatting"); the provider reports it as the instant at which the
  day begins at the place the reading belongs to
- A depth is an amount of precipitation, and its description MUST declare the canonical unit its value is reported in,
  millimetres or centimetres, so that a provider reports rain in millimetres and snow in centimetres without
  converting; a depth with no declared unit is read in millimetres
- The warning and critical values say from where on the reading deserves attention; they are in the same unit as the
  value, and a reading for which a high value is not a fault declares none
- An attribution is the credit the reading's source requires wherever its data is shown: a text, optionally a shorter
  text, and optionally a link; a reading with no source that requires a credit declares none
- A path in a description MAY contain parameter segments written `{name}`; such a description is a pattern that stands
  for every reading whose path has any one non-empty segment at that position. The path of a subscribed reading is
  matched against the exact paths first and then against the patterns; of several patterns the one with the most
  literal segments wins, and a tie is resolved in the order listed and logged once. The description a widget receives
  for a matched path is the pattern's with every `{name}` in its path, label, full name and device replaced by the
  segment that matched
- The catalog lists patterns as written
- A path that matches neither an exact path nor a pattern is unavailable, with the reason that the provider has no such
  reading
- The description of a reading MUST be available to a widget together with the value, without I/O
- The catalog of a provider MUST be obtainable without any widget being subscribed to it
- A provider built before the warning and critical values, the time kind, patterns, attributions or the date, speed and
  depth kinds existed MUST load and run unchanged

#### Scenario: Default label comes from the catalog
- **WHEN** a widget shows `system:cpu/core/2/load` and the user has set no label
- **THEN** the widget can show the label the provider declared for that reading

#### Scenario: Catalog without subscribers
- **WHEN** the catalog of a registered provider is requested while no widget uses it
- **THEN** its readings are listed and the provider does no sampling

#### Scenario: Reading declares its levels
- **WHEN** a provider describes a temperature with a warning value of 80 and a critical value of 90
- **THEN** a widget that shows the reading can read both values from the description

#### Scenario: Reading declares no levels
- **WHEN** a widget reads the description of `system:cpu/load`
- **THEN** it has no warning value and no critical value

#### Scenario: Reading declares an attribution
- **WHEN** a provider describes a reading with the attribution text `Weather data by Open-Meteo.com` and a link
- **THEN** a widget that shows the reading can read the text and the link from the description

#### Scenario: Depth declares its unit
- **WHEN** a provider describes a snow reading with the canonical unit centimetres and a rain reading with the canonical
  unit millimetres
- **THEN** a widget that reads either description can tell which unit its value is in, and the formatter converts each
  correctly

#### Scenario: Pattern matches a path
- **WHEN** a provider describes `{location}/current/temperature` and a widget shows
  `weather:Zurich/current/temperature`
- **THEN** the reading is served by the provider, and its description has the path `Zurich/current/temperature`

#### Scenario: Description text is filled in
- **WHEN** the pattern's device is `{location}` and a widget shows `weather:Zurich/current/temperature`
- **THEN** the device name of the description is `Zurich`

#### Scenario: Exact path beats a pattern
- **WHEN** a provider describes both `{location}/place` and `home/place` and a widget shows `weather:home/place`
- **THEN** the description is the one for `home/place`

#### Scenario: Path matches nothing
- **WHEN** a widget shows `weather:Zurich/current/visibility` and no exact path or pattern matches
- **THEN** the reading is unavailable and the reason says the provider has no such reading

#### Scenario: Pattern listed as written
- **WHEN** the catalog of a provider with a pattern is listed
- **THEN** the pattern appears once, with `{location}` in its path, not once per place shown

### Requirement: Reading Formatting
The SDK MUST provide one formatter that turns a reading and its description into what a widget draws: the value text,
the unit text, whether the unit is raised or on the baseline, the widest value text to expect, and whether the value
is current. Widgets MUST use it for readings they show as a value, so that all widgets format alike.

- A percentage is shown without decimals unless the description or the widget asks for some, with `%` raised
- A temperature is shown with `°` raised, without decimals unless asked
- A plain number is shown with the description's unit text on the baseline
- A text value is shown as it is; an on/off value is shown as `On` or `Off`
- A time is shown as the time of day in the value's own UTC offset, never converted to the PC's zone. The clock is the
  user's Windows region setting unless the widget asks for one: a 24 hour time has the value text `HH:mm`, a 12 hour
  time has the value text `h:mm` and the unit `AM` or `PM` on the baseline. The widest value text is the widest time
  the clock can produce
- A date is shown as the day of the value in the value's own UTC offset, never converted to the PC's zone, in the
  language of the user's Windows region, with no unit. The widget MAY choose the style: the abbreviated weekday (`Wed`,
  the default) or the month and day (`Oct 14`, ordered as the region orders them). The widest value text is the widest
  text the style can produce
- A speed is shown without decimals unless asked, with the unit text `mph` or `km/h` on the baseline, converted from
  kilometres per hour
- A depth is shown in inches (unit text `"`, raised) or in the metric unit the description declares as its canonical
  unit, millimetres or centimetres (unit text `mm` or `cm`, on the baseline). Below 10 of the unit it has one decimal and
  from 10 up it has none, unless the widget or description asks for a number of decimals. A depth of zero is shown as
  `0`, and a positive depth that would show as `0.0` is shown as `<0.1`, so a trace of snow never reads as none
- A bearing in degrees can be turned by the SDK into one of the eight compass points (`N`, `NE`, `E`, `SE`, `S`,
  `SW`, `W`, `NW`), the point whose 45 degree sector holds the bearing
- A reading that is unavailable, or pending with no last value, is shown as a dash with its unit kept
- A reading that is stale, or pending with a last value, is shown with that value and marked as not current

#### Scenario: Percentage
- **WHEN** a percentage reading with the value `36.6` is formatted with no decimals requested
- **THEN** the value text is `37`, the unit is `%` and it is raised

#### Scenario: Unavailable reading
- **WHEN** an unavailable percentage reading is formatted
- **THEN** the value text is a dash, the unit is `%`, and the result is marked as not current

#### Scenario: Stale reading
- **WHEN** a stale reading whose last value was `42` is formatted
- **THEN** the value text is `42` and the result is marked as not current

#### Scenario: Stable width
- **WHEN** a percentage reading with a range of 0 to 100 is formatted
- **THEN** the widest value text is `100`, whatever the current value is

#### Scenario: Time on a 24 hour clock
- **WHEN** a time reading of 06:42 at offset +09:00 is formatted on a system whose region uses a 24 hour clock
- **THEN** the value text is `06:42`, with no unit

#### Scenario: Time on a 12 hour clock
- **WHEN** a time reading of 19:03 at offset -07:00 is formatted on a system whose region uses a 12 hour clock
- **THEN** the value text is `7:03` and the unit is `PM` on the baseline

#### Scenario: Time at another place
- **WHEN** a time reading of 06:42 at offset +09:00 is formatted on a PC in a zone at offset -07:00
- **THEN** the value text is `06:42` in a 24 hour clock, not the PC's local time of that instant

#### Scenario: Time unavailable
- **WHEN** an unavailable time reading is formatted
- **THEN** the value text is a dash and the result is marked as not current

#### Scenario: Weekday of a date
- **WHEN** a date reading of the instant 2026-10-14 00:00 at offset +09:00 is formatted with the default style
- **THEN** the value text is `Wed`

#### Scenario: Date at another place
- **WHEN** a date reading of the instant 2026-10-14 00:00 at offset +09:00 (2026-10-13 08:00 at offset -07:00) is
  formatted on a PC in a zone at offset -07:00
- **THEN** the value text is `Wed`, the weekday in the value's own offset, not Tuesday

#### Scenario: Month and day of a date
- **WHEN** the same date reading is formatted with the month and day style on a system with an English (United States)
  region
- **THEN** the value text is `Oct 14`

#### Scenario: Date unavailable
- **WHEN** an unavailable date reading is formatted
- **THEN** the value text is a dash and the result is marked as not current

#### Scenario: Speed in miles per hour
- **WHEN** a speed reading of 16.1 kilometres per hour is formatted for imperial units
- **THEN** the value text is `10` and the unit is `mph`

#### Scenario: Snow in inches
- **WHEN** a depth reading of 7.62 centimetres declared in centimetres is formatted for imperial units
- **THEN** the value text is `3.0` and the unit is `"`

#### Scenario: Snow in centimetres
- **WHEN** the same reading is formatted for metric units
- **THEN** the value text is `7.6` and the unit is `cm`

#### Scenario: Rain in millimetres
- **WHEN** a depth reading of 7 millimetres declared in millimetres is formatted for metric units
- **THEN** the value text is `7.0` and the unit is `mm`

#### Scenario: Large amount has no decimal
- **WHEN** a depth reading of 27 centimetres is formatted for imperial units
- **THEN** the value text is `11` and the unit is `"`

#### Scenario: Trace of snow
- **WHEN** a snow reading of 0.1 centimetres is formatted for imperial units (0.04 inch)
- **THEN** the value text is `<0.1` and the unit is `"`

#### Scenario: No snow
- **WHEN** a snow reading of zero is formatted
- **THEN** the value text is `0`

#### Scenario: Compass point
- **WHEN** the bearings 0, 315, 100 and 359 degrees are turned into compass points
- **THEN** they are `N`, `NW`, `E` and `N`

### Requirement: Display Unit Selection
Where a kind can be shown in more than one unit, the display unit MUST be chosen in this order: the unit the widget
passes for that reading (the user's setting), then the default display unit in the reading's description, then the
unit of the user's Windows region. The formatter converts from the canonical unit. A temperature has Celsius and
Fahrenheit. A speed and a depth have a metric and an imperial system, and the unit that is chosen for a speed or a
depth names a system: Celsius chooses the metric system and Fahrenheit the imperial one, so that one user setting
chooses the units of a card.

#### Scenario: Reading declares its unit
- **WHEN** a temperature reading whose description declares Celsius is formatted on a system whose region uses
  Fahrenheit, and the widget passes no unit
- **THEN** it is shown in Celsius

#### Scenario: Reading declares no unit
- **WHEN** a temperature reading of `20` whose description declares no display unit is formatted on a system whose
  region uses Fahrenheit
- **THEN** it is shown as `68`

#### Scenario: Widget setting wins
- **WHEN** the widget passes Fahrenheit for a temperature reading whose description declares Celsius
- **THEN** it is shown in Fahrenheit

#### Scenario: One setting for temperature, speed and depth
- **WHEN** the widget passes Fahrenheit for a card that shows a temperature, a wind speed and a snow depth on a system
  whose region is metric
- **THEN** the temperature is in Fahrenheit, the speed is in miles per hour and the snow is in inches

#### Scenario: Region decides for speed and depth
- **WHEN** a wind speed reading whose description declares no display unit is formatted with no unit passed, on a system
  whose region uses Fahrenheit
- **THEN** it is shown in miles per hour
