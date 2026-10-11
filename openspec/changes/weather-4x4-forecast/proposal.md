## Why

The 4x2 weather card shows only today. The owner wants a larger card that also shows the next few days, humidity and
wind, and, as a snowmobiler, how much rain and snow is coming (one inch or ten). This is the "4x4 weather widget" item
of milestone 1 (the daily driver) and the second weather change. Nexus has no 4x4 weather widget, so the content is
the owner's design, not a copy.

## What Changes

- **The weather widget gains a second size, 4x4**, as the same widget (`urdeck.widgets.weather`). The top of the 4x4
  card is the 4x2 composition; below it come a strip of "now" readings (feels like, humidity, wind), the snow total of the
  shown days, and a row of forecast columns (weekday, icon, high and low, chance of precipitation, rain, snow).
  New settings: `days` (how many forecast days, default 5, 1 to 7) and `animateForecast` (default off: forecast icons
  are the still poster frame; the main icon keeps its `motion` setting).
- **The weather provider offers a seven day forecast.** **BREAKING:** `today/*` is replaced by `daily/0` to `daily/6`
  (`daily/0` is today). Each day has `date`, `condition`, `high`, `low`, `sunrise`, `sunset`, `precip-chance`, `rain`
  and `snow`. New current readings: `current/humidity`, `current/wind-speed`, `current/wind-direction`. Rain is the
  sum of Open-Meteo's `rain_sum` and `showers_sum` (liquid), snow is `snowfall_sum`; a day on the freezing line can have
  both. The request always asks for seven days and every field, so two widgets on one place never need different requests.
- **Three new reading kinds in the SDK:** `Date` (the existing time value, shown by default as a weekday),
  `Speed` (canonical km/h, shown as mph or km/h) and `Depth` (precipitation; each description declares its canonical
  unit, mm for rain and cm for snow, shown as inches or mm and inches or cm). The formatter gains a `DateStyle` option,
  a display unit for the new kinds that follows the same order as temperature (widget setting, description, region),
  a compass word for a wind direction, and one rule for small amounts: a positive depth under 0.1 inch (or its metric
  equivalent) is shown as `<0.1"`, and zero is shown as `0`.
- **The widget computes the snow total.** It sums the snow of the days it shows, and shows a dash when any of those days
  has no value, never a partial sum.
- **The 4x2 card moves to `daily/0`** and is otherwise unchanged. A stats slot written `weather:<place>/today/high` must
  be rewritten; the stats widget itself needs no change, and no tracked config uses `today/*`.
- Docs: README (readings and settings), `docs/BACKLOG.md` and `docs/ROADMAP.md` (milestone 1 item done), and a cost
  note in `docs/perf/weather.md`.

Deliberately not in this change: base snow depth on the ground (`current/snow-depth`), hourly forecasts, a rain or
snow total published by the provider (the widget sums what it shows), a separate setting for metric versus imperial
wind and depth (the existing `unit` setting implies the system for all of them), animated forecast icons by default,
precipitation types other than rain and snow, an apparent-temperature forecast, UV index and wind gusts.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `weather-provider`: the readings table (`daily/N/*` replaces `today/*`, new current readings, forecast days,
  rain and snow), the units of the new readings, and the network behaviour (seven days in one request).
- `widget-weather`: the 4x4 size, its composition, `days` and `animateForecast`, the snow total, and the sizes
  offered; the 4x2 card reads `daily/0`.
- `data-providers`: the `Date`, `Speed` and `Depth` kinds, a per-description canonical unit for `Depth`, display unit
  selection for speed and depth, the `DateStyle` option, the compass word and the small-amount rule in formatting.

## Impact

- `sdk/UrDeck.Sdk`: `ReadingKind` (`Date`, `Speed`, `Depth`), `DisplayUnit` (speed and depth units, or sibling enums),
  `ReadingDescriptor` (a canonical depth unit), `ReadingFormatOptions` (`DateStyle`, speed and depth units),
  `ReadingFormatter`, `IReadingSource` (region defaults for speed and depth units). All additive, so the assembly
  version stays; the package version moves.
- `src/UrDeck.Engine`: `ReadingHub` region detection for the new units; nothing else, because patterned catalog
  descriptors already carry any kind.
- `providers/UrDeck.Providers.Weather`: catalog, parser, `PlaceWeather` (a list of days), request fields, fixtures.
- `widgets/UrDeck.Widgets.Weather`: size, config, 4x4 layout, forecast columns, snow total, repaint record.
- The owner's working config in `bin/` if it has a stats slot on `today/*` (back it up first; see AGENTS.md).
- Tests: the existing weather provider and stats tests that name `today/*` move to `daily/0`; provider parsing against new fixtures, formatter tests for the new kinds and the trace rule, widget tests at
  4x4 (columns, mixed rain and snow, missing day, `days`, `animateForecast`), snapshot checks.
- Network: still one forecast request per place per interval; the answer grows from one day to seven and a few fields.
  No new limits are approached.
- Open-Meteo terms are unchanged: the credit line stays on both sizes.
