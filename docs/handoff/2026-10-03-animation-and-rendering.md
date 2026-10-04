# Handoff: explore animation and the rendering path (2026-10-03)

> The exploration this note asked for has been done. Its outcome and the revised spike are in
> [2026-10-04-render-host-spike.md](2026-10-04-render-host-spike.md), which replaces "Start here" and "Measure first"
> below. The rest is kept as background.

Context for a fresh session (Opus) that will run `/opsx:explore` and then `/opsx:propose` for roadmap item 14. Read
`AGENTS.md`, `README.md` and `docs/ROADMAP.md` (items 4, 5, 7, 9 and 14) first; this note only adds what those do not
say.

## Start here

1. `/model opus`, then ask: "Read `docs/handoff/2026-10-03-animation-and-rendering.md`, then run `/opsx:explore` on
   animation and the rendering path with me."
2. Explore with the owner (conversation, no code). The owner has said that existing design, docs and agent guidance are
   suggestions: this is a new project and challenging earlier decisions is welcome when the design is better for it.
3. Agree what to measure, then run the spike on the panel before fixing the design (see "Measure first"). A spike is
   throwaway code on a branch; it is the one exception to "no code in explore".
4. `/opsx:propose` a new change (suggested name: `animation`). If the numbers show the rendering path must change, that
   is probably its own change that lands first (suggested name: `render-path`).
5. Implement with `/opsx:apply` in a fresh Sonnet session, review on Opus, then `/opsx:archive`. One PR per change.

## What the owner has decided

- **Continuous looping is the design target**, not "animate briefly, then rest". The first case is a weather icon that
  loops like the one in HYTE Nexus (clouds drifting across the sun). More cases are expected: music visualizers, chart
  rendering with transitions, value changes that ease.
- The owner likes **full-colour animated weather icons**, not single-colour line icons.
- UrDeck should have the same kind of features as Nexus with a **deliberately different look**. It is not a clone.
- The owner's Nexus page also runs an **animated (video-like) background** behind translucent cards. That is roadmap
  item 9 and out of scope here, but it draws on the same CPU and memory budget and the same compositing path.
- Standing preferences: keep designs small and park the rest in the roadmap; performance is priority one; the owner
  prefers to look at the real panel over long automated pixel comparisons.

## What exists today (verified against `main` at the merge of #9)

- **One surface per widget.** `src/UrDeck.Host/WidgetView.cs` is an `SKElement` sized to the widget's card. It rasterizes
  in software into a `WriteableBitmap` at physical resolution; every repaint redraws the whole card and hands the whole
  bitmap to WPF. There is no partial invalidation.
- **Repaints are timer-driven.** A widget declares exactly one of `[RefreshOnTick]`, `[RefreshAdaptive]` (runs at its
  minimum interval today) or `[RefreshOnEvent]` (renders once). `WidgetView` runs a `DispatcherTimer` at background
  priority; each tick awaits `UpdateAsync`, then asks `NeedsRender(now)` and calls `InvalidateVisual()` only when it
  returns true. A widget cannot ask for frames at runtime.
- **One paint path.** `src/UrDeck.Engine/Rendering/WidgetPainter.cs` `Paint` draws the themed card, clips to its
  rounded shape and calls the widget. `PageRenderer` uses the same function for `--snapshot`.
- **Paint cost of the static path** (`docs/perf/theme-card-paint.md`): 0.28 ms for a 1100x550 card including card,
  clip and the Clock. That number is rasterizing into an `SKBitmap`; it does not include the copy to WPF or WPF's own
  composition, which is the part animation will stress.
- **Software composition is a deliberate choice.** `App.xaml.cs` sets `RenderOptions.ProcessRenderMode = SoftwareOnly`,
  which saved about 62 MB private bytes (`docs/perf/memory-investigation.md`); `URDECK_HWRENDER=1` opts back in to
  hardware composition. The budget is 60-70 MB private for the host.
- **Each widget surface costs memory in proportion to its pixels** (width x height x 4 bytes, plus caches). The memory
  investigation measured a 4x2 card at scale 1.5 (1650x825, 5.4 MB); confirm the panel's current scale from
  `urdeck.log` before reusing that figure.
- **OpenTK and GLWpfControl** ship in the output as dependencies of `SkiaSharp.Views.WPF` but are not loaded today.
- **`WidgetRenderContext.Time`** is a wall-clock `DateTime`. There is no monotonic or animation time.
- **Theme and components:** `Theme` (SDK) carries colours, pixel sizes, typefaces, `StrokeRatio` and `StrokeCap`.
  `UrDeck.Sdk.Components` has `Readout` and `TextLine`, both stateless static draw functions. The SDK assembly version
  is 0.2.0.0. There is no motion setting in the theme yet; it was listed as a later knob (off, subtle, full).

## Research already done (re-verify before relying on it)

- The repo is on **SkiaSharp 4.153.0**. 4.0 went stable in June 2026 and reported software shaders about 6x faster and
  GPU rendering up to 24% faster; it also made `SKPath` immutable (`SKPathBuilder`).
- **`SKGLElement`** exists in `SkiaSharp.Views.WPF`: a GPU-backed element using OpenGL through WGL. Untested here. Open
  points: whether it works with one element per widget, whether it requires hardware WPF composition (which would bring
  back the 62 MB), and how it behaves with transparency over the window background.
- **Lottie playback:** the `SkiaSharp.Skottie` package (`Animation`, `SeekFrameTime`, `Render(canvas, rect)`), which
  depends on `SkiaSharp.SceneGraph` and `SkiaSharp.Resources`. A build matching 4.153 was not confirmed.
- **Weather art:** Meteocons is MIT, has 475+ icons in four styles including a full-colour "Fill" style, and ships
  Lottie files. An illustrated set brings its own art style, which a theme cannot restyle.
- Use Context7 (`/mono/skiasharp`) for SkiaSharp API questions, as the owner's global rules require.

## Measure first

The central unknown is what continuous motion costs on the Y70, and no design decision should be taken before it is
measured. Suggested spike, results into `docs/perf/`:

| Case | What it answers |
|---|---|
| Looping Lottie icon on a 4x2 card, software, at 15, 30 and 60 frames per second | The cost of the current path; whether a frame-rate cap alone is enough |
| The same with `URDECK_HWRENDER=1` | Whether hardware composition helps, and what it costs in memory today |
| The same on an `SKGLElement` | Whether GPU rasterizing is viable per widget, and its memory |
| The animated region as its own small surface over a static card | Whether shrinking the repainted area is the cheap win |
| Three animated widgets at once | Whether the cost adds up linearly |

Record CPU as a share of one core and of the whole machine, private bytes, and whether motion looks smooth to the owner.
Also record the idle page again to confirm that a page with no animation is unchanged.

## Open questions for the exploration

1. **How a widget asks for frames.** A new refresh attribute with a rate, a runtime request ("keep painting me until I
   say stop"), or both. How it relates to `NeedsRender` and to the three existing refresh attributes, which are mutually
   exclusive today. A weather widget needs both: a slow data refresh and a fast animation.
2. **Time.** What clock an animating widget reads so motion is smooth and independent of frame rate, and what a snapshot
   shows for an animated widget (a fixed, repeatable frame).
3. **Rendering path.** Stay in software, move to GPU-backed elements, mix per widget, or one surface for the whole page.
   The memory investigation already listed a single page surface and a plain Win32 plus Skia host as options; roadmap
   item 4 keeps the second as an open decision. This is where the numbers decide.
4. **Limits.** A frame-rate cap, a motion level for the user (and whether it lives in the theme, the config or both),
   pausing when the display sleeps, the page is hidden or the window is covered, and backing off when the PC is busy.
   The last one overlaps roadmap item 3 (`system.cpu` and adaptive refresh); decide which change owns it.
5. **Where Lottie lives.** A shared component in the SDK (for example an animated icon that takes a rectangle and a
   time) versus a private dependency of the weather widget. If shared, whether Skottie types appear in the SDK's public
   surface, and how the loader shares the extra assemblies with plugins.
6. **Assets.** How a widget ships and loads files such as animation JSON, and whether a theme may replace them (icon
   packs were parked as a later theme knob).
7. **Transitions.** Easing a value or a gauge between readings: part of this change, or a small helper that arrives
   with the performance widget.
8. **Fault containment.** An animating widget that is slow or throws must not stall the UI thread or other widgets;
   today a slow `Render` blocks the dispatcher.

## Constraints and preferences

- Idle CPU near zero for a page with no animation, and the 60-70 MB private budget, unless the owner agrees to move
  them on the strength of the measurements.
- Widgets reference only `UrDeck.Sdk`; the SDK is MIT and must not depend on GPL code. The SDK is unpublished and has
  one in-repo consumer, so a breaking change is acceptable when it gives a cleaner contract (bump the frozen assembly
  version deliberately, as 0.2.0.0 did).
- Widgets never see resolution or scaling, and take their look from the theme.
- Third-party art and libraries need their licence text and an attribution entry (README licence map).
- Delegate implementation to Sonnet; keep Opus for design and review. One fresh session per exploration.

## Pointers

`docs/ROADMAP.md` (items 4, 7, 9, 14 and "Suggested order"), `docs/perf/memory-investigation.md`,
`docs/perf/theme-card-paint.md`, `openspec/changes/archive/2026-10-03-theme-and-card/design.md` (decisions the theme
change took and why), `openspec/specs/host-shell/spec.md` ("Widget Refresh Scheduling", "SkiaSharp Element
Integration"), `openspec/specs/widget-sdk/spec.md` ("Refresh Policy System", "Render Skipping"),
`src/UrDeck.Host/WidgetView.cs`, `src/UrDeck.Engine/Rendering/WidgetPainter.cs`, `src/UrDeck.Host/App.xaml.cs`.
