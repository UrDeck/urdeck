# provider-homeassistant Specification

## Purpose
Defines the Home Assistant data provider: its settings, the readings it offers and how their kinds and units are decided,
how its connection lives through demand, outages and a rejected token, the states and reasons it reports, the actions it
performs, how its readings are listed, its sample data, and what it sends to whom.
## Requirements
### Requirement: Settings
The provider MUST have the id `homeassistant` and declare two settings (see data-providers, "Provider Settings"): `url`,
the address of the user's Home Assistant (required, `http` or `https`), and `token`, a long-lived access token (required,
a secret whose context is `url`; see provider-secrets). With either missing, its readings are unavailable with the reason
"set up Home Assistant in Settings". It offers a connection test that reports the Home Assistant version and the number
of entities, or why it failed (address unreachable, not a Home Assistant, token rejected).

#### Scenario: Not set up
- **WHEN** a widget shows `homeassistant:sensor.office_temperature` and no address is configured
- **THEN** the reading is unavailable with the reason "set up Home Assistant in Settings" and nothing is contacted

#### Scenario: Test connection
- **WHEN** the user tests a correct address and token
- **THEN** the result names the Home Assistant version and the number of entities

### Requirement: Reading Paths
A reading's path MUST be a Home Assistant entity id (`sensor.office_temperature`, `light.desk`), or an entity id followed
by `/` and one of its attribute names (`light.desk/brightness`). The provider's catalog lists the patterns `{entity}` and
`{entity}/{attribute}`; the concrete description of a shown entity arrives when its state does (see data-providers,
"Descriptions Refined While Running").

#### Scenario: An attribute
- **WHEN** a widget shows `homeassistant:light.desk/brightness` and the light is on at brightness 128
- **THEN** the reading is a percentage of about 50

### Requirement: Kinds And Units
The provider MUST decide each entity's kind from its domain, its device class and its own unit of measurement, never from
Home Assistant's unit system setting, and report values in UrDeck's canonical units:

| Entity | Kind | Value |
|---|---|---|
| device class `temperature`, unit °C or °F | temperature | degrees Celsius |
| device class `speed` or `wind_speed`, unit km/h, mph or m/s | speed | kilometres per hour |
| device class `precipitation`, unit mm, cm or in | depth | millimetres (canonical unit declared) |
| device class `battery` or `humidity`, or unit `%` | percentage | 0 to 100 |
| device class `timestamp` | time | the instant |
| domain `binary_sensor`, `switch`, `light`, `fan`, `input_boolean`, `automation` | on/off | `on` is on |
| any other number with a unit | plain number | as reported, with the unit's text |
| anything else | text | the state as reported |

- The label is the entity's friendly name; the full name is the entity id
- A percentage has the range 0 to 100; a reading whose entity reports `min` and `max` attributes gets that range
- A state that does not fit its kind (`"abc"` for a temperature) is unavailable, with the state in the reason
- When an entity's unit or device class changes while it is shown, its description is replaced and the widget redraws
- The Windows region decides the unit shown, as for weather; a widget's own unit setting overrides it

#### Scenario: Fahrenheit installation
- **WHEN** an entity reports `72` with unit °F and the Windows region uses Celsius
- **THEN** the reading is a temperature of about 22.2 °C and a widget shows `22.2°` in Celsius

#### Scenario: Mixed units in one installation
- **WHEN** one temperature entity reports in °F and another in °C
- **THEN** both readings are correct temperatures in degrees Celsius

#### Scenario: Power
- **WHEN** an entity reports `431` with unit `W` and no conversion applies
- **THEN** the reading is a plain number 431 with the unit text `W`

### Requirement: Connection Follows Demand
The provider MUST hold a connection to Home Assistant only while at least one of its readings is subscribed, and receive
changes only for the entities that are.

- The first subscription opens the connection, authenticates and subscribes to exactly the subscribed entities; their
  current states arrive first, then their changes
- When the set of subscribed entities changes, the provider subscribes to the new set within half a second, once for a
  burst of changes; entities newly shown get their current state at once
- With nothing subscribed for 30 seconds the connection is closed
- Changes to entities nobody shows are never received

#### Scenario: Page flip and back
- **WHEN** the user swipes to a page without Home Assistant readings and back within 30 seconds
- **THEN** the connection stays open and the readings show current values at once

#### Scenario: Nothing shown
- **WHEN** no Home Assistant reading has been shown for 30 seconds
- **THEN** the provider has no connection open

### Requirement: Outages And Reconnecting
When the connection is lost or stops answering, the provider MUST report an interruption (its shown readings become stale
and keep their values; see data-providers, "Interruptions"), then reconnect after a growing delay: 1, 2, 5, 10, 30 and then
every 60 seconds, each varied by up to 20 % so many decks do not reconnect together. A connection that does not answer a
check within 10 seconds (checked every 30 seconds) is treated as lost, so a connection broken by sleep is noticed. After a
successful reconnect the delay starts again at 1 second and the shown readings get current values.

- Each attempt is logged with its number, the delay and the cause; a successful reconnect logs how many attempts it took
- When Home Assistant rejects the token, the provider stops trying, its readings are unavailable with the reason "Home
  Assistant rejected the token; fix it in Settings", and this is logged once. Changing the settings starts it again

#### Scenario: Wi-Fi blip
- **WHEN** the connection drops for 20 seconds while a power reading is shown
- **THEN** the reading is stale with its last value during the outage, the log has the retries, and the reading is ok
  with a current value after the reconnect

#### Scenario: Revoked token
- **WHEN** the user deletes the token in Home Assistant and the provider reconnects
- **THEN** the readings become unavailable with the rejection reason and no further attempts are made

### Requirement: Entity States And Reasons
The provider MUST report Home Assistant's special states and missing entities as follows:

- `unavailable`: the reading is unavailable with the reason "unavailable in Home Assistant"
- `unknown`: the reading is pending, keeping its last value when it has one
- An entity that does not exist, or is removed while shown: the reading is unavailable with the reason
  "no entity '<id>' in Home Assistant", followed by "did you mean …?" naming up to three existing entity ids closest to it
- An attribute the entity does not have: the reading is unavailable with a reason naming the attribute

#### Scenario: Typo
- **WHEN** a widget shows `homeassistant:sensor.ofice_temperature` and `sensor.office_temperature` exists
- **THEN** the reading is unavailable and its reason suggests `sensor.office_temperature`

### Requirement: Actions
The provider MUST offer actions (see provider-actions) with the path `<entity_id>/<service>`, calling Home Assistant's
service `<domain>.<service>` on that entity, where the domain is the entity id's part before the dot. Every service is
allowed. The action runs whether or not the connection for readings is open.

- `toggle` is the entity's natural action: `turn_on` for a scene or script, `press` for a button, play/pause for a media
  player, lock or unlock for a lock (by its last known state; failed when unknown), and Home Assistant's own `toggle`
  for every other domain
- The optional argument fills the service's main field: `light/turn_on` → brightness in percent; `fan/set_percentage` →
  percentage; `cover/set_cover_position` → position; `number/set_value` → value; `select/select_option` → option (text);
  `media_player/volume_set` → volume in percent (0–100, sent as 0–1); `climate/set_temperature` → a temperature in
  degrees Celsius, sent in the installation's unit. Other services ignore the argument
- The result is done when Home Assistant accepts the call; failed with "token rejected" for a rejected token, with a
  message naming the service and the entity when Home Assistant refuses the call (an unknown service, or one the entity
  does not support), and with the cause when Home Assistant does not answer
- The action catalog lists `{entity}/toggle`, `{entity}/turn_on`, `{entity}/turn_off` and `{entity}/{service}` as
  patterns for documentation and editors
- The new state shows when Home Assistant reports it; the provider changes no reading itself

#### Scenario: Toggle a light
- **WHEN** a widget invokes `homeassistant:light.desk/toggle` while the light is off
- **THEN** Home Assistant's `light.toggle` is called for `light.desk`, the result is done, and the reading
  `homeassistant:light.desk` becomes on when Home Assistant reports it

#### Scenario: Run a scene
- **WHEN** a widget invokes `homeassistant:scene.movie_night/toggle`
- **THEN** `scene.turn_on` is called for `scene.movie_night`

#### Scenario: Brightness
- **WHEN** a widget invokes `homeassistant:light.desk/turn_on` with the argument 40
- **THEN** `light.turn_on` is called for `light.desk` with brightness 40 percent

#### Scenario: Unknown service
- **WHEN** a widget invokes `homeassistant:light.desk/explode`
- **THEN** the result is failed with a message naming `light.explode` and `light.desk`

### Requirement: Natural Action And Icon
The concrete description of each entity MUST name its natural action (`<entity_id>/toggle`) for the domains listed under
"Actions" (scene, script, button, input_button, media_player, lock, and every domain with Home Assistant's own toggle:
light, switch, fan, cover, input_boolean, automation, climate) and none for others (sensor, binary_sensor, and the rest),
and MUST name an icon key: by domain and device class (a light → `home/light`, an outlet → `home/outlet`, a fan →
`home/fan`, a lock → `home/lock`, a door, window, motion or moisture sensor → `home/door`, `home/window`, `home/motion`,
`home/leak`, a temperature, humidity, power or battery sensor → `home/thermometer`, `home/droplet`, `home/bolt`,
`home/battery`, a scene, script or button → `home/scene`, `home/script`, `home/button`, a cover by device class →
`home/blinds`, `home/garage` or `home/door`, a media player → a `media/` key), then by the entity's own icon name where a
well-known one maps (`mdi:lightbulb…`, `mdi:led-strip…`, `mdi:lamp…` → `home/light`, `mdi:power-plug…` → `home/outlet`),
and otherwise `home/entity`. The description of a light's `brightness` attribute MUST name `<entity_id>/turn_on` as its
natural action (it takes the level in percent as its argument), so a widget that sets a level needs no knowledge of Home
Assistant's services.

#### Scenario: A light's brightness
- **WHEN** the description of `homeassistant:light.desk/brightness` is read
- **THEN** it is a percentage and names the action `light.desk/turn_on`

#### Scenario: A sensor is display-only
- **WHEN** the description of `sensor.office_temperature` is read
- **THEN** it names no action and the icon `home/thermometer`

#### Scenario: A light strip
- **WHEN** the description of a `light` entity whose icon is `mdi:led-strip-variant` is read
- **THEN** it names the action `light.<name>/toggle` and the icon `home/light`

### Requirement: Listing Entities
The provider MUST be able to list its concrete readings (see data-providers, "Listing Concrete Readings"): one
description per entity in Home Assistant, built by the same rules as a shown entity's, from a single request.

#### Scenario: Browse
- **WHEN** the readings of `homeassistant` are listed for an installation with 1,234 entities
- **THEN** 1,234 descriptions are returned, each with its entity id as path, its friendly name, kind, unit, action and icon

### Requirement: What It Sends
The provider MUST contact only the configured address: a WebSocket while a reading is shown, an HTTPS (or HTTP, as
configured) request per action, and one request to list entities when a missing entity is explained or the readings are
listed. It sends the token only to that address. It logs the address, the Home Assistant version, the number of entities
subscribed, and each change of connection state, never the token.

#### Scenario: Connected line
- **WHEN** the provider connects to Home Assistant 2026.10.3 with four entities shown
- **THEN** the log has one line naming the address, the version and four entities, and no token

### Requirement: Sample Data
The provider MUST carry a sample file (see sample-data) with fictional entities covering every kind in "Kinds And Units"
(at least a light on and one off, a switch, a temperature, a humidity, a power, a battery, a timestamp, a scene and a
text state), with their icons and natural actions, and an `{entity}` pattern so any entity id shows a value in `--demo`.

#### Scenario: Demo mode
- **WHEN** the host runs with `--demo` and a page shows `homeassistant:light.living_room`
- **THEN** the light shows the sample's state without any address configured and nothing is contacted

