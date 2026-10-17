## Why

Small cards look different in three drafts: Obsidian's dock buttons are circles, Rigline's tiles light a strip along their
bottom edge, and Blueprint's tiles carry a small mark instead of the page cards' brackets. Format 2 holds this as
`card.compact`, a partial override of the card values for cards of one grid cell and for dock slots, and nothing applies it.
This is the last of the four card chrome changes (`docs/design/card-chrome.md`, question 7).

## What Changes

- **Which cards are compact.** Every dock slot, and every page card whose grid size is 1x1.
- **What applies.** `compact.radius`, `compact.borderWidth`, `compact.shape` (`round`, `chamfer` or `circle`),
  `compact.accentStrip` and `compact.cornerMarks` replace the page values for compact cards; a value the compact override
  does not set comes from the page card. `compact.cornerMarks` and `compact.accentStrip` are merged key by key over the
  page's (Blueprint's tick gives its style, corners, size and inset; thickness and colour come from the page's marks).
- **Circle.** A compact `circle` is a rounded card with a radius of half its smaller side: a circle on a square card. A card
  that is not square becomes a capsule, with one information line. Cells are square, so it happens only through rounding.
- **What a widget sees.** In the dock the widget's theme carries the compact corner radius and border width, so an icon
  tile echoes a circle; on a page a 1x1 widget keeps the page's values (only its chrome is compact). Padding is not
  overridden (format 2 has no compact padding).
- **Everything else follows.** The underlay of a compact card is made from its compact chrome; tint and strip colours count
  dock slots by position (as `card-chrome-back` set).
- The "Card Shape Is Page-Wide" requirement names the compact exception.

Not in this change: a compact padding, a validator finding for compact values (`theme-cli`).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `widget-card`: Card Shape Is Page-Wide allows compact cards their own shape; a new Compact Cards requirement.

## Impact

- `src/UrDeck.Engine/Themes`: the resolved style gains its compact variant (merged partial overrides, circle).
- `src/UrDeck.Engine/Rendering` and `Layout`: compact detection from the layout item; `CardLook` carries it; compact pictures
  for the underlay.
- `src/UrDeck.Host`: the dock's `Theme` takes the compact radius and border width; `PageHost` passes compact looks.
- `tests/UrDeck.Engine.Tests`: unit tests and goldens of the dock page; v1 goldens unchanged.
- `docs/themes.md`, the site's theme page.
- Depends on `card-chrome-back`, `card-chrome-front` and `card-underlay`.
