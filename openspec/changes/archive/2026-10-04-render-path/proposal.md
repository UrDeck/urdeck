## Why

UrDeck's host composites its window in software through WPF. The platform goals agreed on 2026-10-04 (video and
painted backgrounds behind translucent cards, page swipes, a web page widget, continuous animation) all need layers
composited on the GPU, and none of the WPF routes provide that cleanly. A spike on the Y70 panel showed that a WinUI 3
host does: it layers Skia cards, a video and a web view correctly, costs nothing while the page is still, and a full
page stays well under the cost of HYTE Nexus (`docs/perf/render-host-spike.md`, `docs/perf/nexus-baseline.md`). The
host is five files today, so this is the cheapest moment to replace it, before more widgets and the animation change
are built on it.

## What Changes

- **BREAKING** (for the host only): `src/UrDeck.Host` is rebuilt on WinUI 3 (Windows App SDK), unpackaged and
  self-contained. The WPF application, its window, `WidgetView` and `WpfAssemblyCache` are removed.
- The window is composited by the GPU from independent layers: the window background and one Skia surface per widget.
  A page on which nothing changes produces no frames.
- Each widget keeps its own surface and is still drawn by Skia on the CPU through the same `WidgetPainter` path. The
  surface is transparent outside the card and honours a translucent card fill, so layers placed behind widgets in later
  changes show through.
- The window's content area covers the target monitor exactly, with no border inside the monitor's bounds, and the
  page comes back after sleep, resume and display changes without a restart.
- Software-only composition and its `URDECK_HWRENDER` opt-out are removed.
- `SkiaSharp.Views.WPF` is replaced by `SkiaSharp.Views.WinUI`; OpenTK and GLWpfControl leave the output.
- Behaviour that stays the same: monitor selection, refresh policies and render skipping, plugin discovery and
  hot-reload, configuration handling, theme application, the application icon, `urdeck.log`, and `--snapshot`.
- The cost bar is restated: no worse than Nexus on the same panel, and a still page costs close to nothing. The new
  baseline is measured and recorded, replacing the 60-70 MB figure written for the WPF host.

Not part of this change: animation (frame requests, an animation clock, the split-flap Clock), backgrounds, the web
page widget, pages and touch, the editor, and drawing widgets with Skia on the GPU. Each is its own later change; this
one gives them the host they need.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `host-shell`: the window and widget surface requirements stop prescribing WPF and software rasterizing into a WPF
  bitmap, and instead require a GPU-composited window built from per-widget layers that presents nothing while the page
  is still, an exact borderless cover of the monitor's content area, and recovery after sleep. Refresh scheduling keeps
  its behaviour but no longer names a WPF timer.

## Impact

- **Code:** `src/UrDeck.Host` is rewritten (`App`, `MainWindow`, `WidgetView`, `MonitorPlacement`; `WpfAssemblyCache`
  is deleted). `sdk/`, `src/UrDeck.Engine`, `widgets/` and `tests/` are not expected to change.
- **Dependencies:** adds `Microsoft.WindowsAppSDK` and `SkiaSharp.Views.WinUI`; removes `SkiaSharp.Views.WPF`; moves
  SkiaSharp to the patch level the spike ran on. The self-contained Windows App SDK makes the output folder larger.
- **Memory:** the empty-window floor rises (94 MB private measured in the spike against 61 MB for today's host). The
  owner accepted this against the Nexus bar.
- **Build and CI:** the host builds as an unpackaged WinUI 3 app from the `dotnet` CLI; CI on `windows-latest` must
  still pass with warnings as errors.
- **Docs:** `README.md`, `AGENTS.md` (the WPF gotchas), `CONTRIBUTING.md` (budgets), `docs/ROADMAP.md`, and the
  `host-shell` spec's purpose line.
- **Later changes:** `animation` builds its frame clock in this host; `display-targeting` is reworked against it.
