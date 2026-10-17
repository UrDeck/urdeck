## Why

After `card-chrome-back` a card has its shape, fill and border, but not the light and marks that make Obsidian, Prism,
Paper, Carbon and Rigline look like their mocks: the rim light along the top and bottom edges (`card.highlight`), the soft
radial light inside the card (`sheen`), the lit line along one edge (`accentStrip`, Carbon's scan line and Rigline's tile
strips) and the registration marks at the corners (`cornerMarks`, Carbon and Blueprint). Two of them must be drawn over the
widget's content so the content can never hide them. This is the second of the four card chrome changes
(`docs/design/card-chrome.md`).

## What Changes

- **Highlight.** `highlight.top` and `highlight.bottom` draw a line of `thickness` just inside the top or bottom edge,
  following the corners, fading out over the first and last eighth of the card's width, over the content.
- **Sheen.** `sheen` draws a radial light centred at `x`, `y` (fractions of the card) of radius `size` times the card's
  width, from its colour to transparent, over the fill and under the content.
- **Accent strip.** `accentStrip` draws a line of `thickness` along `side`, `length` of the side centred, in its colours by
  the card's index (`card-chrome-back`'s order), optionally fading along its length (`fade`) and with a glow inside the card
  (`glow`), under the content.
- **Corner marks.** `cornerMarks` draws `bracket` (an L along both edges) or `tick` marks at the named corners, `size` long,
  `thickness` wide, `inset` from the card's edge to the mark's outer edge, over the content. This change draws the part of a
  mark that lies on the card; the part outside the card comes with `card-underlay`.
- **Cost.** Within the 1.5 times budget of `card-chrome-back`: the sheen and the strip join the back image; the highlights
  and the strip's glow are made once per card size (Prism measured 0.46 ms with highlights made per repaint, against 0.34).

Not in this change: anything outside the card (`card-underlay`), `compact.accentStrip` and `compact.cornerMarks`
(`card-compact`).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `widget-card`: new requirements for edge light and sheen, the accent strip and corner marks on the card.

## Impact

- `src/UrDeck.Engine/Rendering`: `CardChrome` (the front pass's pieces, the sheen and strip in the back), the per-size cache.
- `src/UrDeck.Engine/Themes`: the resolved style gains the highlight, sheen, strip and mark values (hairlines rounded).
- `tests/UrDeck.Engine.Tests`: unit tests and goldens; v1 goldens unchanged.
- `docs/themes.md`, the site's theme page, `docs/perf/card-chrome.md`.
- Depends on `card-chrome-back`.
