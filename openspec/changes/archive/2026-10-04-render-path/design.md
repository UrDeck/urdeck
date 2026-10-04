## Context

See `proposal.md` for the motivation. The evidence is in `docs/perf/render-host-spike.md` (the WinUI 3 spike on the
Y70), `docs/perf/nexus-baseline.md` (the cost bar) and `docs/handoff/2026-10-04-render-host-spike.md` (the decisions of
the exploration). The spike's throwaway code is on the local branch `spike/render-host` (`spike/RenderHostSpike/`); it
is a reference for the csproj settings and window calls, not code to merge.

Today's host (`src/UrDeck.Host`, five files) is a WPF `Window` with a `Canvas` of `WidgetView` elements, each an
`SKElement` that rasterizes into a `WriteableBitmap`; WPF composes them in software (`RenderMode.SoftwareOnly`). All
drawing goes through `WidgetPainter.Paint` in the engine, which `--snapshot` shares. Widgets, the SDK and the engine
know nothing about WPF.

What the spike established, on the panel:

- An unpackaged, self-contained WinUI 3 app builds and runs from `dotnet build`, with no Visual Studio.
- `SKXamlCanvas` (Skia on the CPU, composited by WinUI on the GPU) is transparent over an image, a video and a web
  view. A still page painted each card once and used no GPU.
- `SKSwapChainPanel` (Skia on the GPU) rendered opaque; the cause was not established.
- The window rectangle matched the monitor, but the content area was 3 pixels short on every side until the window was
  placed 3 pixels outside the bounds.
- The owner scrolled the web view by touch and judged it good.

Not established: sleep and resume, monitor hot-plug, mixed-DPI placement, and plugins in a collectible
`AssemblyLoadContext` under WinUI 3.

## Goals / Non-Goals

**Goals:**

- The same page as today (the Clock on its themed card) on a WinUI 3 host, with every existing `host-shell` behaviour
  kept.
- A layer model that later changes extend without touching this code's structure: a background layer behind the
  widgets, hosted layers (a web view) among them, and an animation frame clock.
- All framework code inside `src/UrDeck.Host`, so a later framework change costs a host rewrite and nothing else.
- A measured new baseline in `docs/perf/`.

**Non-Goals:**

- Anything that makes the page look different: backgrounds, animation, new widgets, touch input, pages.
- Drawing widgets with Skia on the GPU.
- Changes to the SDK, the engine, the plugin contract or `--snapshot` output.
- A raw Win32 and DirectComposition host. It stays the fallback if a gate in the task list fails.

## Decisions

### 1. WinUI 3 as the host framework

WinUI 3 (Windows App SDK 2.5.1, the stable release the spike used), unpackaged (`WindowsPackageType=None`) and
self-contained (`WindowsAppSDKSelfContained=true`), so the host needs no installer and no machine-wide runtime.

Alternatives considered:

- **Raw Win32 with DirectComposition.** The most stable layer and the smallest footprint, but the web view layering,
  the video layer and the gesture handling would all be hand-built, and the editor would need a second framework. Kept
  as the fallback.
- **WPF with hardware composition, or `SKGLElement`.** Costs the 62 MB measured in `memory-investigation.md` and still
  cannot layer WPF content over GPU-drawn content cleanly.
- **Avalonia.** Not evaluated; version 12 lists SkiaSharp 3.119 as its minimum and the repo is on 4.153.

The churn risk of Microsoft UI frameworks is accepted, and bounded by keeping the host thin (decision 3).

### 2. One `SKXamlCanvas` per widget; Skia stays on the CPU

Each widget gets an `SKXamlCanvas` placed on a XAML `Canvas` at its card rectangle, the direct successor of today's
`SKElement`. Its paint handler calls `WidgetPainter.Paint` unchanged. WinUI composites the canvases on the GPU, and a
canvas that is not invalidated costs nothing.

`SKSwapChainPanel` is not used: it rendered opaque in the spike, and CPU rasterizing is not the bottleneck for cards
that repaint once a minute. Whether animated content needs a GPU-backed or smaller surface is a question for the
`animation` change, which has the spike's numbers (3.4% of one core for a full-card repaint at 30 frames per second).

Alternative considered: one surface for the whole page. Rejected because every repaint would redraw and re-upload the
full 1100x3840 surface, and because separate layers are what backgrounds, page swipes and hosted layers need.

### 3. The host stays a thin shell, rewritten in place

`src/UrDeck.Host` keeps its name, assembly name (`UrDeck.Host.exe`) and role; its WPF files are replaced. No
WinUI type appears outside this project. The page is built as an ordered stack inside the window's root:

```
 root Grid
 ├─ background layer   (a solid theme colour in this change; item 9 puts an image or video here)
 └─ widget Canvas      (one SKXamlCanvas per widget; a later change adds hosted layers such as a web view)
```

The stack exists from the start so that later changes add children to it and do not restructure the window.

Alternative considered: a new project next to the WPF host, switching over later. Rejected: two hosts to keep building
for a product with no users yet; `git revert` is the rollback.

### 4. Window placement: cover with the content area

The window is borderless through `AppWindow` and an `OverlappedPresenter` without border and title bar, and placed in
physical pixels. Because the window keeps a thin non-client frame, the host measures the difference between the window
rectangle and the client rectangle after placement and enlarges the window by that inset, so the content area equals
the monitor's bounds. The inset is measured, not hard-coded to the 3 pixels the spike saw. The existing
`MonitorPlacement` monitor enumeration and selection logic is kept; only the code that moves the window changes.

The warning for a bounds mismatch now compares the content area with the target.

### 5. Display changes, sleep and device loss

The retarget logic is carried over as it is: on a display settings change or a resume, re-select and re-cover at once,
then again after 1.5 and 5 seconds. Scaling changes are observed through the XAML root and trigger the same coalesced
page rebuild as today (window size, scale and theme form the rebuild key).

After a resume or a device reset the host forces a page rebuild, which recreates every surface. This is the simple
answer to device loss and is verified on the panel as a gate.

### 6. Timers and threading

A `DispatcherQueueTimer` per widget replaces the `DispatcherTimer`, with the same behaviour: await `UpdateAsync`, ask
`NeedsRender`, invalidate the canvas. No frame clock is added; that belongs to `animation`.

### 7. `--snapshot` runs before the UI framework starts

The entry point is a hand-written `Main` (the generated one is disabled). It handles `--snapshot` with the engine alone
and exits, without starting the XAML application; otherwise it starts the application through `App.xaml`. The spike
found that building the application resources in code crashes, so `App.xaml` stays.

### 8. Plugins

The loader is untouched. `WpfAssemblyCache` is deleted because the caches it evicts are WPF's. Whether WinUI pins
plugin assemblies is unknown, so unloading is verified early (replace the Clock plugin while running, confirm the old
context is collected) before the rest of the host is built on it. Widget types are never used as XAML types, which
keeps them out of the XAML type caches by construction.

### 9. Packages

`Microsoft.WindowsAppSDK` 2.5.1 and `SkiaSharp.Views.WinUI` are added and `SkiaSharp.Views.WPF` is removed. SkiaSharp
moves from 4.153.0 to 4.153.1 everywhere, the combination the spike ran. `UseWPF` is replaced by `UseWinUI`; the target
framework stays `$(UrDeckWindowsTfm)`. `SkiaSharp.Views.WinUI` declares a dependency on Windows App SDK 1.4 and ran
against 2.5.1 in the spike.

### 10. What is dropped

`RenderMode.SoftwareOnly` and `URDECK_HWRENDER` (there is no software composition to opt out of). `URDECK_MEMLOG`
stays.

## Risks / Trade-offs

- **Sleep, resume or hot-plug leaves the page blank or misplaced** → It is the first gate in the task list, tested on
  the panel before the remaining work. If it cannot be made reliable, stop and reconsider the fallback host.
- **WinUI pins plugin assemblies and hot-reload leaks** → Verified as the second gate. If it leaks, look for the cache
  as was done for WPF; if that fails, stop.
- **Higher memory floor** (94 MB private for an empty window against 61 MB) → Accepted by the owner against the Nexus
  bar; the new baseline is recorded so later changes are measured against it.
- **GPU use next to a game** → A still page uses no GPU. Contention only matters once something animates, so it is
  measured in the `animation` change.
- **No hardware GPU** (remote desktop, a virtual machine) → Expected to fall back to the platform's software
  rasterizer; not verified. `--snapshot` does not depend on it.
- **Framework churn** → The host is the only project that references the framework.
- **CI** → Building WinUI 3 with warnings as errors and `dotnet format --verify-no-changes` on `windows-latest` is
  unproven; it is an early task so a problem shows before the host is finished.
- **Larger output folder** from the self-contained Windows App SDK → Accepted; packaging is item 12.

## Migration Plan

One pull request replaces the host. There is no data migration: `urdeck-config.json`, themes and plugins keep their
formats and locations. Rollback is reverting the squash commit.

## Open Questions

- Whether `SKSwapChainPanel` can be made transparent. It does not affect this change; it matters to `animation` if a
  full-card repaint at 30 frames per second turns out to be too expensive.
