## Why

Paper, Pop and Prism cast shadows (`card.shadow`, soft or a hard offset), Rigline glows (`card.glow`), and Blueprint's and
Carbon's corner marks sit partly or wholly outside the card. A widget's surface is exactly its card, so nothing outside the
card can be drawn today. Spike S1 decided where it goes: a compositor sprite per card under the widgets, stretching a small
Skia picture (`docs/perf/theme-look.md`, "S1 page underlay"). The card-chrome explore settled the rest
(`docs/design/card-chrome.md`, questions 2, 3, 5, 6, 10): the press, the dock, translucent cards and the pictures' sizes. This
is the third of the four card chrome changes.

## What Changes

- **Underlay.** Under the cards of every page and of the dock, the host draws each card's shadow, glow and the part of its
  corner marks outside the card. It is drawn under every card, so it never covers a card's content, and each card's own
  shape is cut out of its own shadow and glow, so a translucent card does not show them through its fill.
- **Shadow and glow.** `shadow` (offset `x`, `y`, `blur`, `spread`, `color`; `blur` 0 is a hard offset, Pop) and `glow`
  (`blur`, `color`, `alpha`; the colour defaults to the border colour) follow the card's shape.
- **Translucent cards.** When a card's fill is translucent, the reach of its shadow and glow is limited to the gap between
  cards, so a neighbour's shadow cannot show through it (Prism's draft: 193 px of reach against a 25 px gap showed as a cloud
  through the cards below, `prism-reach.png`). One information line says so. Opaque themes keep their full reach.
- **Press.** A pressed card's shadow, glow and outside marks shrink and dim with the card.
- **Dock.** The dock's cards get the same underlay, from the dock's own size, kept inside the dock's band so it never lies
  over the page.
- **Snapshot.** A snapshot draws the same underlay with the same routine before the cards.
- **Cost.** Made when the layout or the theme changes, never per repaint; at most 20 ms to make and 2 MB per page.

Not in this change: compact cards' own chrome (`card-compact`), the animated border (`motion`), `pattern` (`page-pattern`), the
validator's findings (`theme-cli`).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `widget-card`: new requirements for what is drawn outside the card, the reach on translucent cards, and its cost.
- `widget-input`: Press Feedback carries what is drawn outside the card with the card.
- `dock`: the dock's cards have what is drawn outside them, inside the dock's band.

## Impact

- `src/UrDeck.Engine/Rendering`: the underlay routine shared by the host and `PageRenderer`; picture making (nine-grid
  insets, exact-size pictures, half size for blurred-only pictures); the reach limit in the resolved style.
- `src/UrDeck.Host`: a new underlay element in `PageHost` (one `SpriteVisual` per card, a `CompositionNineGridBrush` on a
  `LoadedImageSurface` or a surface from the picture), the press binding in `WidgetView`, the dock's underlay clipped to its band.
- `tests/UrDeck.Engine.Tests`: unit tests, goldens with shadows and glows; v1 goldens unchanged.
- `docs/perf/card-chrome.md` (memory, build time, press and slide on the panel), `docs/themes.md`, the site's theme page.
- Depends on `card-chrome-back` and `card-chrome-front` (the mark routine).
