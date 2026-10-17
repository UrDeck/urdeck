## Context

`PageHost` is a XAML `Canvas` of `WidgetView`s, one per card, each an `SKXamlCanvas` exactly the card's size; the dock is a
second `PageHost` in the window's overlay, built from a theme resolved for the slot's side. `WidgetView.AnimatePress` animates
`Scale` and `Opacity` on the view's own composition visual. `PageRenderer` draws a snapshot on one canvas. The S1 prototype
(`src/UrDeck.Host/PageUnderlay.cs` on the local branch `spike/theme-look-s1`) put an empty `Canvas` first in `PageHost`, with
one `SpriteVisual` per card painted by a `CompositionNineGridBrush` over one small Skia picture made into a
`LoadedImageSurface`; it measured +1.2 MB and 1.3 ms. The card-chrome explore (`docs/design/card-chrome.md`, questions 2, 3,
5, 6, 10; `docs/perf/card-chrome.md`) found that soft shadows make pictures larger than every 1x1 card, and Prism's draft
larger than the budget.

## Goals / Non-Goals

**Goals:**
- One routine draws a card's outside (shadow, glow, the outside part of its marks, its own shape cut out); the sprite
  picture and the snapshot both use it.
- Inside the budget for every draft: 20 ms, 2 MB per page.
- Press, slide and input behave as without it (S1 showed it for slide and input).

**Non-Goals:**
- Cutting every card out of every shadow (the page-sized canvas S1 rejected at +33 MB); the reach limit makes it unnecessary.
- Animated glows or borders (`motion`).

## Decisions

### 1. Pictures: nine-grid when the card is big enough, exact size when it is not

A picture holds one card's outside around a card of a chosen size. Its *corner zone* is how far into the card, along an
edge, the picture still varies: the corner's radius or cut, plus 1.5 times the blur, plus the larger offset and a positive
spread (for marks, their size and thickness). Insets of bleed plus corner zone let a `CompositionNineGridBrush` stretch it
around any card at least twice the zone in both directions. A card smaller than that gets a picture at its exact size, which
needs no stretching. Pictures are kept per (resolved style, compact, size class) on the page's style, so every page after the
first finds them made, and the picture arrives with the cards instead of a frame later (S1).

*Alternative: always one nine-grid picture.* Rejected: Paper's zone is 163 px and Rigline's 139 px; a 1x1 card is 250 px.
*Alternative: a picture per card.* Rejected: memory grows with the page.

### 2. Blurred-only pictures at half size

A picture whose pieces are all blurred (Paper's and Prism's shadows, Rigline's glow) is made at half the size in each
direction and stretched by the compositor, a quarter of the memory. One with a hard shadow or marks is made at full size
(S1: those need it). Whether the card's cut-out edge, then soft, shows through a translucent card is looked at on the panel;
if it does, Prism's picture is made at full size (its limited reach keeps it small).

### 3. The reach limit is in the resolved style

The resolved style knows the fills, so it decides translucency (any card fill, gradient stop or tint with alpha under 255) and
scales the shadow's offset and blur and the glow's blur by `gap / reach` when the reach exceeds the gap; logged once at
information level per loaded theme. The routine never sees the unlimited values, so window and snapshot agree.

### 4. The press binds, it does not copy

Each card's sprite gets an `ExpressionAnimation` on `Scale` and on `Opacity` that reads the view's visual (`ElementCompositionPreview.GetElementVisual(view)`),
and its `CenterPoint` is the card's centre in the sprite (bleed plus half the card). `WidgetView` does not change. If an
expression on the hand-off visual does not run (not verified), `WidgetView.AnimatePress` starts the same two key-frame
animations on its sprite instead; the spec is the same either way.

### 5. The dock's underlay is clipped to its band

The dock's `PageHost` gets its own underlay from the dock's style. Its slots are equal squares, so one exact-size picture
serves them. The underlay element has a clip (`Canvas.Clip` or the container visual's `Clip`) of the dock's band, and the
snapshot clips the dock's underlay pass to the band too. Without it Rigline's glow (70 px at the dock's size) would lie over
the bottom row's content, because the dock is above the page.

### 6. The snapshot pass

`PageRenderer.Render` draws, after the background and before the cards, every card's outside with the same routine (no
nine-grid: it draws at each card's size), then the dock's clipped to its band.

## Risks / Trade-offs

- [Display scalings other than 100 %: S1 set the nine-grid's inset scales for them and never looked] → one look at 150 % on
  the panel during this change.
- [`LoadedImageSurface` decodes asynchronously; a shadow can appear a frame after its card] → pictures kept per theme and
  size (decision 1); if it still shows at startup, use a `CompositionDrawingSurface` filled from the Skia pixels instead.
- [The limited reach changes Prism's look against its mock] → the owner agreed to the limit (the picture is
  `prism-reach.png`); Prism's theme change may still widen its gap.
- [The first build in a process over 20 ms (S1: 22 to 34 ms, the first blur and encode)] → accepted for the first
  page at startup, as S1 did; measured again here.
