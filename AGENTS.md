# Agent and contributor guide

UrDeck is a lightweight widget dashboard for secondary/case displays (WinUI 3 host, SkiaSharp rendering, plugin widgets).
Read `README.md` for what it is, `docs/ROADMAP.md` for the milestones, `docs/BACKLOG.md` for what to work on, and `CONTRIBUTING.md` for the full process.

## Workflow (non-negotiable)

- **Never commit or push to `main`.** Branch off it (`feat/...`, `fix/...`, `docs/...`, `chore/...`), push, open a pull
  request. `main` is protected: PRs need green CI and are **squash-merged** only.
- The PR title is a conventional commit (`feat(scope): ...`; types `feat fix docs test perf refactor chore ci build
  revert`). It becomes the squash commit title and the PR description becomes the commit body, so write both well.
- One backlog item / OpenSpec change per PR where possible. Behavior changes go through `openspec/changes/`.
- Tick `tasks.md` items only when verified; archive the change when all tasks are done.

## Commands

```powershell
dotnet build urdeck.slnx -c Release                     # 0 warnings required; warnings are errors
dotnet test urdeck.slnx -c Release
dotnet test tests/UrDeck.Engine.Tests -c Release      # on Linux, with tests/UrDeck.Analyzer.Tests and tests/UrDeck.Cli.Tests; urdeck.slnx holds the host, which needs Windows
dotnet test tests/UrDeck.Engine.Tests -c Release -p:CollectCoverage=true   # enforces the UrDeck.Engine line-coverage floor
dotnet format urdeck.slnx --severity warn               # apply style; CI runs it with --verify-no-changes
$env:URDECK_UPDATE_GOLDENS=1; dotnet test tests/UrDeck.Engine.Tests -c Release --filter Golden   # rewrite the golden images (Windows), then clear the variable; CONTRIBUTING.md "Golden images"
openspec validate --all --strict
dotnet run --project tools/UrDeck.Cli -c Release -- shader validate --all src/UrDeck.Engine/Backgrounds/Builtin --fail-on-warnings
dotnet test tests/UrDeck.Cli.Tests -c Release           # the tool's tests (Windows and Linux); also in urdeck.slnx
```

The website (`site/`, needs Node 22; details in `site/README.md`):

```powershell
cd site; npm ci; npm run build   # builds and runs the checks (schema copy, internal links, third-party references)
npm run dev                      # local preview
pwsh scripts/Render-SiteDecks.ps1   # re-render the sample deck pictures (Windows, builds the host)
npm run media                    # the widget pictures, background posters and loops (Windows; loops need ffmpeg)
```

`urdeck-cli shader init|validate|render` (`docs/backgrounds.md`, "The command line tool") makes, checks and renders a background folder; CI
runs it on the sixteen built-ins and on an `init` of a copy of one. `render --update-manifest` rewrites `manifest.json` (refusing a comment, a trailing comma or a duplicate key).

Render without a screen (from `src/UrDeck.Host/bin/Release/net10.0-windows10.0.19041.0/`):
`UrDeck.Host.exe --snapshot out.snapshot.png --size 1100x3840`. `--snapshot <folder> --frames N [--fps F] [--start <local time>]` writes N
pictures one frame of time apart (a split-flap clock flips, the weather icons move); the site's loops use it.

CI (`.github/workflows/ci.yml`) runs the build, format and tests on `windows-latest` and the Linux tests when a change touches
anything outside docs, root Markdown, `openspec/`, `site/` and most of `.github/`; OpenSpec validation runs when `openspec/` changed.
The required checks are `ci-ok` and `pr-title` (`workflow_dispatch` runs everything). Run the commands locally before pushing.

## Layout

```
sdk/UrDeck.Sdk       MIT. The plugin contract: attributes, Widget<T>, WidgetConfig, render context, Theme, Components (Readout, TextLine,
                     Gauge, ImageTile), Data (IDataProvider, readings, ReadingFormatter), Input (ITapTarget), Launch (LaunchTarget,
                     ILauncher), Icons (IIconSource)
sdk/UrDeck.Analyzer  MIT. Roslyn analyzer (URDECK001-005), netstandard2.0
src/UrDeck.Engine     GPL. Plugin loader, config store, grid and dock layout, display identity and placement, gestures and hit testing, PageRenderer, ReadingHub (runs the
                      providers), per-widget WidgetServices, Launcher, IconService (shell, file and site icons), Backgrounds (settings, shader store,
                      palette, image cover-fit, DeckBackground), logging
tools/UrDeck.Cli      GPL. `urdeck-cli`, a portable dotnet tool (DotMake.CommandLine): `shader init`, `shader validate` and `shader render` on a background folder;
                      uses only the engine's public API (not in its InternalsVisibleTo), runs on Windows and Linux, exit codes 0/1/2
src/UrDeck.Host       GPL. WinUI 3 app: window/monitor placement, one SKXamlCanvas layer per widget
providers/            GPL. First-party data provider plugins (UrDeck.Providers.System); reference only UrDeck.Sdk
widgets/              first-party widget plugins (UrDeck.Widgets.Clock, .Stats, .Weather, .Shortcut); with the providers, copied to
                      plugins/ by the host build (the UrDeckPlugin list in UrDeck.Host.csproj)
tests/                xUnit projects
site/                 the website (Astro) published at urdeck.app; user documentation in `src/content/docs/`, sample decks in `decks/`
docs/                 ROADMAP.md (milestones), BACKLOG.md (work items and status), perf/, design notes
openspec/             spec-driven change documents
```

Shared build settings live in `Directory.Build.props`; package versions only in `Directory.Packages.props`
(no `Version=` on `PackageReference`). Style is in `.editorconfig`: file-scoped namespaces, `_camelCase` private
instance fields, PascalCase static fields, `var` only when the type is apparent, LF line endings. Every `.cs` file
needs the SPDX license header (`dotnet format` adds it): GPL-3.0-or-later everywhere except `sdk/` (MIT).
Widgets and providers reference only `UrDeck.Sdk`, never `UrDeck.Engine` or `UrDeck.Host`; keep it that way, it is the license boundary.
See the README license map before moving code between projects: it can change the license.

## Windows / WinUI 3 gotchas

- Only the host must target `net10.0-windows10.0.19041.0` (`$(UrDeckWindowsTfm)`): the Windows App SDK and
  `SkiaSharp.Views.WinUI` target that Windows SDK version; plain `net10.0-windows` does not resolve them. Everything
  else, the test projects included, targets `net10.0` (`$(UrDeckTfm)`) and builds on Linux too.
- The tests run on Windows and Linux (CI runs both). A test that needs Windows (shell icons, the system provider, named
  kernel objects, `C:\` paths) is `[WindowsFact]` / `[WindowsTheory]` (`tests/UrDeck.Engine.Tests/Support/WindowsOnlyAttributes.cs`),
  with `[SupportedOSPlatform("windows")]` on the class where the code under test has it; it shows as skipped with a reason
  on Linux. `[WindowsGlyphFact]` is for a test that expects pixel-exact text (FreeType hints advances and its edge pixels
  differ). Do not turn CA1416 off. In a test theme, take the typefaces from a resolved theme: the system default font
  is empty in a bare Linux container and draws nothing.
- The host is an unpackaged, self-contained WinUI 3 app with its own `Main` (`Program.cs`): `--snapshot` runs with the
  engine alone and exits before the XAML application starts. `App.xaml` must stay (building the application resources
  in code crashes). Widget types are never XAML types.
- The deck window has `WS_EX_NOACTIVATE`: it never takes focus, so Escape does not close it. Close it from the tray icon (right-click, Quit; Windows 11 may hide it in the overflow flyout), or
  start it with `URDECK_ACTIVATABLE=1` (the deck window can take focus and Escape closes it) when developing.
- The deck is not the only window: a left click on the tray icon (or its Settings entry) opens the settings window
  (`SettingsWindow`, one page, the monitor picker), an ordinary activatable window with a taskbar button while it is
  open. It talks to the host only through `ISettingsHost`, and `App` keeps one reference that it drops on close (a new
  window per opening, so nothing lingers). Quit closes it first. `URDECK_SETTINGS_SOAK=1` opens and closes it 20 times at
  startup and logs memory (`Soak ...` lines; the numbers are in `docs/perf/settings-window.md`). To try the window without a
  deck on a screen, copy the build output elsewhere and give its config a `display` that is not connected.
- Launching `UrDeck.Host.exe` (not `--snapshot`) blocks the shell. Start it detached and read `urdeck.log` next to the exe;
  it logs monitors, target vs. actual content area, placed widgets, reloads and warnings.
- Where the window is, and whether it is shown, is decided by `PlacementController` (`src/UrDeck.Engine/Display`, tested
  with scripted display sequences); `MainWindow` only reports signals and arms one timer. The log has `Display chosen
  with ...` / `Display matched by device path` (which monitor and why), `Placement: A -> B` for each state change,
  `Placement attempt N on <name>: <what differed>` for each retry, `Placement verified on ...` (with `rebuilding the page`
  or `page kept`), and `Deck hidden: waiting for ...` / `Deck shown on ...`. No placement lines while nothing changes.
- `SystemEvents` (power resume, display changes) must be subscribed from a thread-pool thread, as `MainWindow` does:
  subscribed from the UI thread (which has the `DispatcherQueueSynchronizationContext` from `Program.Main`) it delivered
  nothing, not even Suspend. The log says `Windows: power mode Resume` / `display settings changed` when one arrives;
  if a sleep leaves no such line, the subscription is broken, not the placement.
- The host remembers its display in `urdeck-config.json` (`display`) and writes the file when it does. With that
  display not connected there is no window at all, only the tray icon: delete `display` from the config next to the exe
  to make it choose again. `--snapshot` never writes the config.
- Screen capture: use Windows PowerShell 5.1 (`powershell.exe`, not `pwsh`), call `SetProcessDpiAwarenessContext(-4)`
  first, then `Graphics.CopyFromScreen` on the monitor bounds. `PrintWindow` on the window returns blank. Prefer one
  capture, or ask the user to look.
- In Git Bash, MSYS rewrites `/p:Foo` style switches as paths; use `-p:Foo`.
- Plugins load from a shadow copy in a collectible `AssemblyLoadContext`. A removed `SKXamlCanvas` can stay
  alive after it leaves the tree, so `WidgetView.Dispose` drops its widget reference (without that, old plugin contexts
  were not collected); keep plugin types out of long-lived static caches.
- The host build copies `src/UrDeck.Host/urdeck-config.json` over the one next to the exe whenever the source file is
  newer. After editing the tracked sample, a build replaces a working config in `bin/`: back it up first.
- The daily copy is installed with `pwsh scripts/Install-Local.ps1` (`%LOCALAPPDATA%\Programs\UrDeck`); the build output
  is only for development. The script never touches `urdeck-config.json`, the log, `cache/`, `themes/` or foreign plugins.
- `urdeck.log` lines are `[yyyy-MM-dd HH:mm:ss.fff] LEVEL message`; the file rotates to `urdeck.log.1` at 5 MB.
- One host per application folder: `Program.Main` handles `--snapshot`, then `--quit`, then the guard (a named mutex and
  event keyed on the folder, `UrDeck.Engine/Startup/InstanceNames`), then the XAML application. `--quit` sets the event
  and waits for the mutex. "Start with Windows" is the `UrDeck` value under `HKCU\...\Run` (`RunKeyAutostart`); Windows
  owns the setting, the config has no field for it.
- The background (`docs/backgrounds.md`, `src/UrDeck.Engine/Backgrounds`, host `BackgroundLayer`): what it is comes from `BackgroundPlan.Resolve(settings, theme)`,
  an ordered list of candidates: the configuration's `background` (`none` is explicit; absent and `null` leave the choice to the theme), the
  theme's `background.default` shader, its `background.static` (picture inside the theme folder, gradient or colour, drawn by `StillPainter`) and the
  colour. `Build` tries them in order and logs each failure once; the layer goes on from `CandidateIndex + 1` when a picture cannot be decoded.
  `RequestKey` joins every candidate's key with its origin, the theme's name and the scrim, so a theme change that alters the result restarts the layer and
  an unrelated save does not. The scrim (`background.scrim`) is painted last by `DeckBackground.PaintAtTime`, so the swap chain panel, the still canvas and
  `--snapshot` all draw it. `motion.level` does not touch the background. `ThemeCatalog`, `BackgroundCatalog` and `PairingIndex` (`Recommend`) are the
  read-only services for a settings editor. In `urdeck.log` look for `Background: shader '...' (built-in) from theme default, surface ...` (or `configuration`,
  `theme still background`) and `Background: colour (theme colour)`. A moving shader is an
  `SKSwapChainPanel` (`EnableRenderLoop = false`), a still one or a picture an `SKXamlCanvas`, both under the cards in the
  `_background` grid with `IsHitTestVisible = false`. The render scale is the element's size times `scale` with its
  composition visual scaled back (`ElementCompositionPreview.GetElementVisual(el).Scale`): never a `RenderTransform` or
  `Viewbox`, they feed the panel's composition scale and it renders at full size again. Frames are paced from
  `CompositionTarget.Rendering` (30 per second, subscribed only while running), not `FrameClock`: a 1/30 s dispatcher timer
  delivered about 22 frames per second in the spike. Look for `Background: shader '...' (built-in), surface WxH, scale S,
  moving|still` (check the surface size at other display scalings), `Background frames started (reason)` and `Background
  frames stopped (reason): N frames in S s` (N/S about 30), `Windows: display off` / `display on`, and `Background: none`
  in `urdeck.log`. The layer keys on `DeckBackground.RequestKey`, so an unrelated config save does not restart the motion,
  and a background that failed is not tried again until its key changes. A background is a folder `backgrounds/<name>/` next to the exe
  holding `manifest.json` (required, version 1) and the entry `<name>.sksl`; the sixteen built-ins are embedded folders of the same shape
  (`UrDeck.Backgrounds.<name>/<file>`) with a `sample.png` made by `urdeck-cli shader render` (`docs/backgrounds.md`). A loose
  `.sksl` is not loaded (the log names the folder to make). `ShaderLint` checks a folder and `DeckBackground.Create` logs each finding once as
  `Background 'name': SHADERnnn message (file)` (the codes are in `docs/backgrounds.md`); the built-ins must lint clean. `scripts/Install-Local.ps1`
  never touches `backgrounds/`. The deck window must keep **square corners**
  (`WindowCorners.Square`, `DWMWA_WINDOW_CORNER_PREFERENCE` = do not round): with Windows 11's rounded corners the desktop
  compositor blends translucent content over a swap chain three times along the window's diagonal, a bright dotted line
  through every translucent card (`glass` theme, moving background). The log says `Window corners are square`.
  `FrameClock` delivers about 21 frames per second, not 30 (`docs/perf/backgrounds.md`).
- A change to a widget's settings, a theme or a background updates its page in `site/src/content/docs/` as well as the README or `docs/`.
- Do not add per-widget styling or resolution assumptions: styling belongs in the theme (`src/UrDeck.Engine/Themes`, `docs/themes.md`)
  and the user never sees resolution or scaling. Widgets draw no card and name no font; use the SDK components.
