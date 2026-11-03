## MODIFIED Requirements

### Requirement: WidgetRenderContext
Each widget MUST receive a `WidgetRenderContext` during `Render` containing:

- `SKCanvas Canvas` — SkiaSharp canvas for 2D drawing, sized to the widget's card, already showing the host-drawn card
  and clipped to its shape
- `DateTime Time` — Current timestamp at render time
- `Size PixelSize` — The card's pixel dimensions (width, height); the widget's drawing area starts at `(0, 0)`
- `Theme Theme` — The active theme, with every size already in pixels for this surface (see the `theme` capability)
- `SKRect ContentRect` — The card's area inset by the theme's padding; where content normally goes
- `WidgetConfig Config` — The widget's configuration (concrete type)
- `CancellationToken CancellationToken` — For cooperative cancellation
- `MotionLevel Motion` — The effective motion level (`full`, `reduced` or `off`; see motion, "Effective Motion Level"),
  so a widget or a component can stop motion of its own content. It is `full` where the host has no level to report (a
  preview that does not set one)

The colour-only `ThemeColors` type no longer exists. This is a deliberate breaking change to the SDK: the SDK assembly
version changes with it, and widgets built against the earlier SDK must be rebuilt. The `Motion` member is additive: a
widget that does not read it is unchanged, and the SDK assembly version does not change for it.

#### Scenario: Context is populated at render time
- **WHEN** the runtime calls `Render()`
- **THEN** `WidgetRenderContext` is fully populated with current time, card pixel size, theme, content rectangle,
  config and motion level

#### Scenario: Content rectangle follows the theme
- **WHEN** the theme's padding resolves to 20 pixels and the card is 550 by 275 pixels
- **THEN** `ContentRect` is the rectangle from `(20, 20)` to `(530, 255)`

#### Scenario: Motion level reaches the widget
- **WHEN** the effective motion level is `off` and a widget is painted
- **THEN** the context's motion level is `off`

#### Scenario: Motion changes while running
- **WHEN** the effective motion level changes from `full` to `reduced`
- **THEN** the next paint of each widget carries `reduced`
