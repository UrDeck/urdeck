## 1. Groundwork

- [x] 1.1 On `feat/preview-renderer`, the build is clean and every golden passes before any change (`dotnet build urdeck.slnx -c Release`, `dotnet test tests/UrDeck.Engine.Tests -c Release --filter Golden`)
- [x] 1.2 Read `SamplePage.cs`, `ThemePreview.cs`, `PageRenderer` (`Render`, `PaintBackground`), `BorderPicture`, `ResolvedCardStyle.BorderAnimation`/`WithoutBorder()`, the host's `BorderMotion`, `ReadingHub` (catalog patterns, `SampleOnceAsync`, `CommitSample`), `ProviderDescriptor.Factory`, `WidgetPluginLoader` (provider registration), `WidgetRegistry.TryCreate`, `SnapshotCommand.BuildBackground`, `StatsWidget` history, `WeatherCatalog`, the system provider's describe routines, and the CLI's `ThemeRenderRunner`

## 2. Sample data (engine)

- [x] 2.1 `SampleFile` reader in `src/UrDeck.Engine/Data` (design, decision 1): version 1, descriptors, `value`/`keys`/`period`/`unavailable`, placeholders; strict, one finding per problem with the path named; bad-fixture tests for each finding
- [x] 2.2 Value evaluation: linear numbers, stepped text/on-off/time/date, repeat with `period` (also before 0), hold after the last key, relative dates and times against a moment's local date, `{placeholder}` replacement in text; tests for every scenario of `sample-data` "Sample Values Over Time", "Readings Of A Place" and "Times And Dates Relative To The Drawing"
- [x] 2.3 `SampleProvider` (decision 2) and a helper that finds `urdeck.sample.<id>.json` in an assembly and makes the replacing `ProviderDescriptor`; a broken file logs one warning and yields no sample
- [x] 2.4 `ReadingHub` manual sampling mode and `SampleNow()` (decision 3); tests: subscribe in manual mode starts the provider without a loop, `SampleNow()` publishes through the normal commit (states, change notifications), patterns resolve `weather:Zurich/...` to the sample, the same moment gives the same values

## 3. Provider samples

- [x] 3.1 `providers/UrDeck.Providers.Weather/sample.json` (embedded): every path of `WeatherCatalog.Describe()` with the real descriptors, today's values equal to the old `SamplePage` (18, partly cloudy, night, 21/14, 07:14/18:21) and seven forecast days
- [x] 3.2 `providers/UrDeck.Providers.System/sample.json` (embedded): a plausible machine (8 cores, one adapter with every reading), CPU 68 and GPU 82 at time 0, keyframes for CPU, GPU and memory load that repeat
- [x] 3.3 Coverage tests (decision 7): weather on every platform, system as `[WindowsFact]` against the real `Describe()` plus the family check; both files parse without findings

## 4. Widgets and SDK

- [x] 4.1 `WidgetSizeAttribute.Preview` (default true) and the descriptor's sizes carrying it; SDK minor version; a registry test for `Preview = false`
- [x] 4.2 `DefaultConfig` overrides for Stats, Weather and Shortcut (decision 8); a test that configuration files without those settings load as before

## 5. Preview renderer

- [x] 5.1 The sample page as `src/UrDeck.Preview/SamplePage/urdeck-config.json` (embedded), `PreviewPage` loading any configuration, `PreviewWidgets` registry of the four first-party widgets, the preview `IWidgetHost` (hub, fixed region, no launches, `Loading` icons) (decision 4); `UrDeck.Preview` references the two provider projects
- [x] 5.2 Move the background build of `SnapshotCommand.BuildBackground` into the engine and use it from the host and the preview
- [x] 5.3 `PageRenderer` optional frame time (decision 6): background at the frame's time, animated border composed from the conic source and the ring per step for `spin`, stepped opacity for `pulse`, still at `reduced`/`off`; null keeps today's drawing
- [x] 5.4 `PreviewRequest`, `PreviewRenderer.Render` and `RenderFrames` (decision 5), including the widget-alone page and cut, and the sparkline pre-roll
- [x] 5.5 Remove `SamplePage.cs`; `ThemePreview.Render` calls the renderer; `GoldenTests` and `MotionSnapshotTests` use the new page; every golden byte-identical, or redrawn with the reason recorded for the pull request
- [x] 5.6 Tests: twice the same bytes; frame 0 equals the still preview (Rigline with `horizon`, a stats keyframe page); the border at the quarter-turn frame matches the still border's colours a quarter turn on; `reduced` frames all equal; a gauge rises across frames; a sparkline is full at frame 0; widget alone sizes (4x2 → 1100x550) and the refusal of an undeclared size
- [x] 5.7 Default-config tests (decision 7): every first-party widget at every previewed size in `default-dark`, not empty, every subscription available

## 6. Host demo mode

- [x] 6.1 `--demo` (decision 9): the plugin loader replaces providers that have a sample, the icon service stops site icon requests, one log line naming the replaced providers; works with `--snapshot` and `--start`
- [x] 6.2 Tests: the loader replaces a provider with a sample and keeps one without; the icon service makes no site request in demo mode

## 7. Command line

- [x] 7.1 `preview render` (`urdeck-cli` spec, "Rendering A Preview"): options, exit codes, frames folder, theme findings to the error stream
- [x] 7.2 `theme render` on the renderer; its existing CLI tests pass unchanged
- [x] 7.3 `catalog export` (`urdeck-cli` spec, "Exporting The Catalog"): document shape including widget settings and finding codes, sorting, LF, stable bytes; a test that every finding code the engine defines is listed with a summary
- [x] 7.4 CLI tests for every scenario of the two new requirements, running on Linux and Windows

## 8. Verification by test and build

- [x] 8.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet format urdeck.slnx --severity warn --verify-no-changes`; `dotnet test urdeck.slnx -c Release`; the engine tests with `-p:CollectCoverage=true`; `openspec validate --all --strict`; `urdeck-cli theme validate --builtin` clean
- [x] 8.2 Linux: the engine, CLI and analyzer tests in a `mcr.microsoft.com/dotnet/sdk:10.0` container; `preview render` of every built-in theme with its own background there, compared by eye with the Windows pictures (only text edges differ)
- [x] 8.3 Frames by eye: Rigline and Ember sample pages as 105 frames at 15 per second made into a loop with ffmpeg, beside the host on the panel; the border's look matches (ask the owner to look; record what was seen in a "Findings during apply" section of `design.md`)
- [x] 8.4 Demo mode on the panel: the deck with `--demo` shows the sample readings and moving gauges, the log line is present, no weather request in the log; a `--demo --snapshot --start` picture twice gives the same readings

## 9. Documentation

- [x] 9.1 Sample files for provider authors (a section in the data provider docs: format, placeholders, relative times, the coverage check) and the widget docs (`DefaultConfig` must draw something, `Preview = false`)
- [x] 9.2 The command line: `preview render` and `catalog export` in `docs/themes.md`/`docs/backgrounds.md` where the tool is described, the site's command line page, the README command list and `AGENTS.md` Commands; `--demo` in the host's options (README and the site)
- [x] 9.3 `docs/design/look-and-feel/STATUS.md`: PR 18 row (built: preview renderer, sample data, demo mode, catalog export) and PR 17 row (now the changes `site-media-pipeline`, `site-gallery`, `docs-rewrite`); tick change 1 in `docs/handoff/2026-10-22-site-media-automation.md`
