---
title: Home Assistant
description: Show the states of your Home Assistant (temperatures, power, batteries, lights) on the deck, and change them.
section: Start
order: 5
---

The `homeassistant` data provider shows the states of your [Home Assistant](https://www.home-assistant.io/) on the deck: a
room's temperature, the power your desk draws, a phone's battery, whether the lamp is on. Any widget that shows readings can
show them, for example [Stats](/docs/widgets/stats/). It also runs Home Assistant's services, for a widget that switches
something.

It talks only to your Home Assistant, at the address you give it, and only while one of its readings is on the screen.

## Setting it up

1. In Home Assistant, open your profile (your name at the bottom of the sidebar), then the **Security** tab. Under
   **Long-lived access tokens**, choose **Create token**, name it `UrDeck`, and copy the token. Home Assistant shows it only once.
2. In UrDeck, open the settings window (click the tray icon). Under **Providers**, **Home Assistant**, enter the **Address** you
   open Home Assistant at (for example `http://homeassistant.local:8123`) and paste the **Token**. **Test connection** says which
   version of Home Assistant answered and how many entities it has. Then **Save**.

The address can also go into `urdeck-config.json`:

```json
"providers": { "homeassistant": { "url": "http://homeassistant.local:8123" } }
```

The token never goes into the file: it is kept in Windows Credential Manager, or read from the environment variable
`URDECK_SECRET_HOMEASSISTANT_TOKEN` (see [Secrets](/docs/#secrets)). Until both are set, every Home Assistant reading shows
**set up Home Assistant in Settings** and nothing is contacted. The [reference](/docs/#provider-homeassistant) lists the
settings.

## Readings

A reading is `homeassistant:` followed by an entity id, as Home Assistant writes it:

```json
{ "typeId": "urdeck.widgets.stats", "col": 0, "row": 0, "width": 2, "height": 2,
  "slots": [ { "reading": "homeassistant:sensor.office_temperature" } ] }
```

An attribute of an entity is the entity id, `/`, and the attribute's name. A light's `brightness` is shown as a percentage:

```json
{ "reading": "homeassistant:light.desk/brightness" }
```

A card of four gauges and three numbers:

```json
{
  "typeId": "urdeck.widgets.stats", "col": 0, "row": 0, "width": 4, "height": 4,
  "slots": [
    { "reading": "homeassistant:sensor.office_temperature" },
    { "reading": "homeassistant:sensor.office_humidity" },
    { "reading": "homeassistant:sensor.phone_battery" },
    { "reading": "homeassistant:light.desk/brightness" },
    { "reading": "homeassistant:sensor.desk_power" },
    { "reading": "homeassistant:sensor.washer_status" },
    { "reading": "homeassistant:light.desk" }
  ]
}
```

A reading's label is the entity's name in Home Assistant; a slot's own `label` replaces it.

### Finding an entity id

In Home Assistant: **Settings**, **Devices & services**, **Entities**, then the entity, the gear icon, and **Entity ID**.

Or list them with [the command line tool](/docs/command-line/), which shows each entity's current value and name:

```powershell
urdeck-cli readings browse homeassistant --search basement --domain sensor
```

```text
homeassistant:sensor.basement_temperature  temperature  18.5 °  Basement temperature
homeassistant:sensor.basement_humidity     percent      52 %    Basement humidity
```

`--domain light` keeps the lights, `--search` keeps what contains the text in its id or its name, and `--format json` prints JSON.
The tool uses the address in your configuration (`--url` gives another) and the token from Credential Manager or the environment
variable; it never prints the token.

## Units

Each entity's own unit decides what it is, whatever Home Assistant's unit system says, so one installation can mix °F and °C.
UrDeck then shows it in your Windows region's units, as it does for the weather; a widget's own unit setting overrides that.

| In Home Assistant | On the deck |
|---|---|
| A temperature in °C or °F | A temperature |
| A speed or wind speed in km/h, mph or m/s | A speed |
| Precipitation in mm, cm or in | An amount of rain or snow |
| A battery, a humidity, or anything in `%` | A percentage from 0 to 100 |
| A timestamp | A time |
| A binary sensor, switch, light, fan, input boolean or automation | On or off |
| Any other number with a unit (W, kWh, V, ppm, lx) | The number with its unit |
| Anything else | The state as text |

An entity that has `min` and `max` (an input number) fills a gauge between them. When an entity's unit changes in Home Assistant,
the deck follows.

## When something is wrong

| The reading shows | Because |
|---|---|
| **unavailable in Home Assistant** | Home Assistant itself has no state for the entity (a device that is offline). |
| Its last value, dimmed | The connection to Home Assistant was lost. UrDeck tries again after 1, 2, 5, 10 and 30 seconds and then every minute, and the readings come back by themselves. |
| **no entity '…' in Home Assistant; did you mean …?** | There is no such entity. The suggestion names the closest ids, which usually means a typo. |
| **… has no attribute '…'** | The entity has no attribute of that name (right now). |
| **Home Assistant rejected the token; fix it in Settings** | The token was deleted or mistyped. UrDeck stops trying until you save a new one. |
| A dash, or the value it had | The entity's state is `unknown` (Home Assistant restarting, say); the value returns when it has one. |

`urdeck.log` has a line for every change: `Connected to Home Assistant 2026.10.3 at http://homeassistant.local:8123/: 4
entities subscribed`, each new attempt with its number, its delay and its cause, and `reconnected after 2 attempts`. The token is
never in the log.

## Switching things

A widget that acts on a reading (the [control](/docs/widgets/control/) tile) runs a service of Home Assistant on its entity. The action
`homeassistant:light.desk/toggle` is each kind of entity's natural tap: a light, switch, fan, cover, input boolean, automation or
climate toggles; a scene or script runs; a button is pressed; a media player plays or pauses; a lock locks or unlocks (by its last
state, so the lock must be on a page). Any other service works by its name, for example `homeassistant:light.desk/turn_off`, and
one value can go with it: a light's brightness in percent (`light.desk/turn_on`), a fan's speed (`set_percentage`), a cover's
position (`set_cover_position`), a number's value (`set_value`), an option (`select_option`), a media player's volume in percent
(`volume_set`) or a thermostat's temperature in °C (`set_temperature`, sent in your installation's unit).

The new state shows when Home Assistant reports it. If Home Assistant refuses the call, `urdeck.log` names the service and the
entity.

## What it sends

Only to the address you set, and nowhere else:

- While at least one Home Assistant reading is on the screen, one WebSocket connection, subscribed to exactly the entities shown.
  Changes to other entities are never sent to the deck. Thirty seconds after the last one leaves the screen, the connection is
  closed.
- One request for each action.
- One request for the list of entities when a reading names an entity that does not exist (for the suggestion), when you list
  them with `readings browse`, or when you test the connection.

The token goes only to that address. With `http://` it travels unencrypted on your network, as it does for Home Assistant's own
app; use `https://` if your Home Assistant has it. See [privacy](/privacy/).

## Demo mode and pictures

With `--demo`, and in the site's pictures, Home Assistant readings come from made-up entities (`light.living_room`,
`sensor.office_temperature`, `sensor.desk_power`, …) and any other entity id shows `on`. Nothing is contacted, and actions do
nothing.
