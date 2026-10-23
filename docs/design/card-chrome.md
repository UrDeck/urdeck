# Card chrome: design decisions

**2026-10-15**, from the explore of `card-chrome` (`docs/handoff/2026-10-15-card-chrome-explore.md`). One decision per question of the
handoff, with the evidence, what was not verified and what the apply has to measure. The numbers are in `docs/perf/card-chrome.md`;
the throwaway prototype and its pictures are on the local branch `spike/card-chrome-paint` (`spikes/card-chrome`).

Two decisions here change what the package or the spikes said, on new evidence: **the engine limits a shadow or glow's reach to the
gap when the card fill is translucent** (question 6), and **a back with a gradient or a sheen is cached as an image per size**
(question 10). The proposed split into changes is at the end.

## 1. The painter split

**Decision.** `WidgetPainter.Paint` keeps its signature for callers and gains a card look:

```
look  = CardLook(style, index, compact)            // per card; style is a ResolvedCardStyle
chrome = cache.For(look, size)                      // SizedCardChrome: shape, paints, shaders, cached images for this size
save  = CardChrome.DrawBack(canvas, chrome)         // clips to the shape, then fill, sheen, strip, border; the clip stays
widget.Render(context)                              // clipped to the shape, as today
CardChrome.DrawFront(canvas, chrome, save)          // highlight (still clipped), restore, then inside corner marks
```

- **`CardShape`** is a small record `{ SKRoundRect? Round; SKPath? Path; SKRect Bounds }`. `round` and `circle` are an
  `SKRoundRect` (a circle is a radius of half the smaller side: cells are square, so a 1x1 card or a dock slot is square
  within the grid's 1 px rounding, and a non-square card becomes a capsule, logged once at info). Only `chamfer` is an `SKPath`.
- **The v1 fast path**: a `ResolvedCardStyle.IsPlain` (no card chrome key) draws exactly today's `DrawCard` and
  `ClipRoundRect`, and `DrawFront` only restores. That path is the code of today moved, not rewritten; the goldens prove it.
- **The non-plain back clips first and then fills** (`DrawPaint` under the clip), and strokes the border twice as wide under
  the same clip, so the border is inside the edge for any shape, the chamfer included. The prototype draws every draft this way.
- **Engine-internal**: `ResolvedCardStyle` (resolved per cell size, like `Theme`, and disposed with it), `CardLook`,
  `CardShape`, `CardChrome`, `SizedCardChrome`. **The SDK `Theme` gains nothing** (see 7 and 8 for why it does not need to).
- **The error card**: on an exception, restore, clear the whole surface (not only the clip: an inside mark can sit in a
  corner), then `DrawBack`, the error text, `DrawFront`. The error card wears the full chrome, tint included.
- **`PageRenderer.PaintAt` must clip to the card's rectangle.** A live surface ends at the card's edge; the snapshot path
  draws on the page canvas and does not clip today. With marks drawn after the content and outside the shape clip, the two
  would differ. Found in the code (`PageRenderer.PaintAt`), not by a failing picture.

## 2. Where each piece is drawn

| Piece | Where | Note |
|---|---|---|
| shape, fill, `fillGradient`, `tint` | surface, `DrawBack` | gradient fill cached with the back (10) |
| `sheen` | surface, `DrawBack` | cached with the back |
| `accentStrip` and its `glow` | surface, `DrawBack` | the glow is clipped to the card and cached per size |
| border: solid, `dashed`, `borderGradient` linear and conic (static) | surface, `DrawBack` | under the content, as v1 |
| `highlight` | surface, `DrawFront`, clipped to the shape | over the content |
| `cornerMarks`, the part inside the card | surface, `DrawFront`, not clipped to the shape | the surface's own edge clips it |
| `cornerMarks`, the part outside the card | underlay sprite | the underlay clips the card's rectangle away |
| `shadow`, `glow` | underlay sprite | the card's own shape is cut out of them |

- **A mark that sits half in, half out is drawn by both**, from one routine (`CardChrome.DrawMarks(canvas, cardRect, marks)`):
  the surface draws it whole and its edge clips it; the underlay draws it whole with the card's rectangle clipped away. The seam
  is the card's edge, a whole pixel, so nothing is drawn twice. Carbon is the case: `inset` -0.009 and thickness 0.018 put the
  bracket across the card's edge (`gallery.png`); Blueprint's (`inset` -0.045) is wholly outside, and its compact tick wholly inside.
- **`inset` is the distance from the card's edge to the mark's outer edge.** The schema did not say; this reading puts Carbon's
  bracket on the edge as the mock shows it.
- **A snapshot** draws the underlay pass first (every card's bleed, with the same routine as the sprite picture), then each card
  through `WidgetPainter` clipped to its rectangle (1).

## 3. The press effect

**Decision: the sprite follows the card.** `WidgetView.AnimatePress` scales and fades the view's own visual around its centre.
The card's underlay sprite binds its `Scale` and `Opacity` to that visual with an `ExpressionAnimation`, and its `CenterPoint`
is the card's centre in the sprite (the bleed plus half the card). Then the shadow, glow and outside marks shrink and dim with
the card, the band S1 saw goes, and no other code has to know about presses: whatever the view's visual does, the sprite does.
A pressed Pop card shrinks into its hard shadow as one piece.

- Evidence: `WidgetView.AnimatePress` (`src/UrDeck.Host/WidgetView.cs`) animates `Scale` and `Opacity` on
  `ElementCompositionPreview.GetElementVisual(this)`; input finds the widget from the layout (`PagerController.FindView`), so the
  sprite takes no input either way.
- **Not verified**: an expression on the hand-off visual of a XAML element. If it does not run, start the same two key-frame
  animations on the sprite in `AnimatePress` (S1 item 6 says the sprite is a visual and can take them).

## 4. Dashed borders on a chamfer

**Decision: it is a stroke inside the surface, with the dash period fitted to the perimeter.** `SKPathEffect.CreateDash` follows
the chamfer round the cut corner cleanly (`dash-chamfer.png`). Unfitted, the dash ends wherever the perimeter does, which leaves
one odd corner; `n = round(length / (on + off))` with both lengths scaled to fill the perimeter exactly removes it. The path starts
on the top edge so the joint falls on an edge, not a corner. Hairlines: a 2 px dash is crisp on the straight edges and
antialiased on the 45-degree cut, which cannot be helped. No draft uses a dashed chamfer (Blueprint is square); it works anyway.
`dotted` (in `02`) is not a sanitized value today (`solid`, `dashed`, `none`) and stays out.

## 5. The dock

**Decision: the dock's `PageHost` gets the same underlay, from the dock's own resolve, clipped to the dock's band.**

- The dock already resolves a second `Theme` for the slot's side (`MainWindow.ApplyLayout`, `DockLayout.SlotSide`), so it
  resolves its `ResolvedCardStyle` the same way, always compact.
- The slots are equal squares (`DockLayout.Compute`), so the dock needs **one picture at the slot's exact size**, no nine-grid,
  and Obsidian's circle is no harder than a rounded square.
- **Clip the dock's underlay to the dock's band.** The dock is in the overlay, above the page: a glow of Rigline's reach (99 px
  at a cell, 70 px at a slot) would otherwise lie over the bottom row's content. In a snapshot the dock's bleed is drawn
  clipped to the band as well. The band's own margins (half a gap round each slot) are what the dock's shadow can use.
- Tint and the accent strip's colours follow the slot's position (8). The Pop mock agrees: its four dock tiles are the first
  four `cardTints` in order.

## 6. Translucent cards and reach

**Decision (changed): the engine limits the reach to the gap when the card fill is translucent, and the validator warns.**
Translucent means any of `cardFill`, a `fillGradient` stop or a `cardTints` entry has alpha under 255. The shadow's offset and
blur (and the glow's blur) are scaled down together until the bleed equals the gap, logged once at info.

- Evidence, a picture: `prism-reach.png`. With sprites, Prism's shadow (193 px of reach against a 25 px gap) shows as a
  dark cloud through the top third of every card below another. That is not the faint band S1 saw at 43 px.
  Cutting every card out of every shadow (the rejected page canvas) and the reach limited to the gap look almost the same.
- Evidence, budgets: unlimited, Prism's picture is 922 x 922 px, 3.2 MB and 23 ms to build, over both underlay budgets, and
  its 1x1 cards need pictures of their own (10). Limited, it is a few kilobytes.
- An opaque theme keeps its full reach (Paper 95 px, Rigline 99 px): the neighbour hides it.
- **For the owner to look at**: the three panels of `prism-reach.png`. The question is whether Prism's limited shadow (only in
  the gaps and below the last card) is still the look, or whether the theme change should give Prism a larger gap.

## 7. Compact cards

**Decision.**

- **Detection**: a card is compact when it is a dock slot, or a page card whose grid size is 1x1 (`WidgetLayoutItem.GridSize`;
  1x1 is the smallest size, so "one cell or less" is exactly 1x1).
- **What applies**: `card.compact` overrides `radius`, `borderWidth`, `shape` (`circle` included), `accentStrip` and
  `cornerMarks`, as `ThemeCardCompactDefinition` has them. **There is no compact `padding`** (`02` listed one; the first draft did not
  build it), so a compact card's content rectangle is the page's.
- **What the widget sees**: the page's `Theme` on a 1x1 page card, and the dock's `Theme` in a slot. The dock's `Theme` takes the
  compact `radius` and `borderWidth` (with `circle`, `CardRadius` is half the slot), so `ImageTile` and `Gauge`, which echo
  `CardRadius`, round a dock icon the way its card is rounded. A 1x1 card on a page keeps the page's `CardRadius` in its `Theme`;
  only its chrome is compact. **No second SDK `Theme`**: it would resolve the typefaces again (2.3 MB per family on the panel,
  `theme-look.md`) or need the SDK to share them, for a difference of a few pixels in an icon tile's corner (Prism: 66 against 74 px).
- **`circle` on a card that is not square**: a capsule (radius half the smaller side) with one info line. Cells are square, so
  it happens only through the grid's 1 px rounding, where it cannot be seen.
- A merge question for the apply: `compact.cornerMarks` is a partial override of `card.cornerMarks` (Blueprint's compact tick
  gives only `style`, `corners`, `size` and `inset`; its thickness and colour come from the page's marks).
- The Carbon mock shows no marks on the dock tiles, but the draft has no `compact.cornerMarks`, so they inherit the page's. A
  question for Carbon's theme change, not for the engine.

## 8. Tint and index

**Decision.**

- **The index** is the card's place among the page's placed, visible cards, row by row and then column by column of their grid
  position, from 0 on every page. A hidden widget or one that is not placed takes no index; a card moved to another page takes
  its place there. Row-major is what the eye reads and does not depend on the order of the configuration's list, which a
  settings editor may reorder. One engine function computes it from the layout (`CardOrder`, next to `GridLayoutManager`), and
  `PageHost` and `PageRenderer` both call it.
- **Dock slots** take their slot's position (0 to 3), whether or not a neighbour slot is empty.
- **The same index** cycles `accentStrip.colors`.
- **Text stays `Theme.Text`.** `02` asked for `colors.ink`, or the more contrasting of text and background, on a tinted card;
  that needs a `Theme` per card. Pop, the only draft with a tint, has `ink` equal to `text` (`#111111`), so nothing is lost. The
  validator (theme CLI) checks the text's contrast against every `cardTints` entry instead.
- Not adopted: a colouring that keeps neighbours apart. The cycle can put two equal tints side by side; the mock's page was
  coloured by hand (its clock is the fifth tint, its weather card white), which no rule reproduces. A per-widget tint would be a
  configuration change, not chrome.

## 9. Hairline snapping

**Decision: in the card style's resolution, and only off the v1 fast path.** Line widths of 2 px or less after resolution
(`borderWidth` of a non-plain style, `highlight.thickness`, `cornerMarks.thickness`, `accentStrip.thickness`) are rounded to whole
pixels, never below 1; one warning per key and theme when one resolves under 1 px (`loaded.Warnings.Once`).

- **It must not touch a v1 theme**: glass's `borderWidth` 0.008 is 2.2 px on the 1100 px page but 1.56 px in the dock (cell 195) and
  1.6 px on an 800 px panel. Rounding it would move the dock golden. So the plain style keeps today's unrounded width, as the
  fast path requires.
- Pixels are physical: the host resolves at `width x scale / 4` (`MainWindow.ApplyLayout`). The inside stroke (deflate by half the
  width, or the doubled stroke under the clip) puts a whole-pixel line on whole pixels when the surface's origin is on one.
- **Not verified**: at a display scaling other than 100 % a card's position in DIPs can land between physical pixels, and then
  every line of its surface is resampled. S1 did not look at other scalings either. Worth one look during the apply.

## 10. Cost of the drawn pieces

**Decision: cache by pixels, not by objects.** The prototype shows that building paints, shaders and paths on every paint costs
nothing measurable; filling the card's pixels with a shader is what costs (`docs/perf/card-chrome.md`).

- **A back with a `fillGradient` or a `sheen` is drawn once per (style, size) into an image and blitted**: Ember 1.34 to 0.26 ms,
  Obsidian 1.35 to 0.31, Rigline 1.40 to 0.27, Prism 4.24 to 0.46 (budget 0.34 on that machine). A solid back is drawn directly
  (blitting it is slower). The cache belongs to the page's resolved style, so cards of one size share an image across pages.
- **Prism's highlights** must be cached too (two shaders and clips after the content): 0.46 ms is over the budget. Not measured.
- **Blurred pieces inside the surface** (Rigline's compact strip glow) are an image per size.
- **Underlay pictures**: one nine-grid picture per (style, compact) when every card is at least twice the corner zone, and an exact
  picture per card size for the cards that are not. With the reach limit (6) the worst opaque case is Paper: a 1.0 MB nine-grid
  plus about 0.8 MB for a 1x1 card's own picture, 7 ms each. **A picture with only blurred pieces is made at half size**, which
  quarters that. Keep pictures per (theme, size), not per page, so a page after the first shows its shadows with its cards (S1).
- **To measure in the apply**: the cached backs' memory on the panel (by arithmetic about 5 MB for the S1 page in a gradient theme,
  nothing in a v1 theme; if it is too much, the fallback is the fill drawn by the compositor with a `CompositionLinearGradientBrush`
  under the surface); the underlay pictures' memory and build time for Paper and Rigline on the panel; Prism's paint with cached
  highlights; the v1 no-regression check (±2 % CPU, ±1 MB).

## 11. The split

The handoff's cut holds, with the inside work split in two, the dock's underlay moved into the underlay change, and compact cards
last. Each leaves `main` working and the v1 goldens unchanged; each adds goldens for the drafts it makes drawable (the fixtures
are in `tests/UrDeck.Engine.Tests/Fixtures/themes/`, rendered with the golden harness).

| # | Change | Scope | Depends on | Model |
|---|---|---|---|---|
| 1 | `card-chrome-back` | `ResolvedCardStyle` and its resolution (hairlines, translucency, the style's fast-path flag), `CardShape` (round, chamfer, circle as a radius), `CardLook` and `CardOrder`, `DrawBack`/`DrawFront` with the fast path, the per-size cache with cached backs, `PageRenderer` clipping each card to its rectangle, the error card. Pieces: `shape`/`cut`/`cutCorners`, `fillGradient`, `tint`, border solid, `dashed` (fitted) and `borderGradient` linear and conic. Makes Ember, Blueprint's border and Pop's tint drawable. Spec deltas: `widget-card` ("Card Shape Is Page-Wide" names the chamfer and the compact exception; "Same Card Everywhere" covers the marks), `theme`. | none | **Opus**: the split, the cache, the per-card look through `PageHost`, `WidgetView` and `PageRenderer`, and the fast path are the architecture |
| 2 | `card-chrome-front` | `highlight`, `sheen`, `accentStrip` (fade, colours by index, inside glow cached), corner marks inside the card (the shared `DrawMarks`, `inset` as defined in 2), cached highlights (Prism's paint). Makes Obsidian, Carbon and Paper's highlight drawable. | 1 | **Sonnet**: additive pieces into the shape 1 sets, against goldens |
| 3 | `card-underlay` | Sprites under the cards in `PageHost` (shadow soft and hard, glow, outside marks), the shared routine with the snapshot pass, pictures per (theme, size), nine-grid or exact size, half size for blurred-only pictures, the reach limit for translucent fills, the press binding, the dock's underlay clipped to its band. Measured on the panel (memory, build, press, slide). Makes Paper, Pop, Prism and Rigline's shadow and glow, and Blueprint's marks drawable. | 1 (and 2 for `DrawMarks`) | **Opus**: compositor work, measurement on the panel, two paths kept alike |
| 4 | `card-compact` | Compact detection, the overrides (`radius`, `borderWidth`, `shape` circle, `accentStrip`, `cornerMarks` as a partial merge), the dock's `Theme` taking the compact radius, the compact underlay picture, tint and strip colours by slot. Makes Obsidian's dock, Rigline's tiles and Blueprint's ticks drawable. | 1, 2, 3 | **Sonnet**: rules settled here, against goldens and one look at the dock |

Outline of tasks common to all four: the spec delta, the engine code with unit tests (shape areas for a chamfer, the index
order, hairline rounding with the warning, the reach limit, the fast path's flag), goldens for the drafts concerned, a paint
benchmark row added to `docs/perf/card-chrome.md`, the docs (`docs/themes.md` and the site's theme page, as `AGENTS.md` asks), and
for 3 and 4 a look at the panel with the owner.

Not in these changes: `borderAnimate` (`motion`), `pattern` (`page-pattern`), the validator's findings (`theme-cli`: reach beyond the
gap, contrast against each tint, `|inset| + thickness` against half the gap), and moving the drafts to built-ins (one theme change
per pair, as `STATUS.md` orders them).

## Not verified, in one place

- The press expression on a hand-off visual (3).
- Hairlines and the sprites at display scalings other than 100 % (9).
- The cached backs' and the pictures' memory on the panel (10).
- Whether a half-size picture's soft cut-out edge shows through a translucent card (10).
- Prism with its highlights cached (10).
- Nothing was run in the live host: every number here is the engine's Skia path in one process, and every picture a snapshot.
