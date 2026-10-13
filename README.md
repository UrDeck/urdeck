# UrDeck

UrDeck is a lightweight widget dashboard for a secondary or case display, built as a low-overhead replacement for HYTE Nexus. It was designed around the HYTE Y70 Touch panel (1100x3840, portrait) but adapts to any monitor: the layout is a 4-column grid of square cells computed from the monitor width.

It is a .NET 10 WinUI 3 application (unpackaged, self-contained Windows App SDK) that draws each widget with SkiaSharp on its own GPU-composited layer. Widgets are plain C# plugin DLLs that can be added, replaced or removed while UrDeck is running.

## Status

Phase 1 (foundation) is implemented: widget SDK and Roslyn analyzer, hot-reloading plugin loader, grid layout, the WinUI 3 host with per-monitor DPI handling, JSON config with hot-reload, and a built-in Clock widget. Data providers are in as a second plugin kind, with a first-party `system` provider (CPU load per core and in total, memory load) and a Stats widget that shows any reading; a `weather` provider (Open-Meteo, any number of places, seven forecast days) and a Weather widget with animated icons, at 4x2 and at 4x4 with the forecast, are in too. Verified on the real 1100x3840 panel. Taps reach widgets, and a Shortcut widget shows the icon of an application, a folder or a web address and opens it on tap. A dock at the bottom of the screen keeps up to four widgets, normally shortcuts, in the same place on every page.

Not done yet: an editor UI, the other widgets, sensors that need administrator rights, the `[RefreshAdaptive]` load-based scaling (such widgets currently refresh at their minimum interval), a theme editor and a monitor picker. See `docs/ROADMAP.md` for the plan. The bar is "no worse than HYTE Nexus" on the same panel, and a page with nothing moving costs close to nothing: the WinUI 3 host measures about 101 MB private, 0% CPU and 0% GPU idle with one Clock (see `docs/perf/render-host-baseline.md` and `docs/perf/nexus-baseline.md`).

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build, run, test

```powershell
dotnet build urdeck.slnx -c Release      # builds everything and copies the first-party plugins to plugins/
dotnet run --project src/UrDeck.Host -c Release
dotnet test urdeck.slnx -c Release       # grid, config, plugin loader and analyzer tests
```

The window covers the target monitor completely. Press Esc to close it. Diagnostics go to `urdeck.log` next to the executable.

### Installing a copy and starting with Windows

`dotnet build` output is for development: a build either fails on locked files or replaces the working configuration with
the sample. To run UrDeck daily, install a copy:

```powershell
pwsh scripts/Install-Local.ps1            # publishes to %LOCALAPPDATA%\Programs\UrDeck (or -Destination <folder>)
```

The script closes a running copy (`UrDeck.Host.exe --quit`), replaces the application's files and starts it again; add
`-Start` to start a copy that was not running. It never replaces `urdeck-config.json` (the sample is copied only when
there is none), the log, `cache/`, `themes/`, `backgrounds/` or plugins it did not build. Copy your working `urdeck-config.json` into the
installed folder once.

Right-click the tray icon and choose **Start with Windows** to start that copy at sign-in. The check mark shows what
Windows will do, including after you switch it off in Task Manager or Settings. Only one copy runs per folder: a second
start from the same folder logs a line and exits, while a build beside the installed copy still starts.
`UrDeck.Host.exe --quit` asks the copy in the same folder to close and waits up to 10 seconds.

`urdeck.log` lines start with the date (`[2026-10-11 07:15:02.250]`); at 5 MB the file becomes `urdeck.log.1`.

### Closing the window

The window never takes focus, so touching or clicking the deck does not interrupt a game or another application. As a
consequence it receives no keyboard input and Escape does not close it. Close it from the tray icon (right-click, Quit); a
left click on the icon opens [Settings](https://urdeck.app/docs/#the-monitor). Windows 11
may keep a new icon in the overflow flyout (the `^` arrow) until you drag it onto the taskbar. For development, set
`URDECK_ACTIVATABLE=1` before starting the host: the window can then take focus and Escape closes it.

### Snapshots

`--snapshot` renders the active page and the dock off-screen with the same layout and widget code as the live window, writes a PNG and exits (no window is shown):

```powershell
dotnet run --project src/UrDeck.Host -c Release -- --snapshot out.png --size 1100x3840
```

`--size WxH` defaults to the target monitor's resolution. `--page N` renders the page at index N (0-based, default
`activePage`) with the band and the indicator showing that page; an index outside the page list fails with a logged reason
and exit code 1. `*.snapshot.png` files are git-ignored.

## Configuration

UrDeck is configured by `urdeck-config.json` next to the executable (the tracked sample is
`src/UrDeck.Host/urdeck-config.json`; the build copies it to the output folder when the source is newer, so edit the copy
next to the executable for live tweaks). The file is hot-reloaded about a second after saving; an invalid edit is ignored
and the last good config is kept.

The user documentation lives on the website, at [urdeck.app/docs](https://urdeck.app/docs/): the config file, pages and
the dock, the monitor and the settings window, each widget (
[Clock](https://urdeck.app/docs/widgets/clock/), [Stats](https://urdeck.app/docs/widgets/stats/),
[Weather](https://urdeck.app/docs/widgets/weather/), [Shortcut](https://urdeck.app/docs/widgets/shortcut/)),
[themes](https://urdeck.app/docs/themes/) and [backgrounds](https://urdeck.app/docs/backgrounds/). Its source is
`site/src/content/docs/`; change it in the pull request that changes a setting. The references for authors are
[docs/themes.md](docs/themes.md) and [docs/backgrounds.md](docs/backgrounds.md).

## Writing a widget

A widget is a class deriving from `Widget<TConfig>` with a few attributes; the Roslyn analyzer checks the rules at compile time and plugins hot-reload while UrDeck runs. See [CONTRIBUTING.md](CONTRIBUTING.md#writing-a-widget) for a full example, the project file and the rules, and `widgets/UrDeck.Widgets.Clock` for a real one. A widget that shows data from a provider is `widgets/UrDeck.Widgets.Stats`; a data provider is a class implementing `IDataProvider` in a plugin of its own, see [Writing a data provider](CONTRIBUTING.md#writing-a-data-provider) and `providers/UrDeck.Providers.System`.

## Project layout

| Path | Purpose |
|---|---|
| `sdk/UrDeck.Sdk` | Plugin SDK (`net10.0`, MIT): attributes, `Widget<TConfig>`, `IWidget`, `WidgetConfig`, render context, `Theme`, the `Readout` / `TextLine` / `Gauge` / `ImageTile` components, tap input (`UrDeck.Sdk.Input`), launch targets and the launch and icon services (`UrDeck.Sdk.Launch`, `UrDeck.Sdk.Icons`), and the data provider contract and reading formatter (`UrDeck.Sdk.Data`) |
| `sdk/UrDeck.Analyzer` | Roslyn analyzer (`netstandard2.0`, MIT) reporting URDECK001-005 |
| `src/UrDeck.Engine` | Plugin loader, config store, grid and dock layout, gesture recognition and hit testing, `PageRenderer`, the reading hub that runs the data providers, the launcher and the icon service |
| `src/UrDeck.Host` | WinUI 3 application (`net10.0-windows10.0.19041.0`): window and monitor placement, one `SKXamlCanvas` layer per widget |
| `providers/UrDeck.Providers.System`, `providers/UrDeck.Providers.Weather` | First-party data providers: `system` (CPU, memory and GPU readings) and `weather` (Open-Meteo, any number of places) |
| `widgets/UrDeck.Widgets.Clock`, `widgets/UrDeck.Widgets.Stats`, `widgets/UrDeck.Widgets.Weather`, `widgets/UrDeck.Widgets.Shortcut` | Built-in plugins: Clock (`urdeck.widgets.clock`), Stats (`urdeck.widgets.stats`), Weather (`urdeck.widgets.weather`) and Shortcut (`urdeck.widgets.shortcut`) |
| `tests/UrDeck.Engine.Tests`, `tests/UrDeck.Analyzer.Tests` | xUnit tests |
| `site/` | The website at urdeck.app (Astro): landing page, user documentation (`src/content/docs/`), "Tested on"; see `site/README.md` |
| `docs/` | `ROADMAP.md` (the milestones), `BACKLOG.md` (what to work on next), performance notes |
| `openspec/` | Spec-driven documents: `specs/` holds the current capability specs (grid layout, host shell, widget SDK, Clock), `changes/` holds proposals in flight and the archive of finished changes |

The Windows SDK suffix on the host and test target frameworks is required: the Windows App SDK and `SkiaSharp.Views.WinUI` target `net10.0-windows10.0.19041`.

## License

| Path | License |
|---|---|
| `src/UrDeck.Host`, `src/UrDeck.Engine`, `tools/` (`urdeck-cli`, which links the engine), `providers/`, `widgets/`, `tests/` | [GPL-3.0-or-later](LICENSE) with the [plugin exception](PLUGIN-EXCEPTION.md) |
| `site/` (the website: text, pictures and code) | [GPL-3.0-or-later](LICENSE), except the Inter font below; code samples in the pages are MIT |
| `sdk/` (`UrDeck.Sdk`, `UrDeck.Analyzer`) | [MIT](sdk/LICENSE) |
| Community widgets and data providers | The author's choice |
| Windows App SDK (self-contained runtime shipped with the host) | Microsoft; see the licence files in the `Microsoft.WindowsAppSDK` NuGet package |
| Inter variable font (`src/UrDeck.Engine/Themes/Builtin/`) | [SIL Open Font License 1.1](src/UrDeck.Engine/Themes/Builtin/Inter-OFL.txt); the licence text ships with the font |
| Meteocons weather icons (embedded in `widgets/UrDeck.Widgets.Weather`) | [MIT](THIRD-PARTY-NOTICES.md#meteocons-weather-icons), Copyright (c) 2020-present Bas Milius; the licence text ships with the icons |

**Data services.** The weather provider uses [Open-Meteo](https://open-meteo.com/) (CC BY 4.0; the card credits it) and its
geocoding, whose place database is from [GeoNames](https://www.geonames.org/) (CC BY 4.0). Open-Meteo's free service is for
non-commercial use only; commercial use needs an Open-Meteo subscription. All third-party notices are in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Widgets and data providers that talk to UrDeck only through the SDK API may use any license, including proprietary ones; that is what the plugin exception grants. The license boundary is an assembly boundary: widgets and providers reference only `UrDeck.Sdk` (MIT), never the GPL engine or host. Code samples in the docs are MIT.

## Contributing

All changes go through a pull request that is squash-merged into `main`; see [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, code standards and performance budgets, and [AGENTS.md](AGENTS.md) for the command cheat sheet.
