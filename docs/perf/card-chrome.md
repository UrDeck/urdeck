# Card chrome: paint cost and underlay pictures (card-chrome explore)

Measured 2026-10-15 for the card-chrome explore (`docs/design/card-chrome.md`), Windows 11, Release build, SkiaSharp
4.153.1, software raster, no panel. The harness is `spikes/card-chrome` on the local branch `spike/card-chrome-paint` (not
for merging); the pictures it wrote are in `spikes/card-chrome/captures` there. Every value is the draft theme's at a cell of
275 px (a 1100 px wide panel); line widths of 2 px or less are rounded to whole pixels, as the design proposes.

## Paint cost per card

One 1100 x 550 surface (a 4x2 card), `SKBitmap` target in the platform colour type. The content is the same in every row: the
text `21:47` at 300 px, 0.091 ms alone. Each number is the median of three runs of 300 paints after 50 to warm up.

| Style | v1 path | Chrome, objects built per size | Chrome, objects built every paint | Back cached as an image per size | Fill at 1/4 size, stretched |
|---|---|---|---|---|---|
| default-dark (v1) | 0.224 | 0.236 | 0.237 | 0.267 | 1.400 |
| glass (v1) | 0.265 | 0.288 | 0.283 | 0.276 | 1.470 |
| Carbon | 0.203 | 0.258 | 0.259 | 0.260 | 1.459 |
| Ember (chamfer, gradient fill and border) | n/a | **1.342** | 1.351 | **0.264** | 1.560 |
| Blueprint (dashed) | 0.177 | 0.239 | 0.241 | 0.214 | 1.358 |
| Obsidian (gradient fill and border, highlight) | 0.213 | **1.347** | 1.385 | **0.310** | 1.570 |
| Paper (highlight) | 0.188 | 0.246 | 0.250 | 0.307 | 1.447 |
| Pop | 0.224 | 0.264 | 0.257 | 0.267 | 1.460 |
| Prism (gradient fill, sheen, two highlights) | 0.309 | **4.236** | 4.242 | **0.462** | 1.721 |
| Rigline (gradient fill, static conic border) | 0.238 | **1.403** | 1.395 | **0.270** | 1.634 |
| Rigline compact (dock slot, cell 195; strip with glow) | 0.227 | 1.399 | 1.525 | 0.271 | 1.648 |

"v1 path" is today's `WidgetPainter` code with the style's fill, radius and border, without the rest of the chrome: the
reference for the budget. The budget is 1.5 times today's paint, here 1.5 x 0.224 = 0.34 ms.

- **A gradient over the whole card costs about 1.1 ms in software, and a radial sheen as much again.** Ember, Obsidian and
  Rigline are four times over the budget, Prism twelve times. The cost is per pixel, not per object: building the paints,
  shaders and paths on every paint costs the same as building them once per size (the third and fourth columns).
- **Cached as an image per (style, size), the back costs 0.26 to 0.31 ms**, inside the budget, except Prism at 0.46 ms: its two
  highlights build two shaders and two clips per paint after the content. They should be cached too; not measured.
- **A solid back is cheaper drawn than blitted** (Paper 0.25 against 0.31 ms, Blueprint 0.24 against 0.21): cache only a back
  that has a gradient fill or a sheen.
- **The fill at a quarter of the size, stretched, is no good**: bilinear scaling in software costs more (1.4 ms) than the gradient.
- The blurred strip of Rigline's compact cards costs 0.13 ms per paint when blurred every time (the last row, 1.525 against
  1.399 ms) on a 140 px strip; cached as an image per size, nothing measurable.
- **Not measured:** the cached back's memory on the panel. By arithmetic it is 4 bytes per pixel per distinct card size of the
  theme: 2.3 MB for a 4x2 card at 1100 px, about 5 MB for the five sizes of the S1 page, and nothing for a v1 theme.

## Underlay pictures

The underlay of one card (shadow, glow, the part of the corner marks outside the card) drawn by the prototype routine into a
picture, as the sprites of S1 would hold it. "Corner zone" is how far into the card along an edge the picture still varies
(the corner's radius or cut plus the blur's falloff and offset); a nine-grid stretches only what lies beyond it, so a card
smaller than twice the zone needs its own picture. Five builds each; the build is the drawing and the PNG encode.

| Style | Bleed | Corner zone | Nine-grid picture | Memory | Build | Smallest card the grid fits | At 1/4 size |
|---|---|---|---|---|---|---|---|
| Carbon (marks) | 8 px | 30 px | 78 x 78 | 0.02 MB | 2.2 ms first, then 0.2 | 62 px | 2 KB |
| Blueprint (marks) | 17 px | 27 px | 90 x 90 | 0.03 MB | 0.3 ms | 56 px | 2 KB |
| Paper (soft shadow) | 95 px | 163 px | 518 x 518 | 1.02 MB | 6.8 ms | **328 px** | 66 KB |
| Pop (hard shadow) | 16 px | 51 px | 136 x 136 | 0.07 MB | 0.5 ms | 104 px | 5 KB |
| Prism (soft shadow) | **193 px** | 267 px | 922 x 922 | **3.24 MB** | **23 ms** | **536 px** | 208 KB |
| Rigline (glow) | 99 px | 139 px | 478 x 478 | 0.87 MB | 6.8 ms | **280 px** | 56 KB |

- **Prism's draft is over both underlay budgets** (2 MB, 20 ms) with a single picture, and its 1x1 cards (250 px) and 2x1 cards
  need pictures of their own on top.
- **A 1x1 card (250 px) is too small for the nine-grid in Paper, Prism and Rigline.** The nine-grid of S1 assumed a picture
  smaller than every card; with soft shadows that does not hold.
- A picture that holds only blurred pieces loses nothing visible at a smaller size (S1: the blurred shadow survived half size);
  at a quarter it is 16 times smaller. Whether the card's own cut-out edge, then soft, shows through a translucent fill was
  not looked at.

## Prism's reach through translucent neighbours

`prism-reach.png` (in the captures): a 4x2, two 2x2 and a 4x2 card in Prism's draft over a violet gradient, three ways. With
sprites (every shadow under every card, each card cut out of its own shadow only) the shadow of the card above shows as a dark
cloud in the top third of the cards below it, plainly, not faintly as in S1 (Prism's reach is 193 px against a 25 px gap; S1's
was 43 against 30). Cutting every card out of every shadow (the page-sized canvas S1 rejected) and limiting the reach to the
gap look almost the same: with a gap of 25 px the shadow shows only in the gaps and below the last card.

## Dashed border on a chamfer

`dash-chamfer.png`: Blueprint's dash (15 px on, 10 px off, 2 px wide) on an Ember chamfer and on Blueprint's square card, with
4x crops of a plain corner and a cut corner. `SKPathEffect.CreateDash` follows the path round the cut corner without a
fault. Unfitted, the period ends wherever the perimeter does, so one corner has a stub or a long gap; fitting the period to
the perimeter (`n = round(length / period)`, both lengths scaled to fill it) removes it at no measurable cost. The diagonal edge
is antialiased (a 2 px line cannot be pixel-snapped at 45 degrees); the straight edges are crisp at a whole-pixel width.
