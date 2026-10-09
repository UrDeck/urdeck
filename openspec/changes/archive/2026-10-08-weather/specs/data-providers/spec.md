## MODIFIED Requirements

### Requirement: Reading Identity
Every reading MUST be addressed by an id of the form `<provider id>:<path>`, where the path is one or more segments
separated by `/`. Ids are compared without regard to case. The provider id part selects the provider; the path is
defined by that provider. A segment MAY contain any character except `/`, including spaces, commas and letters outside
ASCII, so that a provider can carry text a user typed (for example the name of a place); a provider that needs a `/`
inside such text defines its own escape for it.

#### Scenario: Id selects the provider
- **WHEN** a widget asks for `system:cpu/core/2/load`
- **THEN** the request is served by the provider registered as `system`, which is given the path `cpu/core/2/load`

#### Scenario: Segment with free text
- **WHEN** a widget asks for `weather:Portland, Oregon/current/temperature`
- **THEN** the request is served by the provider registered as `weather`, which is given the path
  `Portland, Oregon/current/temperature`

#### Scenario: Ids differing only in case
- **WHEN** two widgets ask for `weather:zurich/place` and `weather:Zurich/place`
- **THEN** they receive the same reading

#### Scenario: Malformed id
- **WHEN** a widget asks for an id with no `:` or an empty path
- **THEN** the reading is unavailable and the reason is logged once

### Requirement: Reading Values
A reading's value MUST be one of: a number, a text, an on/off state, or a time. A time is an instant together with the
UTC offset it is to be read in, so that a sunrise at another place can be shown in that place's local time. Values MUST
be of types defined by the SDK, so that a widget can read a value from any provider without referencing that
provider's assembly. Two time values are equal only when both the instant and the offset are equal.

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
maximum), a warning value and a critical value, a unit text for plain numbers, a default display unit, a number of
decimals and an attribution.

- The kinds are: plain number, percentage, temperature, text, on/off and time; a temperature is always reported in
  degrees Celsius
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
- A provider built before the warning and critical values, the time kind, patterns or attributions existed MUST load
  and run unchanged

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
- **WHEN** a widget shows `weather:Zurich/current/humidity` and no exact path or pattern matches
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
