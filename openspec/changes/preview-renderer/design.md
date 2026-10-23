## Context

See proposal.md for why. This is the first of four changes settled in an explore on 2026-10-22; the others
(`site-media-pipeline`, `site-gallery`, `docs-rewrite`) build on what this one makes. Nothing here touches the site.

What exists:

- **Sample page.** `src/UrDeck.Preview/SamplePage.cs` builds the page in code (clock 4x1 at row 0, stats 2x1 CPU and GPU
  at row 1, weather 4x2 at row 2, four shortcuts at row 4, a dock of four) with a private `Host` that answers
  `IReadingSource` from a dictionary: CPU 68, GPU 82, Portland, OR at 18 °C partly cloudy at night, high 21, low 14,
  sunrise 07:14, sunset 18:21; `RegionUsesFahrenheit` false, `RegionUses24HourClock` true; icons always `Loading` (the
  shortcuts draw their letter tiles); time 13 October 2026, 21:47:32 local. `ThemePreview.Render` passes it to
  `PageRenderer.RenderToBitmap` without a background. Its users: `GoldenTests`, `MotionSnapshotTests`, the CLI's
  `ThemeRenderRunner`.
- **Page renderer.** `PageRenderer.RenderToBitmap(width, height, widgets, theme, time, chrome, dock, background)` draws
  cards, chrome, underlay, pattern, dock, indicator and, when given, a `DeckBackground` painted at
  `BackgroundClock.SnapshotTime`. An animated border is drawn still (the `motion` change kept snapshots and goldens
  byte-identical). The host's turning border is a compositor sprite: `BorderPicture` makes the conic source (512 px
  square, rotation 0 is the still border) and the ring mask; the host rotates the source about the card's centre at
  scale 1.5, in `period × 15` steps per turn (`BorderStepsPerSecond = 15`), and `pulse` steps the opacity between 0.55
  and 1. `ResolvedCardStyle` has `BorderAnimation` and `WithoutBorder()`.
- **Readings.** `ReadingHub` (engine) owns catalogs (exact paths and `{placeholder}` patterns), subscriptions, reading
  states and a sampling loop per provider driven by a `TimeProvider`; its constructor takes the region flags.
  `ProviderDescriptor.Factory` lets a host register a provider instance directly. `SystemProvider` is
  `[SupportedOSPlatform("windows")]` and its catalog depends on the machine (cores, adapters, vendor readers);
  `WeatherCatalog.Describe()` is static and uses `{location}/...` paths with `daily/N/...` for N 0 to 6.
- **Widgets.** `Widget<TConfig>.DefaultConfig` is `virtual` and returns `new TConfig()`; nothing calls it and no widget
  overrides it. `WidgetRegistry.TryCreate(Type)` makes a `WidgetDescriptor` from the attributes. Stats keeps a
  `FractionHistory` per position, filled with `(now, fraction)` on each paint while its sparkline is on.
- **Motion.** `MotionPolicy`, `MotionLevel`; `BackgroundPlan.Resolve(settings, theme, userMotion)` stills a shader below
  `full`.
- **Spike, 2026-10-22.** `urdeck-cli theme render` for six themes in `mcr.microsoft.com/dotnet/sdk:10.0` (no fonts
  installed) against Windows: about 1 % of pixels differ, all at glyph edges; small text a pixel or two tighter. Accepted.

## Goals / Non-Goals

**Goals:**
- One routine draws every picture of the product made by a tool, a test or CI; the host's live snapshot is not needed.
- A preview's pixels depend only on its inputs (`preview-rendering`, "Previews Are Deterministic").
- Real readings and sample readings take the same path through `ReadingHub`.
- Every golden byte-identical.

**Non-Goals:**
- Loading third-party widget plugins into the tool (the tool knows the first-party widgets; a `--plugins` folder is a
  later change).
- Driving the host's `--snapshot --frames` through the new renderer (it keeps its own loop; demo mode makes it
  deterministic, and the site moves to the tool).
- A theme picker in the app; any site work.

## Decisions

### 1. The sample file: embedded, self-describing, keyed by provider id

Each provider project embeds `sample.json` with the logical name `urdeck.sample.<providerId>.json` (one assembly may hold
several providers). Shape:

```json
{
  "version": 1,
  "readings": {
    "cpu/load": { "kind": "percent", "label": "CPU", "name": "CPU load", "unit": "%", "min": 0, "max": 100,
                  "keys": [ { "at": 0, "value": 68 }, { "at": 2, "value": 54 }, { "at": 4, "value": 71 } ], "period": 6 },
    "{location}/place": { "kind": "text", "label": "Place", "name": "Place", "value": "{location}" },
    "{location}/daily/1/date": { "kind": "date", "label": "Date", "name": "Date", "value": 1 },
    "{location}/daily/0/sunrise": { "kind": "time", "label": "Sunrise", "name": "Sunrise", "value": "07:14" },
    "gpu/fan": { "kind": "percent", "label": "Fan", "name": "GPU fan", "unavailable": "no fan reading on this card" }
  }
}
```

`value` and `keys` are exclusive; `period` (seconds) makes `keys` repeat. Field names follow `ReadingDescriptor`
(camelCase) and `ReadingKind` names, so a reader is a straight map. The reader lives in the engine (`Data/SampleFile`),
is strict (unknown keys, a wrong kind for a value, `keys` not starting at 0 or not ascending are errors with the path
named) and returns one finding per problem.

*Why self-describing:* `SystemProvider` cannot be constructed on Linux and its catalog is this machine's. Alternative
considered: take descriptors from `Describe()` and only values from the file. Rejected: no previews on Linux, and the
sample machine would change with the machine that draws it. The coverage test (decision 7) keeps the two in step.

*Why embedded:* the plugin loader shadow-copies assemblies; a resource travels with the DLL and needs no file next to it.

### 2. `SampleProvider`: an `IDataProvider` over a sample file and a clock

`SampleProvider(SampleFile file, Func<DateTime> now, DateTime start)` returns the file's descriptors from `Describe()`
(placeholders kept, so the hub's existing pattern matching resolves `weather:Zurich/...`), does nothing in `Start` except
keep the sink, and in `SampleAsync` publishes every demanded path's value at `now() - start` seconds: keyframes
interpolated (numbers linear, others stepped), relative dates and times resolved against `now()`'s local date,
`{placeholder}` in text values replaced from the demanded path. It completes synchronously. A sample replaces its
provider by registering a `ProviderDescriptor` with the real id and name and `Factory = () => new SampleProvider(...)`.

### 3. Manual sampling for previews: `ReadingHub` gets a mode without its loop

`ReadingHub` gains a constructor option `ReadingHubSampling.Manual`: subscribing starts providers and sets demand as
today, but no sampling loop runs; `SampleNow()` calls `SampleAsync` on every started provider once, synchronously, and
commits as a sample commit (states, change list, the same publication path). The preview sets its clock, calls
`SampleNow()`, then paints. The host never uses the manual mode.

*Alternative:* a fake `TimeProvider` advanced per frame, letting the loop run. Rejected: the loop's waits are async and
racy to settle, and frames would depend on the provider's interval rather than on the frame's time. The manual mode keeps
everything else (catalog, patterns, states, formatting) on the real path, which is what the spec asks.

### 4. The sample page is an embedded `urdeck-config.json`

`src/UrDeck.Preview/SamplePage/urdeck-config.json`, embedded, holds exactly today's page and dock as configuration
(widgets with `col`, `row`, `width`, `height` and their settings; the weather card with `location: "Portland, OR"`). A
`PreviewPage` loads any configuration through the engine's configuration reader and creates widgets with a
`PreviewWidgets` registry: the four first-party widget types by type id, created and configured as the plugin loader
does (`Attach`, `Configure` with the concrete config). `SamplePage.cs` and its `Host` are removed; `ThemePreview.Render`
becomes a thin call into the renderer with background none, kept for the tests' readability.

The preview's `IWidgetHost` is the hub (readings) plus: region flags fixed (Celsius, 24-hour), a launcher that launches
nothing, and an icon source that answers `Loading` for every icon, as today (letter tiles; the goldens depend on it).

### 5. `PreviewRenderer` in `UrDeck.Preview`

```
PreviewRequest(Page | WidgetAlone, Theme, BackgroundChoice, Size, Moment, MotionLevel)
PreviewRenderer.Render(request)                  -> SKBitmap
PreviewRenderer.RenderFrames(request, n, fps, sink)  // sink(i, bitmap)
```

Per drawing: build the hub (manual, decision 3) with a `SampleProvider` per first-party provider that has a sample
(`UrDeck.Preview` references the two provider projects for their resources; it never constructs `SystemProvider`); build
the page; subscribe; resolve the background with `BackgroundPlan.Resolve(settings, theme, level)` and
`DeckBackground.Create`/`PrepareAsync` as `SnapshotCommand.BuildBackground` does (moved to the engine so both use one
routine); for each frame set the clock, `SampleNow()`, and call `PageRenderer.RenderToBitmap` with the frame's moment,
the background time and the border time (decision 6). Widget alone: a configuration with one page holding the widget at
(0, 0) with its size, drawn at `width × (rows × width / 4)` with no indicator and no dock, then cut to the widget's cells.

**History pre-roll:** Stats fills its sparkline history as it paints (`FractionHistory.Add(now, fraction)`), so before
frame 0 the renderer paints the page off-screen once per second of the theme's sparkline span
(`GaugeTheme.Sparkline` history seconds), from `-span` to `0`, with `SampleNow()` before each paint. Painting at the real
size matters: a widget decides from its card size whether its sparkline is on. At most 600 paints of one page, done only
when the theme has a sparkline; the cost is measured in the tests.

### 6. Animated border and background in frames: `PageRenderer` takes a motion time

`PageRenderer.Render` gains an optional `FrameTime? motion` (elapsed seconds since the drawing's start, and the level).
When it is null, nothing changes: the still border and `BackgroundClock.SnapshotTime`, so every golden and the host's
snapshot are untouched. When it is set:

- The background is painted at `SnapshotTime + elapsed` (still at `reduced` and `off`, through the plan's speed 0).
- A card whose style has a `BorderAnimation` (at level `full`) has its back painted from `WithoutBorder()`, and the
  border composed over it as the host composes it: the conic source picture drawn rotated by
  `360 × floor(elapsed × 15) / (period × 15)` degrees about the card's centre at scale 1.5, through the ring as a mask
  drawn at the card's exact size with the same border path routine (`SizedCardChrome`); for `pulse`, the still border at
  the stepped opacity. The same `BorderPicture` source is used, so colours and angle 0 match the panel.

*Alternative:* render the border into the cached card back per angle. Rejected: one back per step per size defeats the
cache, and the composition approach mirrors the host, so a drift between the two shows up in one place.

### 7. Coverage and default-config tests

- **Weather** (cross-platform): every path of `WeatherCatalog.Describe()` has a sample with an equal descriptor.
- **System** (`[WindowsFact]`): every path of the real `Describe()` on the test machine, with numbered segments
  (`core/3`, `gpu/1`) mapped to the sample's numbering, has a sample with an equal descriptor (device names excepted);
  and the sample's families equal the families the provider's describe routines can produce (read from
  `CpuReadings`/`GpuReadings`/`MemoryReadings` constants).
- **Samples parse** (cross-platform): both files read without findings; a set of bad fixtures fails with the expected
  findings.
- **Default configs** (cross-platform): for each first-party widget and each `[WidgetSize]` with `Preview`, draw the
  widget alone in `default-dark` with sample data; fail when its card area differs from the empty card's in under 2 % of
  pixels, or when any of its subscriptions reads other than available.

### 8. Default configurations

Overrides of `DefaultConfig` only (configuration files keep `new TConfig()` defaults):

| Widget | Default |
|---|---|
| Clock | unchanged (`new ClockConfig()`, the time) |
| Stats | slots `system:cpu/load`, `system:gpu/load`, `system:memory/load` (label `RAM`), `system:gpu/temperature` |
| Weather | `location: "Portland, OR"`, the configuration type's other defaults |
| Shortcut | `target: "https://urdeck.app"`, `label: "UrDeck"` |

Portland matches the sample page so the same place reads back in every picture.

### 9. Demo mode in the host

`--demo` is a command-line switch only (no configuration value: a configuration is the user's real setup, and a
forgotten flag in it would hide real readings). `HostContext` passes it to the plugin loader, which, for each provider
it registers, looks for `urdeck.sample.<id>.json` in the provider's assembly and registers a `SampleProvider` in its place
(start = process start, clock = `DateTime.Now`, or `--start` plus elapsed under `--snapshot`). The icon service gets a
switch that answers site icons with "no icon" without a request; local and shell icons are unchanged (Windows only,
deterministic per machine). One log line: `Demo mode: sample data for system, weather`.

### 10. `[WidgetSize(…, Preview = false)]`

A `bool Preview { get; init; } = true` named property on the attribute, copied onto the descriptor's sizes.
*Alternative:* an enum category (the owner's first idea). A bool covers the one case known now; an enum can replace it
later without changing JSON or configuration. The SDK's minor version goes up.

### 11. The command line

- `preview render` maps its options onto `PreviewRequest` (`PreviewRenderRunner`, beside `ThemeRenderRunner`).
- `theme render` builds a `PreviewRequest` with background none and the sample page; its output is unchanged.
- `catalog export` (`CatalogExportRunner`): `ThemeCatalog.List`, `BackgroundCatalog.List`, `PairingIndex.BackgroundsFor`,
  the `PreviewWidgets` registry's descriptors with `DefaultConfig` serialized the way the configuration writer writes a
  widget, each widget's settings (public settable properties of its configuration type beyond `WidgetConfig`'s own, with
  the JSON name, a JSON type or the accepted values of a string the widget validates, and the default from `new TConfig()`),
  the provider descriptors (reflection on `[DataProvider]`, no instance), and the finding codes with severity and summary
  from the theme and shader code tables (`ThemeFinding`, `ShaderDiagnostic`). `docs-rewrite` builds the docs' settings
  and troubleshooting tables from these two lists. Written with sorted lists, sorted
  object keys, LF and a final newline. A JSON schema for it is not published in this change (the site is the only reader).

## Risks / Trade-offs

- [Goldens move because the sample's descriptors differ from `SamplePage`'s hand-made ones, such as a label] → The
  sample's descriptors are taken from the real catalog (decision 7), which is what the panel shows; if a golden changes,
  redraw it and say which label in the pull request.
- [A sample drifts from the provider on Linux, where the system catalog cannot be read] → the system coverage test runs
  on Windows in CI on every change that touches providers.
- [Pre-roll cost for long sparkline spans] → only with a sparkline on, one paint per second of span; measured in the
  tests' time; capped at the theme's span.
- [The border composed in Skia differs from the compositor's (filtering, the 1.5 scale)] → the spec asks for the same look,
  not the same bytes; a test compares frame 0 with the still border and the quarter-turn frame's edge colours with the
  still border's colours a quarter turn on.
- [Manual hub mode drifts from the live loop] → it shares commit, states and catalog code; only scheduling differs, and
  the existing hub tests keep covering the loop.

## Migration Plan

Internal only. `SamplePage` users move to the new page in the same change; `theme render`'s options and output stay.
Rollback is reverting the change.
