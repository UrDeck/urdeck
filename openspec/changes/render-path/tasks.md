## 1. Gates on the panel (stop and report if one fails)

- [x] 1.1 Owner, with the spike app (`spike/RenderHostSpike` on the local branch `spike/render-host`, case `all`, presenter `comp`): put the PC to sleep and resume; the video, the cards and the web view are all shown again
- [x] 1.2 Owner, with the spike app: the Frigate page loads in the web view card (`--url`) and plays its camera feeds
- [x] 1.3 Owner, with the spike app: the looping icon looks smooth at 30 and at 60 frames per second; note which is preferred for the `animation` change
- [x] 1.4 Record the outcome of 1.1 to 1.3 in `docs/perf/render-host-spike.md`, with the owner's earlier confirmation that touch scrolling in the web view works

## 2. Project and build

- [x] 2.1 Add `Microsoft.WindowsAppSDK` 2.5.1 and `SkiaSharp.Views.WinUI` to `Directory.Packages.props`, remove `SkiaSharp.Views.WPF`, and move the SkiaSharp packages to 4.153.1
- [x] 2.2 Convert `src/UrDeck.Host/UrDeck.Host.csproj` to an unpackaged, self-contained WinUI 3 app (`UseWinUI`, `WindowsPackageType=None`, `WindowsAppSDKSelfContained`, the runtime identifier), keeping the target framework, the icon, the manifest's PerMonitorV2 awareness, the config copy and the built-in plugin copy target
- [x] 2.3 Add a minimal `App.xaml` and a hand-written `Main` with the generated one disabled; an empty window starts from `dotnet build urdeck.slnx -c Release` with 0 warnings
- [x] 2.4 Update the comment in `Directory.Build.props` about why the Windows SDK suffix is needed
- [x] 2.5 Push the branch early and confirm CI passes (build, tests, `dotnet format --verify-no-changes`, `openspec validate`); fix the workflow if WinUI 3 needs anything on `windows-latest`

## 3. Plugin unload gate (stop and report if it fails)

- [x] 3.1 In the WinUI 3 host, load plugins with the existing loader and draw one widget on an `SKXamlCanvas`
- [x] 3.2 Replace the Clock plugin DLL while the host runs several times and confirm the old load contexts are collected (the same check that led to `WpfAssemblyCache`); delete `WpfAssemblyCache.cs`

## 4. Window

- [x] 4.1 Borderless window through `AppWindow` and an `OverlappedPresenter`, not resizable or movable, normal Z-order, closes on Escape
- [x] 4.2 Place the window in physical pixels and enlarge it by the measured non-client inset so the content area equals the target monitor's bounds; keep `MonitorPlacement`'s enumeration and selection
- [x] 4.3 Log the placement as today, comparing the content area with the target and warning on a mismatch
- [x] 4.4 Carry over the retarget logic for display settings changes and resume (at once, then after 1.5 s and 5 s), and force a page rebuild after a resume or a device reset
- [x] 4.5 Rebuild the page when the scale changes, coalesced with size and theme changes as today

## 5. Page and widget surfaces

- [x] 5.1 Build the layer stack: a background layer filled with the theme's background colour and a widget `Canvas` above it
- [x] 5.2 Port `WidgetView` to an `SKXamlCanvas` whose paint handler calls `WidgetPainter.Paint`, rendered at the monitor's physical resolution, with the per-widget `DispatcherQueueTimer`, the overlap guard and the `NeedsRender` check
- [x] 5.3 Port `MainWindow`'s page rebuild: theme resolution against the physical cell size, grid layout, widget creation, view disposal, the reaction to `ConfigChanged` and `PluginsChanged`
- [x] 5.4 Confirm a translucent card fill and the rounded corners show the background layer (set a partly transparent `CardFill` in a test theme)
- [x] 5.5 Confirm the error card still appears when a widget throws in `Render`

## 6. Entry point

- [x] 6.1 Handle `--snapshot` in `Main` with the engine alone and exit before the XAML application starts; the PNG matches the one from `main` for the same config and theme
- [x] 6.2 Carry over the startup sequence, the unhandled exception logging, `URDECK_MEMLOG`, and saving the config on exit; remove `RenderMode.SoftwareOnly` and `URDECK_HWRENDER`

## 7. Verify on the panel

- [x] 7.1 Owner: the Clock page looks the same as on the WPF host, with no border inside the panel's edges
- [x] 7.2 `urdeck.log` shows the content area equal to the target bounds and no warnings
- [x] 7.3 Owner: sleep and resume, and unplugging and replugging a monitor, bring the page back on the panel
- [x] 7.4 Editing `urdeck-config.json` (a widget setting, the theme, `monitorName`) is applied without a restart
- [x] 7.5 Measure the host with the method in `docs/perf/nexus-baseline.md` (one Clock, idle): CPU, GPU, private bytes, working set, video memory; confirm no widget is repainted between minute changes; write the result to `docs/perf/render-host-baseline.md`

## 8. Documentation and wrap-up

- [x] 8.1 Update `README.md` (what the host is, the project table, the target framework note) and the license map's third-party entries for the Windows App SDK
- [x] 8.2 Replace the "Windows / WPF gotchas" in `AGENTS.md` with what applies to the WinUI 3 host (keep the capture method and the detached launch if they still hold)
- [x] 8.3 Update `CONTRIBUTING.md`'s host baseline to the measured figure and the Nexus bar
- [x] 8.4 Update `docs/ROADMAP.md`: current state, item 4 and item 14
- [x] 8.5 Edit the purpose line of `openspec/specs/host-shell/spec.md` so it no longer says "WPF host application"
- [x] 8.6 `dotnet build`, `dotnet test`, the coverage run, `dotnet format --verify-no-changes` and `openspec validate --all --strict` all pass
