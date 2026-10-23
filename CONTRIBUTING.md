# Contributing to UrDeck

Thanks for helping. This document covers the workflow, code standards, how to write a widget and what reviewers check.
Agents and humans follow the same rules; the short version for agents is in [AGENTS.md](AGENTS.md).

## Requirements

- Windows 10 or 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download) (`global.json` pins it loosely)
- Node.js, only for `openspec validate` (`npm i -g @fission-ai/openspec`)

## Workflow

1. Pick an item from [docs/BACKLOG.md](docs/BACKLOG.md) (or open an issue first; [docs/ROADMAP.md](docs/ROADMAP.md) has the milestones). Larger changes get an OpenSpec change
   under `openspec/changes/` (proposal, design, specs, tasks) that passes `openspec validate --all --strict`.
2. Branch off `main`: `feat/short-name`, `fix/...`, `docs/...`, `chore/...`. Never commit to `main` directly.
3. Build, test and format locally (commands in [AGENTS.md](AGENTS.md)). Warnings are errors.
4. Push and open a pull request using the template. **The PR title must be a conventional commit**
   (`feat(host): pick the target monitor from a list`); types are `feat`, `fix`, `docs`, `test`, `perf`, `refactor`,
   `chore`, `ci`, `build`, `revert`.
5. CI must be green and conversations resolved. The PR is **squash-merged**: GitHub uses the PR title as the commit
   title and the PR description as the commit body, so keep the description tidy (delete the template comments).
6. The head branch is deleted on merge. Keep branches short-lived and update them from `main` when they fall behind.

`main` is protected by a ruleset: pull request required, required checks `ci-ok` and `pr-title`, linear history, no
force pushes or deletion, squash merges only.

CI runs only the jobs a change can affect (`ci-ok` passes when every job that ran passed; a skipped job counts as passed):

| Changed | Runs |
| --- | --- |
| Anything except the paths below (code, projects, build props, tests, a new folder) | `build` (Windows), `test-linux` |
| Only root `*.md`, `docs/` (not `docs/schemas/`), `site/`, `.github/` (not `ci.yml`), `LICENSE`, `PLUGIN-EXCEPTION.md`, `THIRD-PARTY-NOTICES.md` | nothing but `ci-ok` and `pr-title` |
| `openspec/` | `openspec` (validation) |
| `site/`, `docs/schemas/`, `.github/workflows/site-deploy.yml` | `site` (the website's build and checks) |
| `site/cookbook/` | `test-linux` as well (the recipes are validated with `urdeck-cli`), and `site` |

`workflow_dispatch` (Actions tab, Run workflow) runs every job.

## The website

`site/` is the Astro project behind urdeck.app (how to run and check it: `site/README.md`). A pull request that changes it
is checked by the `site` job (build, schema copy, internal links, no third-party reference); a merge to `main` publishes
it. A change to the settings of a widget, a theme or a background updates its page in `site/src/content/docs/` in the same
pull request: the site is the one home of the user documentation, and `docs/themes.md` and `docs/backgrounds.md` are only the
notes for adding a built-in theme or background.

**The reference is generated.** The tables of theme keys and background manifest keys come from the published schemas
(`docs/schemas/`), a widget's settings table from the catalog (`urdeck-cli catalog export`) with the meanings in the front
matter of its page, the findings tables and the lists of built-in themes, backgrounds and widgets from the catalog. So a new key
needs a `description` in the schema, a new setting needs a line under `settings:` in its widget's page, and a new finding needs a
summary in `FindingCatalog`; the site's build fails, naming the gap, when one is missing, and when a built-in or a code is on no
page. After a change that alters the catalog, run `npm run media` (it rewrites `site/src/generated/catalog.json`).

**The theme cookbook** (`site/cookbook/<recipe>/`: `theme.json`, `recipe.json`, `README.md`) is a set of small theme folders
whose pages show the JSON exactly as it is and a picture drawn from it. CI runs `urdeck-cli theme validate --all site/cookbook
--fail-on-warnings`, so a theme key that is renamed or removed fails the pull request that does it. Add a recipe by adding a
folder; its picture comes with the next media refresh (`npm run media -- cookbook`).

**The pictures and loops** are generated from the product itself: see "Refreshing the site's media" below.

**Adding a "Tested on" entry:** add an object to `site/src/data/tested-on.json` (display, connection, resolution, GPU,
Windows version, what was checked) and open a pull request. The page is made from the file. People without a pull request
use the hardware report issue form.

### Refreshing the site's media

The pictures and loops on the site are generated, never edited by hand and never listed by hand: `urdeck-cli catalog export`
says what exists and `npm run media` in `site/` renders what is missing or stale (`site/README.md`, "Media"). Adding a theme,
widget, widget size or background needs no site file for its media; it only makes the files stale.

- `npm run media -- --status` says how many are missing or stale. CI shows the same counts in a job summary and never fails
  on them, so a pull request that changes a theme does not carry its own pictures.
- The **Site media** workflow (Actions, *Run workflow*; tick "all" to render everything) renders the stale files on Linux
  and opens a pull request `chore(site): refresh media`, or says the media are current. Review that pull request like any other:
  look at the contact sheet of what changed, then squash-merge it.
- Locally, `npm run media` (needs .NET, Node 22 and `ffmpeg`) does the same into your working tree. Prefer the workflow for
  what you commit: text on Linux differs from Windows at the edges of glyphs, and the workflow's output is the reference.

**The GitHub App (once, by the owner).** A pull request opened with the default `GITHUB_TOKEN` does not start the
required checks, so the workflow opens it as an app:

1. Settings > Developer settings > GitHub Apps > New GitHub App. Name it (for example `urdeck-media`), no webhook, no
   callback. Repository permissions: *Contents* read and write, *Pull requests* read and write. Install it on this repository only.
2. Generate a private key on the app's page. Add two repository secrets (Settings > Secrets and variables > Actions):
   `URDECK_MEDIA_APP_ID` (the app's id) and `URDECK_MEDIA_APP_KEY` (the whole `.pem` file).
3. Run the workflow by hand. It should say "Media are current" once the first generation is merged. Without the secrets it
   fails at its first step and says which are missing.

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
- Tests: xUnit in `tests/`, on `net10.0`; CI runs them on Windows (with the solution) and on Linux (the test projects
  alone). Mark a test that needs Windows `[WindowsFact]` (see [AGENTS.md](AGENTS.md)). `UrDeck.Engine` has a line-coverage floor (currently 65%, enforced in CI; raise it when
  coverage improves, never lower it to make a PR pass).
- Dependencies are scanned for known vulnerabilities three ways: NuGet audit fails the build (`NuGetAudit` in
  `Directory.Build.props`), and `.github/workflows/security.yml` runs dependency review (vulnerabilities and licenses on
  PRs), OSV-Scanner and CodeQL. `dotnet test` runs on Microsoft.Testing.Platform (xUnit v3), selected in `global.json`.
- New files get the license header automatically via `dotnet format`.
- Comments explain why, not what. Match the surrounding code.

## Golden images

A golden image is a PNG in `tests/UrDeck.Engine.Tests/Goldens/` that a test renders again and compares with. The sample page
(`src/UrDeck.Preview/SamplePage/urdeck-config.json`: a clock, two stats cards, a weather card and four shortcuts, with the
providers' sample readings and the time 13 October 2026, 21:47:32) is drawn by the preview renderer with every built-in theme at
1100x1920, and with a dock and a page indicator. A change that moves a pixel of a v1 theme fails `GoldenTests`. A change to the
sample page or to a provider's `sample.json` moves the goldens too.

- The goldens run on Windows only (FreeType and DirectWrite rasterise text differently); on Linux they show as skipped.
- A picture passes when at most 0.01 % of its pixels (about 211 of the 2.1 million) have a channel more than 2 away from the
  golden. The limits are `ChannelLimit` and `PixelFraction` in `Support/GoldenImage.cs`; keep them as tight as the noise between
  the developer's machine and `windows-latest` allows.
- On a failure the test writes `<name>.actual.png` and `<name>.diff.png` (differences in red) to `artifacts/golden-failures/`, and CI
  uploads that folder as the `golden-failures` artifact.
- To change a golden on purpose: `URDECK_UPDATE_GOLDENS=1 dotnet test tests/UrDeck.Engine.Tests -c Release --filter Golden`
  (PowerShell: `$env:URDECK_UPDATE_GOLDENS=1` first, and clear it afterwards). Look at every picture that changed before committing it.
  A missing golden fails with the same instruction.
- A pull request that changes a golden says why in its description; a change to a v1 theme golden (`default-dark`, `default-light`,
  `glass`) needs a reason there, because a v1 theme is not meant to move.

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
(see the [theme reference](https://urdeck.app/docs/themes/reference/)). Use `Readout` for a big value with a unit and label, `TextLine` for a line of
text at the theme's label, body or title size, `ImageTile` for an icon or a picture, and `context.Theme` for colours.

Rules enforced at compile time by the analyzer and again by the loader:

- `[Widget]` is required; `Id` is the stable `typeId` used in the config (it defaults to `Name`, so set it explicitly).
- At least one `[WidgetSize(width, height)]` (width 1-4, height at least 1).
- Exactly one of `[RefreshOnTick]`, `[RefreshAdaptive]` or `[RefreshOnData]` (no timer: the widget is painted when shown and
  when a reading it declared changes).
- A public parameterless constructor.

The default configuration (`DefaultConfig`, the settings a widget gets when it is added without any) must draw something
meaningful at every size the widget declares, with the providers' sample data, and depend on nothing of the user's machine (a
file path, an installed application). The previews, the website and a future editor's "add a widget" start from it, and
`PreviewTests.EveryDefaultConfiguration_DrawsSomething_AtEveryPreviewedSize` fails for a card that is empty or reads a reading
that is unavailable. Override it rather than changing the configuration type's own defaults, which are what a file without
the setting gets: `public override StatsConfig DefaultConfig => new() { Slots = [...] };`. A widget that fetches its own data
in `UpdateAsync` has no sample data in a preview: take data through a provider to get one.

`[WidgetSize(4, 8, Preview = false)]` leaves a size out of generated pictures (previews, the website, that test) without
changing where the widget may be placed: for a size that only repeats rows a smaller one shows.

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

### Taking taps, launching and icons

A widget that reacts to a tap implements `UrDeck.Sdk.Input.ITapTarget` beside deriving from `Widget<TConfig>`.
Implementing it is the declaration: the host calls nothing for input on a widget that does not, so a widget that only
shows something costs nothing extra.

```csharp
[Widget("Open", "Opens one thing", Id = "example.open")]
[WidgetSize(1, 1)]
[RefreshOnData]
public class OpenWidget : Widget<OpenConfig>, ITapTarget
{
    private LaunchTarget _target;

    protected override void OnConfigured() => _target = LaunchTarget.Parse(Config.Target);

    public override bool NeedsRender(DateTime now) => false;   // nothing it shows changes by itself

    public override void Render(WidgetRenderContext context)
    {
        var icon = Icons.GetIcon(Config.Target ?? "", (int)context.ContentRect.Width);   // never blocks; null until it is ready
        ImageTile.Draw(context.Canvas, context.Theme, context.ContentRect, icon.Image, label: null, placeholder: _target.DisplayName);
    }

    public bool CanTap(SKPoint point) => _target.IsValid;

    public void OnTap(SKPoint point) => Launcher.Launch(_target.Text);
}
```

- **Points** are in the widget's own pixels, the space of `context.PixelSize`, with `(0, 0)` at the top-left corner of
  the card.
- **`CanTap`** says whether a tap at a point would do something. The host asks when the pointer goes down, to decide
  whether to show the press, and again before it delivers the tap; keep it cheap and free of side effects. The default
  accepts every point.
- **`OnTap`** runs on the UI thread when the tap is recognised: a short press and release that did not move. A swipe, a
  vertical drag and a long hold never tap. Return quickly. Afterwards the host asks `NeedsRender` and repaints the
  widget when it returns `true`.
- **No pressed state in the widget.** While the pointer is down the host scales and dims the whole card with the
  theme's `press` values, on the compositor. Do not draw a pressed look, keep no "is pressed" flag, and do not expect a
  repaint for a press or a release.
- **`Launcher.Launch(target, arguments)`** asks the host to open something the way a desktop shortcut does and returns
  whether it was started; it never throws and it logs a failure itself. Call it from `OnTap`, not later: Windows lets
  the launched application come to the front only while the tap is being handled. `LaunchTarget.Parse(text)` is the
  same classification the host uses (kind, normalised text, a short display name) and needs no host, so a widget can
  tell a valid target from an invalid one without starting anything.
- **`Icons.GetIcon(source, pixelSize)`** returns the icon of a local image file, of what a path or a `shell:` item
  shows in Windows, or of a web address's site. It answers at once: the image, or none with `IsLoading` set while it is
  being loaded on a worker. The host repaints the widget once when the icon is ready or given up on, so ask on every
  paint and draw what you get. The image belongs to the host: do not dispose it and do not keep it for a later paint.
- **`ImageTile.Draw(canvas, theme, rect, image, label, placeholder)`** fits and centres an image (never enlarged beyond
  twice its pixel size), draws an optional label below it, and draws a placeholder square with the first letter of
  `placeholder` when the image is null. It doubles as the loading state.
- Before the widget is attached, and in a test that creates it directly, `Launcher` starts nothing and `Icons` has no
  icons, so no special case is needed. A test passes its own through `Attach(new MyHost(...))` with `IWidgetHost.Launcher`
  and `IWidgetHost.Icons`.
- Other kinds of input (scrolling, a long press) will be further interfaces beside `ITapTarget`.

`widgets/UrDeck.Widgets.Shortcut` is the reference use.

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
- A reading whose description carries an `Attribution` must be credited wherever its data is shown: collect the distinct
  attributions of the readings you draw, `Attribution.Measure` the strip for the content width, take it off the bottom of the
  content rectangle before laying out the rest, and `Attribution.Draw` it (see `widgets/UrDeck.Widgets.Stats`).
- `LottieAnimation` plays a Lottie file the widget embeds (`TryCreate` from a stream, `Draw(canvas, rect, seconds)`, `Dispose`
  when the widget is reconfigured or disposed); it is the one component a widget owns. Compute the time from
  `context.Time`, never from a clock of your own, and report `IsAnimating` while it should be repainted at the frame rate
  (`widgets/UrDeck.Widgets.Weather`). `Log(message)` writes one line to `urdeck.log`.

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
- **Catalog.** `Describe()` lists every reading with its kind (`Number`, `Percent`, `Temperature`, `Text`, `OnOff`, `Time`, `Date`, `Speed`, `Depth`), a short
  label, a full name, and optionally a device, a range, a unit text for plain numbers, a default display unit and
  decimals. A `Depth` reading (rainfall, snowfall) declares the unit its value is reported in (`DepthUnit`, millimetres
  when absent), and the formatter converts it for display; a `Speed` is reported in kilometres per hour. A widget's
  `unit` setting (`celsius` or `fahrenheit`, passed as `ReadingFormatOptions.DisplayUnit`) names the metric or imperial
  system for temperatures, speeds and depths together. A `Date` is a time value shown as a day. The range is what keeps a readout the same size while the value changes. `Warning` and `Critical` say from
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
- **Parameter segments.** A catalog path may have whole segments written `{name}` (`{location}/current/temperature`): such an
  entry is a pattern that stands for every path with any one segment there, so a provider can serve places (or devices) the
  user types without listing them. The engine matches a subscribed path against exact paths first, then patterns (the one
  with the most literal segments wins), and fills `{name}` into the entry's path, label, name and device. `SetDemand` gets
  the concrete paths. The weather provider (`providers/UrDeck.Providers.Weather`) is the example.
- **Times.** `ReadingValue.FromTime(DateTimeOffset)` with a `ReadingKind.Time` entry is an instant plus the UTC offset it is
  read in; the formatter shows it as the time of day in that offset with the region's 12 or 24 hour clock.
- **Attribution.** A source that requires a credit (a CC BY service) declares it on its descriptions:
  `Attribution = new ReadingAttribution("Weather data by Open-Meteo.com", "Open-Meteo.com", "https://open-meteo.com/")`.
  A widget that shows such a reading draws the credit with the `Attribution` component (`Measure` the strip, take it off
  the content rectangle, `Draw` it), so the credit follows the data into every widget. Put the licence text of what you
  ship in `THIRD-PARTY-NOTICES.md`.
- **Network.** Make no request while nothing is subscribed, use https only, bound every request by a timeout, create the
  `HttpClient` in `Start` and dispose it in `Shutdown` (nothing static, so the plugin can be unloaded), and log the first
  failure and the first success after it, not every attempt. Throw from `SampleAsync` on a failure: the engine marks the
  readings stale and retries with its own backoff.
- **Not yet.** Provider settings and secrets (a URL, a token), image values and a catalog that changes while running
  come with the first provider that needs them.

Windows-only APIs need `[SupportedOSPlatform("windows")]` on the provider class when the project targets plain `net10.0`.

### Sample files

A provider can carry made-up readings: previews, the website's pictures, the picture tests and the host's demo mode
(`--demo`) draw with them instead of the provider, so a picture is the same on every machine and shows nothing personal. A
first-party provider must carry one; a provider without one is left out of previews (its readings are unavailable there) and
runs as usual in demo mode. The file is `sample.json`, embedded in the provider's assembly under the name
`urdeck.sample.<provider id>.json`:

```xml
<ItemGroup>
  <EmbeddedResource Include="sample.json" LogicalName="urdeck.sample.example.json" />
</ItemGroup>
```

```json
{
  "version": 1,
  "defaults": { "device": "Sensor 1" },
  "readings": {
    "room/temperature": { "kind": "temperature", "label": "Room", "name": "Room temperature", "min": -20, "max": 50,
                          "keys": [ { "at": 0, "value": 21.5 }, { "at": 3, "value": 22.5 } ], "period": 6 },
    "room/window": { "kind": "onOff", "label": "Window", "name": "Window open", "value": false },
    "room/updated": { "kind": "time", "label": "Updated", "name": "Last update", "value": "07:14" },
    "{place}/name": { "kind": "text", "label": "Place", "name": "Place", "value": "{place}" },
    "room/humidity": { "kind": "percent", "label": "Humidity", "name": "Humidity", "unavailable": "no humidity sensor" }
  }
}
```

- **Readings.** Each key is a path below the provider, and each entry describes the reading as `Describe()` does (the field
  names of `ReadingDescriptor` in camelCase: `kind`, `label`, `name`, and where they apply `device`, `unit`, `displayUnit`,
  `depthUnit`, `min`, `max`, `warning`, `critical`, `decimals`, `attribution`). `defaults` sets any of the optional fields for
  every reading that does not set it. The file describes its readings itself because a catalog may depend on the machine
  (cores, adapters); make the sample a plausible machine.
- **Values.** Give exactly one of `value` (fixed), `keys` (keyframes) or `unavailable` (a reason, to show how a widget draws a
  missing reading). A value has the reading's kind: a number, a text, `true`/`false`, a time of day `"HH:mm"` (that day's time
  in the drawing), or for a `date` a whole number of days from the day drawn (`0` is that day). Keyframes give seconds from the
  start of a drawing (`at`, the first at 0, ascending): a number changes in a straight line between two keys, anything else
  steps; after the last key the value holds, or with `period` (seconds, longer than the last key) the keys repeat, a number
  closing the loop back to the first key, and they continue backwards before 0 (the history a preview fills before its first
  frame). The same moment always gives the same value.
- **Placeholders.** A path may hold whole segments written `{name}`, as a catalog pattern does: the sample answers for any value
  there (`weather:Zurich/...` and `weather:Portland, OR/...` alike), and `{name}` in a text value is replaced by the value asked
  for, so a place's name reads back as the place.
- **The check.** The engine reads the file strictly: an unknown key, a value of the wrong kind or keys out of order are each a
  finding naming the reading, and a file with any finding gives no sample (the log says so once, with the provider's name).
  Add a test like `tests/UrDeck.Engine.Tests/ProviderSampleTests.cs`: the file parses without findings, and every path your
  `Describe()` can list has a sample with an equal description (for a catalog that depends on the machine, every family of
  paths, numbers made `#`). A reading the provider gains without a sample then fails the test, naming it.

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
are backlog item 4; until then, the working targets are:

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
- [ ] Docs updated (README, BACKLOG status, ROADMAP milestones, AGENTS.md if commands or conventions changed)
