## Why

The owner's first page needs a weather card, and it is the next missing piece after the clock and the performance
card. It is also the first widget that needs several things the platform does not have yet: a provider that talks to a
network, readings that depend on a place the user types into a widget, a point in time as a reading (sunrise and
sunset), and full-colour looping animated icons. This is the "Weather" step of the roadmap's suggested order (item 7,
"Weather widget"), and it goes before the elevated-sensors change.

## What Changes

- **A `weather` data provider** that reads Open-Meteo (keyless): current conditions, today's high and low, sunrise and
  sunset. It resolves a place the user types (a city, a zip code, "City, ST", "City, State") through Open-Meteo's
  geocoding, fetches the wanted places once per interval (batched into one request where the API allows), and
  publishes a provider-neutral vocabulary
  (a portable condition class, not Open-Meteo's weather codes).
- **Locations travel in the reading id.** The location is the first path segment
  (`weather:Portland, Oregon/current/temperature`), so any number of widgets can show different places and the
  provider needs no settings beyond `intervalMs`. The provider also publishes the resolved place name, so the card
  shows which place was picked.
- **Patterned catalog descriptors.** A provider may describe a reading with a path containing `{location}`; the
  engine resolves a subscribed path against exact entries first and then against these patterns, and caches the
  concrete description. Today an exact dictionary would mark every location unavailable.
- **A time as a reading.** A new value (a `DateTimeOffset`: an instant plus its UTC offset) and a new reading kind
  `Time`, which the formatter shows as the time of day in the value's own offset, with the region's 12 or 24 hour
  setting. Sunrise and sunset are the first readings of it.
- **A Lottie component in the SDK** over `SkiaSharp.Skottie`: create an animation from a stream, seek it to a time,
  draw it into a rectangle. It is the first SDK component that holds state (an `IDisposable` the widget owns), so the
  rule "a component holds no state between paints" is amended. Skottie types stay out of the SDK's public surface.
- **A 4x2 weather widget** (`urdeck.widgets.weather`): an animated colour icon with day and night variants, the
  temperature, the condition, today's high and low, the place, and sunrise and sunset. The Meteocons icons are
  embedded in the widget's assembly. The widget has a `motion` setting (`full`, or `off` to freeze on a poster
  frame); a still poster frame is also what `--snapshot` and the first paint show.
- **Attribution travels with the data.** A reading's description may carry an attribution text and link. The weather
  card always draws "Weather data by Open-Meteo.com" (no setting hides it), and the stats widget draws the same credit
  for any slot whose reading carries one, so Open-Meteo's CC BY 4.0 terms are met wherever its data is shown.
  GeoNames (geocoding) and Meteocons (MIT) are credited in the notices and the README licence table.
- **A spike comes first.** Skottie has only been tried on one Meteocons file (`partly-cloudy-day`) and only in the
  render spike, not inside a plugin. The change starts with a short, time-boxed check that the packages reach the
  host output through the SDK, that every layer type the icon set uses parses and draws, and what the whole icon set
  weighs, and records it in `docs/perf/weather.md` before the widget is built.

Deliberately not in this change: the 4x4 weather widget (forecast days, humidity, wind; a roadmap item); named
location aliases; a disk cache for geocoding results; localised condition text; an `apiKey` setting for commercial
users and provider settings or secrets in general; the Windows location API (IP geolocation is rejected, it leaks the
location to a third party); wake requests, a transition helper and backing off when the PC is busy; a user-wide
motion level (only the widget's own `motion` setting); asset policy for third-party widgets that ship large media;
dates and durations as reading kinds.

## Capabilities

### New Capabilities

- `weather-provider`: the `weather` provider: identity, locations and geocoding, the reading vocabulary, sampling
  and network behaviour, failure states, and what it costs.
- `widget-weather`: the 4x2 weather widget: identity, configuration, composition, the credit line, icons, motion
  and repaint rules.

### Modified Capabilities

- `data-providers`: location segments in reading ids; the time value and the `Time` kind and its formatting;
  patterned catalog descriptors; attribution in a reading's description.
- `components`: a component may hold state that its widget owns; adds the Lottie animation component.
- `widget-stats`: draws the attribution of a reading that carries one.

## Impact

- `sdk/UrDeck.Sdk`: `ReadingValue` (time), `ReadingKind.Time`, `ReadingFormatter` (time), `ReadingDescriptor`
  (attribution; patterned paths), a Lottie component in `Components`, and package references to `SkiaSharp.Skottie`
  (with `SceneGraph` and `Resources`). All additive, so the assembly version stays; the package version moves.
  `Directory.Packages.props` gains the three packages at the SkiaSharp version.
- `src/UrDeck.Engine`: `ReadingHub` and `ReadingHub.Sampling` resolve catalog entries through one lookup that falls
  back to patterns (about six call sites read the dictionary today); instantiated descriptors are cached.
- `providers/UrDeck.Providers.Weather` (new, GPL, references only the SDK) and `widgets/UrDeck.Widgets.Weather`
  (new, GPL, references only the SDK; icons embedded as resources); both added to the `UrDeckPlugin` list in
  `UrDeck.Host.csproj`. `UrDeck.Widgets.Stats`: draws attribution.
- Tests: new provider and widget tests (geocoding and forecast parsing against recorded responses, condition mapping,
  demand with several locations, failure and stale states); engine tests for pattern resolution; SDK-facing tests for
  the time formatting and the Lottie component's lifetime.
- Network: the first outbound traffic in UrDeck. Only while a weather reading is shown; at most one forecast request
  per place per interval (default 15 minutes, floor 5), one geocoding request per place per session. Far below
  Open-Meteo's free-tier limits (600/minute, 5,000/hour, 10,000/day), which also require non-commercial use.
- Licences and notices: Meteocons (MIT, Copyright (c) 2020-2024 Bas Milius) licence text next to the icons, in the
  README licence table and the third-party notices; Open-Meteo (CC BY 4.0) and GeoNames (CC BY) credited; Skottie
  and SkiaSharp (MIT) in the notices. Wording is copied from the sources' own pages in the first task.
- Docs: `docs/perf/weather.md` (the spike and the cost), `README.md` (the widget, the readings, an example,
  licences), `docs/ROADMAP.md` (the 4x4 weather item, already added).
- The asset and icon art is Meteocons' look; a theme cannot restyle full-colour animated icons. Accepted by the owner.
