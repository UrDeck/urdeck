# UrDeck

UrDeck is a lightweight widget dashboard for a secondary or case display, built as a low-overhead replacement for HYTE Nexus. It was designed around the HYTE Y70 Touch panel (1100x3840, portrait) but adapts to any monitor: the layout is a 4-column grid of square cells computed from the monitor width.

It is a .NET 10 WinUI 3 application (unpackaged, self-contained Windows App SDK) that draws each widget with SkiaSharp on its own GPU-composited layer. Widgets are plain C# plugin DLLs that can be added, replaced or removed while UrDeck is running.

## Status

Phase 1 (foundation) is implemented: widget SDK and Roslyn analyzer, hot-reloading plugin loader, grid layout, the WinUI 3 host with per-monitor DPI handling, JSON config with hot-reload, and a built-in Clock widget. Data providers are in as a second plugin kind, with a first-party `system` provider (CPU load per core and in total, memory load) and a Stats widget that shows any reading. Verified on the real 1100x3840 panel.

Not done yet: an editor UI, the other widgets (weather, the performance widget with gauges, shortcuts), sensors that need administrator rights, the `[RefreshAdaptive]` load-based scaling (such widgets currently refresh at their minimum interval), a theme editor and a monitor picker. See `docs/ROADMAP.md` for the plan. The bar is "no worse than HYTE Nexus" on the same panel, and a page with nothing moving costs close to nothing: the WinUI 3 host measures about 101 MB private, 0% CPU and 0% GPU idle with one Clock (see `docs/perf/render-host-baseline.md` and `docs/perf/nexus-baseline.md`).

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
| `providers` | Optional settings per data provider, keyed by provider id: `"providers": { "system": { "intervalMs": 500 } }` samples the `system` provider every 500 ms instead of its default 2000 ms. A value below the provider's minimum (250 ms for `system`) is raised to it and logged. Applies on config reload without a restart. Absent by default; other properties are kept in the file. |
| `dock` | Stored but not used yet. |

### Stats widget

`urdeck.widgets.stats` shows readings from any data provider: a large value with its unit and a label, optionally inside a
gauge. Its settings are a list of `slots`, which fill the positions of the card's size in order:

| Size | Positions |
|---|---|
| 1x1, 2x2 | one reading (slot 1) |
| 4x2 | two gauges side by side (slots 1 and 2) above three text stats (slots 3 to 5) |
| 4x4 | four gauges in two rows (slots 1 to 4) above three text stats (slots 5 to 7) |

Slots beyond the positions of the size stay in the file; a position without a slot stays empty.

```json
{ "typeId": "urdeck.widgets.stats", "col": 0, "row": 2, "slots": [ { "reading": "system:cpu/core/2/load" } ] }
```

A 4x4 performance card: four gauges (CPU load, GPU temperature and load, memory load) above three text stats (the
GPU's power, its clock and the CPU's first core):

```json
{ "typeId": "urdeck.widgets.stats", "col": 0, "row": 0, "width": 4, "height": 4, "slots": [
  { "reading": "system:cpu/load" }, { "reading": "system:gpu/temperature" },
  { "reading": "system:gpu/load" }, { "reading": "system:memory/load", "label": "RAM" },
  { "reading": "system:gpu/power" }, { "reading": "system:gpu/clock" }, { "reading": "system:cpu/core/1/load" } ] }
```

| Slot field | Meaning |
|---|---|
| `reading` | The reading id, `<provider>:<path>`. With no slot the widget shows `system:cpu/load`. |
| `label` | Absent: the label the provider declares (for example `Core 2`). A text: shown instead. `""`: no label. |
| `unit` | `celsius` or `fahrenheit` for readings that have several units (otherwise the reading's default, then the region's). |
| `decimals` | Number of decimals to show (default 0). |
| `style` | `plain`, `gauge` (the theme's default gauge style), `ring`, `bar` or `verticalBar`. Absent: plain at 1x1 and 2x2, the theme's gauge style in a gauge position of a 4x2 or 4x4. Text positions are always plain. A reading that has no range to measure against (a text, a plain number) is drawn plain whatever the style. |
| `min`, `max` | The gauge's range, replacing the one the provider declares (a percentage defaults to 0 to 100). |
| `warning`, `critical` | From these values on the gauge is drawn in the theme's warning or critical colour. Replace the provider's own levels; of the `system` readings only the GPU temperature has any (80 and 90 degrees). In the reading's canonical unit (degrees Celsius for a temperature). |

The fill eases to a new value over about 300 ms; the number and the colour change at once.

The `system` provider offers `system:cpu/load` (all logical processors), `system:cpu/core/<n>/load` (logical processor `n`,
counted from 1) and `system:memory/load`, all as percentages, with no administrator rights. CPU load is the share of time the processors are busy, so with a CPU that boosts it reads lower under load than Task Manager, which weighs by clock speed (`docs/perf/data-providers.md`). A reading that does not exist
shows a dash and the id; a reading whose provider stopped answering keeps its last value in the dimmed text colour. Only
providers that a shown widget uses are started, and one that nothing uses stops five seconds later.

The GPU readings are `system:gpu/load`, `system:gpu/temperature`, `system:gpu/power` (watts) and `system:gpu/clock`
(MHz) for the main GPU, the one with the most dedicated video memory, and `system:gpu/<n>/...` for each adapter,
counted from 1 in that order. They need no administrator rights and cost nothing unless a GPU slot is shown.

| Reading | Source | Verified |
|---|---|---|
| load, temperature | Windows itself (graphics kernel statistics and adapter performance data), any vendor | NVIDIA (RTX 4090) |
| power, clock | the vendor's library: `nvml.dll`, which the NVIDIA driver installs | NVIDIA (RTX 4090) |

On AMD and Intel the load and temperature should work (the code does not depend on the vendor) but nobody has run them
yet, and power and clock show a dash until their vendor libraries are added. `urdeck.log` names each GPU and the source
of each of its readings when it is first sampled, which makes a report from another GPU useful. GPU load is the busiest
engine of the adapter, as in Task Manager; a load sample costs about 6 ms of one core every provider interval while a
GPU load slot is shown (`docs/perf/gpu-readings.md`). Laptops whose NVIDIA GPU is powered down are untested.

Making a theme: create `themes/<name>/theme.json` next to `UrDeck.Host.exe` with only the values you want to change (colours, card radius/gap/padding, font, text sizes); everything else comes from `default-dark`. Bundle a font by putting the `.ttf` in the same folder and naming it in the file. Edit the theme, then save the config file to apply it. Full format: [docs/themes.md](docs/themes.md).

Grid math: `ColumnWidth = screenWidth / 4` and `RowHeight = ColumnWidth`, so on a 1100 px wide panel each cell is 275x275 and a 4x2 widget is 1100x550.

## Writing a widget

A widget is a class deriving from `Widget<TConfig>` with a few attributes; the Roslyn analyzer checks the rules at compile time and plugins hot-reload while UrDeck runs. See [CONTRIBUTING.md](CONTRIBUTING.md#writing-a-widget) for a full example, the project file and the rules, and `widgets/UrDeck.Widgets.Clock` for a real one. A widget that shows data from a provider is `widgets/UrDeck.Widgets.Stats`; a data provider is a class implementing `IDataProvider` in a plugin of its own, see [Writing a data provider](CONTRIBUTING.md#writing-a-data-provider) and `providers/UrDeck.Providers.System`.

## Project layout

| Path | Purpose |
|---|---|
| `sdk/UrDeck.Sdk` | Plugin SDK (`net10.0`, MIT): attributes, `Widget<TConfig>`, `IWidget`, `WidgetConfig`, render context, `Theme`, the `Readout` / `TextLine` components, and the data provider contract and reading formatter (`UrDeck.Sdk.Data`) |
| `sdk/UrDeck.Analyzer` | Roslyn analyzer (`netstandard2.0`, MIT) reporting URDECK001-005 |
| `src/UrDeck.Engine` | Plugin loader, config store, grid layout, `PageRenderer`, the reading hub that runs the data providers |
| `src/UrDeck.Host` | WinUI 3 application (`net10.0-windows10.0.19041.0`): window and monitor placement, one `SKXamlCanvas` layer per widget |
| `providers/UrDeck.Providers.System` | First-party data provider `system` (CPU and memory load) |
| `widgets/UrDeck.Widgets.Clock`, `widgets/UrDeck.Widgets.Stats` | Built-in plugins: Clock (`urdeck.widgets.clock`) and Stats (`urdeck.widgets.stats`) |
| `tests/UrDeck.Engine.Tests`, `tests/UrDeck.Analyzer.Tests` | xUnit tests |
| `docs/` | `ROADMAP.md` (what to work on next), performance notes |
| `openspec/` | Spec-driven documents: `specs/` holds the current capability specs (grid layout, host shell, widget SDK, Clock), `changes/` holds proposals in flight and the archive of finished changes |

The Windows SDK suffix on the host and test target frameworks is required: the Windows App SDK and `SkiaSharp.Views.WinUI` target `net10.0-windows10.0.19041`.

## License

| Path | License |
|---|---|
| `src/UrDeck.Host`, `src/UrDeck.Engine`, `providers/`, `widgets/`, `tests/` | [GPL-3.0-or-later](LICENSE) with the [plugin exception](PLUGIN-EXCEPTION.md) |
| `sdk/` (`UrDeck.Sdk`, `UrDeck.Analyzer`) | [MIT](sdk/LICENSE) |
| Community widgets and data providers | The author's choice |
| Windows App SDK (self-contained runtime shipped with the host) | Microsoft; see the licence files in the `Microsoft.WindowsAppSDK` NuGet package |
| Inter variable font (`src/UrDeck.Engine/Themes/Builtin/`) | [SIL Open Font License 1.1](src/UrDeck.Engine/Themes/Builtin/Inter-OFL.txt); the licence text ships with the font |

Widgets and data providers that talk to UrDeck only through the SDK API may use any license, including proprietary ones; that is what the plugin exception grants. The license boundary is an assembly boundary: widgets and providers reference only `UrDeck.Sdk` (MIT), never the GPL engine or host. Code samples in the docs are MIT.

## Contributing

All changes go through a pull request that is squash-merged into `main`; see [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, code standards and performance budgets, and [AGENTS.md](AGENTS.md) for the command cheat sheet.
