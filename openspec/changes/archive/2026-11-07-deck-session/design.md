## Context

Today (see `src/UrDeck.Host/MainWindow.cs`):

```
MainWindow ── config/theme/plugin reload, BackgroundSettings, MotionGate, PageNavigator,
   │          LayoutKey + RebuildLayout (ThemeResolver, ChromeLayout, GridLayoutManager, DockLayout),
   │          readings changed → repaint, PlacementController + MonitorPlacement, power and animation watchers
   ├─ BackgroundLayer (SKSwapChainPanel / SKXamlCanvas)
   ├─ PatternLayer
   └─ PagerController ── GestureRecognizer (engine), slide/settle on composition offsets, indicator
         └─ PageHost (per page, per dock) ── UnderlayView, BorderMotion
               └─ WidgetView : SKXamlCanvas ── the IWidget instance, WidgetServices, reading subscriptions,
                                               refresh timer, wake timer, FrameClock entry, taps and press state
```

The engine already holds the pure parts (`PageNavigator`, `GestureRecognizer`, `ChromeLayout`, `DockLayout`,
`GridLayoutManager`, `WidgetHitTest`, `BackgroundPlan`, `PageRenderer`). What is in the host but is not XAML: widget
lifetime and scheduling in `WidgetView`, page lifetime in `PageHost`, the swipe's state machine around the recognizer
in `PagerController`, the reload reactions and layout keying in `MainWindow`, and `MotionGate`.

`specialized-display-output` needs all of that, with a different way of drawing: one canvas per frame, offsets
applied in Skia, input from the digitizer instead of XAML pointer events, no `DispatcherQueueTimer` per widget.

## Goals / Non-Goals

**Goals:**
- One place, free of XAML, that decides what the deck shows and when a widget must be painted.
- The window path behaves exactly as before, verified by the goldens, the existing tests, the logs and the owner on
  the panel.
- New session tests run on Linux.

**Non-Goals:**
- The direct presenter (next change).
- Changing what is drawn, the layout, the frame pacing, the log lines or the performance.
- Making the session public SDK, or a plugin point.

## Decisions

### D1. The split

```
                       ┌──────────────────────── DeckSession (engine) ────────────────────────┐
config, themes,  ───▶  │ reloads · theme + background plan · motion decision (MotionGate)     │
plugins, readings      │ PageNavigator + PagerState (swipe offset, neighbour, settle)          │
                       │ layout for a DeckSurface (size px, scale) · chrome · dock             │
                       │ PageSlots: WidgetSlot per placed widget (instance, services,          │
                       │   subscriptions, refresh/wake schedule, IsAnimating, tap, pressed)    │
                       │ dirty set → "paint these slots" / "layout changed" / "motion changed" │
                       └──────────────▲──────────────────────────────┬─────────────────────────┘
                     surface, shown,  │                              │ what to draw
                     input samples    │       IDeckPresenter         ▼
                       ┌──────────────┴───────────────┐   ┌──────────────────────────────┐
                       │ WindowPresenter (host)       │   │ DirectPresenter (next change)│
                       │ placement, XAML layers,      │   │                              │
                       │ SKXamlCanvas per slot        │   │                              │
                       └──────────────────────────────┘   └──────────────────────────────┘
```

- **`WidgetSlot`** owns what `WidgetView` owns today except the canvas: the `IWidget`, its `WidgetServices`, the
  reading subscriptions (subscribed while its page is in use and the deck is shown), the refresh interval and wake
  times, the `IsAnimating` registration with the frame source, `ITapTarget` and the pressed state. It paints through
  `Paint(SKCanvas, size)`, called by whichever presenter draws it, with the same card drawing as now. On dispose it
  drops its widget reference (the collectible-context rule in `AGENTS.md`).
- **`PageSlots`** replaces the non-XAML half of `PageHost`: the slots of one page or the dock, the reading-to-slot
  map, the underlay and border-motion decisions.
- **`PagerState`** holds the non-XAML half of `PagerController`: the recognizer, which pages are alive (current, and
  the neighbour during a swipe), the offset, and the settle animation as a function of time. The window presenter
  applies the offset to composition visuals; a direct presenter translates its canvas.
- **`DeckSession`** holds what `MainWindow` decides: reload reactions, `LoadedTheme`, `BackgroundSettings`, the motion
  decision, the layout key and rebuild, readings-changed to dirty slots, plugin reload releasing slots first.
- **`IDeckPresenter`** is small: it reports its `DeckSurface` (size in device-independent units and scale, or none
  while hidden), raises "surface changed" and "shown/hidden", feeds input samples, and receives "layout changed",
  "paint these slots", "pager moved" and "background changed". Names are the implementer's; the shape is the decision.

*Alternative:* make the window presenter render the whole page with `PageRenderer` into one surface too, so both
presenters are identical. Rejected here: it changes the GPU composition the window path was measured with
(`docs/perf/render-host-baseline.md`) and would move goldens-adjacent behaviour in a refactor. It can be measured later.

### D2. Scheduling without `DispatcherQueue`

The engine cannot see `DispatcherQueue`. The session takes an `IDeckScheduler` (post to the deck thread, a one-shot
timer, now) and a frame source (`IsAnimating` clients get a tick at the frame rate while any is registered). The host
implements both with `DispatcherQueue` and `CompositionTarget.Rendering`/`FrameClock` exactly as now; tests use a
fake with a manual clock. Everything the session does stays on one thread, the deck's UI thread, so widgets keep
their threading contract (all calls on one thread).

### D3. Order of work

Move one piece at a time, each a green build with the window behaving the same: `MotionGate` → `WidgetSlot` (with
`WidgetView` delegating to it) → `PageSlots` → `PagerState` → `DeckSession` (with `MainWindow` delegating). Each step
keeps the log lines byte-for-byte where the tests or docs refer to them.

### D4. A second presenter in the same process (added 2026-11-06 for the editor)

The editor's preview (`editor-shell`, design D2) is a `DeckSession` of its own with a presenter in the settings window.
Build the session so that this needs no second refactor:

- **No process-wide state in the session.** Several sessions may live at once on the deck's UI thread; nothing static
  holds a session, a slot or a widget (the collectible-context rule applies to each).
- **Injected inputs**: the configuration source (the typed config and its change event), the readings source (an
  interface over what `ReadingHub` gives a widget: read, describe, subscribe; the preview passes a sample or composite
  source), the theme and background inputs, the scheduler and the frame source. The window presenter's session gets
  today's objects.
- **Page chosen from outside**: `ShowPage(index or id)` moves the navigator with the normal settle; the deck's window
  session exposes it for "the deck follows the editor" and raises `PageChanged` when a swipe settles.
- **Input optional**: a session whose presenter feeds no input samples has no pager gestures and no taps; nothing in the
  session assumes input exists.
- **Layout exposed**: the current layout (each slot's card and cell rectangles in surface units, the dock's, the
  indicator's) is readable, so a presenter can hit-test and draw overlays.
- **Shown/hidden** already in `IDeckPresenter` stops subscriptions and frames; the preview uses it when minimised.
- **One paint path for "all slots into one canvas"**: a helper that paints every visible slot of the current page and
  the dock into one `SKCanvas` in z-order (what the direct presenter needs too); the window presenter keeps one canvas per
  slot.

Tests: two sessions with fake presenters on the same fake scheduler (independent pages, a reading change dirties slots in
both, a plugin reload releases slots in both), a session with no input, and `ShowPage`.
## Risks / Trade-offs

- [A subtle timing change (refresh, wake, frame registration) shows as a widget that stops updating or an idle cost
  that rises] → compare `docs/perf/display-recovery.md` idle numbers and a ten-minute run on the panel before merging;
  the session tests pin refresh and wake scheduling with a fake clock.
- [Plugin hot reload leaks old contexts if a slot keeps a reference] → keep the existing reload tests, add one that
  a disposed slot holds no widget, and watch the process's memory over a few reloads on the panel.
- [The refactor grows into a redesign] → D1's alternative is explicitly out; anything not needed by a second
  presenter stays where it is.

## Migration Plan

None. A refactor; rollback is reverting the pull request.
