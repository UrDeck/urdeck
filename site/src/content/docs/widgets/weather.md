---
title: Weather
description: A weather card with animated icons and a forecast, for any place.
section: Widgets
order: 12
---

`urdeck.widgets.weather` is a 4x2 card with an animated colour icon (day and night variants), the temperature, the
condition in words, today's high and low, the place, and sunrise and sunset in the place's own local time. At 4x4 the same
card sits on top of a forecast: what it feels like, the humidity and the wind, the snow expected over the days shown, and
one column per day with the weekday, an icon, high and low, the chance of precipitation and the rain and snow expected.

It takes its data from the `weather` provider and needs no provider setting: the place is typed into the widget.

```json
{ "typeId": "urdeck.widgets.weather", "col": 0, "row": 0, "width": 4, "height": 2, "location": "Portland, OR" }
{ "typeId": "urdeck.widgets.weather", "col": 0, "row": 2, "width": 4, "height": 4, "location": "Portland, OR", "unit": "fahrenheit", "days": 5 }
```

| Setting | Meaning |
|---|---|
| `location` | The place as text: a city (`Zurich`), `City, ST` (`Portland, OR`), `City, State` (`Portland, Oregon`), a postal code (`90210`), optionally with a country code after a comma (`Paris, FR`). It is looked up once per run and the first match is used; `urdeck.log` lists the other matches, so an ambiguous name (`Springfield`) can be fixed by adding a state or by giving coordinates. There is no default: with none the card shows dashes. |
| `latitude`, `longitude` | Decimal degrees. When both are set they replace `location` and no search is made. |
| `label` | Absent: the place's resolved name. A text: shown instead. `""`: no place is shown. |
| `unit` | `celsius` or `fahrenheit`. Absent: the region's. It chooses the system of the whole card: Celsius is kilometres per hour, millimetres and centimetres; Fahrenheit is miles per hour and inches. |
| `motion` | `periodic` (the default) plays the icon's loop once a minute and rests on its first frame in between; `full` loops it all the time; `off` shows only the first frame. |
| `periodSeconds` | Seconds between loops in the `periodic` mode (default 60). |
| `days` | 4x4 only: how many forecast days, 1 to 7 (default 5). |
| `animateForecast` | 4x4 only: `true` lets the forecast icons move as `motion` says. Off by default: each moving icon costs about 2.7 MB of memory. |

The card's parts follow the [theme](/docs/themes/): at 4x4 the rule between now and the forecast is the theme's `divider`, each forecast day can sit on the theme's `well`, and the condition on its `chip` (a pill, in some themes). A theme that sets none of them draws the card as before.

Any number of weather cards can show different places on one page; two cards with the same place share one request.
A new or edited place is fetched at once. The data is fetched every 15 minutes
(`"providers": { "weather": { "intervalMs": 600000 } }`; the minimum is 5 minutes). When the network is down the values
stay on the card in the dimmed colour and come back by themselves.

## What goes over the network

The weather provider sends the place you typed to [Open-Meteo](https://open-meteo.com/) to find its coordinates, and the
coordinates to get the forecast. That is all it sends, and nothing is sent while no weather card or weather reading is
shown. Use `latitude` and `longitude` to skip the search.

## Readings

The provider's readings, for any place (`weather:<place>/<reading>`, `@lat,lon` for coordinates), can be shown by a
[Stats](/docs/widgets/stats/) slot:

| Reading | Meaning |
|---|---|
| `place` | The resolved name of the place. |
| `current/temperature`, `current/apparent` | Air temperature now, and what it feels like. |
| `current/condition` | `clear`, `partly-cloudy`, `cloudy`, `fog`, `drizzle`, `rain`, `sleet`, `snow`, `thunder` or `unknown`. |
| `current/humidity` | Relative humidity, in percent. |
| `current/wind-speed`, `current/wind-direction` | Wind speed and the bearing it blows from, in degrees. |
| `daily/N/condition`, `daily/N/high`, `daily/N/low` | Day `N` (`0` is today, up to `6`): the condition and the extremes. |
| `daily/N/sunrise`, `daily/N/sunset` | A time in the place's own zone. |
| `daily/N/precip-chance`, `daily/N/rain`, `daily/N/snow` | The chance of precipitation in percent, and the rain and snow expected. |

```json
{ "typeId": "urdeck.widgets.stats", "col": 0, "row": 2, "slots": [ { "reading": "weather:Tokyo/current/temperature", "label": "Tokyo" } ] }
```

Rain and snow are model forecasts, not measurements.

## Credit

Weather data is by [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0), and every card that shows it draws the credit
"Weather data by Open-Meteo.com"; no setting hides it. Open-Meteo's free service is for **non-commercial use only**;
commercial users need an Open-Meteo subscription, which UrDeck does not support yet. The place database is from
[GeoNames](https://www.geonames.org/) (CC BY 4.0) and the icons are
[Meteocons](https://github.com/basmilius/weather-icons) (MIT).
