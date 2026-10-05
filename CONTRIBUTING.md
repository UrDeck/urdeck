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
- Exactly one of `[RefreshOnTick]`, `[RefreshAdaptive]` or `[RefreshOnEvent]`.
- A public parameterless constructor.

Animation: a widget that moves overrides `IsAnimating` and returns `true` while it is mid-animation. The host reads it
after each paint; while it is `true` the host repaints the widget about 30 times a second (without calling
`UpdateAsync` or `NeedsRender`) and stops after the paint that returns `false`. The widget does not choose the rate.
Rules: draw from `context.Time`, never a frame count, so the motion is the same at any frame rate; draw the resting
state on the first paint and on the first paint after `Configure`; make the paint after which `IsAnimating` first
returns `false` the resting state; keep `IsAnimating` cheap and free of side effects. See the Clock's `flap` style.

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

For a first-party widget also add a `ProjectReference` (with `ReferenceOutputAssembly=false`) and a copy step in
`src/UrDeck.Host/UrDeck.Host.csproj`, as the Clock has. Otherwise build the DLL and copy it into the host's `plugins/`
folder (next to `UrDeck.Host.exe`). While UrDeck is running, adding, replacing or deleting a DLL there reloads the plugins
and rebuilds the page after about half a second; the original file is never locked because plugins load from a shadow
copy in their own collectible `AssemblyLoadContext`. Reference the widget by its `Id` in `urdeck-config.json`. A widget
that throws in `Render` shows a red error tile instead of taking the dashboard down.

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
