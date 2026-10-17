## Context

`WidgetPainter.Paint` draws a filled rounded rectangle and an inside border (`DrawCard`), clips to the rounded rectangle,
lets the widget render, and on an exception wipes the clip and draws the error. `PageRenderer.PaintAt` translates to the
card and calls the same painter on the page canvas without clipping. The SDK `Theme` carries the card's fill, border,
radius, gap and padding in pixels; `ThemeCardDefinition` already holds every format 2 card key, merged and sanitised.
The decisions behind this change, with their evidence, are in `docs/design/card-chrome.md` (questions 1, 2, 4, 8, 9, 10)
and the numbers in `docs/perf/card-chrome.md`. The prototype is `spikes/card-chrome` on the local branch
`spike/card-chrome-paint`.

## Goals / Non-Goals

**Goals:**
- The painter's final shape: a back pass, the content, a front pass, with today's code as the path for a version 1 theme.
- A per-card look (style, index) reaching every card in the window, the dock and the snapshot, so the next three changes
  only add pieces.
- Shape, fill, tint and border of the eight drafts drawn; v1 goldens unchanged.

**Non-Goals:**
- Any front piece, anything outside the card, compact cards (the other three changes).
- A change to the SDK `Theme`.
- The validator's findings (`theme-cli`).

## Decisions

### 1. A resolved card style next to `Theme`, engine-internal

`ResolvedCardStyle` is resolved from the loaded theme for a cell size, as `Theme` is, and lives as long as it (the host
holds one for the page and one for the dock, disposed with their `Theme`). It holds every card value in pixels and colours,
the hairline rounding applied, and `IsPlain`: true when the definition has no format 2 card key. The SDK does not see it.

*Alternative: put the card values on the SDK `Theme`.* Rejected: they are host concepts, and the MIT API would grow with them
(`03-engine-work.md` 1.1).

### 2. `CardLook` per card, `SizedCardChrome` per size

`CardLook(style, index)` is what a card needs beyond its size (the next changes add `compact`). `CardOrder.Assign(layout)`
in `Layout` gives each placed, visible item its index (row, then column); the dock uses slot positions. `PageHost` builds
each `WidgetView` with its look; `PageRenderer` does the same for a snapshot. `SizedCardChrome` holds what depends on the
size: the `CardShape` (an `SKRoundRect`, or an `SKPath` for a chamfer), the paints and shaders, the fitted dash, and the
back image when the back has a gradient. It is cached on the style by (index's tint, size), so cards of one size share it
across pages, and released with the style.

*Alternative: build the objects on each paint.* Measured: it costs the same as building them once (the objects are cheap);
the pixels are the cost, so only the gradient back is an image. Kept the cache anyway because the back image needs one.

### 3. Back, content, front; the fast path is today's code

```
save = CardChrome.DrawBack(canvas, chrome)    // plain: today's DrawCard then ClipRoundRect
                                              // else: clip to the shape, fill (or blit the back image), border
widget.Render(context)
CardChrome.DrawFront(canvas, chrome, save)    // restore; front pieces come in card-chrome-front
```

The plain branch is the body of today's `DrawCard` moved, not rewritten, so a version 1 theme draws the same bytes. The
other branch clips first and fills under the clip, and strokes the border twice as wide under the same clip: the inside
border for any shape. The back image (fill gradient, and in the next change the sheen and the strip) is drawn once per size
with the same calls into an image of the platform's colour type, then blitted unclipped (its edge is already
antialiased), and the clip is set after it. A solid back is drawn directly: blitting it was slower.

### 4. Dash fitted to the perimeter

`SKPathMeasure` gives the shape's length once per size; `n = max(1, round(length / (on + off)))`, both lengths scaled by
`length / (n x (on + off))`. The chamfer path starts on the top edge, so the joint falls on an edge.

### 5. Hairlines in the style's resolution

For a style that is not plain, the border width, and later the highlight, mark and strip thicknesses, are rounded when 2 pixels
or less (`Math.Max(1, Math.Round(px))`), with `loaded.Warnings.Once` under 1 pixel. Not for a plain style: glass's 0.008 cell
is 1.56 pixels in the dock and would move its golden.

### 6. Snapshot clipping and the error card

`PageRenderer.PaintAt` clips to the card's rectangle before painting, as the surface does in the window. On an exception the
painter restores, clears the whole surface (`SKBlendMode.Src`, transparent), and draws back, error text and front.

## Risks / Trade-offs

- [The back image's memory: 4 bytes per pixel per distinct card size in a gradient theme, about 5 MB for a page of five
  sizes] → measured on the panel in this change; if too high, the fallback is the gradient drawn by the compositor under the
  surface (`docs/design/card-chrome.md` 10), which is a change of design, not of the specs.
- [The plain branch drifts from today's code] → the goldens of the v1 themes, and a test that a plain style takes it.
- [Tint index differs between window and snapshot] → one function, called by both, with its own tests.
- [Hairlines at display scalings other than 100 %: a card at a fractional physical position is resampled] → one look on the
  panel at 150 %; recorded, not fixed here.
