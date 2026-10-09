# SDK/Engine Split — Design

> **Status: reference only.** This is the full design from an Opus review of the SDK/Engine split. The owner chose a much
> smaller first step (the split itself, a frozen SDK `AssemblyVersion`, a loader warning); the rest is parked in
> [docs/BACKLOG.md](../BACKLOG.md) under "Parked". Names such as `UrDeck.Core` below describe the code before the split.

## Context

`src/UrDeck.Core` (`net10.0`, SkiaSharp only) contains 20 source files. The host (`src/UrDeck.Host`) references it directly; the Clock plugin references it with `Private="false"` and gets the analyzer as an `OutputItemType=Analyzer` project reference. Plugins are loaded by `WidgetPluginLoader` from a shadow copy into a collectible `PluginLoadContext`, whose `Load` override first asks the default context for *any* assembly (`Default.LoadFromAssemblyName`) and only then falls back to the plugin's `deps.json` and sibling files.

Facts checked against the code and build output (2026-09-29):

- Nothing outside `UrDeck.Core` uses `GridCell`, `IPluginService`, `WidgetConfig.SaveConfigJson`/`LoadConfigJson` or `WidgetPluginLoader.Registry`. `ToConcrete` is only called by `WidgetRegistry.CreateWidget`. `RefreshStrategy` is only used by `WidgetDescriptor`. `UrDeckJson` is used by `ConfigStore` and `WidgetConfig`.
- `UrDeck.Core` has the default `AssemblyVersion` 1.0.0.0. SkiaSharp's `AssemblyVersion` is `major.minor.0.0` (4.153.0 ships 4.153.0.0; 3.119.4 shipped 3.119.0.0).
- The Clock's `bin/` contains only `UrDeck.Widgets.Clock.{dll,pdb,deps.json,xml}`. Class libraries do not copy package dependencies, and `Private="false"` stops the project reference, so neither Core nor SkiaSharp is copied.
- Current coverage (`UrDeck.Core.Tests`): 70.6% line overall. Files that become SDK: 76/124 lines (61%), mostly reached indirectly through the loader tests. Files that become Engine: 317/433 lines (73%, about 76% once the dead `GridCell` is deleted).
- Dependabot PR #2 moved SkiaSharp from 3.119.4 to 4.153.0, a **major** version. Under a published SDK that would have been a breaking change for every plugin. The docs (README, `urdeck-framework`) still say 3.119.4.
- The owner is the only human author of every file that moves to `sdk/` (the other commits are AI co-authors and Dependabot), so relicensing them from GPL to MIT needs nobody else's consent.

## Goals / Non-Goals

**Goals:** make the license boundary an assembly boundary; keep the SDK surface minimal and stable; keep `plugins/` hot-reload and type identity correct; land forward-looking namespaces and seams; keep the build green at every step; no user-visible behavior change.

**Non-Goals:** publishing the package (item 13); the widget template; designing the event bus, input API, theme API or display backends; a separate SDK repository.

## Decisions

### 1. Type-by-type boundary

Rule: a type goes into `UrDeck.Sdk` only if a plugin author must name it, or if it appears in the signature of something they must name. Everything else goes into `UrDeck.Engine`, and inside the engine it is `internal` unless the host needs it.

| Type (today, `UrDeck.Core.*`) | Goes to | Namespace after | Notes |
|---|---|---|---|
| `WidgetAttribute`, `WidgetSizeAttribute`, `CategoryAttribute`, `RefreshOnTickAttribute`, `RefreshAdaptiveAttribute`, `RefreshOnEventAttribute` | **Sdk** | `UrDeck.Sdk` | Unchanged API. |
| `TimeUnit` | **Sdk** | `UrDeck.Sdk` | Used by `[RefreshOnTick]`. |
| `IWidget`, `IWidget<TConfig>` | **Sdk** | `UrDeck.Sdk` | The host drives widgets through `IWidget`; it must be the same type in every load context. Gains a default-implemented `Attach(IWidgetHost)` (decision 4). |
| `Widget<TConfig>` | **Sdk** | `UrDeck.Sdk` | Gains `protected IWidgetHost Host`. |
| `WidgetConfig` | **Sdk** | `UrDeck.Sdk` | Stays the base of every widget config. `ToConcrete` moves out; `SaveConfigJson`/`LoadConfigJson` removed (unused). See decision 2. |
| `WidgetRenderContext` | **Sdk** | `UrDeck.Sdk` | Becomes `sealed`; keeps its public constructor (widget unit tests need it); later additions are `init` properties so the constructor never changes. |
| `ThemeColors` (incl. `DefaultDark`) | **Sdk** | `UrDeck.Sdk` | Exposed by the render context. `DefaultDark` stays as the SDK-side fallback palette until the theme engine replaces it with data. |
| *new* `IWidgetHost`, `IWidgetLog` | **Sdk** | `UrDeck.Sdk` | Host services seam (decision 4). Implemented only by the engine. |
| `RefreshStrategy` | Engine | `UrDeck.Engine.Plugins` | Only describes the descriptor; widgets never see it. |
| `WidgetDescriptor` | Engine | `UrDeck.Engine.Plugins` | Public (the host reads `RefreshInterval`). |
| `WidgetRegistry` | Engine | `UrDeck.Engine.Plugins` | `internal`; `WidgetPluginLoader.Registry` removed. |
| `WidgetPluginLoader` | Engine | `UrDeck.Engine.Plugins` | Public (the host). Name kept; generalizing it to plugin kinds is a later engine-internal refactor. |
| `PluginLoadContext` | Engine | `UrDeck.Engine.Plugins` | Stays `internal`; changes in decision 3. |
| `IPluginService` | **deleted** | | Single implementation, no consumer. |
| `ConfigStore`, `UrDeckConfig`, `PageConfig`, `DockItemConfig` | Engine | `UrDeck.Engine.Config` | Public (the host). `PageConfig.Widgets` stays `List<WidgetConfig>` (an SDK type; the engine depends on the SDK). |
| `UrDeckJson` | Engine | `UrDeck.Engine.Config` | `internal`. |
| `WidgetConfig.ToConcrete` | Engine | `UrDeck.Engine.Config.WidgetConfigConverter` (internal static) | Pure plumbing over public members; keeps its private-resolver comment (collectible ALC safety). |
| `GridLayoutManager`, `WidgetLayoutItem` | Engine | `UrDeck.Engine.Layout` | Public (the host). |
| `GridCell` | **deleted** | | Unused. It is trivial to bring back when the editor (item 11) needs overlap checks. |
| `PageRenderer`, `WidgetPainter` | Engine | `UrDeck.Engine.Rendering` | Public (host and snapshot). `PageRenderer` is the future basis of display backends, but it stays engine code. |
| `UrDeckLog` | Engine | `UrDeck.Engine.Diagnostics` | Public (host). Widgets log through `IWidgetHost.Log` instead. |

Resulting SDK surface: 6 attributes, 1 enum, 2 widget interfaces, 1 base class, 1 config base, 1 render context, 1 color record, 2 service interfaces. That is small enough to review line by line in `PublicAPI.Shipped.txt`.

### 2. Awkward types and how they are resolved

- **`WidgetConfig` carries placement** (`typeId`, `col`, `row`, `width`, `height`, `isVisible`, `parameters`), which is engine data, next to widget settings. *Decision:* keep it in the SDK unchanged. The page JSON is flat (placement and settings on one object), `ToConcrete` relies on that, and the editor (item 11) needs placement on the same object. The spec text documents the placement properties as host-owned: widgets read them and never write them. *Rejected:* splitting into an engine `WidgetPlacement` plus an SDK `WidgetConfig`, which changes the config model and `ToConcrete` in what should be a pure move. It can still be done later without a break by adding types and obsoleting members.
- **JSON helpers on `WidgetConfig`.** `ToConcrete` is engine plumbing (moved). `SaveConfigJson`/`LoadConfigJson` are unused and would drag `UrDeckJson` into the SDK (removed). The SDK keeps only the `System.Text.Json` attributes, which are in the shared framework and add no dependency. The camelCase naming is documented as a contract. The options object is not exposed.
- **Logging and services for widgets.** Widgets are created with `Activator.CreateInstance` (a public parameterless constructor is a rule that both the analyzer and the loader enforce), so constructor injection is out. *Decision:* the SDK defines `IWidgetHost` (engine-implemented; 0.1 has only `IWidgetLog Log`) and `IWidget.Attach(IWidgetHost host)` as a **default interface member** (no-op). `Widget<TConfig>` implements it and stores the value in `protected IWidgetHost Host`, which defaults to a no-op host so unit tests and `OnConfigured` never see null. `WidgetRegistry.CreateWidget` calls `Attach` before `Configure`. The engine's implementation prefixes log lines with the widget id and writes through `UrDeckLog`. It is one small object per widget instance with no static state, so hot-reload is unaffected. *Rejected:* `Microsoft.Extensions.Logging.Abstractions` (it is not in `Microsoft.NETCore.App`, so it would be another package that every plugin depends on and another assembly to share across load contexts); routing `System.Diagnostics.Trace` (no per-widget attribution, and it gives no home for future services); a static `WidgetLog` in the SDK (static state shared across plugins, and it cannot be substituted in tests).
- **Does the render context leak engine types?** No. It exposes `SKCanvas`, `DateTime`, `System.Drawing.Size`, `ThemeColors`, `WidgetConfig` and `CancellationToken`. `System.Drawing.Size` comes from `System.Drawing.Primitives` in the shared framework, not from Windows GDI+. It stays; replacing it with `SKSizeI` is not worth a break.
- **Metadata duplicated on `IWidget`** (`Name`, `Description`, `Category`, `SupportedSizes`). The engine reads the attributes, and the host only uses `Name` for logs and the error tile. It stays, because removing it would break the contract for little gain.

### 3. Type identity across `AssemblyLoadContext`s

Current behavior: `PluginLoadContext.Load` returns the default context's copy of any assembly the default context can load. `UrDeck.Sdk` is in the host's `deps.json` (the host references Engine, which references Sdk), so it is on the TPA list and in the default context. That mostly works, but the real code breaks in these cases:

1. **Plugin built against a newer SDK** (it references `UrDeck.Sdk` 0.3.0.0 and the host has 0.2.0.0). The default (TPA) binder refuses a higher requested version, the `FileLoadException` is caught, and the resolver or sibling fallback runs. If the author shipped `UrDeck.Sdk.dll` next to the plugin, a **second SDK copy loads into the plugin context**. Its widgets then do not implement the host's `IWidget`, and `LoadAssembly` skips them **without any log line** (`!typeof(IWidget).IsAssignableFrom(type)` → `continue`). Otherwise `GetTypes()` throws and only a generic warning is logged.
2. **Shared DLLs dropped into `plugins/`.** `LoadAll` treats every `*.dll` as a plugin, so a stray `UrDeck.Sdk.dll` or `SkiaSharp.dll` is loaded into its own collectible context by `LoadFromAssemblyPath` (which bypasses `Load`). A native `libSkiaSharp.dll` produces a "failed to load plugin" warning.
3. **Engine and host are reachable.** `UrDeck.Engine` is in the default context, so a plugin that references it binds fine, which crosses the license and stability boundary without anyone noticing.

Changes (PR 2 in `tasks.md`):

- `PluginLoadContext.Load`: a fixed **shared set** `{UrDeck.Sdk, SkiaSharp}` binds by simple name to the default context's already-loaded assembly, ignoring the requested version. It never falls through to private copies. A **denied set** `{UrDeck.Engine, UrDeck.Host}` returns null. Framework assemblies keep the current default-first behavior.
- `WidgetPluginLoader.LoadAll`: before shadow-copying, read `AssemblyName.GetAssemblyName(path)`. If the name is in the shared or denied set, skip the file with one `INFO` line ("provided by the host"). After `LoadFromAssemblyPath`, and before `GetTypes`, run a **reference check** on `GetReferencedAssemblies()` (metadata only):
  - `UrDeck.Sdk`: accept if `MinimumCompatibleVersion <= referenced (major.minor) <= host SDK version`, otherwise reject with `WARN "<plugin> targets UrDeck.Sdk X.Y; this host supports A.B–C.D"`.
  - `SkiaSharp`: accept if it is the same major as the host's and the minor is not higher than the host's. Otherwise reject.
  - `UrDeck.Engine` or `UrDeck.Host`: reject (`WARN "... references UrDeck.Engine; plugins may only use UrDeck.Sdk"`). This is subject to open question 1.
  - A rejected plugin's context is unloaded immediately and its shadow directory is queued for deletion (the existing `_pendingDeletes` path).
- `LoadAssembly`: when a type looks like a widget (it has `[Widget]` by attribute *name*) but is not assignable to `IWidget`, log a warning instead of skipping silently.
- **`WpfAssemblyCache`**: no change. It evicts entries whose load context is collectible. `UrDeck.Sdk` and SkiaSharp live in the default context and are never evicted, and stray copies are no longer loaded at all. The only edit is the `using` for `UrDeck.Engine.Diagnostics`.
- **Shadow copy**: unchanged. The plugin's `.deps.json` still lists `UrDeck.Sdk`, but the shared set short-circuits it before the resolver.
- Engine rule (documented in `AGENTS.md`): engine services handed to plugins (`IWidgetHost` now, the event bus later) must not keep plugin types in static or long-lived caches. Subscriptions are owned per plugin context and dropped on unload.

*Rejected:* the host checking the SDK version only through a marketplace manifest. The DLL's reference is the one source of truth that cannot drift, and manually installed plugins have no manifest.

### 4. Namespaces, assemblies, package ids

- **Namespaces move** from `UrDeck.Core.*` to `UrDeck.Sdk` / `UrDeck.Engine.*`. Keeping `UrDeck.Core.*` inside assemblies with different names would mislead people for the lifetime of the SDK. With no third-party plugins, changing it now costs nothing.
- **Flatten the SDK** to a single `UrDeck.Sdk` namespace for the widget contract, so a widget needs `using UrDeck.Sdk;` plus `using SkiaSharp;`. The `Attributes`/`Enums`/`Interfaces` "namespace by kind" layout goes away. Future feature areas get sub-namespaces: `UrDeck.Sdk.Theming`, `UrDeck.Sdk.Events`, `UrDeck.Sdk.Providers`, `UrDeck.Sdk.Input`, `UrDeck.Sdk.Displays`.
- **Engine namespaces**: `UrDeck.Engine.Plugins`, `.Config`, `.Layout`, `.Rendering`, `.Diagnostics`.
- **Assemblies**: `UrDeck.Sdk.dll`, `UrDeck.Engine.dll`, and `UrDeck.Analyzer.dll`, whose name is unchanged (it is not a package, and its diagnostic ids stay URDECK001-005).
- **Packages**: only `UrDeck.Sdk` is packable (and it contains the analyzer). `UrDeck.Engine`, `UrDeck.Host`, the widgets and the tests set `IsPackable=false`. The analyzer project also gets `IsPackable=false`, so there is no separate analyzer package.
- **Analyzer metadata names** change in `WidgetAnalyzer.cs`: `WidgetBaseMetadataName = "UrDeck.Sdk.Widget\`1"` and `AttrNs = "UrDeck.Sdk."`. If this is missed, `GetTypeByMetadataName` returns null and the analyzer **silently disables itself**. The analyzer tests catch this, because the URDECK001-005 cases would stop reporting.
- **Target frameworks**: `UrDeck.Sdk` and `UrDeck.Engine` target `$(UrDeckTfm)` (`net10.0`). The engine has no Windows dependency, which keeps headless rendering and USB display backends possible. The analyzer targets `netstandard2.0`; the host and tests target `$(UrDeckWindowsTfm)`. *Policy:* the SDK targets the oldest .NET the current host supports. Raising the SDK's TFM is a breaking SDK release. No `netstandard2.0` build of the SDK: plugins only ever run inside the `net10.0` host.

### 5. SkiaSharp policy

The SDK exposes SkiaSharp types, so SkiaSharp's major version is part of the SDK contract.

- `UrDeck.Sdk` declares SkiaSharp as a **range** `[4.153.0, 5.0.0)` through `VersionOverride="$(UrDeckSdkSkiaSharpRange)"` (property in `sdk/Directory.Build.props`). It is therefore compiled against the floor, and the nuspec carries the range.
- The host and engine use the central `SkiaSharp` version in `Directory.Packages.props` and may take minor and patch updates freely (Dependabot). The host must never ship a SkiaSharp lower than the SDK floor. The build enforces this, because NuGet resolves the host's graph to at least the floor.
- A SkiaSharp **major** update is an SDK breaking release (a 0.x minor with `MinimumCompatibleVersion` raised, or a major after 1.0). `.github/dependabot.yml` gets an `ignore` rule for `SkiaSharp*` `version-update:semver-major`, so that decision is taken on purpose.
- At runtime, SkiaSharp binds version-agnostically to the host copy (decision 3), and the reference check rejects plugins compiled against a higher SkiaSharp minor or a different major.
- Plugins must not reference `SkiaSharp.Views.*` or any other windowing package. The SDK does not expose them.

### 6. Project files

**First-party widget after the split** (`widgets/UrDeck.Widgets.Clock/UrDeck.Widgets.Clock.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$(UrDeckTfm)</TargetFramework>
  </PropertyGroup>
</Project>
```

and a new `widgets/Directory.Build.props` supplies the references for every first-party widget:

```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
  <ItemGroup>
    <!-- UrDeck.Sdk and SkiaSharp come from the host at runtime; never copy them next to the plugin. -->
    <ProjectReference Include="$(MSBuildThisFileDirectory)../sdk/UrDeck.Sdk/UrDeck.Sdk.csproj" Private="false" />
    <ProjectReference Include="$(MSBuildThisFileDirectory)../sdk/UrDeck.Analyzer/UrDeck.Analyzer.csproj"
                      ReferenceOutputAssembly="false" OutputItemType="Analyzer" />
  </ItemGroup>
</Project>
```

**Third-party widget with the future package** (its own repository):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <!-- The lowest SDK version the widget needs; it is the widget's declared minimum. -->
    <PackageReference Include="UrDeck.Sdk" Version="0.1.0" />
  </ItemGroup>
</Project>
```

A class library does not copy package dependencies, so nothing besides the widget DLL lands in `bin/`. An author who needs private package dependencies sets `EnableDynamicLoading=true`, which copies everything. For that case the package ships `build/UrDeck.Sdk.targets` (also under `buildTransitive/`), which removes `UrDeck.Sdk` and `SkiaSharp*` from `ReferenceCopyLocalPaths`. It does nothing when `IsTestProject` is true or `UrDeckCopySdkAssemblies=true`, so widget unit-test projects still get the runtime DLLs. The analyzer arrives automatically from `analyzers/dotnet/cs`.

**Third-party widget without the package** (before item 13): it references a built `UrDeck.Sdk.dll` with `<Reference Include="UrDeck.Sdk" HintPath="..." Private="false" />` plus `<Analyzer Include=".../UrDeck.Analyzer.dll" />`. This is documented briefly in `CONTRIBUTING.md` as interim.

**Analyzer packaging** (`sdk/UrDeck.Sdk/UrDeck.Sdk.csproj`): a `ProjectReference` to the analyzer with `ReferenceOutputAssembly="false"` and `PrivateAssets="all"` (so it builds first and does **not** become a nuspec dependency), plus a target registered in `TargetsForTfmSpecificContentInPackage` that calls `GetTargetPath` on the analyzer and adds the DLL as `TfmSpecificPackageFile` with `PackagePath="analyzers/dotnet/cs"`. Other `UrDeck.Sdk.csproj` settings: `IsPackable=true`, `PackageId=UrDeck.Sdk`, `Version=$(UrDeckSdkVersion)`, `AssemblyVersion` computed as `Major.Minor.0.0`, `AssemblyMetadata UrDeck.Sdk.MinimumCompatibleVersion`, the PublicAPI files as `AdditionalFiles`, and `PackageReadmeFile` (a short `sdk/UrDeck.Sdk/README.md` for nuget.org).

**Host** (`src/UrDeck.Host/UrDeck.Host.csproj`): `ProjectReference` to `..\UrDeck.Engine\UrDeck.Engine.csproj` (the SDK comes transitively and lands in `deps.json`/TPA). The Clock copy step stays unchanged.

**Guard**: `sdk/Directory.Build.targets` fails the build if a project under `sdk/` has a `ProjectReference` that resolves outside `sdk/`. This keeps the MIT tree from depending on GPL code, and it is what makes a later `git subtree split sdk/` possible.

### 7. Directory layout and per-directory licensing

```
LICENSE                    GPL-3.0 (root default)
PLUGIN-EXCEPTION.md
Directory.Build.props      GPL defaults, shared TFMs
Directory.Packages.props   all package versions (SDK uses VersionOverride for its SkiaSharp range)
sdk/                       MIT: everything the SDK package or a widget author touches
  LICENSE                  MIT (moved from src/UrDeck.Analyzer/LICENSE)
  Directory.Build.props    imports root; PackageLicenseExpression=MIT, Copyright (c), UrDeckSdk* version properties
  Directory.Build.targets  guard: no ProjectReference outside sdk/
  .editorconfig            MIT SPDX header (nested, inherits root rules)
  UrDeck.Sdk/              + PublicAPI.Shipped.txt, PublicAPI.Unshipped.txt, README.md, build/UrDeck.Sdk.targets
  UrDeck.Analyzer/         moved from src/
  tests/
    UrDeck.Sdk.Tests/
    UrDeck.Analyzer.Tests/ moved from tests/
  (later) templates/       dotnet new widget template (item 13)
src/                       GPL
  UrDeck.Engine/
  UrDeck.Host/
widgets/                   GPL official widgets; Directory.Build.props wires SDK + analyzer
tests/                     GPL
  UrDeck.Engine.Tests/
```

- **SDK tests under `sdk/tests/`** (MIT), not `tests/`. This keeps the whole MIT tree self-contained for a later subtree split, and one directory means one license. *Rejected:* keeping all tests in `tests/` with a GPL header. Legally fine (tests are not shipped), but it splits the SDK across two license trees.
- **Nested `sdk/.editorconfig`** (no `root = true`) instead of a section in the root file:

  ```ini
  # Everything under sdk/ is MIT-licensed (see sdk/LICENSE).
  [*.cs]
  file_header_template = SPDX-License-Identifier: MIT\nCopyright (c) 2026 Patrick Bigler

  [tests/**.cs]
  dotnet_diagnostic.CA1707.severity = none
  dotnet_diagnostic.CA1861.severity = none
  dotnet_diagnostic.CA1816.severity = none
  ```

  The root `.editorconfig` drops the `[src/UrDeck.Analyzer/**.cs]` section and changes its header comment to "`sdk/` is MIT and overrides this in `sdk/.editorconfig`". The root `[tests/**/*.cs]` section stays for `tests/`. *Rejected:* a root `[sdk/**.cs]` section. It works too, but the rules would not travel with the directory.
- `src/UrDeck.Analyzer/UrDeck.Analyzer.csproj` loses its own `PackageLicenseExpression`/`Copyright` (they come from `sdk/Directory.Build.props`) and gains `IsPackable=false`.
- The existing MIT header text (`Copyright (c) 2026 Patrick Bigler`) is kept. Moved SDK files are rewritten from the GPL header to the MIT header by `dotnet format` (IDE0073), which is the actual relicensing act. The PR description must say so explicitly.

### 8. Wording changes in docs

**`PLUGIN-EXCEPTION.md`**: the opening paragraph is replaced by:

> Additional permission under section 7 of the GNU General Public License, version 3 ("GPLv3"), granted by the copyright holders of UrDeck for the UrDeck host, the engine (`UrDeck.Engine`) and the official widgets (together, the "Program").

The **Definitions** become:

> - **SDK Interface**: the public types, members and attributes of the `UrDeck.Sdk` assembly (including the `UrDeck.Analyzer` assembly shipped with it), in any version published by the copyright holders, together with the third-party types that `UrDeck.Sdk` exposes in its public API (such as SkiaSharp).
> - **Plugin**: a separate module (a .NET assembly, or a data package such as a theme) that the Program loads at run time through its plugin mechanisms and that interacts with the Program only through the SDK Interface. A module that references `UrDeck.Engine` or `UrDeck.Host` is not a Plugin.

In **Limits**, the second bullet ends with: "...unless it is code that is separately licensed under the MIT License, namely everything under the `sdk/` directory, including the `UrDeck.Sdk` and `UrDeck.Analyzer` assemblies." The Permission and Removal sections are unchanged. The interim "until `UrDeck.Sdk` exists" clause is gone. The data-package wording and the Engine sentence depend on open questions 1 and 2.

**`README.md`**:

- Project layout table: replace the `src/UrDeck.Core` row with
  `| sdk/UrDeck.Sdk | Widget SDK (net10.0, MIT, NuGet UrDeck.Sdk): attributes, Widget<TConfig>, IWidget, WidgetConfig, render context, theme colors, host services |`
  and `| src/UrDeck.Engine | Engine (net10.0): plugin loader, config store, grid layout, page renderer, logging |`.
- The analyzer row path becomes `sdk/UrDeck.Analyzer` ("bundled in the UrDeck.Sdk package"), and the tests row becomes `tests/UrDeck.Engine.Tests, sdk/tests/UrDeck.Sdk.Tests, sdk/tests/UrDeck.Analyzer.Tests`.
- License map:

  | Path | License |
  |---|---|
  | `sdk/` (`UrDeck.Sdk`, `UrDeck.Analyzer`, their tests and templates) | [MIT](sdk/LICENSE) |
  | `src/` (`UrDeck.Engine`, `UrDeck.Host`), `widgets/`, `tests/` | [GPL-3.0-or-later](LICENSE) with the [plugin exception](PLUGIN-EXCEPTION.md) |
  | Community widgets | The author's choice |

  The following paragraph becomes: "Widgets reference only `UrDeck.Sdk`, which is MIT-licensed; the plugin exception lets them be loaded into the GPL host under any license, including proprietary ones. Code samples in the docs are MIT." The `dotnet test` comment becomes "engine, SDK and analyzer tests". The Status paragraph says "widget SDK (`UrDeck.Sdk`, MIT) and Roslyn analyzer".

**`CONTRIBUTING.md`**:

- Licensing, first bullet: "The default is GPL-3.0-or-later with the plugin exception; everything under `sdk/` (`UrDeck.Sdk`, `UrDeck.Analyzer`, their tests) is MIT." Second bullet: "...`sdk/` gets the MIT header from `sdk/.editorconfig`." New bullet: "Widgets and other plugins reference only `UrDeck.Sdk`. Code under `sdk/` must never reference `src/` (the build fails if it does)."
- Code standards: "Tests: xUnit in `tests/` (engine) and `sdk/tests/` (SDK, analyzer). Line-coverage floors: `UrDeck.Engine` 65%, `UrDeck.Sdk` 80% (enforced in CI; raise them when coverage improves, never lower them to make a PR pass). Public SDK API changes must update `sdk/UrDeck.Sdk/PublicAPI.Unshipped.txt`."
- Writing a widget: "...in a class library that references `UrDeck.Sdk` (the NuGet package once published, otherwise the project)". The sample `using`s become `using SkiaSharp;` and `using UrDeck.Sdk;`. The project-file sample becomes the first-party csproj from decision 6, with a sentence about `widgets/Directory.Build.props` and a second sample with `<PackageReference Include="UrDeck.Sdk" Version="0.1.0" />` for third-party widgets.
- Review checklist, new item: "SDK API changes recorded in `PublicAPI.Unshipped.txt`; breaking changes called out and `MinimumCompatibleVersion` raised."

**`AGENTS.md`**:

- Commands: replace the coverage line with
  `dotnet test tests/UrDeck.Engine.Tests -c Release -p:CollectCoverage=true   # UrDeck.Engine line-coverage floor`
  `dotnet test sdk/tests/UrDeck.Sdk.Tests -c Release -p:CollectCoverage=true  # UrDeck.Sdk line-coverage floor`
  `dotnet pack sdk/UrDeck.Sdk -c Release -o artifacts/packages          # SDK package with the analyzer`.
- Layout block:
  `sdk/UrDeck.Sdk       MIT widget SDK: attributes, Widget<T>, WidgetConfig, render context, host services (NuGet UrDeck.Sdk)`
  `sdk/UrDeck.Analyzer  Roslyn analyzer (URDECK001-005), netstandard2.0, packed inside UrDeck.Sdk`
  `sdk/tests           SDK and analyzer tests (MIT)`
  `src/UrDeck.Engine    GPL engine: plugin loader, config store, grid, PageRenderer, logging`
  `src/UrDeck.Host      WPF app`.
- A gotcha bullet: "License boundary = assembly boundary: widgets reference only `UrDeck.Sdk`; nothing under `sdk/` references `src/`. The plugin loader always binds `UrDeck.Sdk` and SkiaSharp to the host's copies and rejects plugins built against a newer SDK or referencing `UrDeck.Engine`."

**`docs/ROADMAP.md`**:

- Item 1 layout block: replace with the tree from decision 7 (short form).
- Coverage bullet: "...floors: `UrDeck.Engine` 65% line, `UrDeck.Sdk` 80% line".
- Licenses bullet: delete "Interim state: `UrDeck.Core` is GPL until the SDK/Engine split below, so only the analyzer is MIT today."
- SDK/Engine split item: tick it and replace the text with "done (`openspec/changes/sdk-engine-split`): `sdk/` (MIT: `UrDeck.Sdk` + analyzer), `src/UrDeck.Engine` (GPL)".
- Item 3: "an in-process event bus: contract in `UrDeck.Sdk` (`UrDeck.Sdk.Events`, reached through `IWidgetHost`), implementation in `UrDeck.Engine`."
- Item 13: "Publish `UrDeck.Sdk` (already packable, analyzer bundled, public API tracked) ...; manifest `sdk` range must match the SDK version the DLL references (the loader enforces the same rule)."
- Current state: add the split.

Other OpenSpec changes (edited in PR 1, because they are active documents): in `theme-engine/design.md` decisions 1-2, "embedded in `UrDeck.Core`" becomes "loaded by `UrDeck.Engine`", and "lives in `UrDeck.Core`" becomes "the types widgets see live in `UrDeck.Sdk` (`UrDeck.Sdk.Theming`)". The Plugin ABI risk says `UrDeck.Sdk`. In `theme-engine/proposal.md` Impact, `UrDeck.Core` becomes `UrDeck.Sdk` (model, primitives, render context) + `UrDeck.Engine` (loader). In `display-targeting/proposal.md`, `UrDeck.Core` (`UrDeckConfig`) becomes `UrDeck.Engine`. `urdeck-framework` is historical: it is not rewritten, and it is reconciled on archive (see proposal).

### 9. Tests, coverage floors and CI

| Today | After | License |
|---|---|---|
| `tests/UrDeck.Core.Tests/ConfigTests.cs` `WidgetConfigTests` | `sdk/tests/UrDeck.Sdk.Tests/WidgetConfigTests.cs` | MIT |
| `ConfigTests.cs` `ConfigStoreTests` | `tests/UrDeck.Engine.Tests/ConfigStoreTests.cs` | GPL |
| `GridLayoutTests.cs`, `PluginLoaderTests.cs` | `tests/UrDeck.Engine.Tests/` | GPL |
| `tests/UrDeck.Analyzer.Tests/` | `sdk/tests/UrDeck.Analyzer.Tests/` (references `UrDeck.Sdk` instead of Core) | MIT |
| — | `sdk/tests/UrDeck.Sdk.Tests/WidgetTests.cs`: attribute-derived metadata and precedence (`[Category]` over `Widget.Category` over "General"), `SupportedSizes`, `Configure` with the wrong type throws, `OnConfigured` called, default `UpdateAsync`, `ThemeColors.DefaultDark` values, render context construction; PR 3 adds: default `Host` is a no-op, `Attach` stores the host | MIT |
| — | `tests/UrDeck.Engine.Tests/WidgetConfigConverterTests.cs` (`ToConcrete`: extension data maps to typed properties, missing properties keep defaults, non-`WidgetConfig` type throws) | GPL |
| — | Loader boundary tests (PR 2): stray `UrDeck.Sdk.dll`/`SkiaSharp.dll` in `plugins/` skipped; plugin with a private SDK copy next to it still binds to the host's `IWidget`; plugin referencing SDK 99.0 rejected with a warning; plugin referencing `UrDeck.Engine` rejected; `Attach` called before `Configure` | GPL |

The version and engine-reference tests compile a stub assembly named `UrDeck.Sdk` or `UrDeck.Engine` with Roslyn (as `PluginLoaderTests.BuildPlugin` already does) and reference it from the test plugin, so `GetReferencedAssemblies()` carries the fake version.

Coverage floors (coverlet.msbuild, `Include` per assembly, `ThresholdType=line`):

- `tests/UrDeck.Engine.Tests`: `[UrDeck.Engine]*`, **65%** (inherits the Core floor; today's engine lines are about 73-76% covered). Raise it to the measured value rounded down to a multiple of 5 once PR 2's tests land.
- `sdk/tests/UrDeck.Sdk.Tests`: `[UrDeck.Sdk]*`, **80%**. The SDK is small and pure; only its own tests count, not indirect engine coverage.
- `CoverletOutput`: `artifacts/coverage/engine/` and `artifacts/coverage/sdk/` (the CI artifact upload already takes `artifacts/coverage/`).

CI (`.github/workflows/ci.yml`, `build` job). Format, Build, Test and OpenSpec steps are unchanged (they run on `urdeck.slnx`). Replace "Coverage floor (UrDeck.Core)" with:

```yaml
      - name: Coverage floor (UrDeck.Engine)
        run: dotnet test tests/UrDeck.Engine.Tests -c Release --no-build -p:CollectCoverage=true

      - name: Coverage floor (UrDeck.Sdk)
        run: dotnet test sdk/tests/UrDeck.Sdk.Tests -c Release --no-build -p:CollectCoverage=true

      - name: Pack SDK
        run: dotnet pack sdk/UrDeck.Sdk/UrDeck.Sdk.csproj -c Release --no-build -o artifacts/packages

      - name: Check SDK package
        shell: pwsh
        run: |
          # inline: open the .nupkg (zip) and assert lib/net10.0/UrDeck.Sdk.dll,
          # analyzers/dotnet/cs/UrDeck.Analyzer.dll, build/UrDeck.Sdk.targets, license MIT,
          # a SkiaSharp dependency range, and no dependency on UrDeck.Engine or UrDeck.Analyzer
```

The `.nupkg` is uploaded as a CI artifact (`artifacts/packages/`), which also gives testers a package before item 13. The required check names (`build`, `pr-title`) do not change, so the branch ruleset needs no edit.

`urdeck.slnx` folders: `/sdk/` (Sdk, Analyzer), `/sdk/tests/` (Sdk.Tests, Analyzer.Tests), `/src/` (Engine, Host), `/widgets/`, `/tests/` (Engine.Tests).

### 10. SDK versioning and public API tracking

- **SemVer on the package.** `0.x` until the SDK is declared stable. In `0.x` a minor release may break (it must say so in the release notes and raise `MinimumCompatibleVersion`), and a patch never changes the public API. From `1.0`, standard SemVer: breaking only in a major. The first version is **0.1.0**; nothing has been published, and 1.0 waits until the theme, event and input APIs have settled.
- **Assembly identity.** `AssemblyVersion = Major.Minor.0.0` (like SkiaSharp), so patch releases never change binding. `FileVersion`/`InformationalVersion` carry the full version.
- **Compatibility window in the assembly.** `[assembly: AssemblyMetadata("UrDeck.Sdk.MinimumCompatibleVersion", "0.1")]`, generated from `$(UrDeckSdkMinimumCompatibleVersion)`. The engine accepts a plugin whose referenced `UrDeck.Sdk` version `v` satisfies `MinimumCompatibleVersion <= v <= host SDK version` (major.minor). This one rule works for both 0.x and ≥1.0 and adds no public type.
- **How a plugin declares its target.** Implicitly and authoritatively, through the `UrDeck.Sdk` version it compiled against (its `PackageReference` minimum). The future marketplace manifest (item 13) carries `"sdk": ">=0.3.0 <1.0.0"` for the index and picker. Registry CI reads the DLL's `UrDeck.Sdk` reference and rejects a manifest whose lower bound differs from it. No attribute is needed in the plugin.
- **Public API tracking: adopt `Microsoft.CodeAnalysis.PublicApiAnalyzers` on `UrDeck.Sdk` only.** Add it to `Directory.Packages.props` with the same release train as `Microsoft.CodeAnalysis.Analyzers` (3.11.0) and use `PrivateAssets=all`. `PublicAPI.Shipped.txt` starts empty and `PublicAPI.Unshipped.txt` holds the 0.1.0 surface, generated with the RS0016 code fix and including nullable annotations (`#nullable enable`). RS0016/RS0017 are warnings, so they are errors in this repo: every public change shows up as a diff in review. At release, Unshipped moves to Shipped. The engine does not use it, because it has no compatibility promise. The analyzer already tracks its rules through `AnalyzerReleases.*.md`.
- **Binary compatibility check: add package validation after 0.1.0 is published.** Set `EnablePackageValidation=true` now (it validates the package on its own), and `PackageValidationBaselineVersion` to the last published version from the first release on. That catches binary breaks that PublicAPI files do not show clearly (for example, a sealed class or a changed default value). An intentional 0.x break goes into `CompatibilitySuppressions.xml` together with raising `MinimumCompatibleVersion`.
- **Interface evolution rule.** Members added to SDK interfaces that plugins implement (`IWidget`) are default interface members. Interfaces that only the engine implements (`IWidgetHost`, `IWidgetLog`, future `IEventBus`) are documented as "not for implementation by plugins" and may gain members in a minor release.

### 11. Forward compatibility: where the planned features land

Only seams and namespaces are fixed here; the features are designed in their own changes.

| Feature | `UrDeck.Sdk` (MIT) | `UrDeck.Engine` (GPL) | `UrDeck.Host` |
|---|---|---|---|
| Theme engine (item 5) | `UrDeck.Sdk.Theming`: theme model and the style types widgets call; `WidgetRenderContext.Style` as an `init` property (constructor unchanged); `ThemeColors` stays | Theme loader, built-in theme registry, `themes/` watcher, per-theme caches | Window background, selection on config reload |
| Event bus and `[RefreshOnEvent]` (item 3) | `UrDeck.Sdk.Events`: publish/subscribe contracts reached through `IWidgetHost` (for example `Host.Events`); `[RefreshOnEvent]` unchanged | Bus implementation; subscriptions owned per plugin context and dropped on unload | Marshal invalidation to the UI thread |
| Data providers (items 3, 7) | `UrDeck.Sdk.Providers`: provider contract and attribute (a second plugin kind) | Loader generalized from "widget types" to plugin kinds (internal refactor of `WidgetPluginLoader`), provider lifecycle | — |
| Touch/input (item 8) | `UrDeck.Sdk.Input`: an optional interface a widget implements, plus pointer/gesture records in widget-local pixels; no WPF types | Hit-testing through the layout, routing to the widget | Translate WPF touch/manipulation events |
| Display backends (item 13) | `UrDeck.Sdk.Displays`: backend contract (device enumeration, frame push as SkiaSharp pixmap/bytes) | Frame scheduling with `PageRenderer`; backend loading uses the same plugin loader (native DLLs already resolve through `LoadUnmanagedDll`) | Picker UI |
| Theme packs | JSON schema published under `sdk/` (MIT) together with the `UrDeck.Sdk.Theming` model | Loads packs from `themes/` | — |
| Marketplace (item 13) | Versioning rules above; template under `sdk/templates/` | Same reference check for manifest-installed plugins | — |

Invariants that keep these from forcing another split: the SDK stays `net10.0` with no Windows or WPF types; nothing in `sdk/` references `src/`; engine services reach plugins only through SDK interfaces; new plugin kinds are new SDK namespaces plus engine registrars, not new assemblies. A second SDK assembly (for example `UrDeck.Sdk.Displays`) is only justified if a plugin kind needs a heavy dependency that widgets must not inherit. That is not the case for anything planned.

## Migration plan (summary)

Three PRs, each green on its own (details and verification in `tasks.md`):

1. `refactor: split UrDeck.Core into UrDeck.Sdk (MIT) and UrDeck.Engine (GPL)`: layout, moves, namespaces, surface trims, test split, license files, CI coverage steps, docs. Mostly mechanical; one atomic PR, because a half-split does not build.
2. `feat(engine): enforce the SDK boundary in the plugin loader`: shared/denied assembly sets, stray-DLL skip, reference and version check, tests. Needs judgment.
3. `feat(sdk): make UrDeck.Sdk packable with the analyzer, public API baseline and host services`: `IWidgetHost`/`IWidgetLog`/`Attach`, packaging, PublicAPI files, version properties, package validation, CI pack step, SkiaSharp range and Dependabot rule. Needs judgment.

PR 2 and PR 3 are independent of each other after PR 1. The theme engine should start after PR 3, so its API lands in `PublicAPI.Unshipped.txt` from its first commit.

## Risks / Trade-offs

- **Analyzer silently disabled** if its metadata names are not updated. It is caught by the analyzer tests; PR 1 also greps for `UrDeck.Core` across the repo, which must return nothing outside archived docs.
- **Stale outputs.** Incremental builds leave `UrDeck.Core.dll` in the host's `bin/`, and developers may have old plugins in `plugins/`. `UrDeck.Core` is no longer in the host's `deps.json`, so a stale plugin fails to resolve it and is rejected with a warning, while the host itself is unaffected. Verification uses a clean build (`git clean -xdf` of `bin`/`obj`, or a fresh clone).
- **Relicensing** is valid only because the owner is the sole human author of the moved code. Once outside contributions arrive, moving code from `src/` into `sdk/` requires the contributors' agreement. `CONTRIBUTING.md` says so.
- **Version-agnostic binding** of SkiaSharp and the SDK could hide an incompatible plugin. That is why the reference check runs before `GetTypes()`. A plugin that passes the check but uses an API removed in a later minor fails at call time and is contained by `WidgetPainter`'s error tile and `UpdateAsync` try/catch, as today.
- **Default interface members** on `IWidget` are invisible to authors who implement `IWidget` directly. That is acceptable, since `Widget<TConfig>` is the documented path.
- **`sdk/tests` vs `tests/`**: two test roots. This is accepted for the license tree, and all test projects are in `urdeck.slnx`, so `dotnet test urdeck.slnx` still runs everything.

## Open questions for the owner

1. **Block plugins that reference `UrDeck.Engine`?** A GPL-licensed plugin could legally use engine internals. Blocking keeps the "plugins only use the SDK" rule simple, keeps the engine free to change, and matches the exception's definition of a Plugin. *Recommendation:* block it in the loader now. Open specific engine capabilities through the SDK when someone needs them.
2. **Plugin exception wording.** Should the exception name data packages (themes) and non-widget plugin kinds explicitly, and cover the third-party types the SDK exposes (SkiaSharp)? This changes a license text, so the owner should approve it (and optionally have it reviewed). *Recommendation:* adopt the wording in decision 8 in PR 1.
3. **Copyright line for the MIT tree.** Keep `Copyright (c) 2026 Patrick Bigler`, or switch `sdk/` to "Patrick Bigler and UrDeck contributors" before outside contributions arrive? *Recommendation:* keep the current line. Contributors add their own lines when they make substantial contributions (common MIT practice), and nothing needs to change now.
