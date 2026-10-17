## Why

Format 2 lets a theme give its cards a chamfered shape, a gradient fill, a colour per card and a dashed or gradient border
(`card.shape`, `cut`, `cutCorners`, `fillGradient`, `tint` with `colors.cardTints`, `borderStyle`, `borderDash`,
`borderGradient`). The values are read, checked and kept, and nothing draws them: every card is still a filled rounded
rectangle. Ember, Blueprint, Obsidian, Pop, Prism and Rigline depend on them. This change is the first of four that draw
card chrome (`docs/design/card-chrome.md`): it sets the painter's shape (a back pass before the widget, a front pass after
it), the per-card look and the caches the other three build on.

## What Changes

- **Painter split.** The card is drawn in two passes around the widget's content, a back pass (shape, fill, border) and a
  front pass (empty in this change; the front pieces come with `card-chrome-front`). A theme that sets none of the card keys
  of format 2 takes today's drawing unchanged, byte for byte.
- **Shape.** `card.shape` `chamfer` cuts the corners named by `cutCorners` (default top-right and bottom-left) by `cut`,
  limited to half the smaller side; the other corners are square. `round` is today's rounded rectangle.
- **Fill.** `fillGradient` replaces the card fill with a linear gradient at its angle. `tint: cycle` fills each card with
  `colors.cardTints` in turn, by the card's place on its page (row by row, then column by column, placed and visible cards
  only, from the first on every page) or by its dock slot's position; the text colour stays the theme's.
- **Border.** `borderStyle` `dashed` with `borderDash`, its period fitted to the card's perimeter so no corner is odd;
  `borderGradient` linear (at its angle) or conic (round the card's centre), static; all inside the card's edge as today.
- **Hairlines.** A line width of 2 pixels or less in a format 2 card style is drawn at a whole number of pixels, never
  under 1, with one warning per key when it resolves under 1 pixel. A version 1 theme's border width is not rounded.
- **Cost.** The repaint of a card stays within 1.5 times today's: a back with a gradient is drawn once per card size and
  reused (a gradient over a card costs about 1.1 ms in software against 0.26 ms reused, `docs/perf/card-chrome.md`).
- **Snapshot.** Each card of a snapshot is clipped to its own rectangle, as a live card is by its surface, so the two stay
  alike once chrome is drawn after the content.
- **Error card.** A widget that fails is shown on the card with its full chrome.

Not in this change: `highlight`, `sheen`, `accentStrip`, corner marks (`card-chrome-front`); `shadow`, `glow` and anything
outside the card (`card-underlay`); `compact` (`card-compact`); `borderAnimate` (`motion`); the validator's findings
(`theme-cli`); moving the drafts to built-in themes (one theme change per pair).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `widget-card`: the card's shape, fill and border follow the format 2 card values; the snapshot matches the window with
  them; the error card wears the chrome; hairlines; the repaint cost; version 1 cards unchanged.
- `theme`: the "Card chrome values" scenario of Format 2 Values no longer says nothing is drawn.

## Impact

- `src/UrDeck.Engine/Rendering`: `WidgetPainter` (the split and fast path), new `CardChrome`, `CardShape`, a per-size
  chrome cache; `PageRenderer` (card look per card, clip per card).
- `src/UrDeck.Engine/Themes`: a resolved card style beside `ThemeResolver` (pixels, hairlines, the fast-path flag).
- `src/UrDeck.Engine/Layout`: the card order for `tint`.
- `src/UrDeck.Host`: `PageHost` and `WidgetView` pass each card's look; the dock passes slot positions.
- The SDK does not change.
- `tests/UrDeck.Engine.Tests`: unit tests, goldens for the fixture themes this makes drawable; the v1 goldens unchanged.
- `docs/themes.md`, the site's theme page, `docs/perf/card-chrome.md`.
