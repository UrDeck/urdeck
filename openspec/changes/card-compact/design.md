## Context

After the first three changes a card's look is `CardLook(style, index)`, its chrome is made per size from the resolved style,
and its outside comes from pictures per (style, size class). The host resolves a `Theme` and a style for the page and another
pair for the dock's slot size (`MainWindow.ApplyLayout`). `ThemeCardCompactDefinition` holds `radius`, `borderWidth`, `shape`,
`accentStrip` and `cornerMarks`, sanitised, and is not merged with the page groups. Widgets that echo the card's radius:
`ImageTile`, `Gauge`, the clock's flap display (`CardRadius`). The decision and its evidence: `docs/design/card-chrome.md`,
question 7.

## Goals / Non-Goals

**Goals:**
- Obsidian's circles, Rigline's tile strips and Blueprint's ticks, in the dock and on 1x1 page cards.
- No second SDK `Theme` on a page.

**Non-Goals:**
- A compact padding, compact typography or any compact value format 2 does not have.

## Decisions

### 1. A compact variant of the resolved style

The resolved style resolves its compact variant at the same time (the same cell size), with the compact values applied and
`accentStrip` and `cornerMarks` merged key by key. A style without `card.compact` returns itself as its compact variant, so a
theme without it makes nothing more. `CardLook` gains `Compact`; the layout item's `GridSize` 1x1, or being a dock slot,
selects it. The per-size cache and the pictures key on the variant like any other style.

### 2. Circle as a radius

`circle` is the rounded rectangle with radius half the smaller side; `CardShape` needs no new kind and the round fast
path's code draws it. Cells are square and the dock's slots are square, so the capsule case needs only its information line.

### 3. The widget's theme: the dock's takes the compact values; a page's does not

The dock already has its own `Theme`, all of whose cards are compact; `ThemeResolver` gives it the compact `CardRadius` and
`CardBorderWidth` (half the card for a circle). On a page a second `Theme` for 1x1 cards would resolve the typefaces again
(2.3 MB per family on the panel, `docs/perf/theme-look.md`) or need the SDK to share them, for a few pixels of an icon tile's
corner (Prism: 66 against 74 px). So a 1x1 widget on a page keeps the page's `Theme`.

*Alternative: a public `Theme.With(...)` copy in the SDK.* Rejected: it grows the MIT API with a host concept and makes
typeface ownership shared.

## Risks / Trade-offs

- [A 1x1 page card's icon tile is rounded by the page radius inside a compact card] → invisible in the drafts (Prism 8 px of
  difference, Obsidian's circles are a dock feature); revisit if a theme makes it visible.
- [Carbon's draft has no compact marks, so its dock tiles inherit the page brackets, which the mock does not show] → a
  question for Carbon's theme change, not this one.
