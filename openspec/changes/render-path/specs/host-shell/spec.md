## REMOVED Requirements

### Requirement: WPF Host Window
**Reason**: The host no longer uses WPF. The window requirement is restated without naming a UI framework and gains an
exact content-area cover and recovery after sleep.
**Migration**: See the added requirement "Host Window". Monitor selection, Escape to close and normal Z-order are
unchanged.

### Requirement: SkiaSharp Element Integration
**Reason**: Widget surfaces are no longer WPF elements rasterized into a WPF bitmap and composed in software.
**Migration**: See the added requirements "Widget Surfaces" and "GPU Composition". Widgets are unaffected: they still
receive an `SKCanvas` for their own surface with the card already drawn.

## ADDED Requirements

### Requirement: Host Window
The host application MUST provide a borderless window that:

- Covers the target monitor's full physical bounds (not the work area) with its content area exactly: no window border
  or frame is visible inside the monitor's bounds
- Cannot be resized or moved by the user
- Is process-wide PerMonitorV2 DPI aware; monitors are enumerated in physical pixels
- Re-applies the exact bounds when the monitor's scaling changes or the window moves to a monitor with different scaling
- Sits in normal Z-order (does not use topmost) and does NOT use window transparency
- Closes on Escape

#### Scenario: Content area covers the full monitor
- **WHEN** the host starts on a 1100×3840 target monitor
- **THEN** the window's content area is 1100×3840 physical pixels at the monitor's origin
- **THEN** a mismatch between the content area and the target bounds is logged as a warning

#### Scenario: Window survives display changes
- **WHEN** display settings change, or the machine resumes from sleep
- **THEN** the host re-selects the target monitor and re-covers it, re-checking after 1.5s and again after 5s because
  monitors wake in arbitrary order

#### Scenario: Page returns after sleep
- **WHEN** the machine resumes from sleep, or the graphics device is reset
- **THEN** the window background and every widget are shown again without restarting the host

### Requirement: Widget Surfaces
The host MUST give every widget its own drawing surface:

- One surface per widget, positioned at the widget's card rectangle (see grid-layout); there is NO page-level drawing
  surface
- The surface is rendered at the monitor's physical resolution
- The host draws the widget's card on the surface before the widget renders (see widget-card)
- Pixels outside the card's rounded shape are transparent, and a card fill with an alpha below 255 is blended with
  whatever is behind the surface
- Widgets MUST NOT draw outside their assigned surface
- A surface is redrawn only when its own widget is repainted
- A widget whose `Render` throws is drawn as a themed error card and the failure is logged; other widgets are unaffected

#### Scenario: Widget renders to its own surface
- **WHEN** a widget's `Render()` is called
- **THEN** the `Canvas` in `WidgetRenderContext` is that widget's own surface, already showing its card, and
  `PixelSize` is the surface's pixel size

#### Scenario: Widget render throws
- **WHEN** a widget throws inside `Render()`
- **THEN** an error card with the widget name and message is drawn in its place and the exception is logged

#### Scenario: Translucent card
- **WHEN** the active theme's card fill is partly transparent
- **THEN** the window background is visible through the card and around its rounded corners

#### Scenario: One widget repaints
- **WHEN** one widget on a page of several is repainted
- **THEN** the other widgets' `Render()` is not called

### Requirement: GPU Composition
The host window MUST be composited by the GPU from independent layers: the window background and one layer per widget
surface. The host MUST NOT produce frames while no layer changes, and MUST NOT run a continuous render loop.

#### Scenario: Still page
- **WHEN** a page is shown and no widget is repainted
- **THEN** no widget surface is redrawn and the host's GPU use is not measurably above zero

#### Scenario: Layer changes
- **WHEN** one widget is repainted
- **THEN** only that widget's layer is updated and the page is composited again

## MODIFIED Requirements

### Requirement: Widget Refresh Scheduling
The host MUST drive each widget from its own refresh policy, with no global render loop:

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
