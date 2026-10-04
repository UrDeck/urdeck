# Handoff: the render host spike (2026-10-04)

Outcome of the `/opsx:explore` session on animation and the rendering path (roadmap item 14), and the plan for the
spike that comes next. It replaces the "Measure first" section of
[2026-10-03-animation-and-rendering.md](2026-10-03-animation-and-rendering.md); the rest of that note is still useful
background. Read `AGENTS.md`, `README.md` and `docs/ROADMAP.md` (items 4, 7, 8, 9, 11 and 14) first.

> **Result (2026-10-04):** the WinUI 3 half of the spike was run the same day and passed the memory and layering
> checks; the owner confirmed touch scrolling in the web view by hand. The host decision is WinUI 3, and the change is
> proposed as `openspec/changes/archive/2026-10-04-render-path`. Results: `docs/perf/render-host-spike.md`. Sleep and resume, the Frigate
> page and smoothness are the first tasks of that change. The sections below are kept as the record of the plan.

## Start here

1. `/model opus`, then ask: "Read `docs/handoff/2026-10-04-render-host-spike.md` and run the render host spike with me."
2. Build the spike as throwaway code on a `spike/render-host` branch. It is not merged; only its results are
   (`docs/perf/render-host-spike.md`).
3. With the results, `/opsx:propose` the change `render-path` (the new host). `animation` follows it as its own change.
4. Implement with `/opsx:apply` in a fresh Sonnet session, review on Opus, then `/opsx:archive`. One PR per change.

## What the owner decided in the exploration

| Topic | Decision |
|---|---|
| Rendering | Composition moves to the GPU. Software WPF composition is no longer the target. |
| Backgrounds (item 9) | In scope for the platform: a static image, a video file, and a painted visualization (OpenGL or Direct3D). "Cool looks can justify a reasonable cost." |
| Web page widget | A real requirement. The owner uses one in Nexus today to show Frigate camera feeds. |
| Cost bar | "No worse than Nexus", and the owner is flexible even on that. The real goal is an open source, trustable alternative to a closed-source tool. Numbers: `docs/perf/nexus-baseline.md`. |
| Editing | A standalone editor does all editing (add, configure, move, delete). No editing on the panel: Nexus splits it between an app and the touch screen, which the owner finds inconsistent, and other screen types may have no touch. |
| Touch on the panel | For using the deck only: swiping between pages, tapping, scrolling inside a widget, interacting with a web widget. |
| First animation consumer | The Clock, with a second style (split-flap) next to today's simple one. |
| Weather | Its own change after `animation`. It stays the hardest case (slow data, fast frames, Lottie, assets). |
| Rebuild | Nothing is fixed. The host may be rebuilt from the ground up if that gives the product that is wanted. |

`display-targeting` (item 6) was not part of this exploration and stays a future change. Its draft is written against
the WPF window, so rework it after the host is settled.

## Why the GPU, in short

- A video or painted background behind translucent cards changes every pixel of the 1100x3840 window on every frame.
  Repainting less of a card saves nothing once the background moves.
- A page swipe is cheap when each widget is a cached layer that slides, and expensive when the whole window is
  recomposed in software on every frame (expected, not measured).
- Turning WPF's hardware mode back on is not the answer: it costs the 62 MB measured in
  `docs/perf/memory-investigation.md` and still cannot cleanly layer WPF content over GPU-drawn content.
- `SKGLElement` depends on `OpenTK.GLWpfControl` (confirmed in the `SkiaSharp.Views.WPF` package manifest). Whether it
  needs hardware WPF composition was not verified; it is not a candidate either way.

The target shape, whichever framework hosts it:

```
 ┌──────────────────── panel window (GPU-composited) ─────────────┐
 │  background layer:  image | video texture | painted visual     │
 │  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐            │
 │  │ widget layer │ │ widget layer │ │ web view     │            │
 │  │ (Skia,cached)│ │ (Skia,cached)│ │ layer        │            │
 │  └──────────────┘ └──────────────┘ └──────────────┘            │
 │  a frame is presented only when some layer changed             │
 └────────────────────────────────────────────────────────────────┘
   editor (item 11): a standalone window; writes the config, draws its own preview with PageRenderer
```

Widgets keep drawing on an `SKCanvas`, so the SDK, the engine, the widgets and `--snapshot` are expected to survive.
What is replaced is `src/UrDeck.Host` (five files today). Keep all framework code inside the host so that a later
framework change costs a host rewrite and nothing else.

## The open decision: which host

Two candidates. The spike decides.

| | WinUI 3 (first candidate) | Raw Win32 with DirectComposition (fallback) |
|---|---|---|
| Web view as a layer, with touch | built-in control | WebView2 composition hosting; we forward pointer input |
| Video background | built-in media element | we build it on Media Foundation |
| Gestures (pan, flick, inertia) | built in | Windows' Interaction Context API, or our own recognizer |
| Editor controls | the same framework | a second framework for the editor |
| Risk | Microsoft UI framework churn; unknown baseline memory | more to build and debug; the most stable layer |

WinUI 3 goes first because it ships the three pieces the requirements need and would serve the editor too. It passes
only if all three checks hold:

1. **Memory:** an empty borderless window has an acceptable private-bytes baseline against the Nexus numbers.
2. **Layering:** translucent Skia cards and a rounded web view card composite correctly over a video background.
3. **Placement:** the window covers the Y70 exactly and recovers after sleep and resume.

If any check fails, run the same cases on raw Win32 with DirectComposition and accept a second framework for the
editor. Since the editor only writes the config (the host already reloads on `ConfigChanged`) and draws its preview
with `PageRenderer`, the two are loosely coupled either way.

Avalonia was not evaluated. Avalonia 12 lists SkiaSharp 3.119.4 as its minimum and the repo is on 4.153.0; whether it
has a usable web view was not checked.

## Spike cases

Record for each: CPU as a share of one core, GPU share (3D and video decode), private bytes summed over every process
the host starts (web view processes included), dedicated video memory, and whether motion looks smooth to the owner on
the panel. Use the method in `docs/perf/nexus-baseline.md` so the numbers compare.

| Case | What it answers |
|---|---|
| Today's WPF host, one Clock, idle | The baseline being replaced (66 MB private in the memory investigation; record it again) |
| Empty borderless window on the Y70 | Check 1; the floor of the candidate host |
| Image background, two translucent Skia cards, nothing moving | Idle cost: no frames presented, CPU and GPU near zero |
| One card with a looping Lottie icon, at 30 and at 60 frames per second | The cost of continuous motion in one widget; which cap looks right |
| Video background under the translucent cards | Check 2; the cost of item 9 |
| A web view card showing the owner's Frigate page, rounded, scrolled by touch | Check 2; the cost of one web widget; whether touch reaches it |
| Swiping between two pages by touch | Whether pages slide smoothly as layers; gesture handling |
| Sleep and resume, and a monitor hot-plug | Check 3; device loss handling |
| Everything at once | The page to compare with Nexus |

## To verify during the spike (not checked in the exploration)

- A `SkiaSharp.Views.WinUI` build that matches the repo's SkiaSharp version, and whether its swap chain panel can be
  transparent over other content.
- Which Skia GPU backend to use. SkiaSharp offers OpenGL, Vulkan, Direct3D 12 (`SkiaSharp.Direct3D.Vortice`, documented
  as net8.0 only) and the newer Graphite API. What matters: maturity at the repo's version, and sharing textures with
  video decode.
- WinUI 3 as an unpackaged app, with plugins in a collectible `AssemblyLoadContext` (see `WpfAssemblyCache.cs` for the
  trouble WPF caused here).
- The panel's scale. Notes from 2026-09-29 say the Y70 runs at 100% (so a 4x2 card is about 2.4 MB, not the 5.4 MB the
  memory investigation measured at scale 1.5). Confirm from `urdeck.log`.
- GPU contention: how the deck behaves next to a game that saturates the GPU. Software rendering never touched the
  GPU; this will.

Already verified: `SkiaSharp.Skottie`, `SkiaSharp.SceneGraph` and `SkiaSharp.Resources` are published at 4.153.0 and
4.153.1, so Lottie playback matches the repo's SkiaSharp.

## Proposed for the `animation` change (not yet agreed with the owner)

Found in the code: `WidgetView.RefreshAsync` awaits `UpdateAsync` on every tick before it asks `NeedsRender`, so data
refresh and repaint are one cadence; and each widget has its own `DispatcherTimer` at background priority, which is
not a frame clock.

Proposal to discuss:

- **A widget asks for frames while it renders** ("paint me again", or "paint me again at time T"), like the browser's
  `requestAnimationFrame`. A widget that stops asking stops being painted, so a loop and a short transition use one
  mechanism. It could also replace `NeedsRender` polling (the Clock would ask for the next minute boundary).
- **One frame clock in the host**, which owns the rate: the cap, the motion level, pausing when the display sleeps,
  backing off when the PC is busy.
- **A monotonic animation time in the render context**, pinned in `--snapshot` so an animated widget has a repeatable
  frame.
- **Data refresh stays on its own slow cadence**, independent of frames.

The Clock's split-flap style is the first consumer: a transition once a minute that then rests. Its tiles must take
colours and radius from the theme. It does not exercise a continuous loop; the spike's Lottie icon and later the
weather widget do.

## Parked ideas from the session

- **Camera widget:** a purpose-built alternative to showing Frigate in a web widget. Frigate bundles go2rtc, which
  restreams each camera at `rtsp://<host>:8554/<camera>` and serves MSE and WebRTC. A first version could draw JPEG
  snapshots or MJPEG at a few frames per second on the canvas (the endpoint was not checked); real video would share
  the video layer built for backgrounds and probably needs FFmpeg.
- **Twitch chat:** either a web widget on Twitch's popout chat page, or a native widget, which needs pan gestures from
  the host and a shared scroll component in the SDK.
- **Gesture arbitration:** the host sees every touch first; a horizontal swipe changes page, a vertical pan scrolls
  the widget under the finger; widgets declare what they consume. Belongs to item 8.
- **Frosted glass** (blur behind translucent cards) becomes practical with GPU composition; a later theme knob.
