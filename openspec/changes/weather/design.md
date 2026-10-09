## Context

See `proposal.md` for why. The platform this builds on:

- **Providers** are plugins loaded into a collectible `AssemblyLoadContext` from `plugins/` and sampled by the engine's
  `ReadingHub`. A provider is created on its first subscription and stopped after its last. Its catalog is read once
  (`Describe()`), held in `ProviderRuntime.Catalog` as an exact `path -> ReadingDescriptor` dictionary, and consulted
  at about six sites (`CheckPath`, `CommitLocked`, `FailProvider`, `GetCatalog`, the `Wanted` loop and the
  `_descriptors` map behind `IReadingSource.Describe`). A path not in it is unavailable.
- **The hub already retries.** A sample that throws or times out is a failure: the readings go stale, and the next
  attempt comes after `min(interval x 2^(failures-1), 60 s)`, so a 15-minute provider that fails retries every
  minute. It also drops a value equal to the stored one, so an unchanged answer repaints nothing. A provider may
  publish from its own callbacks outside a sample (a "pushed" provider).
- **Reading ids** split at the first `:`; the path only has to be non-empty with no leading, trailing or doubled `/`,
  so free text is already a legal segment.
- **The SDK** holds the shared components (`Readout`, `TextLine`, `Gauge`), all static draw functions, and already
  references `SkiaSharp`. It is loaded once from the host's copy; a plugin's own `UrDeck.Sdk.dll` is not used.
  `PluginLoadContext.Load` asks the default context first and only then the plugin's folder, and the host build copies
  only each plugin's own DLL to `plugins/` (the shadow copy takes only the DLL, `.pdb` and `.deps.json`).
- **`animation`** gave widgets `IsAnimating` and one host frame clock at 30 fps. A widget draws from
  `WidgetRenderContext.Time` and rests on the first paint.
- **The render spike** (`H:\projects\urdeck-render-spike`) played a Meteocons file with `SkiaSharp.Skottie` 4.153.1:
  `Animation.TryCreate`, `SeekFrameTime`, `Render(canvas, rect)`. At 30 fps, repainting a whole 1050x520 card, it
  measured 3.4% of one core and 3.1% GPU 3D; 7.5% at 60 fps.

## Goals / Non-Goals

**Goals:**

- Weather for any number of places on one page, typed into each widget, with no provider settings.
- Credit every source the way its licence asks, wherever its data is shown, without each widget remembering to.
- Make the new platform pieces (a pattern catalog, a time value, a Lottie component, an attribution component) general
  enough that the next provider or widget uses them unchanged.
- No cost for a page without weather, and a bounded, small cost with it.

**Non-Goals:**

- The 4x4 weather widget and anything it needs (forecast days, humidity, wind).
- Named location aliases, a geocoding cache on disk, an API key, localisation, the Windows location API.
- A user-wide motion level, a pinned animation time, wake requests, backing off when the PC is busy.
- Changes to the plugin loader or the build's plugin copying.

## Decisions

### 1. The location is the first path segment of the reading id

```
 widget config                          ids the widget subscribes to
 location: "Portland, OR"   ───────▶    weather:Portland, OR/place
                                        weather:Portland, OR/current/temperature
                                        weather:Portland, OR/current/condition      ...
 latitude/longitude set     ───────▶    weather:@45.52,-122.68/...
```

The widget builds the segment: trimmed text with `%` written `%25` and `/` written `%2F`, or `@lat,lon` with invariant
culture formatting. The provider parses it back (`LocationKey`), compares case-insensitively like every id, and
treats `@` followed by two numbers as coordinates. Any other text is a search. The segment is also the key of every
per-location cache in the provider.

Alternatives considered:

- **Location in the provider's settings** (`providers.weather.location`): one place per install, and it brings back
  provider-defined settings, which nothing else needs yet.
- **A query string on the id** (`weather:current/temperature?place=Zurich`): general, but it changes the id grammar
  and every place that splits ids, for one provider.
- **One provider instance per place**: the hub has one runtime per provider id, and a batched request wants one
  instance to see all places.
- **Named locations in settings, widgets pick a name**: friendlier for ten widgets sharing a place, worse for the
  common case of typing a city into a widget. Parked.

### 2. Patterned catalog descriptors, resolved through one lookup

A descriptor path may contain `{name}` as a whole segment. `ProviderRuntime` keeps the exact entries as today plus a
list of patterns. One hub method resolves a path:

```
 Resolve(rt, "Zurich/current/temperature")
   1. exact dictionary hit                               -> that descriptor
   2. patterns with the same segment count, literal segments equal (ignoring case); most literal segments wins
   3. instantiate: replace {location} in Path, Label, Name and Device with the matched segment
   4. cache the instance in rt (and in _descriptors for Describe(id))   -> never re-instantiated
   5. no match                                           -> null (unavailable, "no such reading")
```

All six dictionary sites call it. Publishing resolves only paths that are in `Wanted` (a provider that publishes a
matching path nobody asked for is ignored and logged once), so a provider cannot grow the cache by publishing. The
caches are cleared by `ReleaseAll` like the rest. `GetCatalog` returns the exact entries plus the patterns as written.
A pattern that is malformed (an unclosed brace, a brace inside a segment) is logged and skipped at `CacheCatalog`.
An exact entry always wins, so a provider can special-case one location.

Alternatives considered:

- **A catalog that grows while the provider runs** (`sink.Describe`): the roadmap's parked item. It makes `Describe()`
  stop being "callable without `Start`", needs races between description and subscription to be handled, and lets a
  provider fill the catalog without bound. Patterns keep the catalog static and finite.
- **Wildcards in the matcher only, no template substitution**: then a description's device and label could not name
  the location, and every widget would need the provider's help to label a reading.

### 3. A time is a new value type holding a `DateTimeOffset`

`ReadingValueType.Time`, `ReadingValue.FromTime(DateTimeOffset)` and an implicit conversion, `Time` returning a
`DateTimeOffset`, and `ReadingKind.Time`. The struct keeps the instant as Unix seconds in the existing `double` and
adds the UTC offset in whole minutes in one `int`; equality includes both. The hub's "drop an equal value" test then
treats a changed offset with an unchanged instant as a change.

```
 Time reading --ReadingFormatter--> ReadingText( "06:42", null, Baseline )            24 hour
                                                ( "6:42",  "AM", Baseline )           12 hour
```

The formatter shows the time of day in the value's own offset. The clock comes from the widget's
`ReadingFormatOptions.Use24HourClock` (new, optional), then `IReadingSource.RegionUses24HourClock` (a default
interface member, true when the region's short time pattern has no `tt`; the hub takes it as a constructor option for
tests exactly as it takes the Fahrenheit flag). The AM/PM text is the literal `AM`/`PM`, as the Clock widget uses. The
widest text is `00:00` or `12:00`. Without a catalog entry the kind follows the value.

Alternatives considered:

- **`TimeSpan` (a time of day)**: loses the day and the zone. After sunset the widget needs tomorrow's sunrise, and a
  Tokyo widget must read Tokyo's sunrise in Tokyo's clock.
- **`DateTime`**: no offset.
- **Epoch seconds as a number with a unit**: no type safety, and no place for the offset.
- **A text such as `06:42`**: ignores the 12/24 hour setting and cannot be compared or sorted.
- **A kind per shape (`Date`, `DateTime`, `Duration`)**: dates and date-times are formatting styles of the same value,
  added when a use appears; a duration is a number with a unit.

### 4. Attribution is part of the description, drawn by a component

```
 ReadingAttribution(Text, ShortText?, Url?)        in UrDeck.Sdk.Data
 ReadingDescriptor.Attribution { get; init; }      copied when a pattern is instantiated
 Attribution.Measure(theme, attributions, width)   -> height          in UrDeck.Sdk.Components
 Attribution.Draw(canvas, theme, rect, attributions)
```

`Attribution` draws each distinct `Text` once as a label-step `TextLine` in the muted colour, switches to `ShortText`
when the full text does not fit on one line, and shrinks like any text line otherwise. `Measure` lets a widget carve
the strip out of its content rectangle before layout. It draws text only; the link is in the description for a future
tap or an about screen.

Why on the descriptor: the credit is a property of the data, not of the widget. Open-Meteo asks for it "next to any
location" its data is shown, so the stats widget, or any future widget, showing `weather:` readings needs it as much as
the weather widget. Carrying it on the description lets the stats widget honour it with no knowledge of Open-Meteo.

Alternatives considered:

- **Hard-code the credit in the weather widget**: leaves the stats widget (and a community widget) showing the data
  without it.
- **A page-level credits strip drawn by the host**: one place to look, but it takes space from the layout grid and
  needs a rule for which page shows what; revisit when the page indicator (item 8) arrives.
- **Say weather readings are for the weather widget only**: cannot be enforced.

### 5. Lottie is an SDK component over `SkiaSharp.Skottie`

`UrDeck.Sdk.Components.LottieAnimation : IDisposable`: `TryCreate(Stream, out LottieAnimation?)`, `Duration`, and
`Draw(SKCanvas, SKRect, double seconds)`. Draw seeks, computes the fit rectangle from the animation's own size
(aspect kept, centred, so a non-square target is not stretched), and renders. No Skottie type is public.

`UrDeck.Sdk.csproj` takes `SkiaSharp.Skottie` as a package reference, at the SkiaSharp version, which pulls in
`SceneGraph` and `Resources` (pure managed). The host reaches them through Host -> Engine -> Sdk, so they land in the
host's `deps.json` and the plugin load context resolves them from the default context, where `PluginLoadContext.Load`
already looks first. A plugin therefore never carries a private copy, and the loader and the build need no change.
Because the types live in the default context, a plugin that holds a `LottieAnimation` does not pin its own context
beyond the object it holds; the widget disposes it on reconfigure and in `Dispose`.

The SDK's `Version` moves to 0.6.0; `AssemblyVersion` stays (everything is additive), guarded by `SdkContractTests`.
Skottie's version is locked to SkiaSharp's in `Directory.Packages.props` with a comment, which is true of SkiaSharp
itself and is what the parked SDK versioning policy will state.

Alternatives considered:

- **A service on `IWidgetHost`**: its only gain was a shared parse cache, and a widget rarely shares an icon.
- **A private dependency of the weather widget**: the host build copies only the plugin's DLL, so Skottie would need
  the build to copy dependencies and the loader to stop logging them as "no widgets" plugins.
- **Hand-drawn vector icons**: themeable and cheap, but not the full-colour look the owner chose.

### 6. The weather provider

`providers/UrDeck.Providers.Weather`, GPL, references only the SDK, `[DataProvider("weather", "Weather",
DefaultIntervalMs = 900000, MinIntervalMs = 300000)]`.

```
 SetDemand(paths) ─▶ distinct LocationKeys ─▶ new ones? ─▶ single-flight Fetch(new)  (publishes outside a sample)
 SampleAsync     ─▶ Fetch(all wanted)  ── resolve (once per key) ─▶ forecast ─▶ parse ─▶ sink.Publish*
                                │                       │
                        no match: Unavailable     network error: throw  ─▶ hub marks stale, retries <= 60 s
```

- **Pieces**, each testable alone: `LocationKey` (parse, escape, normalise), `GeocodingClient`, `ForecastClient`,
  `WeatherParser` (JSON to a per-place model), `ConditionMap` (below), and an internal `IWeatherHttp` seam so tests
  feed recorded responses and never touch the network.
- **HTTP.** One `HttpClient` made in `Start` and disposed in `Shutdown`, over a `SocketsHttpHandler` with a short
  pooled-connection lifetime, a 20 second timeout, HTTPS URLs only, and a `User-Agent` of `UrDeck/<version>` with the
  repository URL. Nothing is static, so the plugin context can be collected.
- **Resolution.** `https://geocoding-api.open-meteo.com/v1/search?name=<text>&count=5&language=en`. The first result
  wins; the others are logged. The result is kept per key for the session, including "no match", which stays
  unavailable until the provider restarts (a different text is a different key). The place name is the result's
  `name` plus `admin1` when it differs from the name. A network failure while resolving is a failed sample, not a
  no-match. The 2026-10-08 probe is recorded in the roadmap.
- **Forecast.** `https://api.open-meteo.com/v1/forecast` with `current=temperature_2m,apparent_temperature,is_day,
  weather_code`, `daily=temperature_2m_max,temperature_2m_min,sunrise,sunset`, `timezone=auto`,
  `forecast_days=1`, `timeformat=unixtime`. The response's `utc_offset_seconds` gives the offset of every instant.
  Whether one request can carry several comma-separated coordinates with `timezone=auto` is checked in the spike; if
  it does the provider batches all places into one request, if not it sends one per place, which the spec allows.
- **New places do not wait.** `SetDemand` compares the wanted keys with the ones it has data for and starts a fetch
  for the new ones on the thread pool, serialised with `SampleAsync` by a semaphore so two fetches never overlap. It
  publishes outside a sample (a pushed publication), so a widget whose location was just edited shows values in
  seconds. A failure there publishes `Unavailable` with a reason for those places only, and logs once.
- **Failure.** `SampleAsync` throws on a forecast or resolution transport error, a non-success status or an answer it
  cannot read. The hub then marks the readings stale and retries within a minute. The provider logs the first failure
  and the first success after it, not every attempt.
- **Condition classes** from WMO codes: 0 and 1 `clear`; 2 `partly-cloudy`; 3 `cloudy`; 45 and 48 `fog`; 51, 53 and
  55 `drizzle`; 56, 57, 66 and 67 `sleet`; 61, 63, 65, 80, 81 and 82 `rain`; 71, 73, 75, 77, 85 and 86 `snow`; 95, 96
  and 99 `thunder`; anything else `unknown`.
- **Catalog.** The nine patterns of the spec. Temperatures declare no display unit (the region decides, unlike
  hardware readings, which declare Celsius). Every description carries the attribution of decision 4.

Alternatives considered:

- **Fully pushed provider with its own timer and backoff**: it would ignore the hub's interval setting
  (`providers.weather.intervalMs`) and duplicate the retry logic the hub already has. The hub-sampled provider with a
  push for new places gets both.
- **A provider-level geocoding cache on disk**: parked; one lookup per place per session is cheap.
- **Mapping WMO codes in the widget**: then every consumer of the readings would repeat it, and a second provider
  would have to speak WMO.

### 7. The weather widget

`widgets/UrDeck.Widgets.Weather`, GPL, references only the SDK. `[Widget("Weather", ..., Id =
"urdeck.widgets.weather")] [WidgetSize(4, 2)] [RefreshOnData] [Category("Weather")]`, with a `WeatherConfig`
(`location`, `latitude`, `longitude`, `label`, `unit`, `motion`) that preserves unknown properties the way
`StatsConfig` does.

```
 ┌──────────────────────────────────────────────────────────────┐
 │  ┌──────────┐    14°                                          │
 │  │          │    Portland, Oregon                             │
 │  │   icon   │    Partly cloudy                                │
 │  │  (loop)  │    High 17°   Low 9°                            │
 │  └──────────┘    Sunrise 6:42 AM     Sunset 7:03 PM           │
 │  Weather data by Open-Meteo.com                               │
 └──────────────────────────────────────────────────────────────┘
```

The sketch fixes the contents and their order, not the proportions; those are settled by looking at the panel (the
owner's standing preference), using `Readout` for the temperature and the two times, `TextLine` for the rest, and
`Attribution` for the strip, whose height is measured first and removed from the content rectangle.

- **Subscriptions** are the eight ids of the location in the table above (not `apparent`, which no composition shows).
  With no location it subscribes to nothing and the card shows dashes and the neutral icon.
- **Icon choice.** `(condition class, is-day)` maps to an animation named in a table in the widget. Meteocons names are
  confirmed against the real file list in the spike; the expected mapping is `clear` to `clear-day`/`clear-night`,
  `partly-cloudy` to `partly-cloudy-day`/`-night`, `cloudy` to `overcast` (or `cloudy`), `fog`, `drizzle`, `rain`,
  `sleet`, `snow` and `thunder` to the matching file, and `unknown` or a missing value to `not-available`.
- **Assets.** The chosen JSON files are `EmbeddedResource`s of the widget assembly, read with
  `GetManifestResourceStream` when an icon is first needed; the Meteocons licence text is embedded beside them. Only
  the animation on screen is parsed and kept; the previous one is disposed when the icon changes and on reconfigure.
- **Motion.** The widget records the render time of its first paint. The first paint, a new icon and `motion: "off"`
  draw the poster frame, a fixed fraction of the animation's duration chosen by eye in the spike. After the first
  paint, `t = (poster + elapsed) mod duration`, so the loop continues from the poster; a negative elapsed (a clock
  change) draws the poster. `IsAnimating` is true while `motion` is `full` and an animation is loaded. This is all
  inside the widget, using `context.Time`; there is no pinned animation time.
- **Repaint.** When not animating, `NeedsRender` compares a record of what was drawn (value texts, units, place,
  currency flags, icon key, credit) with the current one, as the stats widget does.
- **Condition text** comes from a small table in the widget (English). Localisation is parked.

### 8. The stats widget draws attributions

It collects the distinct attributions of the descriptions of the readings it shows, calls `Attribution.Measure` for the
card's content width, removes that height from the bottom of the content rectangle before its composition runs, and
calls `Draw` into the strip. With no attribution the measured height is zero and nothing changes, which a test pins
with an existing pixel comparison.

### 9. Licences and notices

The first task copies the exact wording from the sources' own pages. Then: `THIRD-PARTY-NOTICES.md` at the repository
root (the roadmap's item 12 plans the file; this change creates it with Meteocons, Open-Meteo, GeoNames, SkiaSharp and
Skottie); `Meteocons-LICENSE.txt` next to the icon sources and embedded; rows in the README licence table; and the
statement that Open-Meteo data is converted (units, condition classes). The credit on the card is only the
Open-Meteo line; GeoNames is credited in the notices and README (see Open Questions).

### 10. Tests do not touch the network

Recorded responses (one place, several places, no match, polar night, error status, truncated JSON) live under
`tests/UrDeck.Engine.Tests/Fixtures/weather/` and are fed through `IWeatherHttp`. The hub tests use the existing
`FakeProvider` and `FakeTime` for pattern resolution. The widget is rendered to a bitmap with a fake reading source,
as the stats tests do.

## Risks / Trade-offs

- **Skottie may draw some Meteocons layers wrongly** (only `partly-cloudy-day` has been tried) -> the spike renders
  every icon the mapping needs and looks at them; a class whose icon fails falls back to the nearest one that works,
  and if many fail the owner decides before more is built.
- **A SDK dependency every consumer pays** (three packages) -> accepted by the owner; the packages are managed and
  small, and the sizes are recorded in `docs/perf/weather.md`.
- **Full-card repaint at 30 fps costs about 3.4% of a core** with one animated icon (measured in the spike) -> the
  `motion: off` setting, the host frame clock that runs only while something animates, and a recorded measurement
  in `docs/perf/weather.md`; smaller surfaces stay the parked item 14 follow-up.
- **The geocoder can pick the wrong place** for an ambiguous text -> the resolved name is on the card, the log lists
  the candidates, and coordinates are the escape hatch.
- **A just-edited location shows pending until its fetch finishes** (a few seconds) -> pending is a dash, and the
  fetch is started the moment the demand changes.
- **After sleep the data can be up to one interval old and still read as current** -> accepted for now; the engine's
  backoff handles a failed sample, and a resume hook is the parked "wake" work. `is-day` can be wrong by up to one
  interval at sunrise and sunset.
- **The free tier is for non-commercial use** -> UrDeck qualifies; the widget's documentation says commercial users
  need an Open-Meteo key, and the optional `apiKey` setting is parked.
- **Unloading the plugin with a live `HttpClient`** -> disposed in `Shutdown`, nothing static, and a plugin-reload
  test in the style of `PluginLoaderTests` checks the context is collected.
- **`DateTimeOffset` offsets are whole minutes** -> all real zones qualify; a value with seconds in its offset is
  rounded and logged by the provider.

## Open Questions

- **Does the card also name GeoNames?** The default is no: the place name is the only GeoNames data shown, and CC BY
  accepts credit "in any reasonable manner", which the notices and README are. If the owner wants it on the card,
  only the provider's attribution strings change.
- **The poster frame fraction** and which Meteocons style files are used are settled by looking at them in the spike.
