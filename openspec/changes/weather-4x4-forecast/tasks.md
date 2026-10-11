## 1. SDK: kinds and formatting

- [ ] 1.1 Add `ReadingKind.Date`, `Speed` and `Depth`; a `DepthUnit` enum (`Millimetre`, `Centimetre`) and `ReadingDescriptor.DepthUnit` (null reads as millimetres); a date reading uses the existing time value (`ReadingValueType.Time`), so no new value type
- [ ] 1.2 Add `DateStyle` (`Weekday`, `MonthDay`) and `ReadingFormatOptions.DateStyle`; add `IReadingSource.RegionCulture` as a default member returning `CultureInfo.CurrentCulture` (the hub and test sources can override it)
- [ ] 1.3 `ReadingFormatter`: the date case (weekday abbreviation or month and day in the value's own offset and the region's culture; widest text; dash when unavailable; kind follows the value without a descriptor stays `Time`)
- [ ] 1.4 `ReadingFormatter`: the speed case (km/h to mph by the display unit, no decimals by default, `mph` or `km/h` on the baseline) and the depth case (canonical unit from the descriptor, imperial `"` raised or metric `mm`/`cm` on the baseline, one decimal below 10 and none from 10, `0` for zero, `<0.1` for a positive amount that rounds to 0.0)
- [ ] 1.5 Read the display unit as the system for speed and depth (Celsius metric, Fahrenheit imperial) in the same order as temperature: widget, description, region
- [ ] 1.6 Add `ReadingFormatter.CompassPoint(double degrees)` (eight points, any bearing, negative and over 360 normalised)
- [ ] 1.7 Tests in `ReadingFormatterTests` for every scenario of the data-providers delta (weekday, date at another place, month and day, speed, snow in inches and centimetres, rain in millimetres, large amount, trace, zero, compass, one setting for the whole card, region decides), plus stale, pending and unavailable for each new kind
- [ ] 1.8 Move the SDK package version from 0.7.0 to 0.8.0 with a note in the csproj comment, keep `AssemblyVersion`, and make `SdkContractTests` pass

## 2. Weather provider

- [ ] 2.1 Probe Open-Meteo once for one place and for three places with the new request (`current=` plus `relative_humidity_2m,wind_speed_10m,wind_direction_10m`; `daily=` weather_code, temperature max and min, sunrise, sunset, `precipitation_probability_max`, `rain_sum`, `showers_sum`, `snowfall_sum`; `forecast_days=7`; `timezone=auto`; `timeformat=unixtime`); save the answers under `tests/UrDeck.Engine.Tests/Fixtures/weather/` (keep the old fixtures until the tests move); edit a copy for a day with rain and snow together, one with nulls (a missing snowfall), and the polar case
- [ ] 2.2 Extend the request in `ForecastClient`; replace the day fields of `PlaceWeather` with a list of up to seven day records, add humidity, wind speed and wind direction; `WeatherParser` reads them (a missing day or field is null, an answer with fewer than seven days is accepted)
- [ ] 2.3 `WeatherCatalog`: replace `today/*` with `daily/0` to `daily/6` (nine fields, written out), add `current/humidity` (percentage), `current/wind-speed` (speed, range 0 to 150), `current/wind-direction` (number, `°`, range 0 to 360); rain with `DepthUnit.Millimetre`, snow with `DepthUnit.Centimetre`; every description keeps the attribution; labels and names as in the spec
- [ ] 2.4 `WeatherProvider.ReadingFor`: map every path to its value; the date is the start of the day at the place with the place's offset; rain is `rain_sum + showers_sum` (unavailable when neither is supplied); snow is `snowfall_sum`; a day with no precipitation is zero and available; the sunrise and sunset reason text works for every day
- [ ] 2.5 Move the provider tests that name `today/*` to `daily/0` (`WeatherProviderTests`, `SdkWeatherTests`, `StatsAttributionTests`) and add tests for: all seven days published, rain and snow on one day, a null snowfall unavailable with a reason, a day beyond 6 unavailable as "no such reading", `current/visibility` unavailable, the weekday at the place, wind, one request serving a widget at 4x2 and one at 4x4, the catalog listing the seven days and the new current readings, the attribution on the new entries
- [ ] 2.6 A test that subscribes a full 4x4 set of ids for one place and for two places, and checks one forecast request per place per sample

## 3. Weather widget: 4x2 on daily/0

- [ ] 3.1 Add `[WidgetSize(4, 4)]`; subscribe by size (`Config.Width` and `Height`); the 4x2 subscribes to its eight ids with `daily/0` replacing `today`; `WeatherConfig` gains `Days` and `AnimateForecast` (unknown properties still preserved; `days` read as an int that clamps to 1 to 7, a non-number counts as not present and is kept in the file)
- [ ] 3.2 Unit setting: map `celsius` and `fahrenheit` to the display unit passed to every formatted reading, including the new kinds
- [ ] 3.3 Move the existing `WeatherWidgetTests` to `daily/0` and make them pass unchanged otherwise (the 4x2 card is pixel-identical)

## 4. Weather widget: the 4x4 card

- [ ] 4.1 `Render` splits the body at 4x4: the top half calls the existing 4x2 drawing with the same proportions, the lower half draws the forecast panel; the credit strip is measured once for the whole card as today
- [ ] 4.2 The strip of now: feels like, humidity, and wind with the compass point (`14 mph NW`), one line, from the formatter
- [ ] 4.3 The snow total over the shown days: sum of the canonical values, dash when any shown day's snow is unavailable or pending with no value, muted when any is stale, formatted through the formatter (trace, zero and rounding rules); label with the number of days (`today` for one)
- [ ] 4.4 The forecast columns from today for `days` days: weekday (`DateStyle.Weekday`), icon, high and low, chance, rain amount, snow amount, with amounts drawn only when positive (and the space kept), alignment across columns, dashes for a day without data, theme text sizes and no pixel constants, correct for 1 to 7 days
- [ ] 4.5 Forecast icons: render the poster frame of each distinct daytime icon once into a bitmap at the drawn size, cache by icon name and size, release the animation at once, clear on reconfigure and dispose; a missing animation draws nothing and logs once; the neutral icon for a day without a condition
- [ ] 4.6 `animateForecast`: when `true`, load one animation per distinct forecast icon (at most `days`), drive them with the main icon's loop timing, and report `IsAnimating` and `NextAnimationAt` for all of them; `motion: off` wins and shows only poster frames
- [ ] 4.7 `Shown` holds the per-day values in a type with element-wise equality; `NeedsRender` repaints for a change in a shown day or the total and not for a day beyond `days`
- [ ] 4.8 Tests in `WeatherWidgetTests` (rendered with a fake reading source): the full 4x4 card at 5 days, 1 day and 7 days with nothing clipped or overlapping, mixed rain and snow, a dry day, a trace of snow in inches, imperial and metric totals (the numbers of the spec), a missing day gives a dash total and a missing day beyond `days` does not, `days` default, clamped and non-number, weekday at the place, the 4x2 ignoring `days` and `animateForecast` and keeping them in the file, forecast icons still by default, animated when asked, `motion: off` wins, daytime variant at night, the poster on first paint, an unavailable forecast condition shows the neutral icon, repaint rules (day inside and beyond `days`), animations released on reconfigure, the credit once for the whole card, a theme change
- [ ] 4.9 A reload test: a plugin load with a 4x4 weather card releases its load context after the card is removed (as the 4x2 test does)

## 5. Verify

- [ ] 5.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, and `openspec validate --all --strict`
- [ ] 5.2 `UrDeck.Host.exe --snapshot` of a page with a 4x4 weather card at the owner's place (needs the network), with `days` 5 and 7, imperial and metric, and with a 4x2 card beside it: values and the credit, not dashes; look at the PNG for clipping and alignment
- [ ] 5.3 On the panel (the owner looks): the 4x4 card's proportions and readability, the weekday and icon row, rain and snow amounts on a day that has them, `animateForecast: true` looking right, and `motion` unchanged on the main icon; back up `bin/urdeck-config.json` first (AGENTS.md)
- [ ] 5.4 Measure the host with one 4x4 card (CPU, GPU, private bytes) with `motion` periodic and full, with `animateForecast` off and on, and with the 4x2 for comparison; record it in `docs/perf/weather.md`; if the 4x4 costs much more per frame than the 4x2, record it and decide with the owner before caching the forecast panel
- [ ] 5.5 A page with no weather still logs no weather provider start and makes no request

## 6. Documentation

- [ ] 6.1 `README.md`: the 4x4 size and its settings (`days`, `animateForecast` with its cost), the new readings table (`daily/N/*`, `current/humidity`, `current/wind-speed`, `current/wind-direction`) replacing `today/*`, `unit` choosing the system, the trace rule, that snow and rain are model forecasts, and an example card
- [ ] 6.2 `docs/BACKLOG.md`: record `weather-4x4-forecast` as implemented with the date, replace the `today/*` names in item 7, and update "Next" and the suggested order; add the new kinds to the SDK notes
- [ ] 6.3 `docs/ROADMAP.md`: tick the 4x4 weather widget in milestone 1 and update "Current state"
- [ ] 6.4 `CONTRIBUTING.md` or the SDK docs: a short note that a depth reading declares its canonical unit and that `unit` chooses the metric or imperial system for speed and depth
