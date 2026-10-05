## ADDED Requirements

### Requirement: Animation Frames
The host MUST repaint animating widgets from one shared frame source:

- After each paint of a widget the host reads `IWidget.IsAnimating`; a widget that returns `true` is animating until a
  later paint after which it returns `false`
- While at least one placed widget is animating, the frame source runs at 30 frames per second and repaints each
  animating widget on every frame; widgets that are not animating are not repainted by it
- When no placed widget is animating, the frame source MUST be stopped, not merely idle
- A frame MUST NOT call `UpdateAsync` or `NeedsRender`
- A widget that is removed (page rebuild, plugin reload, config change) stops being animated, and a widget whose
  `IsAnimating` throws is treated as not animating and the failure is logged
- `--snapshot` ignores `IsAnimating` and paints every widget once

#### Scenario: Still page has no frame source
- **WHEN** a page is shown and no widget is animating
- **THEN** the frame source is not running and the only recurring work is the widgets' refresh timers

#### Scenario: One widget animates
- **WHEN** one widget on a page of several reports `IsAnimating`
- **THEN** that widget is repainted about 30 times per second and the other widgets' `Render()` is not called

#### Scenario: Animation ends
- **WHEN** the last animating widget returns `false` from `IsAnimating` after a paint
- **THEN** the frame source stops and CPU and GPU use return to the idle level

#### Scenario: Animating widget is removed
- **WHEN** the page is rebuilt while a widget is animating
- **THEN** the old widget is no longer repainted and the frame source stops unless another widget is animating

## MODIFIED Requirements

### Requirement: GPU Composition
The host window MUST be composited by the GPU from independent layers: the window background and one layer per widget
surface. The host MUST NOT produce frames while no layer changes, and MUST NOT run a render loop while no widget is
animating (see "Animation Frames").

#### Scenario: Still page
- **WHEN** a page is shown and no widget is repainted
- **THEN** no widget surface is redrawn and the host's GPU use is not measurably above zero

#### Scenario: Layer changes
- **WHEN** one widget is repainted
- **THEN** only that widget's layer is updated and the page is composited again

### Requirement: Widget Refresh Scheduling
The host MUST drive each widget's data refresh from its own refresh policy. The only shared loop is the frame source
for animating widgets (see "Animation Frames"), which is independent of the refresh policies:

- `RefreshOnTick` — a per-widget timer on the UI thread at the declared interval; each tick awaits `UpdateAsync` and then repaints
- `RefreshAdaptive` — a timer at `MinMs` (load-based scaling up to `MaxMs` is specified by a later change)
- `RefreshOnEvent` — the widget renders once on load (event-driven refresh is specified by a later change)
- Overlapping updates for one widget are skipped, not queued
- After each refresh the host asks `IWidget.NeedsRender(now)` and repaints only when it returns `true`; a widget is always painted when first placed, after a config change and after a page rebuild

#### Scenario: Idle dashboard
- **WHEN** only a 1-second Clock is placed
- **THEN** the only recurring work is that widget's 1s timer, and idle CPU stays negligible

#### Scenario: Unchanged widget skips repaint
- **WHEN** a 1-second Clock ticks again within the same displayed minute
- **THEN** `UpdateAsync` runs but `NeedsRender` returns `false` and the widget is not repainted
