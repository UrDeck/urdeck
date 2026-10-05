## Context

See `proposal.md` for the motivation. What shapes the approach:

- `WidgetView` (one `SKXamlCanvas` per widget) owns a `DispatcherQueueTimer` at the widget's declared interval. Each
  tick awaits `UpdateAsync`, asks `NeedsRender(DateTime.Now)` and calls `Invalidate()`. Painting goes through
  `WidgetPainter.Paint`, shared with `--snapshot`.
- The spike measured a full-card repaint (1050x520) at 30 frames per second at 3.4% of one core, and the owner could
  not tell 30 from 60 frames per second on the panel (`docs/perf/render-host-spike.md`).
- A page rebuild (resize, theme change, resume from sleep, plugin reload) disposes every view and creates new widget
  instances, so widget state never survives a rebuild.
- The SDK is unpublished with one in-repo consumer. Its `AssemblyVersion` is frozen at 0.2.0.0 and only moves on a
  breaking change.
- An exploration on 2026-10-04 started from a larger design (a scheduler in the engine, frame and wall-clock wake
  requests, a pinned animation time, a transition helper, a motion level) and cut it down to what the first consumer
  needs. The cut items are listed under Non-Goals so they are not rediscovered as omissions.

## Goals / Non-Goals

**Goals:**

- A widget can animate without changing how any other widget is written or scheduled.
- A page with nothing moving costs what it costs today: no frame source, 0% CPU and GPU between repaints.
- Frames and data refresh are separate cadences.
- The mechanism is small enough to replace cheaply when the weather widget shows what a continuous loop needs.

**Non-Goals:**

- A scheduler in the engine, or any engine change.
- A request to be painted at a wall-clock time. The Clock keeps its 1-second tick, which measures as 0% CPU.
- A monotonic animation time, or one that `--snapshot` can pin. `WidgetRenderContext.Time` has millisecond precision;
  a clock jump costs one wrong frame.
- An SDK transition or easing helper; a user motion level; backing off when the PC is busy (needs `system.cpu` from
  the data providers change); per-widget paint cost tracking; fault containment for a slow `Render`.
- Smaller or GPU-backed surfaces (`SKSwapChainPanel`), Lottie, assets.
- Proving the continuous-loop case, several animated cards at once, or behaviour next to a game. Those arrive with the
  weather widget, which is the first widget that loops.

## Decisions

### 1. The widget says "I am animating"; the host owns the rate

`IWidget` gains `bool IsAnimating => false` (a default interface member) and `Widget<TConfig>` a virtual one, the same
idiom as `NeedsRender`. The host reads it after each paint.

Alternatives considered:

- **Widgets choose their own rate with `[RefreshOnTick(33, Milliseconds)]`.** Works today with no new API, but every
  tick also calls `UpdateAsync`, so a widget that fetches data must throttle itself, and the host cannot pause frames
  without pausing data. The widget also wakes 30 times a second forever, animating or not.
- **A global tick that calls every widget.** Wakes the page when nothing moves.
- **A request made during `Render` (`context.RequestFrame()`).** Equivalent in effect. A property is simpler to test,
  leaves the render context immutable, and matches `NeedsRender`.

### 2. No `AssemblyVersion` bump

Adding a default interface member and a virtual member is not breaking: a plugin built against 0.2.0.0 without the
member loads and gets the default. `AssemblyVersion` stays 0.2.0.0 and the frozen-version test stays as it is. The
package `Version` moves to 0.3.0 and the csproj comment records the addition. `SdkContractTests` gains a test that
`IsAnimating` defaults to `false`.

### 3. One frame clock in the host, running only while needed

A small `FrameClock` class in `src/UrDeck.Host`, owned by `MainWindow` and passed to each `WidgetView`:

```
 WidgetView.OnPaintSurface
      │ after WidgetPainter.Paint: read widget.IsAnimating (guarded)
      ├─ true  ──▶ FrameClock.Add(view)      starts the timer if it was stopped
      └─ false ──▶ FrameClock.Remove(view)   stops the timer when the set is empty

 FrameClock timer tick (about 33 ms) ──▶ Invalidate() on every view in the set
 WidgetView.Dispose ──▶ FrameClock.Remove(view)
```

- The timer is a `DispatcherQueueTimer` at 1/30 s on the UI thread, created once and started and stopped; it is not
  left running with an empty set.
- The paint handler only changes membership; it never invalidates from inside a paint. The next tick does.
- A tick iterates over a copy of the set, because a paint can change membership.
- `IsAnimating` is read through a guarded helper like `SafeNeedsRender`: an exception is logged and counts as `false`.
- The clock logs when it starts and when it stops (one line each), which is how "the frame source stopped" is
  verified from `urdeck.log`.
- The refresh timer path is unchanged. When it paints a widget that then reports `IsAnimating`, the same paint handler
  adds it to the frame clock.

Alternative considered: `CompositionTarget.Rendering`, which is aligned to the display's refresh. It fires at the
display rate (60 Hz on the panel), so every other callback would be skipped to reach 30, and it has to be unsubscribed
just as carefully. A timer is enough for a half-second flip; revisit it if a continuous loop shows visible judder.

Alternative considered: the set and the start/stop rule as a pure class in `UrDeck.Engine` for unit tests. Rejected
for now: it is a set and a timer, and the engine has no other scheduling code to sit beside. It moves there if it
grows (a cap that varies, a motion level).

### 4. The Clock's flap state lives in the widget and is driven by render time

`ClockConfig` gains `Style` (`"simple"` default, `"flap"`; anything else is `simple`). `ClockWidget.Render` branches
on it; the `simple` path is today's code, untouched. The flap drawing goes in its own internal class in the Clock
project so `ClockWidget` stays readable. It is not an SDK component: no other use is known, and it moves to the SDK
if one appears.

State kept by the widget for the flap style:

- the tile characters last shown (four characters, a space for a blank tile),
- the tile characters being flipped from, and the `DateTime` the flip started (the `context.Time` of the paint that
  first saw the new time),
- whether a flip is in progress (what `IsAnimating` returns).

In `Render`, with `t = context.Time - flipStart`:

- no previous characters (first paint, or after `OnConfigured` reset them): draw at rest, no flip;
- the characters differ from the last shown and no flip is running: start a flip now;
- `t` in `[0, duration)`: draw each changed tile mid-flip, `IsAnimating` true;
- otherwise (finished, or `t` negative after a clock change): draw at rest, clear the flip, `IsAnimating` false.

`NeedsRender` is unchanged: it compares the displayed minute. The 1-second refresh tick notices the new minute and
paints; that paint starts the flip; the frame clock carries it for the duration. The flip therefore starts up to a
second after the minute changes, exactly as the digits change today.

The duration is a constant of about 0.5 s, the same for every tile, all changed tiles flipping together. No stagger
and no easing helper; a simple ease on the flap angle may be applied inline.

### 5. Drawing a flip without 3D

A tile is two halves split by the hinge. A flip from `old` to `new` with progress `p` in 0..1:

- at rest: the whole character on the tile, the hinge line across the middle;
- first half (`p < 0.5`): the bottom half shows `old`, the top half shows `new`, and a flap carrying the top half of
  `old` is drawn over the top half, scaled vertically toward the hinge (from full height to nothing);
- second half: the top half shows `new`, the bottom half shows `old`, and a flap carrying the bottom half of `new` is
  drawn over the bottom half, scaled vertically from nothing to full height;
- the moving flap is darkened by black at an opacity that follows how far it is from flat.

Each half is drawn by clipping to the half's rectangle and drawing the whole character, so no character is rasterized
to an intermediate bitmap. A vertical scale stands in for the rotation; it is what makes the effect read as a flap,
and it costs a canvas transform. The whole card is repainted on each frame, as for any repaint.

### 6. Tile look: opaque, derived from existing theme values

No theme format change. Tuned on the panel (the first version, a translucent fill, showed the half behind a flipping
flap and looked wrong), so the flaps are opaque and the look is a deliberate step away from theme translucency:

| Part | From |
|---|---|
| Tile fill | `Theme.Text` at about 14% opacity composited over `Theme.CardFill` over `Theme.Background`: a solid colour |
| Tile radius | `Theme.CardRadius` scaled by tile height over card height, with a small minimum |
| Crease | black gradient across the tile, up to about 28% at the hinge, so each half darkens toward the middle |
| Hinge | black at about 80% opacity, thickness from `Theme.StrokeRatio` times a quarter of the tile height |
| Digits | `Theme.GetTypeface(TextRole.Value)`, `Theme.Text` or the `TextColor` override, sized to the tile |
| Flap shading | black at up to about 45% opacity on the moving flap; a shadow of up to about 40% on the half below it |
| Colon, `AM`/`PM`, date | text in the theme's colours; the date through `TextLine` as today |

Alternative considered: a new theme token for an inset fill. Rejected: a format change and an edit to every built-in
theme for one style of one widget. Add it when a second widget needs a panel inside a card.

The tile block is fitted to the content rectangle: four equal tiles plus the colon and gaps across the width, limited
by the height left above the date line, then scaled by `fontSize` (capped at 1, as for the readout). Gaps and the
tile's aspect are ratios of the tile size, not pixel constants.

### 7. `--snapshot` and tests

`PageRenderer` creates fresh widgets and paints each once, so every widget is at rest; no option to pin a time is
added. Flap frames are covered by `ClockWidgetTests`, which already paints a `ClockWidget` at a chosen `DateTime`:
paint at one minute, paint again a few hundred milliseconds into the next, and assert on `IsAnimating` and on the
pixels differing from the resting frame; paint past the duration and assert rest.

## Risks / Trade-offs

- **The first consumer does not loop** → The API is one member and the host part is one small class; both are cheap
  to change while the SDK is unpublished. The weather change revisits them with a real loop.
- **A widget that returns `true` forever** keeps the frame source alive at about 3% of one core for a full card → It
  is visible in the log (the clock never logs a stop). A cap or watchdog is left for the per-widget cost work in
  roadmap item 4.
- **A slow `Render` now blocks the UI thread up to 30 times a second** instead of once per refresh → Unchanged in
  kind; fault containment is a non-goal here.
- **Timer frames are not aligned to the display refresh**, so a frame may occasionally be shown late → Not expected to
  be visible in a half-second flip; the owner judges it on the panel, and decision 3 names the alternative.
- **A wall-clock jump during a flip** (time sync, daylight saving) → The flip ends at once and the tiles are at rest.
- **Sleep or display off during a flip** → A resume forces a page rebuild, which recreates the widget at rest.
- **Tile fill may be too faint or too heavy on some theme** → Values are tuned by eye on the panel in both built-in
  dark and glass themes before the change is done.

## Migration Plan

One pull request from `feat/animation`. No data migration: a config without `style` behaves as before. Rollback is
reverting the squash commit.

## Open Questions

- The exact tile opacity, shading and flip duration: tuned on the panel during implementation, within the rules above.
