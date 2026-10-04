# Render host spike: WinUI 3 (2026-10-04)

Question: can WinUI 3 be UrDeck's GPU-composited host? Code: `spike/RenderHostSpike/` on branch `spike/render-host`
(throwaway, not in `urdeck.slnx`). Method for the numbers: `docs/perf/nexus-baseline.md` (process tree by parent pid,
CPU from `TotalProcessorTime`, GPU Engine and GPU Process Memory counters, 5 GPU samples over about 25 s after a 15 s
settle). Everything below is observed unless marked **(inference)**. Each row is one run, not a benchmark.

## Environment

- Windows 11 Pro 10.0.26200, 32 logical processors, NVIDIA GeForce RTX 4090. Panel: `\\.\DISPLAY1` 1100x3840 at
  3840,-3573 (tallest monitor, picked by enumeration), scale 1.0 (dpi 96). Other monitors: 3840x2160 (primary), 1080x1920.
- HYTE Nexus was not running (no Nexus process). Unrelated `msedgewebview2` processes from other apps were running; they
  are excluded by the parent-pid filter.
- .NET SDK 10.0.401, Windows App SDK 2.5.1 (stable), SkiaSharp 4.153.1, SkiaSharp.Views.WinUI 4.153.1,
  SkiaSharp.Skottie 4.153.1, WebView2 runtime 154.0.4258.53. No ffmpeg on PATH; video is a 10 s 360p H.264 clip
  (Big Buck Bunny, CC BY, test-videos.co.uk) looped. Lottie: Meteocons `partly-cloudy-day` (fill, MIT).

## What was built

One unpackaged, self-contained WinUI 3 app (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, built with
`dotnet build -c Release`, no Visual Studio). UI is built in code; XAML only for `App.xaml`. Switches: `--case`,
`--seconds`, `--log`, `--topmost`, `--surface canvas|swap`, `--clip wv|host|rect|xamlrect|border`,
`--presenter overlapped|comp|full`. Borderless window from `OverlappedPresenter.SetBorderAndTitleBar(false,false)`,
sized with `AppWindow.MoveAndResize(DisplayArea.OuterBounds)`.

## Results

CPU is percent of one core. GPU is the app's process tree, summed over engines of the type. VRAM is dedicated usage.
Private/working set are summed over the tree.

| Case | Procs | CPU | GPU 3D | GPU video decode | Private MB | Working set MB | VRAM MB |
|---|---|---|---|---|---|---|---|
| WPF host today (1 Clock, idle) | 1 | 0.2 | n/a (no GPU engine entries) | 0 | 61 | 112 | 0 (no entry) |
| `empty` | 1 | 0.3 | 0.0 | 0.0 | 94 | 118 | 44 |
| `static` (image + 2 cards, SKXamlCanvas) | 1 | 0.3 | 0.0 | 0.0 | 121 | 153 | 158 |
| `icon30` | 1 | 3.4 | 3.1 | 0.0 | 128 | 162 | 158 |
| `icon60` | 1 | 7.5 | 4.7 | 0.0 | 130 | 164 | 158 |
| `icon30`, `--surface swap` (SKSwapChainPanel) | 1 | 1.4 | 0.4 | 0.0 | 181 | 182 | 139 |
| `video` | 1 | 2.1 | 1.1 | 3.9 | 203 | 232 | 317 |
| `web` | 7 | 3.3 | 0.7 | 0.0 | 356 | 565 | 209 |
| `all` (video + icon30 + web) | 7 | 10.5 | 3.1 | 3.6 | 419 | 641 | 355 |
| Nexus, 2026-10-03 (owner's page) | 7 | about 20 | about 3 | about 3 | about 627 | n/a | 238 |

Notes on the table:
- `static`: the paint counter showed exactly 2 paints (one per card) over the whole run, so no frames are produced while
  nothing changes. GPU 3D reads 0.0. CPU 0.3 is the same as `empty`.
- `icon30`/`icon60` paint counts matched the cap (about 60 per second for `icon60`). Each frame is a CPU raster of the
  whole 1050x520 card plus an upload (SKXamlCanvas); that is where the CPU and 3D cost comes from **(inference)**.
- `video` is 360p, so decode cost at a real background resolution is higher **(inference)**. The 317 MB VRAM is mostly
  a number to watch, not explained.
- `web` page was Wikipedia "List of Microsoft Windows versions" (static, no media). Per-process: app 120 MB private,
  five or six `msedgewebview2` processes (3 to 101 MB each).
- WPF baseline was run from this worktree's build (`UrDeck.Host.exe`), log shows target 1100x3840, window placed at
  3840,-3573, scale 1.5 then 1.0 after a DPI change event.
- The `web` and `all` runs used `--clip wv`, which does not round the web view (see check 2); a separate run of the
  other clip modes was captured for that finding only.

## Check 1: memory: pass against the stated bar, with a caveat

Empty borderless WinUI 3 window: 94 MB private (WPF host today: 61 MB). Full `all` page: 419 MB private against Nexus's
about 627 MB, with 7 processes against Nexus's 7, 10.5% CPU against about 20%, VRAM 355 MB against 238 MB (higher).
The framework floor is about 33 MB above WPF, which is small next to the web view (about 235 MB) and video (about
80 MB private over static). Caveat: one 29 s sample each, a 360p clip, a static web page; no game running next to it
(GPU contention untested).

## Check 2: layering: pass with SKXamlCanvas; SKSwapChainPanel failed as used

- Translucent Skia cards (SKXamlCanvas) over `MediaPlayerElement` video: correct, alpha honoured, video visible through
  the cards and the Lottie icon (screenshots `video.png`, `all.png`).
- WebView2 (WinUI 3 control) sits above the video and below the Skia cards in z-order as a normal XAML child (`all.png`).
  Its default hosting does not respect a composition clip on the control's own visual (`--clip wv`: corners square).
  A rounded composition clip on the parent Grid's visual (`--clip host`) works, and so does a XAML `Border` with
  `CornerRadius` around the WebView2 (`--clip border`). A rectangular clip (`rect`, `xamlrect`) also works on the parent.
  Evidence: `clips.png` (corner crops, 4 variants).
- `SKSwapChainPanel` (GPU, ANGLE) rendered **opaque**: cleared-transparent pixels showed as dark grey instead of the
  background (`icon30swap.png`). Not investigated further; I did not find a documented alpha option. So the GPU Skia
  control cannot currently be used for translucent cards over other content **(failure observed; cause not established)**.
  Lower CPU/3D but higher private memory (181 vs 128 MB) than SKXamlCanvas for the same page.
- Not tested: web view touch, scrolling, whether `MediaPlayerElement` shares GPU textures with Skia (it does not; it is
  a separate composition layer).

## Check 3 (placement at startup only)

- `AppWindow.MoveAndResize(OuterBounds)` + borderless presenter: window rect and DWM frame bounds are exactly
  3840,-3573 to 4940,267 (1100x3840), logged. Yes at startup.
- However the XAML/client area is 1094x3834: the window keeps `WS_DLGFRAME` (style 0x144B0000), 3 px non-client border per
  side, and a thin light edge is visible in captures. Workaround that worked: move/resize to bounds -3,-3,+6,+6
  (`--presenter comp`): client area 1100x3840 exactly. Clearing `WS_DLGFRAME` via `SetWindowLong` did not stick, and
  `FullScreen` presenter gave worse numbers (client 1084x3801) as I invoked it (move first, then set presenter).
- Sleep/resume and hot-plug not tested.

## Screenshots

Kept by the owner outside the repository, with a copy of the spike code:
`static.png`, `video.png`, `web.png`, `all.png`, `icon30swap.png`, `clip-*.png`, `clips.png`.
