# Weather Provider Specification

## Purpose

Defines the first-party `weather` data provider: how a place is named in a reading id and resolved, which weather
readings it offers for each place, how it behaves on the network and when the network fails, and what it costs. Its
readings are the data behind the weather widget and can be shown by any widget.

## Requirements
### Requirement: Weather Provider Identity
UrDeck MUST ship a data provider with the id `weather` and the name `Weather`. It MUST reference only the widget SDK.
Its default sampling interval MUST be 15 minutes and its minimum 5 minutes; a configured interval below the minimum is
raised to it, as for every provider (see data-providers, "Sampling Rate Settings"). The provider MUST NOT need any
setting of its own: a place is named by the reading id.

#### Scenario: Default interval
- **WHEN** a weather reading is shown and `providers.weather` sets nothing
- **THEN** the provider is sampled every 15 minutes

#### Scenario: Interval below the minimum
- **WHEN** `providers.weather.intervalMs` is 60000
- **THEN** the provider is sampled every 5 minutes and the raise is logged once

#### Scenario: Weather provider loads on a machine without other providers
- **WHEN** only the weather provider is installed
- **THEN** it loads and its catalog is listed without any network access

### Requirement: Locations In Reading Ids
The first path segment of every weather reading id MUST be the location, and the rest of the path says which reading
of that location is meant (`weather:<location>/<reading>`). Any number of locations MAY be shown at the same time and
they MUST NOT affect each other.

- A location is either free text naming a place (a city, a postal code, `City, ST`, `City, State`, optionally with a
  country code) or coordinates written `@<latitude>,<longitude>` in decimal degrees
- A `/` inside a location is written `%2F` and a `%` is written `%25`; the provider reads them back
- Surrounding white space is ignored, and locations are compared without regard to case, so ids that differ only in
  case or surrounding white space name one location and are fetched once

#### Scenario: Two places at once
- **WHEN** widgets show `weather:Zurich/current/temperature` and `weather:Portland, Oregon/current/temperature`
- **THEN** each reading has the value for its own place

#### Scenario: Coordinates
- **WHEN** a widget shows `weather:@47.37,8.54/current/temperature`
- **THEN** the value is for those coordinates and no place search is made

#### Scenario: Slash in a place name
- **WHEN** a widget shows `weather:AC%2FDC Street/current/temperature`
- **THEN** the provider looks up the text `AC/DC Street`

#### Scenario: Same place written twice
- **WHEN** two widgets show `weather:zurich/current/temperature` and `weather:Zurich/current/temperature`
- **THEN** the place is looked up once and fetched once per sample

#### Scenario: Reading that does not exist
- **WHEN** a widget shows `weather:Zurich/current/humidity`
- **THEN** the reading is unavailable with the reason that the provider has no such reading

### Requirement: Place Resolution
A location given as text MUST be resolved to one place with Open-Meteo's geocoding service. The provider MUST resolve
a location at most once per session and MUST NOT resolve it again on each sample.

- When several places match, the first is used; the provider logs the matches it considered, with the one it chose
- A location that matches nothing leaves every reading of that location unavailable, with a reason that contains the
  text that was looked up, until the provider is restarted; a different text is a different location
- A resolved place has a short readable name made of the place's name and its first-level area when that differs from
  the name (for example `Portland, Oregon`); it is published as the location's `place` reading and is the device name
  of the location's other readings
- The text is sent to the service as the user typed it, so what the service understands (for example `Portland, OR`,
  `Portland, Oregon`, `Paris, TX`, `90210`, and a country code after a comma) is what the user can type
- A failure to reach the service is a failed sample (see "Network Behaviour"), not a place that matches nothing

#### Scenario: City and state abbreviation
- **WHEN** a widget shows `weather:Portland, OR/place`
- **THEN** the value is `Portland, Oregon`

#### Scenario: Postal code
- **WHEN** a widget shows `weather:90210/place`
- **THEN** the value is the name of the place that postal code belongs to

#### Scenario: Ambiguous name
- **WHEN** a widget shows `weather:Springfield/place` and several places match
- **THEN** the first match is used and the log lists the others

#### Scenario: Nothing matches
- **WHEN** a widget shows `weather:Xyzzyville/current/temperature`
- **THEN** the reading is unavailable and its reason names `Xyzzyville`, and later samples do not ask the service again

#### Scenario: Coordinates need no lookup
- **WHEN** a widget shows `weather:@47.37,8.54/place`
- **THEN** the value is the text `47.37, 8.54`

### Requirement: Weather Readings
For each location the provider MUST offer these readings. Every description carries the location's resolved name as its
device and the attribution below.

| Path | Meaning | Kind | Default label | Full name |
|---|---|---|---|---|
| `<location>/place` | resolved name of the place | text | `Place` | `Place` |
| `<location>/current/temperature` | air temperature now | temperature | `Now` | `Temperature` |
| `<location>/current/apparent` | what the temperature feels like now | temperature | `Feels like` | `Feels like temperature` |
| `<location>/current/condition` | the sky and weather now, as a condition class | text | `Condition` | `Weather condition` |
| `<location>/current/is-day` | whether the sun is up at the place | on/off | `Daytime` | `Is daytime` |
| `<location>/today/high` | today's highest temperature | temperature | `High` | `Today's high` |
| `<location>/today/low` | today's lowest temperature | temperature | `Low` | `Today's low` |
| `<location>/today/sunrise` | today's sunrise | time | `Sunrise` | `Sunrise` |
| `<location>/today/sunset` | today's sunset | time | `Sunset` | `Sunset` |

- The condition is one of these classes, whatever the data source calls them: `clear`, `partly-cloudy`, `cloudy`,
  `fog`, `drizzle`, `rain`, `sleet`, `snow`, `thunder`, and `unknown` for a code the provider does not know
- Temperatures are in degrees Celsius and their descriptions declare no display unit, so the region decides (see
  data-providers, "Display Unit Selection")
- `today` is the calendar day at the place, not at the PC
- Sunrise and sunset are instants with the place's UTC offset, so they read in the place's own local time; at a place
  where the sun does not rise or set that day the reading is unavailable with a reason
- A reading the source did not supply is unavailable with a reason, and MUST NOT show zero or an empty text

#### Scenario: Catalog lists the readings of a location
- **WHEN** the catalog of `weather` is listed
- **THEN** it contains the readings of the table above, with the location written as `{location}` (see
  data-providers, "Reading Catalog")

#### Scenario: Condition classes
- **WHEN** the source reports thunderstorm with hail, light drizzle and a code the provider does not know
- **THEN** the conditions are `thunder`, `drizzle` and `unknown`

#### Scenario: Region uses Fahrenheit
- **WHEN** a widget shows `weather:Zurich/current/temperature` with no unit on a system whose region uses Fahrenheit
- **THEN** the value is converted from Celsius for display

#### Scenario: Sunrise in the place's time
- **WHEN** a widget in Portland shows `weather:Tokyo/today/sunrise`
- **THEN** the time is Tokyo's local sunrise, not a time converted to the PC's zone

#### Scenario: Polar night
- **WHEN** the source reports no sunrise for the day
- **THEN** `today/sunrise` is unavailable with a reason and the widget shows a dash

### Requirement: Weather Attribution
Every description in the weather catalog MUST carry an attribution with the text `Weather data by Open-Meteo.com`, the
shorter text `Open-Meteo.com` and the link `https://open-meteo.com/`, so that any widget that shows a weather reading
can credit its source (see data-providers, "Reading Catalog").

#### Scenario: Description carries the credit
- **WHEN** a widget reads the description of `weather:Zurich/current/temperature`
- **THEN** it contains the attribution text and the link

### Requirement: Network Behaviour
The provider MUST use the network only while at least one weather reading is shown, MUST use encrypted connections
only, and MUST bound every request by a timeout.

- A sample asks the service for the current conditions and today's values of every wanted location, and makes no more
  than one forecast request per location in a sample
- A newly wanted location MUST be fetched without waiting for the next interval, so a widget that is added or whose
  location is edited shows values within seconds, not minutes
- A request that fails (no connection, timeout, an error status, an answer that cannot be read) makes the sample fail:
  the readings become stale or, if they never had a value, unavailable, and the engine retries after its own backoff
- The provider logs a failure once when it begins and once when it ends, not once per attempt
- Nothing is held after the provider stops: no connection, no timer, no task

#### Scenario: Nothing shown
- **WHEN** no weather reading is subscribed
- **THEN** no request is made

#### Scenario: New location while running
- **WHEN** the user changes a weather widget's location in the configuration while the page is shown
- **THEN** the new location's readings are pending only until its first fetch completes, and the old location is no
  longer fetched if nothing else shows it

#### Scenario: Offline at start
- **WHEN** the PC has no connection when the page is shown
- **THEN** the readings are unavailable, the failure is logged once, and values appear after the connection returns,
  within the engine's retry delay

#### Scenario: Connection lost later
- **WHEN** a sample fails after earlier samples succeeded
- **THEN** the readings keep their last value as stale and are current again after the next successful sample

#### Scenario: Provider stopped
- **WHEN** the last weather widget is removed and the provider stops
- **THEN** the provider holds no connection or timer, and its plugin can be unloaded

### Requirement: Weather Provider Cost
The provider MUST do only the work its subscribed readings need.

- The traffic MUST stay far below Open-Meteo's free-tier limits for one PC: with the minimum interval and a handful of
  locations, well under 10,000 requests a day
- Readings of locations that no widget shows MUST NOT be fetched
- A sample MUST NOT allocate beyond what parsing the answer and publishing the values needs, and a steady state with
  an unchanged answer MUST NOT cause a repaint (the engine already drops unchanged values)

#### Scenario: Handful of locations
- **WHEN** five different locations are shown for a day at the default interval
- **THEN** the provider makes at most 5 forecast requests per interval and at most 5 lookups in total

#### Scenario: Unchanged answer
- **WHEN** two successive samples return the same values
- **THEN** no widget is repainted because of the second one
