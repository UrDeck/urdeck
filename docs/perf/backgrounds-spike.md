# Backgrounds spike: live shader, compositor gradient or video (2026-10-11)

Question: for a moving background behind the cards, is it cheaper to draw it live or to play a video of it? And if
live, on which surface and at what resolution? Code: `spike/RenderHostSpike/` on branch `spike/backgrounds` (throwaway,
built on the `spike/render-host` app; not in `urdeck.slnx`). Method: `docs/perf/nexus-baseline.md` (CPU from
`TotalProcessorTime`, GPU Engine and GPU Process Memory counters, 5 GPU samples four seconds apart after a 15 s settle;
`measure.ps1` and `run-all.ps1` beside the spike). Everything below is observed unless marked **(inference)**. Each row
is one run, not a benchmark.

## Environment

- Windows 11 Pro 10.0.26200, 32 logical processors, NVIDIA GeForce RTX 4090. Panel: 1100x3840 at 3840,-3573, scale 1.0.
  Nothing else was loading the GPU. The installed UrDeck kept running under the spike's topmost window.
- .NET SDK 10.0.401, Windows App SDK 2.5.1, SkiaSharp and SkiaSharp.Views.WinUI 4.153.1 (the versions the host uses).
- Every case has the spike's two translucent Skia cards (`SKXamlCanvas`) over the background, borderless window
  covering the panel (`--presenter comp --topmost`), 50 s per run.

## Cases

- `static`: a 1100x3840 image (the baseline of the render-host spike).
- `comp-gradient`: no app code per frame. One `SpriteVisual` with a `CompositionLinearGradientBrush` whose rotation,
  one stop offset and one stop colour are animated by the compositor.
- `plasma-gpu-*`, `julia-gpu-*`: an SkSL shader (`SKRuntimeEffect`) drawn into one `SKSwapChainPanel` under the cards,
  30 frames per second, at full, half and quarter resolution. `plasma` is four warped sines through a colour ramp
  (cheap). `julia` is a Julia set with up to 128 iterations per pixel, laid along the tall axis so most of the panel
  iterates (expensive on purpose).
- `plasma-cpu-0.25`: the same plasma on an `SKXamlCanvas` (Skia runs the shader on the CPU), quarter resolution.
- `video-*`: `MediaPlayerElement`, looped, muted. The 360p H.264 clip of the render-host spike, and the plasma baked
  to 1100x3840 at 30 frames per second, 10 s, 12 Mbit/s, as HEVC and as H.264.

## Results

CPU is percent of one core. GPU is the spike's process, summed over engines of the type. VRAM is dedicated usage.

| Case | CPU | GPU 3D | GPU video decode | Private MB | Working set MB | VRAM MB | Background frames in 50 s |
|---|---|---|---|---|---|---|---|
| `static` | 0.2 | 0.0 | 0.0 | 122 | 153 | 159 | n/a |
| `comp-gradient` | 5.5 | 5.6 | 0.0 | 104 | 135 | 141 | n/a |
| `plasma-gpu-1.0` (1100x3840) | 3.9 | 1.1 | 0.0 | 167 | 163 | 203 | 1503 |
| `plasma-gpu-0.5` (550x1920) | 3.6 | 0.5 | 0.0 | 146 | 163 | 155 | 1503 |
| `plasma-gpu-0.25` (275x960) | 2.9 | 0.3 | 0.0 | 136 | 163 | 143 | 1503 |
| `julia-gpu-1.0` | 3.2 | 2.4 | 0.0 | 171 | 160 | 203 | 1503 |
| `julia-gpu-0.5` | 3.3 | 0.9 | 0.0 | 144 | 160 | 155 | 1503 |
| `julia-gpu-0.25` | 3.0 | 0.5 | 0.0 | 139 | 164 | 143 | 1503 |
| `plasma-cpu-0.25` | 49.4 | 3.4 | 0.0 | 112 | 144 | 99 | 1502 |
| `video-360p-h264` | 1.7 | 1.8 | 4.1 | 199 | 232 | 333 | n/a |
| `video-native-hevc` | 2.4 | 0.8 | 8.7 | 254 | 247 | 444 | n/a |
| `video-native-h264` | 2.9 | 2.8 | 15.3 | 289 | 314 | 405 | n/a |

The paint callback itself took about 0.1 ms per frame on the GPU surface (it only records the draw) and 15.4 ms per
frame on the CPU canvas at quarter resolution.

## Findings

1. **A live shader on a GPU surface is cheaper than video.** Against the static page, native-resolution video adds
   130 to 170 MB private and 250 to 285 MB of dedicated video memory; the shader adds 15 to 50 MB private and at most
   45 MB of video memory. CPU is about level (3 to 4 against 2.4 to 2.9). GPU: under 2.5% of the 3D engine for the
   shader against 9 to 15% of the video decode engine. The two engines are different hardware, so the percentages are
   not strictly comparable.
2. **Shader complexity barely registers on this GPU.** The 128-iteration Julia set at full resolution cost 2.4% 3D.
   Half resolution cut it to 0.9%, quarter to 0.5%.
3. **About 3% of one core is a floor.** It does not move with the shader or the resolution, so it is the per-frame
   cost of driving the surface (the frame callback, the GL context, the present), not the drawing **(inference; not
   isolated)**.
4. **The compositor-only gradient was the most expensive live option** (5.5% CPU, 5.6% 3D). It animates at the
   display's refresh rate at full resolution and cannot be slowed to 30 frames per second.
5. **The shader has to run on the GPU.** On the CPU canvas the plasma took half a core at quarter resolution. One CPU
   frame is still fine for a snapshot: 300 full-resolution plasma frames took about 130 s while baking, about 0.4 s
   each.
6. **Baking to video works without ffmpeg, and has no use.** `MediaTranscoder` with a `MediaStreamSource` of BGRA
   frames produced 1100x3840 HEVC and H.264 files (9.0 and 9.5 MB for 10 s). It is slower to produce, heavier to play
   and cannot take live parameters.
7. **Rendering below the panel's resolution needs the composition visual.** A XAML `RenderTransform` on the
   `SKSwapChainPanel` feeds the panel's composition scale and the panel renders at full size again (the log showed
   1100x3840 with `--scale 0.5`). Setting `Scale` on the element's composition visual
   (`ElementCompositionPreview.GetElementVisual`) is invisible to XAML: the surface stays 550x1920 and the compositor
   stretches it.
8. **A 33 ms `DispatcherTimer` delivered about 22 frames per second** (173 paints in 8 s). Pacing from
   `CompositionTarget.Rendering` and skipping callbacks that come early gave a steady 30 (1503 in 50 s). The host's
   `FrameClock` uses a `DispatcherQueueTimer` at the same interval; whether it has the same shortfall was not measured.
9. **`SKSwapChainPanel` is opaque** (render-host spike, check 2). For the bottom layer that does not matter: translucent
   `SKXamlCanvas` cards composite over it correctly.

## Owner's look (on the panel, 25 s per case)

- Plasma: full, half and quarter resolution could not be told apart. It is slow and soft.
- Julia: half resolution looked good. Quarter was starting to look a little blurry: not terrible, but noticeable.
- So half resolution is the default, and the scale stays a setting.

## Not measured

- A game loading the GPU beside the deck (the shader shares the 3D engine with it; video decode mostly does not).
- An integrated GPU. The Julia case would cost far more there **(inference)**.
- Display scaling other than 100%.
- Hours of uptime (see the time handling in the `backgrounds` change's design).
