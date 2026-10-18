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

## As built (card-chrome-back)

Measured 2026-10-18 with the engine's own `WidgetPainter` and `ResolvedCardStyle`, the same method as above (one 1100 x 550
surface, `21:47` at 300 px, median of three runs of 300 paints after 50, Windows 11, Release, software raster). This change
draws the shape, the fill (gradient or tint), and the border (solid, dashed, linear or conic gradient); the highlight, sheen,
strip, marks, shadow and glow are not drawn yet, so Carbon, Paper and Prism are cheaper here than they will be.

| Style | Paint (ms) | Against default-dark |
|---|---|---|
| default-dark (v1) | 0.232 | 1.00 |
| glass (v1) | 0.273 | 1.18 |
| Carbon | 0.237 | 1.02 |
| Ember (chamfer, gradient fill and border, back cached) | 0.266 | 1.15 |
| Blueprint (dashed, fitted per side) | 0.241 | 1.04 |
| Obsidian (gradient fill and border, back cached) | 0.274 | 1.18 |
| Paper | 0.197 | 0.85 |
| Pop (tint) | 0.257 | 1.10 |
| Prism (gradient fill, back cached) | 0.299 | 1.29 |
| Rigline (gradient fill, static conic border, back cached) | 0.273 | 1.17 |

- Every draft is within the budget of 1.5 times `default-dark`. A back with a fill gradient is drawn once per (tint, size)
  into an image with its border, and blitted; the border is in the image too, since nothing over it changes between paints.
- The dash is fitted per side, not once to the perimeter: one period fitted to the whole outline puts the joint right but
  leaves the other corners at whatever phase they fall on (on a 4x1 card each corner differs). Each side between two
  corners takes a whole number of periods, scaled by its own factor, and a dash is centred on every corner. Built once per
  size, it costs nothing measurable (Blueprint 0.241 ms).

### Memory of the cached backs, on the panel

Measured 2026-10-18 on the Y70 panel (1100 x 3840, 100 %), the build output's configuration: a 4x2 clock, a 4x4 stats card, a
4x2 weather card and one dock slot. Private memory 20 s after start, three runs each.

| Theme | Private memory (MB) |
|---|---|
| default-dark | 135.8, 133.7, 134.1 |
| Ember without `fillGradient` | 178.7, 180.6, 180.0 |
| Ember | 189.0, 186.1, 190.5 |

- **The back images cost about 8 to 9 MB** here (Ember against the same theme without the gradient), near the arithmetic of
  4 bytes per pixel per distinct size: 2.3 MB for 1076 x 526, 4.6 MB for 1076 x 1076, 0.1 MB for the slot. That is over the
  5 MB the design guessed for the S1 page. **The owner accepted the cost** (2026-10-18): it applies only to a theme with a
  fill gradient, a v1 theme pays nothing, and the compositor fallback (`docs/design/card-chrome.md` 10) stays available if it
  ever matters.
- The rest of Ember's difference from `default-dark` (about 45 MB) is its moving `embers` background and its theme, not the cards.

## As built (card-chrome-front)

Measured 2026-10-18 with the engine's `WidgetPainter` and `ResolvedCardStyle`, the content and surface as above (1100 x 550,
`21:47` at 300 px in the default typeface), Windows 11, Release, software raster. The machine's speed drifted during the runs
(every median rose by about 1.8 times part way through), so the themes were interleaved, nine runs of 300 paints after 50 each,
and the table gives the **fastest** run of each; the ratios of the medians agree within 0.05. "Back only" is the same draft with
`highlight`, `sheen`, `accentStrip` and `cornerMarks` removed: the A/B for this change.

| Style | Paint (ms) | Against default-dark | Back only (ms) |
|---|---|---|---|
| default-dark (v1) | 0.216 | 1.00 | |
| glass (v1) | 0.261 | 1.21 | |
| Carbon (strip with fade, marks; back now an image) | 0.268 | 1.24 | 0.201 |
| Ember | 0.261 | 1.21 | |
| Blueprint (marks wholly outside: nothing on the card) | 0.247 | 1.14 | |
| Obsidian (top highlight) | 0.285 | 1.32 | 0.261 |
| Paper (top highlight) | 0.220 | 1.02 | |
| Pop | 0.257 | 1.19 | |
| **Prism** (sheen in the back image, two highlights) | **0.395** | **1.83** | 0.301 |
| Rigline (page cards: nothing new) | 0.273 | 1.26 | |

- **Prism is over the 1.5 times budget, and the owner accepted it** (2026-10-18). Its back alone is 1.39 times here (1.29 in the
  `card-chrome-back` table, measured on a quieter run), which leaves 0.02 ms for its two highlights; they cost 0.09 ms. The sheen
  costs nothing per paint: it is in the back image. Prism's theme change may look at it again.
- **The highlight is a path made once per size**, drawn after the shape's clip is restored: the outline stroked twice the
  line's thickness and intersected with the shape and the band (`SKPath.Op`). Stroked under the shape's clip on every paint, as
  the design had it, Prism's two cost 0.16 ms (0.463 in all). Splitting each into a rectangle for the straight middle and two
  corner paths gained nothing (0.413).
- Carbon's back is an image now (a strip shades many pixels): 0.067 ms more than its plain back, all of it the strip and the
  marks, within budget. Its memory on the panel is as for a gradient theme (above); not measured.

## As built (card-underlay)

Measured 2026-10-18 on the Y70 panel (1100 x 3840 at 100 %), Release build of the host, a test page of nine cards (a 4x1
clock, a 4x2 weather card, two 1x1 and one 2x1 shortcut, a 4x4 stats card, a 2x2 clock and two more 1x1 shortcuts) and a dock
of two shortcuts. The method is S1's: the same theme with the underlay on and off (a temporary switch, removed), the host's
private memory 20 s after start, five runs of each, interleaved.

| Theme | Pictures (page, then dock) | Private memory, on minus off | First build in the process | Warm build |
|---|---|---|---|---|
| Paper (soft shadow, opaque) | 633x221, 259x259 nine-grid, 221x221, 358x221; 156x156 (all half size) | **+7.7 MB** (188.1 against 180.4) | 36 to 38 ms | 9.6 to 11.2 ms |
| Prism (soft shadow, limited to the gap) | 125x125 nine-grid; 90x90 (half size) | about +4 MB (noisy: the runs without it fell in two groups) | 28 ms | 1.3 ms |
| Rigline (glow, opaque) | 637x225, 239x239 nine-grid, 225x225, 362x225; 160x160 (half size) | about +4 MB (three runs, noisy) | 36 to 38 ms | 11.3 to 12.6 ms |

- **Memory is over the design's 2 MB, and the owner changed the budget to 10 MB** (2026-10-18) for the pictures of a theme, which
  every page shares. Paper's pictures hold about 1.3 MB of pixels; the rest is the decoding path (PNG into a
  `LoadedImageSurface`), which a picture of 125 x 125 pays almost as much of as five larger ones.
- **The alternative was measured and is worse**: a Direct3D 11 device of the host's own, a composition graphics device from it
  (`ICompositorInterop.CreateGraphicsDevice`) and a `CompositionDrawingSurface` per picture written with `UpdateSubresource`
  (no PNG, no image decoder) cost **+30 MB** for Paper and +19 MB for Prism, the same five runs each. The compositor's own
  device is not reachable from WinUI 3, so a second device is the price of that path. Not tried: XAML `Image` elements with a
  `WriteableBitmap` and `Image.NineGrid`, which use the XAML device.
- **Build time is inside the budget** (20 ms) once the process is warm, measured by switching the theme in one process.
  The first build after start is 28 to 38 ms (the first blur, encode and decode), accepted as S1 accepted it.
- **Idle:** with Paper and two ticking clocks for 75 s, the log has one `Underlay (One)` and one `Underlay (Dock)` line.
- The cards smaller than twice the corner zone (Paper's and Rigline's 4x1, 2x1 and 1x1 cards) take exact pictures, as the
  explore found; a picture per row height stretched only sideways would roughly halve Paper's pixels, not the decoding cost.
