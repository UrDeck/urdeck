# UrDeck

UrDeck is a lightweight widget dashboard for a secondary or case display, built as a low-overhead replacement for HYTE Nexus. It was designed around the HYTE Y70 Touch panel (1100x3840, portrait) but adapts to any monitor: the layout is a 4-column grid of square cells computed from the monitor width.

It is a .NET 10 WinUI 3 application (unpackaged, self-contained Windows App SDK) that draws each widget with SkiaSharp on its own GPU-composited layer. Widgets are plain C# plugin DLLs that can be added, replaced or removed while UrDeck is running.

## Status

Phase 1 (foundation) is implemented: widget SDK and Roslyn analyzer, hot-reloading plugin loader, grid layout, the WinUI 3 host with per-monitor DPI handling, JSON config with hot-reload, and a built-in Clock widget. Data providers are in as a second plugin kind, with a first-party `system` provider (CPU load per core and in total, memory load) and a Stats widget that shows any reading; a `weather` provider (Open-Meteo, any number of places, seven forecast days) and a Weather widget with animated icons, at 4x2 and at 4x4 with the forecast, are in too. Verified on the real 1100x3840 panel. Taps reach widgets, and a Shortcut widget shows the icon of an application, a folder or a web address and opens it on tap. A dock at the bottom of the screen keeps up to four widgets, normally shortcuts, in the same place on every page. A Now playing widget (4x2) shows the cover, title, artist and a progress bar of whatever Windows says is playing and skips or pauses it by touch, through a first-party `media` provider over the Windows media session; it reads what players publish to Windows, sends nothing anywhere and logs no titles (see the privacy page). A `homeassistant` provider shows the states of your own Home Assistant (temperatures in its own units, power, batteries, lights, any entity or attribute) over one WebSocket that follows what is on the screen, keeps the last values through an outage, and runs Home Assistant's services for a widget that switches something; it talks only to the address you set, with a token kept in Credential Manager ([Home Assistant](https://urdeck.app/docs/home-assistant/)).

Not done yet: an editor UI, the other widgets, sensors that need administrator rights, the `[RefreshAdaptive]` load-based scaling (such widgets currently refresh at their minimum interval), a theme editor and a monitor picker. See `docs/ROADMAP.md` for the plan. The bar is "no worse than HYTE Nexus" on the same panel, and a page with nothing moving costs close to nothing: the WinUI 3 host measures about 101 MB private, 0% CPU and 0% GPU idle with one Clock (see `docs/perf/render-host-baseline.md` and `docs/perf/nexus-baseline.md`).

## Installing

Download `UrDeck-win-Setup.exe` from the [latest release](https://github.com/UrDeck/urdeck/releases/latest) and run it. It installs
UrDeck for you alone (no administrator rights) into `%LocalAppData%\UrDeck`, adds a Start menu shortcut and starts the deck;
`UrDeck-win-Setup.exe --silent` shows no window. The first releases are not signed, so Windows SmartScreen may say it protected
your PC: choose **More info**, then **Run anyway**. Signing follows in a later release.

UrDeck collects nothing about you. The [privacy page](https://urdeck.app/privacy/) lists the few requests the app makes over the
network and how to turn each off.

- **Updating:** an installed copy checks the releases of this repository about a minute after it starts and then once a day,
  downloads a newer one in the background and offers **Restart to update to `<version>`** in the tray menu. An update you do not
  choose is applied when you quit. A copy on a stable version takes only stable releases. This check is the one request UrDeck
  makes on its own besides the weather, site icons and a Home Assistant you set up; `"updates": "off"` in `urdeck-config.json` turns it off. A copy that was
  not installed (a build, a plain publish) never checks.
- **Uninstalling:** use Windows' **Installed apps**. It closes the deck, removes Start with Windows and the application, and
  leaves `%APPDATA%\UrDeck`.
- **Portable:** `UrDeck-win-Portable.zip` is the same program without an installer; the data folder is the folder you unpack
  it into, so an update keeps your page.

The user documentation, including [installing and updating](https://urdeck.app/docs/installing/), is at
[urdeck.app/docs](https://urdeck.app/docs/). To make a release, see [CONTRIBUTING.md](CONTRIBUTING.md#making-a-release).

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download), only to build from source (a release is self-contained)

## Build, run, test

```powershell
dotnet build urdeck.slnx -c Release      # builds everything and copies the first-party plugins to plugins/
dotnet run --project src/UrDeck.Host -c Release
dotnet test urdeck.slnx -c Release       # grid, config, plugin loader and analyzer tests
dotnet run --project tools/UrDeck.Cli -c Release -- preview render --theme rigline --background theme -o page.png   # a picture with sample data
dotnet run --project tools/UrDeck.Cli -c Release -- catalog export -o catalog.json   # themes, backgrounds, widgets, providers, finding codes
dotnet run --project tools/UrDeck.Cli -c Release -- readings browse homeassistant --domain light   # a provider's readings with their values (your settings and token)
```

The window covers the target monitor completely. Press Esc to close it. Diagnostics go to `urdeck.log` in the data folder (see below); a build is portable, so there it sits next to the executable.

### Installing a copy and starting with Windows

`dotnet build` output is for development: it fails on locked files while a copy runs, and it keeps its data in its own folder
(portable, see below). To run UrDeck daily, install a copy:

```powershell
pwsh scripts/Install-Local.ps1            # publishes to %LOCALAPPDATA%\Programs\UrDeck (or -Destination <folder>)
```

The script closes a running copy (`UrDeck.Host.exe --quit`), replaces the application's files and starts it again; add
`-Start` to start a copy that was not running. It writes no configuration and never touches the data folder: the first
start creates `urdeck-config.json` there from the sample page. A copy installed before the data folder existed has its
configuration, log, `themes/`, `backgrounds/`, `cache/` and your own plugins next to the executable; the script moves them to
the data folder once (when the data folder has no configuration yet, and otherwise names them and moves nothing).

#### The data folder

Your files live in `%APPDATA%\UrDeck`, so replacing or reinstalling the application never touches them: `urdeck-config.json`
(and the `urdeck-config.v<N>.json` backup a format migration leaves), `urdeck.log`, `themes/`, `backgrounds/`, `plugins/` (the
plugins you add; the first-party ones stay in the application folder's `plugins/` and win when both define the same widget type
or provider id) and `cache/`. **Open data folder** in the tray menu opens it. A relative picture `path` of a background
starts there.

**Portable mode:** a file named `urdeck.portable` next to `UrDeck.Host.exe` makes that folder the data folder instead (and
its `plugins/` the only plugin folder). The build output has the marker, so a development build keeps everything in `bin/`;
a published copy has none. Add or remove the marker and restart. The log's first line says which folder and why:
`Data folder: ... (per-user)`, `(portable: ...)` or `(portable release)`. The release's portable zip keeps its data in the folder it was
unpacked into, above the application folder that an update replaces.

Right-click the tray icon and choose **Start with Windows** to start that copy at sign-in. The check mark shows what
Windows will do, including after you switch it off in Task Manager or Settings. Only one copy runs per data folder: a second
start that uses the same data folder logs a line and exits, while a portable build beside the installed copy still starts.
`UrDeck.Host.exe --quit` asks the copy with the same data folder to close and waits up to 10 seconds.

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

`--size WxH` defaults to the target monitor's resolution. `--frames N [--fps F] [--start <local time>]` writes N pictures
(`frame-0000.png` ... into the folder given as the output) F per second (default 30) apart in the time the widgets draw from,
starting at `--start` (default now), so a split-flap clock flips and the weather icons move from one picture to the next;
the website's loops are made with it (`site/README.md`). `--page N` renders the page at index N (0-based, default
`activePage`) with the band and the indicator showing that page; an index outside the page list fails with a logged reason
and exit code 1. `*.snapshot.png` files are git-ignored.

`--demo` (with or without `--snapshot`) runs the deck on sample data: every data provider that carries a sample file (the
first-party `system`, `weather`, `media` and `homeassistant` do) is replaced by it, and a shortcut to a web address draws its fallback instead of fetching
the site's icon, so the deck shows the same made-up readings on any machine and makes no network request of its own. Everything
else is as usual; the configuration is never written. The log says `Demo mode: sample data for homeassistant, media, system, weather`. With
`--snapshot` the samples are read at `--start`, so `--demo --snapshot out.png --size 1100x3840 --start 2026-10-13T21:47:32` gives
the same picture every time. For pictures without Windows, use `urdeck-cli preview render`, which draws with the same samples. Pictures of every theme,
widget size and background, drawn that way, are in the gallery at [urdeck.app/docs/themes](https://urdeck.app/docs/themes/) and on
the widget pages; they are regenerated from the product (`site/README.md`, "Media"), not captured by hand.

## Configuration

UrDeck is configured by `urdeck-config.json` in the data folder (`%APPDATA%\UrDeck`, or next to the executable in a
build; **Open data folder** in the tray menu opens it). The tracked sample is `src/UrDeck.Host/urdeck-config.default.json`: it
ships next to the executable, and the first start copies it to the data folder when there is no configuration. The build never
overwrites a working configuration. The file has a `"version"` (1); a file from a newer version is read but never written. The file is hot-reloaded about a second after saving; an invalid edit is ignored
and the last good config is kept.

The user documentation lives on the website, at [urdeck.app/docs](https://urdeck.app/docs/), and nowhere else: the config
file, [pages and the dock](https://urdeck.app/docs/pages-and-dock/), the monitor and the settings window, each widget
([Clock](https://urdeck.app/docs/widgets/clock/), [Stats](https://urdeck.app/docs/widgets/stats/),
[Weather](https://urdeck.app/docs/widgets/weather/), [Shortcut](https://urdeck.app/docs/widgets/shortcut/),
[Now playing](https://urdeck.app/docs/widgets/nowplaying/)),
[themes](https://urdeck.app/docs/themes/) (with the [theme reference](https://urdeck.app/docs/themes/reference/) and the
[cookbook](https://urdeck.app/docs/cookbook/)), [backgrounds](https://urdeck.app/docs/backgrounds/),
[the command line tool](https://urdeck.app/docs/command-line/) and [troubleshooting](https://urdeck.app/docs/troubleshooting/)
with every finding code. The reference tables (theme keys, background manifest keys, widget settings, finding codes, the
built-in lists) are made from the product when the site is built, and the build fails when one has no description, so a
change to a setting, a theme key or a finding is documented in the pull request that makes it. Its source is
`site/src/content/docs/`. The notes for the people who add a built-in theme or background are
[docs/themes.md](docs/themes.md) and [docs/backgrounds.md](docs/backgrounds.md).

## Writing a widget

A widget is a class deriving from `Widget<TConfig>` with a few attributes; the Roslyn analyzer checks the rules at compile time and plugins hot-reload while UrDeck runs. See [CONTRIBUTING.md](CONTRIBUTING.md#writing-a-widget) for a full example, the project file and the rules, and `widgets/UrDeck.Widgets.Clock` for a real one. A widget that shows data from a provider is `widgets/UrDeck.Widgets.Stats`; a data provider is a class implementing `IDataProvider` in a plugin of its own, see [Writing a data provider](CONTRIBUTING.md#writing-a-data-provider) and `providers/UrDeck.Providers.System`.

## Project layout

| Path | Purpose |
|---|---|
| `sdk/UrDeck.Sdk` | Plugin SDK (`net10.0`, MIT): attributes, `Widget<TConfig>`, `IWidget`, `WidgetConfig`, render context, `Theme`, the `Readout` / `TextLine` / `Gauge` / `ImageTile` / `ButtonRow` components, tap input (`UrDeck.Sdk.Input`), launch targets and the launch and icon services (`UrDeck.Sdk.Launch`, `UrDeck.Sdk.Icons`), and the data provider contract and reading formatter (`UrDeck.Sdk.Data`) |
| `sdk/UrDeck.Analyzer` | Roslyn analyzer (`netstandard2.0`, MIT) reporting URDECK001-005 |
| `src/UrDeck.Engine` | Plugin loader, config store, grid and dock layout, gesture recognition and hit testing, `PageRenderer`, the reading hub that runs the data providers, the launcher and the icon service |
| `src/UrDeck.Host` | WinUI 3 application (`net10.0-windows10.0.19041.0`): window and monitor placement, one `SKXamlCanvas` layer per widget |
| `src/UrDeck.Preview` | the preview renderer (a configuration's page or one widget, in a theme over a background, with sample data, as a picture or frames) and the sample page (`SamplePage/urdeck-config.json`), for `urdeck-cli theme render` and `preview render` and the golden tests; above the engine and the first-party widgets and providers |
| `providers/UrDeck.Providers.System`, `providers/UrDeck.Providers.Weather`, `providers/UrDeck.Providers.Media`, `providers/UrDeck.Providers.HomeAssistant` | First-party data providers: `system` (CPU, memory and GPU readings), `weather` (Open-Meteo, any number of places), `media` (what the current Windows media session plays, and its transport commands; the Windows Runtime projection is embedded in the plugin, see `docs/perf/now-playing.md`) and `homeassistant` (your Home Assistant's entities over its WebSocket, its services over REST) |
| `widgets/UrDeck.Widgets.Clock`, `widgets/UrDeck.Widgets.Stats`, `widgets/UrDeck.Widgets.Weather`, `widgets/UrDeck.Widgets.Shortcut`, `widgets/UrDeck.Widgets.NowPlaying` | Built-in plugins: Clock (`urdeck.widgets.clock`), Stats (`urdeck.widgets.stats`), Weather (`urdeck.widgets.weather`), Shortcut (`urdeck.widgets.shortcut`) and Now playing (`urdeck.widgets.nowplaying`) |
| `tests/UrDeck.Engine.Tests`, `tests/UrDeck.Analyzer.Tests` | xUnit tests |
| `site/` | The website at urdeck.app (Astro): landing page, the user documentation (`src/content/docs/`) and the theme cookbook (`cookbook/`), "Tested on"; see `site/README.md` |
| `docs/` | `ROADMAP.md` (the milestones), `BACKLOG.md` (what to work on next), performance notes, the published JSON Schemas, notes for adding a built-in theme or background |
| `openspec/` | Spec-driven documents: `specs/` holds the current capability specs (grid layout, host shell, widget SDK, Clock), `changes/` holds proposals in flight and the archive of finished changes |

The Windows SDK suffix on the host and test target frameworks is required: the Windows App SDK and `SkiaSharp.Views.WinUI` target `net10.0-windows10.0.19041`.

## License

| Path | License |
|---|---|
| `src/UrDeck.Host`, `src/UrDeck.Engine`, `src/UrDeck.Preview`, `tools/` (`urdeck-cli`, which links the engine), `providers/`, `widgets/`, `tests/` | [GPL-3.0-or-later](LICENSE) with the [plugin exception](PLUGIN-EXCEPTION.md) |
| `site/` (the website: text, pictures and code) | [GPL-3.0-or-later](LICENSE), except the website fonts below; code samples in the pages are MIT |
| `sdk/` (`UrDeck.Sdk`, `UrDeck.Analyzer`) | [MIT](sdk/LICENSE) |
| Community widgets and data providers | The author's choice |
| Windows App SDK (self-contained runtime shipped with the host) | Microsoft; see the licence files in the `Microsoft.WindowsAppSDK` NuGet package |
| Inter variable font (`src/UrDeck.Engine/Themes/Builtin/`) | [SIL Open Font License 1.1](src/UrDeck.Engine/Themes/Builtin/Inter-OFL.txt); the licence text ships with the font |
| Bricolage Grotesque, Instrument Sans and JetBrains Mono variable fonts (`site/public/fonts/`, the website only) | [SIL Open Font License 1.1](site/public/fonts/); each licence text ships with its font |
| Lucide icons (the built-in `outline` icon set, `src/UrDeck.Engine/Icons/Builtin/outline/`) | [ISC, and MIT for the icons derived from Feather](THIRD-PARTY-NOTICES.md#lucide-icons-the-built-in-outline-set), Copyright (c) 2026 Lucide Icons and Contributors; the licence text ships with the set |
| Meteocons weather icons (embedded in `widgets/UrDeck.Widgets.Weather`) | [MIT](THIRD-PARTY-NOTICES.md#meteocons-weather-icons), Copyright (c) 2020-present Bas Milius; the licence text ships with the icons |

**Data services.** The weather provider uses [Open-Meteo](https://open-meteo.com/) (CC BY 4.0; the card credits it) and its
geocoding, whose place database is from [GeoNames](https://www.geonames.org/) (CC BY 4.0). Open-Meteo's free service is for
non-commercial use only; commercial use needs an Open-Meteo subscription. All third-party notices are in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Widgets and data providers that talk to UrDeck only through the SDK API may use any license, including proprietary ones; that is what the plugin exception grants. The license boundary is an assembly boundary: widgets and providers reference only `UrDeck.Sdk` (MIT), never the GPL engine or host. Code samples in the docs are MIT.

## Contributing

All changes go through a pull request that is squash-merged into `main`; see [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, code standards and performance budgets, and [AGENTS.md](AGENTS.md) for the command cheat sheet.
