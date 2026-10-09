# SDK/Engine Split — Tasks

> **Status: reference only.** This is the full design from an Opus review of the SDK/Engine split. The owner chose a much
> smaller first step (the split itself, a frozen SDK `AssemblyVersion`, a loader warning); the rest is parked in
> [docs/BACKLOG.md](../BACKLOG.md) under "Parked". Names such as `UrDeck.Core` below describe the code before the split. PR 1 below was done in a reduced form; PRs 2 and 3 are the parked work.

Three PRs, each green on its own. Labels: **[mechanical]** is fine for a cheaper model (Sonnet) given `design.md`; **[judgment]** needs design sense or debugging (Opus, or Sonnet with review). Use `git mv` for every move so history follows the files. In Git Bash use `-p:Foo`, not `/p:Foo`.

Standard verification block (**V**), run from a clean tree (`bin/`/`obj/` removed) before pushing each PR:

1. `dotnet build urdeck.slnx -c Release` → 0 warnings, 0 errors
2. `dotnet test urdeck.slnx -c Release` → all green
3. Coverage floors: `dotnet test tests/UrDeck.Engine.Tests -c Release -p:CollectCoverage=true` and `dotnet test sdk/tests/UrDeck.Sdk.Tests -c Release -p:CollectCoverage=true`
4. `dotnet format urdeck.slnx --verify-no-changes --severity warn`
5. `openspec validate --all --strict`
6. Snapshot: from `src/UrDeck.Host/bin/Release/net10.0-windows10.0.19041.0/` run `UrDeck.Host.exe --snapshot after.snapshot.png --size 1100x3840`; `urdeck.log` shows `Registered widget 'urdeck.widgets.clock'` and no warnings
7. Hot reload: start `UrDeck.Host.exe` detached, rebuild the Clock with a visible change (or touch-copy the DLL) into `plugins/`, and confirm in `urdeck.log`: reload, `Registered widget`, `Unloaded plugin context(s) collected`, no "not collected" warning. Close with Esc (or stop the process).

## 1. PR 1 — `refactor: split UrDeck.Core into UrDeck.Sdk (MIT) and UrDeck.Engine (GPL)`

### 1.1 Baseline
- [ ] 1.1.1 [mechanical] On `main`, build Release and keep the host output folder (e.g. copy it to a scratch directory) for the before/after snapshot in 1.9.2

### 1.2 Layout and license scaffolding
- [ ] 1.2.1 [mechanical] Create `sdk/` with `LICENSE` (`git mv src/UrDeck.Analyzer/LICENSE sdk/LICENSE`), `sdk/Directory.Build.props` (imports the root props; `PackageLicenseExpression=MIT`, `Copyright (c) 2026 Patrick Bigler`) and `sdk/.editorconfig` (MIT header, test suppressions) exactly as in design decision 7
- [ ] 1.2.2 [mechanical] `sdk/Directory.Build.targets`: an error target that fails when a `ProjectReference` of a project under `sdk/` resolves outside `sdk/` (compare full paths against `$(MSBuildThisFileDirectory)`)
- [ ] 1.2.3 [mechanical] Root `.editorconfig`: remove the `[src/UrDeck.Analyzer/**.cs]` section and update the header comment to point to `sdk/.editorconfig`
- [ ] 1.2.4 [mechanical] `git mv src/UrDeck.Analyzer sdk/UrDeck.Analyzer`; remove its `PackageLicenseExpression`/`Copyright` (now inherited); add `IsPackable=false`
- [ ] 1.2.5 [mechanical] Create `sdk/UrDeck.Sdk/UrDeck.Sdk.csproj` (`$(UrDeckTfm)`, `PackageReference SkiaSharp`) and `src/UrDeck.Engine/UrDeck.Engine.csproj` (`$(UrDeckTfm)`, `ProjectReference` to the SDK, `PackageReference SkiaSharp`, `IsPackable=false`)

### 1.3 Move the SDK types
- [ ] 1.3.1 [mechanical] `git mv` the six attribute files, `Enums/TimeUnit.cs`, `Interfaces/IWidget.cs`, `Widget.cs`, `Config/WidgetConfig.cs`, `Rendering/WidgetRenderContext.cs` and `Rendering/ThemeColors.cs` into `sdk/UrDeck.Sdk/` (flat) and change their namespace to `UrDeck.Sdk`
- [ ] 1.3.2 [mechanical] Split `UrDeckJson` out of `WidgetConfig.cs`; remove `SaveConfigJson`/`LoadConfigJson`; move `ToConcrete` into `src/UrDeck.Engine/Config/WidgetConfigConverter.cs` as an `internal static` extension method (body and comment unchanged)
- [ ] 1.3.3 [mechanical] Mark `WidgetRenderContext` `sealed`
- [ ] 1.3.4 [mechanical] Move `RefreshStrategy` out of the attributes into `UrDeck.Engine.Plugins`

### 1.4 Move the engine types
- [ ] 1.4.1 [mechanical] `git mv` the remaining files of `src/UrDeck.Core` into `src/UrDeck.Engine/{Plugins,Config,Layout,Rendering,Diagnostics}`, change namespaces to `UrDeck.Engine.*` (`Plugin` becomes `Plugins`), and move `UrDeckJson` to `UrDeck.Engine.Config` as `internal`
- [ ] 1.4.2 [mechanical] Delete `GridCell.cs` and `IPluginService.cs`; make `WidgetRegistry` `internal` and remove `WidgetPluginLoader.Registry`
- [ ] 1.4.3 [mechanical] Delete `src/UrDeck.Core` and grep the repository for `UrDeck.Core`: nothing may remain outside `openspec/changes/urdeck-framework` and this change

### 1.5 Consumers
- [ ] 1.5.1 [mechanical] Host: reference `UrDeck.Engine` instead of Core; update `using`s in `App.xaml.cs`, `MainWindow.cs`, `MonitorPlacement.cs`, `WidgetView.cs`, `WpfAssemblyCache.cs`
- [ ] 1.5.2 [mechanical] Add `widgets/Directory.Build.props` (design decision 6); reduce `UrDeck.Widgets.Clock.csproj` to the `TargetFramework`; update the Clock's `using`s to `UrDeck.Sdk`
- [ ] 1.5.3 [judgment] Analyzer: change `WidgetBaseMetadataName` to `"UrDeck.Sdk.Widget\`1"` and `AttrNs` to `"UrDeck.Sdk."`; confirm by temporarily removing `[Widget]` from the Clock that URDECK001 fires in a real build, then revert
- [ ] 1.5.4 [mechanical] `urdeck.slnx`: folders `/sdk/`, `/sdk/tests/`, `/src/`, `/widgets/`, `/tests/` with the new project paths

### 1.6 Tests
- [ ] 1.6.1 [mechanical] `git mv tests/UrDeck.Core.Tests tests/UrDeck.Engine.Tests` (rename the csproj); split `ConfigTests.cs` into `ConfigStoreTests.cs` (stays) and `WidgetConfigTests` (goes to the SDK tests); update `using`s and the plugin source in `PluginLoaderTests.BuildPlugin`/`CreateAndRenderWidget`
- [ ] 1.6.2 [mechanical] Engine coverage: `Include=[UrDeck.Engine]*`, `Threshold=65`, `CoverletOutput=.../artifacts/coverage/engine/`
- [ ] 1.6.3 [mechanical] `git mv tests/UrDeck.Analyzer.Tests sdk/tests/UrDeck.Analyzer.Tests`; reference `UrDeck.Sdk`; update the test source `using`s and `typeof(...WidgetAttribute)`
- [ ] 1.6.4 [mechanical] Create `sdk/tests/UrDeck.Sdk.Tests` (xunit, coverlet, `Include=[UrDeck.Sdk]*`, `Threshold=80`, output `artifacts/coverage/sdk/`) with `WidgetConfigTests` moved from 1.6.1
- [ ] 1.6.5 [judgment] Add `WidgetTests.cs` (metadata defaults and precedence, `SupportedSizes`, wrong config type throws, `OnConfigured`, default `UpdateAsync`, `ThemeColors.DefaultDark`, context construction) and `tests/UrDeck.Engine.Tests/WidgetConfigConverterTests.cs`, until the SDK floor of 80% passes
- [ ] 1.6.6 [mechanical] Run `dotnet format` so every file under `sdk/` gets the MIT header and everything else keeps the GPL header; review the diff for header-only changes on moved files

### 1.7 CI
- [ ] 1.7.1 [mechanical] `.github/workflows/ci.yml`: replace the Core coverage step with the Engine and SDK coverage steps (design decision 9)

### 1.8 Docs
- [ ] 1.8.1 [mechanical] `PLUGIN-EXCEPTION.md`: apply the wording from design decision 8, adjusted to the owner's answers to open questions 1 and 2
- [ ] 1.8.2 [mechanical] README (layout table, license map and paragraph, status, test comment), `CONTRIBUTING.md`, `AGENTS.md`, `docs/ROADMAP.md`: apply the wording from design decision 8; fix the stale SkiaSharp 3.119.4 mentions in README to the actual central version
- [ ] 1.8.3 [mechanical] Update `openspec/changes/theme-engine/{design,proposal}.md` and `openspec/changes/display-targeting/proposal.md` references from `UrDeck.Core` to `UrDeck.Sdk`/`UrDeck.Engine` (design decision 8)

### 1.9 Verify and open the PR
- [ ] 1.9.1 [mechanical] Run the verification block **V** (all 7 steps)
- [ ] 1.9.2 [judgment] Snapshot equality: run the baseline exe from 1.1.1 and the new exe with `--snapshot ... --size 1100x3840` back to back in the same minute (retry if the minute rolls over) and compare pixels (e.g. `System.Drawing.Bitmap` in Windows PowerShell 5.1); they must be identical
- [ ] 1.9.3 [mechanical] PR description: state the relicensing of the moved SDK files from GPL-3.0-or-later to MIT by the sole author, list the surface trims, and link this change

## 2. PR 2 — `feat(engine): enforce the SDK boundary in the plugin loader`

- [ ] 2.1 [judgment] `PluginLoadContext.Load`: shared set `{UrDeck.Sdk, SkiaSharp}` binds by simple name to the default context's loaded assembly (version-agnostic); denied set `{UrDeck.Engine, UrDeck.Host}` returns null; the framework keeps default-first behavior
- [ ] 2.2 [judgment] `WidgetPluginLoader.LoadAll`: skip plugins-folder DLLs whose `AssemblyName` is in the shared or denied sets (one INFO line); after `LoadFromAssemblyPath`, run the reference check (SDK range from `UrDeck.Sdk.MinimumCompatibleVersion` metadata up to the host SDK version; SkiaSharp same major with minor not higher; no engine/host reference); on rejection log a WARN with versions, unload the context and queue the shadow directory for deletion
- [ ] 2.3 [mechanical] Until PR 3 adds the metadata, treat a missing `MinimumCompatibleVersion` as the host's own major.minor
- [ ] 2.4 [mechanical] `LoadAssembly`: warn when a type has an attribute named `WidgetAttribute` but is not assignable to `IWidget`
- [ ] 2.5 [judgment] Tests in `tests/UrDeck.Engine.Tests` (compile stub `UrDeck.Sdk` v99 / `UrDeck.Engine` assemblies with Roslyn and reference them from test plugins): stray `UrDeck.Sdk.dll` and `SkiaSharp.dll` skipped; plugin with a private SDK copy next to it still registers and is assignable to the host `IWidget`; newer-SDK plugin rejected with the expected message; engine-referencing plugin rejected; other plugins still load; rejected context collectible
- [ ] 2.6 [mechanical] Raise the Engine coverage floor to the measured value rounded down to a multiple of 5 (never below 65)
- [ ] 2.7 [mechanical] `AGENTS.md` gotcha bullet on the SDK boundary (design decision 8); verification block **V**; also manually drop a copy of `UrDeck.Sdk.dll` into `plugins/` of a running host and confirm the INFO line and that the Clock stays registered

## 3. PR 3 — `feat(sdk): make UrDeck.Sdk packable with the analyzer, public API baseline and host services`

### 3.1 Host services seam
- [ ] 3.1.1 [judgment] Add `IWidgetHost` and `IWidgetLog` to `UrDeck.Sdk` (documented "implemented by the host only"); add `void Attach(IWidgetHost host) { }` as a default interface member on `IWidget`; `Widget<TConfig>` implements it and exposes `protected IWidgetHost Host` defaulting to an internal no-op host
- [ ] 3.1.2 [judgment] Engine: per-instance host implementation that writes through `UrDeckLog` prefixed with the widget type id; `WidgetRegistry.CreateWidget` calls `Attach` before `Configure`; no static references to plugin types
- [ ] 3.1.3 [mechanical] Tests: `Attach` before `Configure` (engine), no-op default and `Attach` storing the host (SDK); hot-reload collectibility test still passes

### 3.2 Versioning and public API
- [ ] 3.2.1 [mechanical] `sdk/Directory.Build.props`: `UrDeckSdkVersion=0.1.0`, `UrDeckSdkMinimumCompatibleVersion=0.1`, `UrDeckSdkSkiaSharpRange=[4.153.0,5.0.0)` (the floor is the current central SkiaSharp version); `UrDeck.Sdk.csproj` sets `Version`, a computed `AssemblyVersion` of `Major.Minor.0.0`, and the `AssemblyMetadata` item; SkiaSharp uses `VersionOverride="$(UrDeckSdkSkiaSharpRange)"`
- [ ] 3.2.2 [mechanical] Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` to `Directory.Packages.props` and to `UrDeck.Sdk` (`PrivateAssets=all`); create an empty `PublicAPI.Shipped.txt` and generate `PublicAPI.Unshipped.txt` (with `#nullable enable`) via the RS0016 code fix; review the list against design decision 1, line by line
- [ ] 3.2.3 [mechanical] Set `EnablePackageValidation=true` (no baseline until 0.1.0 is published; add `PackageValidationBaselineVersion` in the release PR)
- [ ] 3.2.4 [mechanical] `.github/dependabot.yml`: ignore `SkiaSharp*` `version-update:semver-major`

### 3.3 Packaging
- [ ] 3.3.1 [judgment] `UrDeck.Sdk.csproj`: `IsPackable`, `PackageId`, description, tags, `PackageReadmeFile` (new short `sdk/UrDeck.Sdk/README.md`); analyzer `ProjectReference` with `ReferenceOutputAssembly=false` and `PrivateAssets=all`; a `TargetsForTfmSpecificContentInPackage` target that adds the analyzer DLL at `analyzers/dotnet/cs`
- [ ] 3.3.2 [judgment] `sdk/UrDeck.Sdk/build/UrDeck.Sdk.targets` (packed to `build/` and `buildTransitive/`): remove `UrDeck.Sdk` and `SkiaSharp*` from `ReferenceCopyLocalPaths` unless `IsTestProject` or `UrDeckCopySdkAssemblies` is true
- [ ] 3.3.3 [mechanical] CI: "Pack SDK" and "Check SDK package" steps (design decision 9), and upload `artifacts/packages/`
- [ ] 3.3.4 [judgment] Manual end-to-end check: in a scratch folder outside the repo, create a widget project with a `PackageReference` to the local `.nupkg` (local feed), once plain and once with `EnableDynamicLoading=true`; confirm URDECK002 fires when `[WidgetSize]` is removed, the output contains only the widget's files, and the built DLL loads in the host (`urdeck.log` shows it registered)
- [ ] 3.3.5 [mechanical] `CONTRIBUTING.md`: third-party csproj sample with the package, review-checklist item for `PublicAPI.Unshipped.txt`; verification block **V**

## 4. Close-out

- [ ] 4.1 [mechanical] `docs/ROADMAP.md`: mark the SDK/Engine split done, update "Current state"
- [ ] 4.2 [mechanical] Run `openspec validate --all --strict`, then archive this change; when `urdeck-framework` is archived, reconcile its `widget-sdk` "Plugin Isolation and Hot-Reload" text (`UrDeck.Core` becomes `UrDeck.Sdk`) with the `plugin-sdk` requirements
