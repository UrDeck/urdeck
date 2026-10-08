# Contributing to UrDeck

Thanks for helping. This document covers the workflow, code standards, how to write a widget and what reviewers check.
Agents and humans follow the same rules; the short version for agents is in [AGENTS.md](AGENTS.md).

## Requirements

- Windows 10 or 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download) (`global.json` pins it loosely)
- Node.js, only for `openspec validate` (`npm i -g @fission-ai/openspec`)

## Workflow

1. Pick an item from [docs/ROADMAP.md](docs/ROADMAP.md) (or open an issue first). Larger changes get an OpenSpec change
   under `openspec/changes/` (proposal, design, specs, tasks) that passes `openspec validate --all --strict`.
2. Branch off `main`: `feat/short-name`, `fix/...`, `docs/...`, `chore/...`. Never commit to `main` directly.
3. Build, test and format locally (commands in [AGENTS.md](AGENTS.md)). Warnings are errors.
4. Push and open a pull request using the template. **The PR title must be a conventional commit**
   (`feat(host): pick the target monitor from a list`); types are `feat`, `fix`, `docs`, `test`, `perf`, `refactor`,
   `chore`, `ci`, `build`, `revert`.
5. CI must be green and conversations resolved. The PR is **squash-merged**: GitHub uses the PR title as the commit
   title and the PR description as the commit body, so keep the description tidy (delete the template comments).
6. The head branch is deleted on merge. Keep branches short-lived and update them from `main` when they fall behind.

`main` is protected by a ruleset: pull request required, required checks `build` and `pr-title`, linear history, no
force pushes or deletion, squash merges only.

## Licensing

- See the license map in the [README](README.md#license). The default is GPL-3.0-or-later with the
  [plugin exception](PLUGIN-EXCEPTION.md); everything under `sdk/` (`UrDeck.Sdk`, `UrDeck.Analyzer`) is MIT.
- Every C# file starts with an SPDX header (`SPDX-License-Identifier` plus a copyright line). It is enforced by
  `.editorconfig` (IDE0073), so `dotnet format` adds it to new files; files under `sdk/` get the MIT header
  automatically. Put a new project's license in its csproj (`PackageLicenseExpression`) if it differs from the default.
- By contributing you agree that your contribution is licensed under the license of the directory you change
  (inbound = outbound). There is no CLA or DCO for now.
- Code samples in the docs (README, this file) are MIT-licensed, so widget authors can copy them freely. Do not copy code
  from the official widgets into a differently licensed widget: use the samples or the widget template instead.

## Code standards

- Enforced by `.editorconfig` and the build (`dotnet format urdeck.slnx --verify-no-changes --severity warn` in CI):
  file-scoped namespaces, `using`s outside the namespace and sorted, `_camelCase` private instance fields, PascalCase
  static fields, `I` prefix for interfaces, `var` only when the type is apparent, LF line endings.
- Shared MSBuild settings are in `Directory.Build.props`; package versions are only in `Directory.Packages.props`.
- Tests: xUnit in `tests/`. `UrDeck.Engine` has a line-coverage floor (currently 65%, enforced in CI; raise it when
  coverage improves, never lower it to make a PR pass).
- New files get the license header automatically via `dotnet format`.
- Comments explain why, not what. Match the surrounding code.

## Writing a widget

A widget is a class deriving from `Widget<TConfig>` in a class library that references `UrDeck.Sdk`. Attributes
provide the metadata, so you normally override only `Render`. `Render` is synchronous, runs on the UI thread and must
be fast; override `UpdateAsync` to fetch data off the render path.

```csharp
using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;

public class HelloConfig : WidgetConfig
{
    public string Text { get; set; } = "Hello, UrDeck";
}

[Widget("Hello", "Draws a greeting", Id = "example.hello")]
[WidgetSize(2, 1)]
[WidgetSize(4, 1)]
[RefreshOnTick(1, TimeUnit.Minutes)]
[Category("Examples")]
public class HelloWidget : Widget<HelloConfig>
{
    public override void Render(WidgetRenderContext context)
    {
        // The host has already drawn the card (fill, border, rounded clip). Draw content inside ContentRect with the
        // shared components; the theme supplies fonts, sizes and colours.
        Readout.Draw(context.Canvas, context.Theme, context.ContentRect, "42",
            new ReadoutOptions { Unit = "%", Label = Config.Text, WidestValue = "100" });
    }
}
```

Widgets draw no card or background and name no font or fixed size: the theme owns the look of every widget on the page
(see [docs/themes.md](docs/themes.md)). Use `Readout` for a big value with a unit and label, `TextLine` for a line of
text at the theme's label, body or title size, and `context.Theme` for colours.

Rules enforced at compile time by the analyzer and again by the loader:

- `[Widget]` is required; `Id` is the stable `typeId` used in the config (it defaults to `Name`, so set it explicitly).
- At least one `[WidgetSize(width, height)]` (width 1-4, height at least 1).
- Exactly one of `[RefreshOnTick]`, `[RefreshAdaptive]` or `[RefreshOnData]` (no timer: the widget is painted when shown and
  when a reading it declared changes).
- A public parameterless constructor.

Animation: a widget that moves overrides `IsAnimating` and returns `true` while it is mid-animation. The host reads it
after each paint; while it is `true` the host repaints the widget about 30 times a second (without calling
`UpdateAsync` or `NeedsRender`) and stops after the paint that returns `false`. The widget does not choose the rate.
Rules: draw from `context.Time`, never a frame count, so the motion is the same at any frame rate; draw the resting
state on the first paint and on the first paint after `Configure`; make the paint after which `IsAnimating` first
returns `false` the resting state; keep `IsAnimating` cheap and free of side effects. See the Clock's `flap` style.

### Drawing a gauge

`Gauge.Draw(canvas, theme, rect, style, reading, options)` draws a formatted reading (see below) with a shape, `Ring`,
`Bar`, `VerticalBar` or `Plain` (the readout alone), and places the readout itself. `GaugeScale.Resolve(reading,
descriptor, options)` turns a reading into the `Fraction` (0 to 1, or null) and `Level` (normal, warning, critical) to
pass in `GaugeOptions`; use it so that your widget scales like every other: the range is yours, else the catalog's, else
0 to 100 for a percentage, and the value is rounded to the shown decimals first. With no fraction, draw `Plain`.
`theme.GaugeStyle` is the style the theme prefers. The gauge keeps no state: the fraction you pass is the one drawn, so
an ease is yours (see Animation: draw from `context.Time`, move from the fraction last drawn, keep `IsAnimating` true only
while moving, and draw the first paint at rest). To draw several gauges at one text size, call `Gauge.Measure` for each
and pass `smallest / own` as `GaugeOptions.ReadoutScale`. The stats widget is the reference use.

### Using readings

A widget that shows data from a data provider (CPU load, a sensor, a Home Assistant state) declares the reading ids it
needs and reads them when it paints. It never polls, subscribes or unsubscribes itself: the host subscribes while the
widget is shown and asks `NeedsRender` when one of the declared readings changes.

```csharp
[Widget("Load", "Shows one reading", Id = "example.load")]
[WidgetSize(1, 1)]
[RefreshOnData]
public class LoadWidget : Widget<LoadConfig>
{
    // From the config, cheap, free of side effects, the same ids until the config changes.
    public override IReadOnlyCollection<string> Subscriptions => [Config.Reading];

    public override bool NeedsRender(DateTime now) => /* compare what Render would draw with what it last drew */ true;

    public override void Render(WidgetRenderContext context)
    {
        var reading = Readings.Read(Config.Reading);              // lock-free, no I/O, never throws
        var entry = Readings.Describe(Config.Reading);           // the provider's catalog entry (label, kind, range), or null
        var text = ReadingFormatter.Format(reading, entry, new ReadingFormatOptions(), Readings);
        Readout.Draw(context.Canvas, context.Theme, context.ContentRect, text,
            new ReadoutOptions { Label = entry?.Label });
    }
}
```

- A reading id is `<provider id>:<path>`, for example `system:cpu/core/2/load`; ids are compared without regard to case.
- `Readings` (on `Widget<TConfig>`) is available after the host attached the widget; until then, and in a test that creates
  the widget directly, every reading is unavailable, so a widget needs no special case for it. Tests can pass their own
  `IReadingSource` through `Attach(new MyHost(source))`.
- `ReadingFormatter` decides how a value is written (rounding, the `%` and `°` units, `On`/`Off`, a dash when there is no
  value) and `Readout.Draw` with its result draws a value that is not current (pending with a last value, stale) in the
  theme's muted colour. Use them, so every widget shows the four states (ok, pending, stale, unavailable) alike.
- `[RefreshOnData]` only means "no timer". Reading changes trigger the `NeedsRender` check under any refresh attribute, so a
  widget with `[RefreshOnTick]` may also use readings. `UpdateAsync` is not called for a reading change.
- Compare what you would draw (the formatted text, the label, whether it is current), not the raw value: a load that moves
  from 3.2 to 3.4 percent draws the same `3`, and the widget should not be repainted for it.
- Do not keep the host or anything obtained from `Readings` beyond the widget's own lifetime.

`widgets/UrDeck.Widgets.Stats` is a complete example.

Project file for a first-party widget (modelled on `widgets/UrDeck.Widgets.Clock`; it inherits the shared settings from
`Directory.Build.props`). A third-party widget in its own repo sets `TargetFramework` and the other properties itself.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$(UrDeckTfm)</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <!-- UrDeck.Sdk and SkiaSharp come from the host at runtime; do not copy them next to the plugin. -->
    <ProjectReference Include="..\..\sdk\UrDeck.Sdk\UrDeck.Sdk.csproj" Private="false" />
    <ProjectReference Include="..\..\sdk\UrDeck.Analyzer\UrDeck.Analyzer.csproj">
      <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
      <OutputItemType>Analyzer</OutputItemType>
    </ProjectReference>
  </ItemGroup>
</Project>
```

For a first-party widget or provider also add its project to the `UrDeckPlugin` list in `src/UrDeck.Host/UrDeck.Host.csproj`
(the host builds every listed project and copies its DLL to `plugins/`). Otherwise build the DLL and copy it into the host's `plugins/`
folder (next to `UrDeck.Host.exe`). While UrDeck is running, adding, replacing or deleting a DLL there reloads the plugins
and rebuilds the page after about half a second; the original file is never locked because plugins load from a shadow
copy in their own collectible `AssemblyLoadContext`. Reference the widget by its `Id` in `urdeck-config.json`. A widget
that throws in `Render` shows a red error tile instead of taking the dashboard down.

## Writing a data provider

A data provider supplies readings (numbers, texts or on/off states) that any widget can show. It is a class implementing
`IDataProvider` with `[DataProvider]`, in a plugin assembly that references only `UrDeck.Sdk` (a plugin may hold providers,
widgets or both; it loads from `plugins/` and hot-reloads like a widget). `providers/UrDeck.Providers.System` is the
first-party example and uses the same contract.

```csharp
[DataProvider("example", "Example", DefaultIntervalMs = 2000, MinIntervalMs = 500)]
public sealed class ExampleProvider : IDataProvider
{
    private IReadingSink? _sink;

    public IReadOnlyList<ReadingDescriptor> Describe() =>
    [
        new ReadingDescriptor("room/temperature", ReadingKind.Temperature, "Room", "Room temperature") { Device = "Sensor 1" },
    ];

    public void Start(IReadingSink sink) => _sink = sink;

    public void SetDemand(IReadOnlyCollection<string> paths) { /* remember what is wanted; skip work nobody needs */ }

    public Task SampleAsync(CancellationToken cancellationToken)
    {
        _sink!.Publish("room/temperature", 21.5);   // temperatures are always degrees Celsius
        return Task.CompletedTask;
    }

    public void Shutdown() => _sink = null;
}
```

- **Ids.** The provider id is lowercase letters, digits, `.` and `-`, and is the collision domain: if two plugins declare
  the same id the first by file name is kept and the other is rejected with a warning that names both. A reading is
  addressed as `<provider id>:<path>`; the path is yours (`cpu/core/2/load`). Keep paths stable and portable (the same
  path on every machine), because users write them into their config.
- **Catalog.** `Describe()` lists every reading with its kind (`Number`, `Percent`, `Temperature`, `Text`, `OnOff`), a short
  label, a full name, and optionally a device, a range, a unit text for plain numbers, a default display unit and
  decimals. The range is what keeps a readout the same size while the value changes. `Warning` and `Critical` say from
  which value (canonical unit) the reading deserves attention; declare them only where a high value is a fault (a
  temperature), never for a load, and a gauge colours itself by them. It is called once, before `Start`, and
  must not need `Start`. The catalog is fixed while the provider runs.
- **Lifetime and demand.** The engine creates the provider when a reading of it is first subscribed to (or its catalog is
  requested), calls `Start`, then `SetDemand` with the paths currently subscribed to, and again whenever that set changes.
  Skip the work for readings nobody wants. It stops the provider five seconds after the last subscription ends (so a
  page rebuild does not restart it) and calls `Shutdown`, after which a new instance is created if it is needed again.
- **Sampling and threading.** The engine calls `SampleAsync` on a thread-pool thread, once at start, once more after 250 ms
  (so a provider that needs two samples for a rate, like CPU load, has a value after a quarter of a second) and then per
  interval, never two at once. `SetDemand` may run while a sample is running, so guard what the two share. Everything
  published during one `SampleAsync` becomes visible together when the call returns. A pushed provider (a socket, an
  event) publishes from its own callbacks at any time and may leave `SampleAsync` empty. `IReadingSink` is thread-safe.
  Publish `sink.Unavailable(path, reason)` for a reading you cannot supply right now.
- **Rate.** `DefaultIntervalMs` is used unless the user sets `providers.<id>.intervalMs`; a smaller value is raised to
  `MinIntervalMs`. The provider never sees the setting.
- **States.** Readings the engine shows are ok, pending (subscribed, nothing reported yet), stale (the last sample failed
  or timed out, or the provider reported other readings in three samples in a row but not this one) or unavailable. You do
  not model them; throwing from a member, or not answering, is how a failure is reported.
- **Failures and the hang limit.** An exception from any member is caught and logged with the provider id. After a failure
  the provider is sampled again with a delay that doubles from the interval up to a minute. A sample that has not finished
  after five seconds or five intervals (whichever is longer) counts as failed and is cancelled through the token; if it
  still does not return, the engine never starts a second sample, so a hung provider costs one thread-pool thread until
  its plugin is reloaded. Honour the cancellation token. Killing a hung provider needs the out-of-process helper
  (a later change).
- **Cost.** Do the minimum per sample, reuse buffers, and do nothing while stopped. A page without data widgets costs no
  provider code at all, and that is a budget (see below).
- **Logging.** `sink.Log(message)` writes one line to `urdeck.log` under the provider's id. Use it for facts that make
  a bug report useful (which device, which source), once, not per sample.
- **Not yet.** Provider settings and secrets (a URL, a token), image values and a catalog that changes while running
  come with the first provider that needs them.

Windows-only APIs need `[SupportedOSPlatform("windows")]` on the provider class when the project targets plain `net10.0`.

### Adding a GPU vendor (AMD, Intel)

The `system` provider reads GPU load and temperature from Windows for every vendor. Power and clock come from the
vendor's own library through `IGpuVendorReader` (`providers/UrDeck.Providers.System/IGpuVendorReader.cs`); only NVIDIA
(`NvmlReader`, `nvml.dll`) exists. To add a vendor, implement the interface (load the library from the system directory
only, find the device from the `GpuAdapter`'s PCI location or name, return watts and MHz, release everything in `Close`)
and return it from `WindowsGpuPlatform.CreateVendorReader` for that PCI vendor id (`0x1002` AMD, `0x8086` Intel). The ids
(`system:gpu/power`, `system:gpu/clock`) and the pages do not change. The provider opens the reader only when power or
clock is shown, does not retry a failed open, and logs the source; test it with a fake `IGpuVendorReader`
(`tests/UrDeck.Engine.Tests/GpuReadingsTests.cs`). Say in the PR which hardware you ran it on: the README lists what is
verified per vendor.

## Performance budgets

UrDeck's selling point is a low footprint, so cost is reviewed like correctness. The formal tiers and the benchmark mode
are roadmap item 4; until then, the working targets are:

- Host baseline: about 100 MB private memory in Release with one Clock (the WinUI 3 host, `docs/perf/render-host-baseline.md`), no CPU or GPU use while nothing changes. The bar is "no worse than HYTE Nexus" on the same panel (`docs/perf/nexus-baseline.md`).
- A widget's dominant cost is its render surface (`width x height x 4` bytes); keep additional allocations small and
  avoid per-frame allocations in `Render`.
- Refresh as rarely as the content allows (the Clock needs a repaint once a minute, not every second).
- No blocking or slow work in `Render`; fetch in `UpdateAsync`.

State the measured impact under "Performance impact" in the PR description.

## Review checklist

- [ ] PR title is a conventional commit; description is written for `git log`
- [ ] Builds with 0 warnings; tests added or updated; format clean; CI green
- [ ] OpenSpec change linked/updated and tasks ticked only when verified
- [ ] No per-widget styling or resolution/scaling assumptions leaked in
- [ ] Performance impact measured or explicitly "none"
- [ ] Rendering checked with `--snapshot`, and on the real panel for host/layout/DPI changes
- [ ] Docs updated (README, ROADMAP status, AGENTS.md if commands or conventions changed)
