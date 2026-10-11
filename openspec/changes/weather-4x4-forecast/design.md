## Context

See `proposal.md` for why and the specs for what. What this builds on:

- **The weather provider** (`providers/UrDeck.Providers.Weather`) asks Open-Meteo for `current=` (temperature, apparent,
  is_day, weather_code) and `daily=` (max, min, sunrise, sunset) with `forecast_days=1`, parses it into one
  `PlaceWeather` per place, and publishes nine readings per location. Its catalog is nine `{location}/...` patterns,
  which the engine instantiates per place and caches (`ReadingHub` pattern resolution, from the `weather` change).
- **The weather widget** (`widgets/UrDeck.Widgets.Weather`) is a 4x2 card: `Render` measures the credit strip, takes it
  off the bottom of the content area, draws the icon and `DrawCard` into the remaining `body` rectangle. What it draws is
  captured in a `Shown` record; `NeedsRender` compares it with the last one. Subscriptions are computed from the
  config and reset in `OnConfigured`.
- **The stats widget** already grows through four sizes as one widget and learns its size from
  `context.Config.Width` and `Height` (grid cells), so a widget needs no size API of its own.
- **The formatter** (`ReadingFormatter`) handles number, percentage, temperature, text, on/off and time. A temperature's
  display unit is chosen widget setting, then description, then region (`RegionUsesFahrenheit`). A time is an instant
  plus a UTC offset (`ReadingValueType.Time`), formatted in that offset.
- **The icons** are 13 Meteocons Lottie files embedded in the widget DLL, loaded one at a time through the SDK's
  `LottieAnimation`. A loaded animation costs about 2.7 MB of private bytes (`docs/perf/weather.md`).
- Open-Meteo answers `current` and `daily` in one request; `daily` takes `rain_sum`, `showers_sum`, `snowfall_sum`
  (centimetres of snow, not melted water) and `precipitation_probability_max`. Probed on 2026-10-11 with
  `timeformat=unixtime`: a `daily.time` entry is the start of the local day, and wind speed is km/h by default.

## Goals / Non-Goals

**Goals:**

- One card, two sizes, with the 4x2 composition reused unchanged as the top of the 4x4.
- New SDK reading kinds general enough that the next provider (a different weather source, a sports scoreboard, a
  calendar) uses them unchanged.
- The cost of the 4x2 card does not grow, and a 4x4 card with still forecast icons costs little more than a 4x2.

**Non-Goals:**

- A hourly forecast, a precipitation type other than rain and snow, snow depth on the ground.
- A user-wide units setting (the widget's `unit` plus the region is enough for now).
- Localising condition words (the weekday and month are localised by the formatter because the platform does it for
  free; the condition words stay English as before).

## Decisions

### 1. One widget, the 4x4 card is the 4x2 card on top of a forecast panel

```
 4x4 body (inside the card's padding, above the credit strip)
 ┌────────────────────────────────────────────────┐
 │  the 4x2 composition, drawn by the same code,   │  top half of the body
 │  into a rectangle of the 4x2 body's proportions │
 ├────────────────────────────────────────────────┤
 │  feels like · humidity · wind 14 mph NW         │  now strip (one line)
 │  Snow, next 5 days            9.2"              │  total (one line)
 │  Wed     Thu     Fri     Sat     Sun            │
 │  icon    icon    icon    icon    icon           │  forecast columns
 │  31/18   28/15   ...                            │
 │  80%     90%     ...                            │
 │  2.1"    4.3"                                   │
 │          0.2"rain                               │
 └────────────────────────────────────────────────┘
 credit strip (once, for the whole card)
```

`Render` already hands `DrawCard` a body rectangle and an icon size relative to that rectangle's height, so at 4x4 it
calls the same method with the top half and a second method for the lower half. The 4x2 code does not change, the
two sizes cannot drift apart, and the proportions follow the card rather than pixels. The widget chooses the layout from
`Config.Width` and `Config.Height`: a 4x4 grid cell count, as the stats widget does.

Alternatives considered: a second widget type for the 4x4 (a second id, two configs to keep in step, and the owner
already said same widget); drawing the 4x4 from scratch (duplicates the icon, temperature and place layout, and two
layouts of one idea would drift).

### 2. Forecast ids are enumerated: `daily/0` to `daily/6`, nine fields each

The catalog lists 7 x 9 forecast patterns beside the 9 current ones, each a `{location}/daily/N/<field>` pattern with
`N` written out. The engine's pattern resolution takes no change: it already matches one `{name}` segment, and a
second parameter (`{day}`) would need a second kind of segment and a rule for what `N` may be.

`today/*` goes away rather than aliasing `daily/0`: two names for one reading would have to stay equal forever, and
there is no consumer outside this repository. The 4x2 widget, the provider tests and the stats tests that name
`today/*` move to `daily/0`.

Alternatives considered: a parameter `{day}` with a numeric constraint (new engine rule for little gain); keeping
`today/*` as an alias (two names; the catalog would double); an array-valued reading (the value model has no arrays and
a column needs one reading per field to be unavailable on its own).

### 3. `Date` is a kind over the existing time value

A date reading holds the same value a time reading holds: an instant and the UTC offset to read it in. What changes is
how it is shown: the day, not the clock. A new kind makes the description say which one the reading means, so a catalog
browser or a widget that formats a reading without special knowledge shows `Wed` for a date and `06:42` for a sunrise.
The formatter gets `ReadingFormatOptions.DateStyle` (`Weekday`, `MonthDay`; the weekday is the default) and takes the
language from the user's region (`CultureInfo.CurrentCulture`, with `IReadingSource.RegionCulture` as a default member
so tests can fix it, like `RegionUses24HourClock`). Day values are the start of the local day with the place's offset,
so the weekday is the place's weekday.

Alternatives considered: only a formatter option on `Time` (the catalog entry cannot say what the reading means, so the
default would be wrong for the reading that most needs a different one); a new value type (nothing the existing value
cannot hold).

### 4. `Speed` and `Depth` are kinds; one display unit picks the system

`ReadingKind.Speed` is canonical km/h. `ReadingKind.Depth` is an amount of precipitation whose description declares
its canonical unit with a new `ReadingDescriptor.DepthUnit` (`Millimetre`, `Centimetre`; `Millimetre` when absent).
The provider reports rain in millimetres and snow in centimetres exactly as Open-Meteo does, so it converts nothing.

The display unit follows the order temperature already has, widget, description, region. To keep one user setting for a
card, the existing `DisplayUnit` is read as the system: `Celsius` is metric, `Fahrenheit` is imperial, for all three
kinds. Metric shows speed as km/h and a depth in its canonical unit; imperial shows mph and inches. No new region
property is needed, because a region that uses Fahrenheit is a region that uses miles and inches.

Alternatives considered: separate `SpeedUnit` and `DepthUnit` options on every format call (more precise, and nothing
needs it yet; `unit` stays one word to type); a `UnitSystem` enum beside `DisplayUnit` (renames a public name to say the
same thing); two depth kinds, `Rainfall` and `Snowfall` (the formatter would repeat itself for each, and a description's
unit says the same thing).

### 5. Depth rounding and the trace rule live in the formatter

A depth has one decimal below 10 of the displayed unit and none from 10 up, so `2.1"` and `11"` both read well and a
column's width stays steady. A positive depth that would show as `0.0` shows `<0.1`, and zero shows `0`. The threshold
is in the displayed unit, so the same rule holds for inches, millimetres and centimetres (a trace of rain is under 0.1
mm). Every widget that shows snow therefore treats a trace alike, which is the reason it is not in the weather widget.

### 6. Wind direction is a number; the compass point is a formatter helper

`current/wind-direction` is a plain number (0 to 360, unit `°`) so any widget, a gauge or a rotated arrow, can use it.
The SDK gains `ReadingFormatter.CompassPoint(double degrees)` returning one of eight points; the widget composes
`14 mph NW` itself. A text reading of the compass word would put presentation in the provider and lose the angle.

### 7. Rain is `rain_sum + showers_sum`; snow is `snowfall_sum`; the chance is `precipitation_probability_max`

`precipitation_sum` is the water content of everything and counts snow as meltwater, so it is neither rain nor snow.
Rain and snow are two readings because they have different units and a day near freezing has both. A day with no
precipitation is zero and available (the source says 0.0), and a missing value is unavailable with a reason. The chance is one
number per day, not split by type, as the source gives it.

### 8. The request always asks for seven days and every field

Demand is per reading, but the request is per place. Asking only for the days and fields that are shown would make two
widgets at one place with different `days` settings need different requests (or a merge of them), and the saving is a
few hundred bytes. One fixed request per place keeps the provider as simple as it is now. The parser reads days into an
array on `PlaceWeather`, and `ReadingFor(reading)` maps the catalog path to a field of day N.

### 9. Forecast icons are still pictures unless `animateForecast` is on

Five Skottie animations would cost about 14 MB of private bytes and a repaint of five icons per frame. Still, the icon
is the poster frame of an animation, so the widget draws each distinct forecast icon once into a small bitmap at the
size it is drawn, keeps the bitmaps (about 0.1 MB each at that size) and releases the animations at once. The cache is
keyed by icon name and size, and cleared on reconfigure and dispose, like the main icon. A day with a missing animation
draws nothing and logs once, like the main icon.

With `animateForecast` on, the widget loads one animation per distinct icon of the days shown (at most `days`),
the same way the main icon is loaded, and drives them with the main icon's loop timing (`motion`, `periodSeconds`), so
all of them loop together and `IsAnimating` and `NextAnimationAt` need no new rule. The setting's description in the
README states the cost.

Alternatives considered: parse and draw the animation on every paint (a paint at 15 fps would parse five files per
frame); keeping animations loaded but seeking only the poster frame (pays the 14 MB for no motion); a still PNG set shipped
beside the Lottie files (twice the assets, and a licence notice to repeat).

The day variant is always used for forecast icons: a day's condition describes the day, and the night variant would show
a moon for a daytime forecast.

### 10. The snow total is the widget's sum over the shown days

The widget subscribes to the snow of exactly the days it shows, sums their values in the descriptor's canonical unit
(centimetres), and formats the sum through the formatter, so the trace and rounding rules apply to the total too. It
draws a dash when any shown day's snow is unavailable or pending with no value, and in the muted colour when any is stale. Changing `days` changes the
subscriptions and the sum. A provider-published total would sum a fixed span and not what the card shows.

### 11. Subscriptions and the repaint record depend on the size and on `days`

At 4x2 the widget subscribes to what it does today (with `daily/0` for the high, low, sunrise and sunset). At 4x4 it
adds `current/apparent`, `current/humidity`, `current/wind-speed`, `current/wind-direction` and, for each shown day,
the nine day readings it uses (`date`, `condition`, `high`, `low`, `precip-chance`, `rain`, `snow`; sunrise and sunset
only for day 0). `OnConfigured` already drops the cached ids, so editing `days` re-subscribes with no new
mechanism. The `Shown` record grows a list of per-day values that compares by element (a small record struct with
value equality, because arrays and lists compare by reference), so `NeedsRender` stays a cheap comparison.

### 12. The 4x4 layout is measured from the theme, not from pixels

Row heights come from the theme's text steps (`GetTextSize`) and the body height, as `DrawCard` does; column widths from
the body width divided by the number of days. Icon size is the smaller of the column width and the height left over.
For 7 days the columns are narrower, and text that does not fit its column is shortened by `TextLine`'s existing
fitting rather than clipped. No pixel constant appears.

## Risks / Trade-offs

- [A 4x4 repaint draws much more than a 4x2 each frame, and the main icon animates for a few seconds a minute] →
  Measure at 4x4 with `motion` periodic and full (task 8), and compare with `docs/perf/weather.md`. If it costs too
  much, the forecast panel can be drawn into a cached bitmap that is reused while only the main icon moves; this is
  held back until a measurement says it is needed.
- [Seventy-odd catalog patterns slow pattern resolution or the catalog listing] → Resolution caches the concrete
  description, listing is rare; a test lists the catalog and a test subscribes a full 4x4 set.
- [Snowfall from a forecast model is uncertain, and a total looks more exact than it is] → The card shows the value the
  source gives, rounded as above; the README says it is a model forecast.
- [Mixed language: weekdays follow the region, condition words are English] → Accepted for now; localised condition
  text is already out of scope, and the formatter's culture is injectable so a later change can fix both.
- [Renaming `today/*` breaks a stats slot in someone's config] → Documented in the README and the proposal; there is
  no other consumer, and no tracked config uses it. The owner's own config in `bin/` is checked when it is tested.

## Migration Plan

`today/high`, `today/low`, `today/sunrise` and `today/sunset` become `daily/0/high`, `daily/0/low`, `daily/0/sunrise`
and `daily/0/sunset`. Nothing else is renamed. A page that has only 4x2 weather cards needs no edit. Rollback is
reverting the change: the SDK additions are additive and the assembly version does not move; the package version
moves from 0.7.0 to 0.8.0.
