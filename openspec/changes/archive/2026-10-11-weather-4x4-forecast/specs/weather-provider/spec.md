## MODIFIED Requirements

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
- **WHEN** a widget shows `weather:Zurich/current/visibility`
- **THEN** the reading is unavailable with the reason that the provider has no such reading

#### Scenario: Day beyond the forecast
- **WHEN** a widget shows `weather:Zurich/daily/7/high`
- **THEN** the reading is unavailable with the reason that the provider has no such reading

### Requirement: Weather Readings
For each location the provider MUST offer these readings. Every description carries the location's resolved name as its
device and the attribution below. In the table `N` stands for a forecast day from 0 to 6, where day 0 is today.

| Path | Meaning | Kind | Default label | Full name |
|---|---|---|---|---|
| `<location>/place` | resolved name of the place | text | `Place` | `Place` |
| `<location>/current/temperature` | air temperature now | temperature | `Now` | `Temperature` |
| `<location>/current/apparent` | what the temperature feels like now | temperature | `Feels like` | `Feels like temperature` |
| `<location>/current/condition` | the sky and weather now, as a condition class | text | `Condition` | `Weather condition` |
| `<location>/current/is-day` | whether the sun is up at the place | on/off | `Daytime` | `Is daytime` |
| `<location>/current/humidity` | relative humidity now | percentage | `Humidity` | `Relative humidity` |
| `<location>/current/wind-speed` | wind speed now | speed | `Wind` | `Wind speed` |
| `<location>/current/wind-direction` | the bearing the wind blows from, in degrees | number | `Direction` | `Wind direction` |
| `<location>/daily/N/date` | the day, as the start of that calendar day at the place | date | `Day` | `Forecast day N date` |
| `<location>/daily/N/condition` | the sky and weather of the day, as a condition class | text | `Condition` | `Forecast day N condition` |
| `<location>/daily/N/high` | the day's highest temperature | temperature | `High` | `Forecast day N high` |
| `<location>/daily/N/low` | the day's lowest temperature | temperature | `Low` | `Forecast day N low` |
| `<location>/daily/N/sunrise` | the day's sunrise | time | `Sunrise` | `Forecast day N sunrise` |
| `<location>/daily/N/sunset` | the day's sunset | time | `Sunset` | `Forecast day N sunset` |
| `<location>/daily/N/precip-chance` | the chance of precipitation during the day | percentage | `Chance` | `Forecast day N chance of precipitation` |
| `<location>/daily/N/rain` | the amount of liquid rain expected during the day | depth | `Rain` | `Forecast day N rain` |
| `<location>/daily/N/snow` | the depth of fresh snow expected during the day | depth | `Snow` | `Forecast day N snow` |

- The condition is one of these classes, whatever the data source calls them: `clear`, `partly-cloudy`, `cloudy`,
  `fog`, `drizzle`, `rain`, `sleet`, `snow`, `thunder`, and `unknown` for a code the provider does not know
- Temperatures are in degrees Celsius and their descriptions declare no display unit, so the region decides (see
  data-providers, "Display Unit Selection")
- Wind speed is in kilometres per hour, and a wind direction is a number from 0 to 360 with the unit `°`; the region
  decides how the speed is shown, in the same way as for temperatures
- Rain is in millimetres and snow is in centimetres, and the description of each declares that as its canonical unit
  (see data-providers, "Reading Catalog"); rain is the liquid that falls as rain or showers, snow is the depth of fresh
  snow and not its water content, and the two are separate readings, so a day near freezing can have both
- Rain is unavailable, with a reason, when the source supplied no rain amount; snow is unavailable, with a reason, when
  the source supplied no snowfall; a day without rain or snow has the amount zero, not an unavailable reading
- A day is the calendar day at the place, not at the PC, and day 0 is the calendar day at the place at the time of the
  sample
- Sunrise and sunset are instants with the place's UTC offset, so they read in the place's own local time; at a place
  where the sun does not rise or set that day the reading is unavailable with a reason
- A date is the instant at which the day begins at the place, with the place's UTC offset, so its weekday is the place's
  weekday
- A reading the source did not supply is unavailable with a reason, and MUST NOT show zero or an empty text

#### Scenario: Catalog lists the readings of a location
- **WHEN** the catalog of `weather` is listed
- **THEN** it contains the readings of the table above, with the location written as `{location}` and the forecast days
  0 to 6 each listed (see data-providers, "Reading Catalog")

#### Scenario: Condition classes
- **WHEN** the source reports thunderstorm with hail, light drizzle and a code the provider does not know
- **THEN** the conditions are `thunder`, `drizzle` and `unknown`

#### Scenario: Region uses Fahrenheit
- **WHEN** a widget shows `weather:Zurich/current/temperature` with no unit on a system whose region uses Fahrenheit
- **THEN** the value is converted from Celsius for display

#### Scenario: Today is day 0
- **WHEN** a widget shows `weather:Zurich/daily/0/high` and `weather:Zurich/daily/2/high`
- **THEN** the first is today's highest temperature at Zurich and the second is that of the day after tomorrow

#### Scenario: Sunrise in the place's time
- **WHEN** a widget in Portland shows `weather:Tokyo/daily/0/sunrise`
- **THEN** the time is Tokyo's local sunrise, not a time converted to the PC's zone

#### Scenario: Weekday at the place
- **WHEN** a widget in Portland shows `weather:Tokyo/daily/1/date` while it is Tuesday evening in Portland and
  Wednesday in Tokyo
- **THEN** the weekday is Thursday, the day after Tokyo's today

#### Scenario: Polar night
- **WHEN** the source reports no sunrise for the day
- **THEN** `daily/0/sunrise` is unavailable with a reason and the widget shows a dash

#### Scenario: Rain and snow on the same day
- **WHEN** the source expects 7 millimetres of rain and 7.5 centimetres of snow on the same day
- **THEN** `daily/N/rain` is 7 millimetres and `daily/N/snow` is 7.5 centimetres, each on its own reading

#### Scenario: Dry day
- **WHEN** the source expects no rain and no snow on a day
- **THEN** `daily/N/rain` and `daily/N/snow` are zero and available

#### Scenario: Wind
- **WHEN** the source reports a wind of 14 kilometres per hour from 315 degrees
- **THEN** `current/wind-speed` is 14 and `current/wind-direction` is 315

### Requirement: Network Behaviour
The provider MUST use the network only while at least one weather reading is shown, MUST use encrypted connections
only, and MUST bound every request by a timeout.

- A sample asks the service for the current conditions and the seven days from today of every wanted location, and
  makes no more than one forecast request per location in a sample; it asks for the same days and values whatever the
  readings that are shown, so two widgets that show one place never need different requests
- A newly wanted location MUST be fetched without waiting for the next interval, so a widget that is added or whose
  location is edited shows values within seconds, not minutes
- A request that fails (no connection, timeout, an error status, an answer that cannot be read) makes the sample fail:
  the readings become stale or, if they never had a value, unavailable, and the engine retries after its own backoff
- The provider logs a failure once when it begins and once when it ends, not once per attempt
- Nothing is held after the provider stops: no connection, no timer, no task

#### Scenario: Nothing shown
- **WHEN** no weather reading is subscribed
- **THEN** no request is made

#### Scenario: One request for all the readings of a place
- **WHEN** one widget shows the current temperature of Zurich and another shows five forecast days of Zurich
- **THEN** one forecast request per sample serves both

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
