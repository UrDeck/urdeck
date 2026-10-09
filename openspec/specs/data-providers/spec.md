# Data Providers Specification

## Purpose

Defines how data reaches widgets: providers as a plugin kind, the self-describing readings they publish, how readings
are addressed, when providers run, and how every widget formats a reading and shows its state.
## Requirements
### Requirement: Providers Are Plugins
A data provider MUST be loadable from the `plugins/` directory in the same way as a widget, and MUST need a reference
to the widget SDK only. A plugin assembly MAY contain providers, widgets or both. First-party providers MUST use the
same public contract as any other provider.

- A provider declares a provider id, a display name, a default sampling interval and a minimum sampling interval
- A provider id consists of lowercase letters, digits, `.` and `-`, and is compared without regard to case
- A provider type that lacks its declaration, is abstract or has no public parameterless constructor MUST be skipped
  with a logged reason
- When two providers declare the same id, the first one loaded MUST be kept and the other rejected with a logged
  warning that names both assemblies
- An assembly that contains neither a widget nor a provider MUST be reported in the log as not being a plugin

#### Scenario: Provider plugin is discovered
- **WHEN** a plugin assembly with a valid provider is placed in `plugins/`
- **THEN** the provider is registered under its id and the log names it

#### Scenario: Assembly with a provider and a widget
- **WHEN** one plugin assembly contains a provider and a widget
- **THEN** both are registered

#### Scenario: Duplicate provider id
- **WHEN** two plugin assemblies each declare a provider with the id `system`
- **THEN** one is registered, the other is rejected, and a warning names both assemblies

#### Scenario: Provider built against another SDK
- **WHEN** a plugin's provider type implements a provider contract from a different copy of the SDK
- **THEN** it is not registered and the log says the plugin was built against a different SDK

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

### Requirement: Demand-Driven Provider Lifetime
A provider MUST exist and run only while it is needed.

- A provider MUST NOT be instantiated at plugin load; it is created when a reading of it is first subscribed to, or
  when its catalog is requested
- A provider is told which of its readings are currently subscribed to, and again whenever that set changes
- When the last subscription to a provider ends, the provider MUST be stopped after a linger period of 5 seconds
  unless a new subscription arrives within it
- A stopped provider MUST do no periodic work and hold no timer
- On a plugin reload every provider MUST be stopped and released before its plugin is unloaded, without the linger
  period

#### Scenario: No data widgets
- **WHEN** a page contains no widget that declares a reading
- **THEN** no provider is instantiated and no sampling runs

#### Scenario: First subscription starts the provider
- **WHEN** the first widget that uses a `system` reading is shown
- **THEN** the `system` provider is created and started, and it is told which readings are wanted

#### Scenario: Last subscription ends
- **WHEN** the only widget using a provider is removed and nothing subscribes again within 5 seconds
- **THEN** the provider is stopped

#### Scenario: Page rebuild does not restart the provider
- **WHEN** a page is rebuilt and the same readings are subscribed to again within 5 seconds
- **THEN** the provider keeps running and is not restarted

#### Scenario: Two widgets share a reading
- **WHEN** two shown widgets use the same reading
- **THEN** the provider samples it once per interval and both widgets receive the same value

### Requirement: Sampling
The system MUST sample a running provider periodically, off the UI thread, and independently of every other provider.

- A started provider is sampled at once, again after 250 milliseconds, and then once per interval
- A new sample MUST NOT start while the previous sample of the same provider is still running; the tick is skipped
- All values a provider publishes in one sample MUST become visible to widgets together
- A provider MAY also publish values at any time between samples
- The latest value of a reading MUST be kept after its last subscription ends, until the provider's plugin is unloaded

#### Scenario: Slow sample
- **WHEN** a sample takes longer than the interval
- **THEN** the next tick is skipped and other providers are sampled on time

#### Scenario: Values of one sample arrive together
- **WHEN** a widget uses three readings of one provider and a sample changes all three
- **THEN** the widget is asked to repaint once for that sample

#### Scenario: Value kept across a short absence
- **WHEN** a widget is removed and placed again later while the provider's plugin stays loaded
- **THEN** the reading's last known value is available at once, marked as not current

### Requirement: Sampling Rate Settings
The sampling interval of a provider MUST be the provider's declared default unless the user sets one. The user sets it
per provider id under `providers` in `urdeck-config.json` (`intervalMs`).

- A configured interval below the provider's declared minimum MUST be raised to the minimum and logged
- A change to the setting MUST take effect on configuration reload without restarting the host
- Settings for a provider id that is not registered MUST be kept in the file and otherwise ignored

#### Scenario: Default interval
- **WHEN** no `providers` entry exists for `system`
- **THEN** it is sampled at its declared default interval

#### Scenario: User sets a faster rate
- **WHEN** the user sets `providers.system.intervalMs` to `500` and saves the file
- **THEN** the provider is sampled every 500 milliseconds from the reload on

#### Scenario: Interval below the minimum
- **WHEN** `providers.system.intervalMs` is `10` and the provider's minimum is 250
- **THEN** the provider is sampled every 250 milliseconds and the log says the value was raised

### Requirement: Reading States
Every reading a widget asks for MUST be in exactly one of four states:

- **ok**: the value comes from the provider's most recent successful sample or publication
- **pending**: the reading is subscribed to and its provider has not yet reported on it since the subscription began
- **stale**: the provider's most recent sample failed, timed out, or has not reported this reading in its last three
  samples, and an earlier value exists
- **unavailable**: the provider reported the reading as unavailable, the provider or the reading does not exist, or
  the provider failed and no earlier value exists

A reading that is pending or stale MUST still expose its last known value when one exists. A reading that is
unavailable MUST carry a reason. The reason for a reading becoming unavailable or stale MUST be logged once per
change of state, not once per sample.

#### Scenario: Unknown provider
- **WHEN** a widget asks for `nosuch:value` and no provider `nosuch` is registered
- **THEN** the reading is unavailable and the log names the missing provider once

#### Scenario: Unknown reading of a known provider
- **WHEN** a widget asks for a path that is not in the provider's catalog
- **THEN** the reading is unavailable and the log names the reading once

#### Scenario: Provider reports a reading as unavailable
- **WHEN** a provider reports that a reading cannot be supplied, with a reason
- **THEN** the reading is unavailable and carries that reason

#### Scenario: Sample fails after earlier success
- **WHEN** a provider's sample throws after an earlier sample succeeded
- **THEN** its subscribed readings become stale and keep their last value

### Requirement: Fault Containment
A failing provider MUST NOT affect the UI thread, other providers, or widgets that do not use it.

- An exception from any provider member MUST be caught and logged with the provider id
- A sample that has not finished after 5 seconds or five intervals, whichever is longer, MUST be treated as failed;
  the provider is asked to cancel
- After a failure the provider is sampled again with a delay that doubles on each consecutive failure, up to 60
  seconds, and returns to its interval after a success
- A provider whose sample never returns stays failed until its plugin is reloaded; the system MUST NOT start a
  second sample of it

#### Scenario: Provider throws
- **WHEN** a provider throws during a sample
- **THEN** the exception is logged, its readings become stale or unavailable, and widgets using other providers are
  unaffected

#### Scenario: Provider hangs
- **WHEN** a provider's sample does not return
- **THEN** after the timeout its readings become stale or unavailable, the UI stays responsive, and other providers
  keep being sampled

#### Scenario: Provider recovers
- **WHEN** a provider that failed succeeds on a later sample
- **THEN** its readings are ok again and it returns to its normal interval

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

### Requirement: Display Unit Selection
Where a kind can be shown in more than one unit, the display unit MUST be chosen in this order: the unit the widget
passes for that reading (the user's setting), then the default display unit in the reading's description, then the
unit of the user's Windows region. The formatter converts from the canonical unit.

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
