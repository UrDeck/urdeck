# Weather: spike findings and cost

The spike of the `weather` change, run on 2026-10-08 on the owner's PC (Windows 11 Pro 10.0.26200, .NET 10.0.401). The
Lottie files are the `fill` style of `@meteocons/lottie` from npm (the Meteocons git repository holds the sources, not the
built JSON); the Meteocons repository was at commit `70dfb1d` (2026-04-12).

## Meteocons and Skottie

- **Skottie parses and draws every icon the mapping needs.** Thirteen files, rendered with `SkiaSharp.Skottie` 4.153.1 at
  0, 20, 40, 60 and 80 % of the loop into 120 px cells (`Animation.TryCreate(path)`, `SeekFrameTime`, `Render`): no missing
  layers, wrong colours or clipping. All icons are 128x128 at 60 fps with a 6 second loop.
- **The package reaches the host through the SDK.** With `SkiaSharp.Skottie` referenced from `UrDeck.Sdk`, the host build
  output holds `SkiaSharp.Skottie.dll` (26 KB), `SkiaSharp.SceneGraph.dll` (19 KB) and `SkiaSharp.Resources.dll`
  (22 KB). Skottie itself lives in the native `libSkiaSharp.dll` the host already carries, so the new managed weight is
  about 67 KB.
- **Inside a plugin.** The widget is loaded from `plugins/` through `PluginLoadContext` and creates and draws its animation
  with the SDK's `LottieAnimation`; `--snapshot` of a page with weather cards drew the icons (see the cost section). The
  Skottie types resolve from the host's copy (the default context), so the plugin folder holds only
  `UrDeck.Widgets.Weather.dll`.
- **Private bytes.** In a throwaway console program, creating `partly-cloudy-day` (24 KB) and drawing its first frame added
  about 2.7 MB of private bytes (Skottie's own state and the first glyph and path caches); 100 further frames added 12 KB,
  and swapping to `rain` after disposing the first added 64 KB.
- **`not-available` is drawn in dark navy and is close to invisible on the dark theme.** It is the neutral icon of the
  spec; the owner decides whether to keep it when looking at the panel (task 8.3).
- `drizzle.json` and `rain.json` are the same size but different files (the drops differ); each class keeps its own.

### Chosen icons (condition class, day or night)

| Class | Day | Night | Bytes (day / night) |
|---|---|---|---|
| `clear` | `clear-day` | `clear-night` | 7 302 / 1 820 |
| `partly-cloudy` | `partly-cloudy-day` | `partly-cloudy-night` | 23 820 / 4 351 |
| `cloudy` | `overcast` | `overcast` | 4 830 |
| `fog` | `fog` | `fog` | 4 651 |
| `drizzle` | `drizzle` | `drizzle` | 10 976 |
| `rain` | `rain` | `rain` | 10 976 |
| `sleet` | `sleet` | `sleet` | 36 191 |
| `snow` | `snow` | `snow` | 27 814 |
| `thunder` | `thunderstorms-day` | `thunderstorms-night` | 26 691 / 7 102 |
| neutral | `not-available` | `not-available` | 1 877 |

Thirteen distinct files, 168 401 bytes in all. `cloudy` (one cloud) reads as lighter than WMO code 3 (overcast), so
`overcast` (two clouds) is used for the `cloudy` class. `fog-day` and `fog-night` show only a low sun or moon over fog
lines; the plain `fog` icon reads better by itself and serves both.

### Poster frame

Frame 0 (fraction `0.0`) is the best still frame for every chosen icon: the sun is whole, the rain, snow and sleet are at
their fullest and the lightning bolt is lit. At 0.8 the snow and sleet icons are nearly empty. The poster fraction is
`0.0` for all icons.

## Open-Meteo

- **Forecast, one place.** `forecast?latitude=..&longitude=..&current=temperature_2m,apparent_temperature,is_day,weather_code&daily=temperature_2m_max,temperature_2m_min,sunrise,sunset&timezone=auto&forecast_days=1&timeformat=unixtime`
  answers one JSON **object** with `utc_offset_seconds`, `current` (`time`, `interval`, `temperature_2m`,
  `apparent_temperature`, `is_day`, `weather_code`) and `daily` (arrays of one: `temperature_2m_max`, `temperature_2m_min`,
  `sunrise`, `sunset`, all Unix seconds).
- **Forecast, several places in one request** (comma-separated coordinates, 3 places, `timezone=auto`) answers a JSON
  **array** with one object per place in request order, each with its own `utc_offset_seconds` and `timezone`. The provider
  batches all wanted places into one request. A single place answers an object, not a one-element array, so the parser
  accepts both.
- **No sunrise or sunset** (polar night) comes as `null` in the arrays; a copy of one place with both set to null is the
  `forecast-polar.json` fixture.
- **Geocoding** (`search?name=..&count=5&language=en`): `Portland, OR`, `Portland, Oregon` and `90210` resolve (the first
  result of the first is Portland, Oregon; `90210` is Beverly Hills, California); `Springfield` answers several places
  and the first is Missouri; a no-match answers `{"generationtime_ms":..}` with **no `results` key**. A place whose first-
  level area differs from its name (`Zurich`, `Canton of Zurich`) is named `Zurich, Canton of Zurich`.
- The recorded answers are under `tests/UrDeck.Engine.Tests/Fixtures/weather/`.

## Licences

Meteocons `LICENSE` reads `MIT License`, `Copyright (c) 2020-present Bas Milius` (the roadmap says `2020-2024`; the file in
the repository is the source of truth).

## Cost

Measured on 2026-10-08 with the method in `nexus-baseline.md` and `render-host-baseline.md`: Release build, the live host
on the Y70 panel, default dark theme, sampled 15 s after start. CPU is the change in `TotalProcessorTime` over a 30 s
window; GPU is the `GPU Engine` utilization counters for the process (five samples, 6 s apart); the host had loaded the
real Open-Meteo answers (Zurich, Tokyo, Portland).

| Page | Private bytes | Working set | CPU, % of one core | GPU | Dedicated video memory |
|---|---|---|---|---|---|
| Clock only (the baseline) | 101 MB | 135 MB | 0% | 0% | 125 MB |
| One weather card, `motion: full` | 122 MB | 166 MB | 4.1% | 3.3% | 125 MB |
| Three weather cards, `motion: full` | 136 MB | 183 MB | 7.4% | 10.6% | 125 MB |
| Three weather cards, `motion: off` | 113 MB | 158 MB | 0.04% | 0% | 125 MB |

- One looping icon costs about 4% of a core and 3% of the GPU, which is the figure of the render spike (3.4% and 3.1%) and
  about the same as Nexus's whole GPU use (3% 3D). Each further card adds about 1.5% of a core and 3.5% of the GPU, so
  three looping cards are over the Nexus bar on the GPU (10.6% against about 3%) and under it on the CPU (7.4% against
  about 20%). The cost is the full-card repaint at 30 frames a second; smaller surfaces for animated widgets are the parked
  roadmap item 14.
- `motion: off` brings the cost back to nothing: the frame clock never starts and no widget repaints between data changes.
- The first weather card adds about 21 MB of private bytes over the Clock-only baseline (the HTTP client, the first
  animation and its caches, a 4x2 surface) and each further card about 7 MB; with `motion: off` three cards come to
  113 MB, because nothing keeps repainting and the paint garbage is not produced.
- The frame clock started with the first animating card and stopped at a config change that removed the last one, as in
  `animation-flip.md`.
- A page without a weather widget registers the provider and never starts it: `urdeck.log` has no "weather started" line
  and no request is made. Removing the last weather card stops the provider five seconds later (the engine's linger),
  `Provider 'weather' stopped: nothing subscribes to it any more`.
- Editing a card's `location` while the page is shown: the config reload and the new place's first lookup are 0.13 s apart
  in the log, and the new values are published a moment later.
- **After the first measurement** the icon was changed to animate at 15 frames a second (the host throttles per widget) and the
  default `motion` became `periodic` (one 6 s loop a minute, then the poster frame; the host wakes the widget with a one-shot
  timer, and the frame clock stops in between). Same method, three cards: `full` now costs 4.3% of a core and 5.2% GPU
  (was 7.4% and 10.6%), and `periodic` at rest costs 0% CPU and 0% GPU, about 0.4% CPU averaged over a minute with the loop.
- These are short samples, not a benchmark.
