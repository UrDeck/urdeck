## 1. Spike (throwaway code outside the solution, not merged)

- [ ] 1.1 Fetch the Meteocons repository (Lottie, fill style) into a scratch folder; list the files, note the licence file, and write the file sizes of the set the mapping of design decision 7 needs into `docs/perf/weather.md`
- [ ] 1.2 Render every icon of that mapping with `SkiaSharp.Skottie` 4.153.1 at five times of its loop to PNGs; look at them for missing layers, wrong colours or clipping, and pick the icon for every (condition class, day or night) pair, the neutral icon, and one poster fraction (or a per-icon one) by eye
- [ ] 1.3 Add the Skottie package reference to a scratch copy of the SDK, build the host, and confirm `SkiaSharp.Skottie`, `SceneGraph` and `Resources` are in the host's output and that a widget loaded through `PluginLoadContext` can create and draw an animation; record the private bytes it adds
- [ ] 1.4 Call the Open-Meteo forecast API for one place and for three places in one request with `timezone=auto` and `timeformat=unixtime`; confirm the batched response shape, `utc_offset_seconds` per place, and the `current` field names; save the responses as fixtures (and edit a copy so that `sunrise` and `sunset` are null for the polar case)
- [ ] 1.5 Save the geocoding answers for the probe queries of the roadmap (`Portland, OR`, `90210`, `Springfield`, a no-match, a place with a name that differs from its first-level area) as fixtures
- [ ] 1.6 Record sources, numbers, the chosen icons and poster fraction in `docs/perf/weather.md`; update `design.md` where a finding differs (batching, icon names, poster fraction)

## 2. Licence texts

- [ ] 2.1 Read the Open-Meteo licence and terms pages and the Meteocons `LICENSE` and copy the exact attribution wording and licence texts; update the attribution strings in the weather-provider spec only if the wording differs
- [ ] 2.2 Create `THIRD-PARTY-NOTICES.md` at the repository root with Meteocons (MIT, Copyright (c) 2020-2024 Bas Milius, full text), Open-Meteo (CC BY 4.0, the link, "data converted"), GeoNames (CC BY), SkiaSharp and Skottie (MIT)
- [ ] 2.3 Add the Meteocons row and a "data services" note (Open-Meteo, GeoNames, and that the free tier is non-commercial) to the licence table in `README.md`

## 3. SDK

- [ ] 3.1 Add `ReadingValueType.Time`, `ReadingValue.FromTime`, the implicit conversion, the `Time` property, equality that includes the offset, `ToString`, and `ReadingKind.Time`
- [ ] 3.2 Add `ReadingFormatOptions.Use24HourClock`, `IReadingSource.RegionUses24HourClock` (default member) and the time case in `ReadingFormatter` (value text, `AM`/`PM` unit, widest text, kind following the value without a descriptor); the hub takes the region clock as a constructor option for tests
- [ ] 3.3 Add `ReadingAttribution` and `ReadingDescriptor.Attribution`
- [ ] 3.4 Add the `Attribution` component: `Measure` and `Draw` (distinct texts, label step, muted colour, short text when the full text does not fit, zero height with none)
- [ ] 3.5 Add the `SkiaSharp.Skottie` package reference to the SDK and the central version (the SkiaSharp version, with a comment), then `LottieAnimation` (`TryCreate` without throwing, `Duration`, `Draw` with fit and centre and clamped time, `Dispose` twice allowed)
- [ ] 3.6 Move the package version to 0.6.0 with a note in the csproj comment, keep `AssemblyVersion`, and make `SdkContractTests` pass

## 4. Engine: patterned catalog

- [ ] 4.1 Parse patterns at `CacheCatalog` (whole-segment `{name}` only; log and skip malformed ones) and keep them beside the exact entries in `ProviderRuntime`
- [ ] 4.2 Add the one resolve method (exact, then patterns by most literal segments, a tie in listed order logged once, instantiate with `{name}` replaced in path, label, name and device, cache) and call it from every site that reads `rt.Catalog`
- [ ] 4.3 Resolve a published path only when it is in `Wanted`; log a published path that matches nothing once
- [ ] 4.4 `GetCatalog` lists exact entries and patterns as written; `Describe(id)` answers from the instantiated cache; `ReleaseAll` clears it
- [ ] 4.5 Hub tests: pattern match and filled-in device, exact beats pattern, most literal wins, tie, no match is unavailable with the reason, malformed pattern skipped, publishing an unwanted path is ignored, catalog lists the pattern once, reload clears the cache

## 5. Weather provider

- [ ] 5.1 Create `providers/UrDeck.Providers.Weather` (GPL header, references only the SDK), add it to `urdeck.slnx`, the `UrDeckPlugin` list in `UrDeck.Host.csproj` and the engine test project
- [ ] 5.2 `LocationKey`: parse text and `@lat,lon`, escape and unescape `%2F` and `%25`, trim, compare without case, tests
- [ ] 5.3 `ConditionMap` for the WMO codes of design decision 6, with a test per class and for an unknown code
- [ ] 5.4 `WeatherParser` for the forecast answer (one place and several), including the offset and null sunrise and sunset, tests with the fixtures from the spike
- [ ] 5.5 `GeocodingClient` and `ForecastClient` behind `IWeatherHttp`; one `HttpClient` created in `Start` and disposed in `Shutdown`, timeout, HTTPS only, `User-Agent`; tests with a fake that returns the fixtures, an error status and a truncated body
- [ ] 5.6 The catalog: the nine patterns of the spec, with labels, names, device `{location}`, no display unit on temperatures, and the attribution on every one
- [ ] 5.7 `SetDemand` and `SampleAsync`: distinct keys, resolve once per key (negative results kept for the session), batch or loop per the spike, publish all values of a sample together, `Unavailable` with a reason for a place that matches nothing, a thrown failure for transport errors
- [ ] 5.8 New places: start a single-flight fetch from `SetDemand` that publishes outside a sample and reports `Unavailable` for those places when it fails; the semaphore that keeps it from overlapping a sample
- [ ] 5.9 Log only the first failure and the first success after it; log the candidate matches of an ambiguous place and the place chosen
- [ ] 5.10 Tests: two places publish independently, same place written twice fetched once, no request while nothing is subscribed, offline then recovery, a new place fetched without waiting for the interval, no-match stays unavailable and is not asked again, `Shutdown` leaves nothing running, a plugin-reload test that the load context is collected

## 6. Weather widget

- [ ] 6.1 Create `widgets/UrDeck.Widgets.Weather` (GPL header, references only the SDK), add it to `urdeck.slnx`, the `UrDeckPlugin` list and the engine test project; `WeatherConfig` with unknown properties preserved
- [ ] 6.2 Build the location segment and the eight subscription ids (coordinates win; no location subscribes to nothing)
- [ ] 6.3 Add the chosen icons and `Meteocons-LICENSE.txt` as embedded resources; the (condition class, day or night) table; load only the animation on screen and dispose the previous one on change and on reconfigure; a missing resource draws no icon and logs once
- [ ] 6.4 Composition at 4x2 with `Readout`, `TextLine` and `Attribution`: temperature, place (with the three label states), condition words, high and low, sunrise and sunset in the place's clock, credit strip measured first; unit override
- [ ] 6.5 Motion: poster frame on the first paint, for a new icon, and when `motion` is `off`; `t = (poster + elapsed) mod duration` after that; negative elapsed draws the poster; `IsAnimating` while `full` and an animation is loaded; an unknown value behaves as `full`
- [ ] 6.6 `NeedsRender` against a record of what was drawn (texts, units, place, currency, icon key, credit)
- [ ] 6.7 Tests (rendered with a fake reading source): the full card, every unavailable and stale state, a changing temperature width, Fahrenheit and the 12 hour clock, label states, coordinates win, two widgets two places, the credit with every reading unavailable and with an empty label, no credit without attribution, motion full and off, the first paint is the poster, a clock jump, repaint rules, icon swap releases the old animation

## 7. Stats widget

- [ ] 7.1 Collect the attributions of the shown readings, measure and reserve the strip, draw it; with none the card is pixel-identical to before
- [ ] 7.2 Tests: a weather slot at 1x1 shows the credit, two weather readings show it once, a `system:` card is unchanged

## 8. Verify

- [ ] 8.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, and `openspec validate --all --strict`
- [ ] 8.2 `UrDeck.Host.exe --snapshot` of a page with a 4x2 weather card for two places and a 1x1 stats card on a weather reading: values and credits, not dashes (needs the network)
- [ ] 8.3 On the panel: the icon loops, `motion: off` freezes on the poster frame, a night icon appears for a place where it is night, two places show their own sunrise and sunset, and the owner approves the proportions and the look of the icons
- [ ] 8.4 On the panel: editing `location` in the configuration shows the new place within seconds; a typo shows dashes and a log line; the network off shows stale values after a while and recovery after it returns
- [ ] 8.5 Measure the host with one and with three weather cards (CPU, GPU, private bytes) and with `motion: off`, compare with `docs/perf/render-host-baseline.md`, and record it in `docs/perf/weather.md`
- [ ] 8.6 A page without weather logs no weather provider start and makes no request; removing the weather card stops the provider and its requests

## 9. Documentation

- [ ] 9.1 `README.md`: the weather widget and its settings, the readings and the location forms that work, an example page, the non-commercial note for Open-Meteo, the notices
- [ ] 9.2 `CONTRIBUTING.md`: a provider author's note on attribution (a source that requires a credit declares it on its descriptions and widgets draw it with the attribution component)
- [ ] 9.3 `docs/ROADMAP.md`: "Current state", item 3 and the weather rows of item 7 marked done, the 4x4 weather row kept as the next weather change
