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
dotnet test tests/UrDeck.Engine.Tests -c Release -p:CollectCoverage=true   # enforces the UrDeck.Engine line-coverage floor
dotnet format urdeck.slnx --severity warn               # apply style; CI runs it with --verify-no-changes
openspec validate --all --strict
```

Render without a screen (from `src/UrDeck.Host/bin/Release/net10.0-windows10.0.19041.0/`):
`UrDeck.Host.exe --snapshot out.snapshot.png --size 1100x3840`.

CI (`.github/workflows/ci.yml`) runs all of the above on `windows-latest`; run them locally before pushing.

## Layout

```
sdk/UrDeck.Sdk       MIT. The plugin contract: attributes, Widget<T>, WidgetConfig, render context, Theme, Components (Readout, TextLine,
                     Gauge, ImageTile), Data (IDataProvider, readings, ReadingFormatter), Input (ITapTarget), Launch (LaunchTarget,
                     ILauncher), Icons (IIconSource)
sdk/UrDeck.Analyzer  MIT. Roslyn analyzer (URDECK001-005), netstandard2.0
src/UrDeck.Engine     GPL. Plugin loader, config store, grid layout, gestures and hit testing, PageRenderer, ReadingHub (runs the
                      providers), per-widget WidgetServices, Launcher, IconService (shell, file and site icons), logging
src/UrDeck.Host       GPL. WinUI 3 app: window/monitor placement, one SKXamlCanvas layer per widget
providers/            GPL. First-party data provider plugins (UrDeck.Providers.System); reference only UrDeck.Sdk
widgets/              first-party widget plugins (UrDeck.Widgets.Clock, .Stats, .Weather, .Shortcut); with the providers, copied to
                      plugins/ by the host build (the UrDeckPlugin list in UrDeck.Host.csproj)
tests/                xUnit projects
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

- Host and test projects must target `net10.0-windows10.0.19041.0` (`$(UrDeckWindowsTfm)`): the Windows App SDK and
  `SkiaSharp.Views.WinUI` target that Windows SDK version; plain `net10.0-windows` does not resolve them.
- The host is an unpackaged, self-contained WinUI 3 app with its own `Main` (`Program.cs`): `--snapshot` runs with the
  engine alone and exits before the XAML application starts. `App.xaml` must stay (building the application resources
  in code crashes). Widget types are never XAML types.
- The window has `WS_EX_NOACTIVATE`: it never takes focus, so Escape does not close it. Close it from the tray icon (right-click, Quit; Windows 11 may hide it in the overflow flyout), or
  start it with `URDECK_ACTIVATABLE=1` (the window can take focus and Escape closes it) when developing.
- Launching `UrDeck.Host.exe` (not `--snapshot`) blocks the shell. Start it detached and read `urdeck.log` next to the exe;
  it logs monitors, target vs. actual content area, placed widgets, reloads and warnings.
- Screen capture: use Windows PowerShell 5.1 (`powershell.exe`, not `pwsh`), call `SetProcessDpiAwarenessContext(-4)`
  first, then `Graphics.CopyFromScreen` on the monitor bounds. `PrintWindow` on the window returns blank. Prefer one
  capture, or ask the user to look.
- In Git Bash, MSYS rewrites `/p:Foo` style switches as paths; use `-p:Foo`.
- Plugins load from a shadow copy in a collectible `AssemblyLoadContext`. A removed `SKXamlCanvas` can stay
  alive after it leaves the tree, so `WidgetView.Dispose` drops its widget reference (without that, old plugin contexts
  were not collected); keep plugin types out of long-lived static caches.
- The host build copies `src/UrDeck.Host/urdeck-config.json` over the one next to the exe whenever the source file is
  newer. After editing the tracked sample, a build replaces a working config in `bin/`: back it up first.
- Do not add per-widget styling or resolution assumptions: styling belongs in the theme (`src/UrDeck.Engine/Themes`, `docs/themes.md`)
  and the user never sees resolution or scaling. Widgets draw no card and name no font; use the SDK components.
