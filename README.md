# UrDeck

UrDeck is a lightweight widget dashboard for a secondary or case display, built as a low-overhead replacement for HYTE Nexus. It was designed around the HYTE Y70 Touch panel (1100x3840, portrait) but adapts to any monitor: the layout is a 4-column grid of square cells computed from the monitor width.

It is a .NET 10 WinUI 3 application (unpackaged, self-contained Windows App SDK) that draws each widget with SkiaSharp on its own GPU-composited layer. Widgets are plain C# plugin DLLs that can be added, replaced or removed while UrDeck is running.

## Status

Phase 1 (foundation) is implemented: widget SDK and Roslyn analyzer, hot-reloading plugin loader, grid layout, the WinUI 3 host with per-monitor DPI handling, JSON config with hot-reload, and a built-in Clock widget. Verified on the real 1100x3840 panel.

Not done yet: an editor UI, other widgets, shared data providers and the `[RefreshOnEvent]` / `[RefreshAdaptive]` refresh strategies that build on them (such widgets currently render once, or refresh at their minimum interval), a theme editor and a monitor picker. See `docs/ROADMAP.md` for the plan. The bar is "no worse than HYTE Nexus" on the same panel, and a page with nothing moving costs close to nothing: the WinUI 3 host measures about 101 MB private, 0% CPU and 0% GPU idle with one Clock (see `docs/perf/render-host-baseline.md` and `docs/perf/nexus-baseline.md`).

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build, run, test

```powershell
dotnet build urdeck.slnx -c Release      # builds everything and copies the Clock plugin to plugins/
dotnet run --project src/UrDeck.Host -c Release
dotnet test urdeck.slnx -c Release       # grid, config, plugin loader and analyzer tests
```

The window covers the target monitor completely. Press Esc to close it. Diagnostics go to `urdeck.log` next to the executable.

### Snapshots

`--snapshot` renders the active page off-screen with the same layout and widget code as the live window, writes a PNG and exits (no window is shown):

```powershell
dotnet run --project src/UrDeck.Host -c Release -- --snapshot out.png --size 1100x3840
```

`--size WxH` defaults to the target monitor's resolution. `*.snapshot.png` files are git-ignored.

## Configuration

Config is `urdeck-config.json`, located next to the executable (`src/UrDeck.Host/bin/<Configuration>/net10.0-windows10.0.19041.0/`). The tracked default lives at `src/UrDeck.Host/urdeck-config.json`; the build copies it to the output folder when the source is newer, so edit the copy next to the executable for live tweaks and the source file for new defaults. The file is hot-reloaded about a second after saving; an invalid edit is ignored and the last good config is kept. Missing files are created with defaults.

```json
{
  "pages": [
    {
      "name": "Default",
      "widgets": [
        {
          "typeId": "urdeck.widgets.clock",
          "col": 0,
          "row": 0,
          "width": 4,
          "height": 2,
          "format": "24h",
          "showDate": true,
          "fontSize": 1.0
        }
      ]
    }
  ],
  "activePage": 0,
  "theme": "default-dark",
  "monitorName": "tallest"
}
```

| Field | Meaning |
|---|---|
| `pages[].widgets[]` | Widgets on a page. `typeId` is the widget's `Id`; `col` (0-3), `row`, `width` (1-4) and `height` are in grid units. Invalid values are clamped and a warning is logged. An unknown `typeId` leaves its cell empty. |
| any other property on a widget | Widget-specific settings, e.g. the Clock's `format` (`"24h"` or `"12h"`), `showDate`, `textColor` (hex), `fontSize` (multiplier) and `style` (`"simple"`, the default, or `"flap"`, a split-flap display whose digits flip when the minute changes). |
| `activePage` | Index of the page to show. |
| `monitorName` | Target monitor: `"primary"`, `"tallest"` (tallest portrait monitor), `"widest"`, `"largest"` (most pixels), or a device name such as `"DISPLAY1"`. |
| `monitor` | 1-based monitor index, used when `monitorName` matches nothing. Falls back to the primary monitor. |
| `theme` | Name of the theme: `default-dark` (default), `default-light`, `glass`, or a folder under `themes/` next to the executable. See [docs/themes.md](docs/themes.md). Unknown names fall back to the default with a warning; changing it applies on config reload without a restart. |
| `dock` | Stored but not used yet. |

Making a theme: create `themes/<name>/theme.json` next to `UrDeck.Host.exe` with only the values you want to change (colours, card radius/gap/padding, font, text sizes); everything else comes from `default-dark`. Bundle a font by putting the `.ttf` in the same folder and naming it in the file. Edit the theme, then save the config file to apply it. Full format: [docs/themes.md](docs/themes.md).

Grid math: `ColumnWidth = screenWidth / 4` and `RowHeight = ColumnWidth`, so on a 1100 px wide panel each cell is 275x275 and a 4x2 widget is 1100x550.

## Writing a widget

A widget is a class deriving from `Widget<TConfig>` with a few attributes; the Roslyn analyzer checks the rules at compile time and plugins hot-reload while UrDeck runs. See [CONTRIBUTING.md](CONTRIBUTING.md#writing-a-widget) for a full example, the project file and the rules, and `widgets/UrDeck.Widgets.Clock` for a real one.

## Project layout

| Path | Purpose |
|---|---|
| `sdk/UrDeck.Sdk` | Widget SDK (`net10.0`, MIT): attributes, `Widget<TConfig>`, `IWidget`, `WidgetConfig`, render context, `Theme` and the `Readout` / `TextLine` components |
| `sdk/UrDeck.Analyzer` | Roslyn analyzer (`netstandard2.0`, MIT) reporting URDECK001-005 |
| `src/UrDeck.Engine` | Plugin loader, config store, grid layout, `PageRenderer` |
| `src/UrDeck.Host` | WinUI 3 application (`net10.0-windows10.0.19041.0`): window and monitor placement, one `SKXamlCanvas` layer per widget |
| `widgets/UrDeck.Widgets.Clock` | Built-in Clock plugin (`urdeck.widgets.clock`) |
| `tests/UrDeck.Engine.Tests`, `tests/UrDeck.Analyzer.Tests` | xUnit tests |
| `docs/` | `ROADMAP.md` (what to work on next), performance notes |
| `openspec/` | Spec-driven documents: `specs/` holds the current capability specs (grid layout, host shell, widget SDK, Clock), `changes/` holds proposals in flight and the archive of finished changes |

The Windows SDK suffix on the host and test target frameworks is required: the Windows App SDK and `SkiaSharp.Views.WinUI` target `net10.0-windows10.0.19041`.

## License

| Path | License |
|---|---|
| `src/UrDeck.Host`, `src/UrDeck.Engine`, `widgets/`, `tests/` | [GPL-3.0-or-later](LICENSE) with the [plugin exception](PLUGIN-EXCEPTION.md) |
| `sdk/` (`UrDeck.Sdk`, `UrDeck.Analyzer`) | [MIT](sdk/LICENSE) |
| Community widgets | The author's choice |
| Windows App SDK (self-contained runtime shipped with the host) | Microsoft; see the licence files in the `Microsoft.WindowsAppSDK` NuGet package |
| Inter variable font (`src/UrDeck.Engine/Themes/Builtin/`) | [SIL Open Font License 1.1](src/UrDeck.Engine/Themes/Builtin/Inter-OFL.txt); the licence text ships with the font |

Widgets that talk to UrDeck only through the SDK API may use any license, including proprietary ones; that is what the plugin exception grants. The license boundary is an assembly boundary: widgets reference only `UrDeck.Sdk` (MIT), never the GPL engine or host. Code samples in the docs are MIT.

## Contributing

All changes go through a pull request that is squash-merged into `main`; see [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, code standards and performance budgets, and [AGENTS.md](AGENTS.md) for the command cheat sheet.
