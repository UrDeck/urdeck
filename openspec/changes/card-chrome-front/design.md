## Context

`card-chrome-back` gives the painter its back pass, front pass, per-card look, per-size cache and the back image for
gradients. This change adds pieces to that shape. Evidence and decisions: `docs/design/card-chrome.md` (questions 1, 2, 10);
the prototype draws every piece (`spikes/card-chrome`, branch `spike/card-chrome-paint`, `Chrome.Front` and `Chrome.Back`).

## Goals / Non-Goals

**Goals:**
- The four pieces drawn as the mocks show them, within the repaint budget.
- One routine for corner marks that `card-underlay` reuses for the part outside the card.

**Non-Goals:**
- The outside part of a mark, shadows, glows (`card-underlay`); compact overrides (`card-compact`).

## Decisions

### 1. Where each piece goes

| Piece | Pass | Cached |
|---|---|---|
| sheen | back, after the fill, clipped | in the back image |
| accent strip, its glow | back, after the sheen, clipped | in the back image (the glow's blur is the costly part) |
| highlight | front, still clipped to the shape | its shaders per size; the line itself is cheap |
| corner marks (on the card) | front, after the clip is restored | no: a few lines |

A style with a sheen or a strip has a back image even with a solid fill, because both shade many pixels or blur. The strip's
colour depends on the index, so the back image is keyed by (tint, strip colour, size), which `card-chrome-back`'s cache key
grows to include.

*Alternative: the strip in the front pass.* Rejected: the mocks show content (Rigline's tile icons, Carbon's bars) over
the strip, and the spec puts it under the content.

### 2. The highlight as a clipped stroke

The highlight strokes the card's shape twice its thickness wide, clipped to the shape (so half lies inside) and to a band at
the top or the bottom as tall as the corner's radius or cut plus the thickness, with a horizontal gradient (transparent,
colour at 12 %, colour at 88 %, transparent). This follows the corner without building an arc path. The two shaders are made
per size: Prism's paint with them made per repaint was 0.46 ms against a budget of 0.34.

### 3. One mark routine, the edge as the seam

`CardChrome.DrawMarks(canvas, cardRect, marks, paint)` computes the lines from the card's rectangle. The front pass calls it
unclipped by the shape; the surface's own edge (and the snapshot's clip to the card's rectangle from `card-chrome-back`) cuts
it at the card's edge. `card-underlay` calls it with the card's rectangle clipped away. The seam is the card's edge, a whole
pixel, so no pixel is drawn twice. The `inset` is to the mark's outer edge (the schema did not say; this puts Carbon's
bracket across the edge as its mock shows).

### 4. `tick` is one line

The schema calls a tick "short, diagonal-free" and does not say more. It is drawn as one line along the top or bottom edge.
Blueprint's mock shows an L at the bottom-left of its dock tiles, which is a `bracket`; Blueprint's theme change picks the
style by eye, and this change does not need to know.

## Risks / Trade-offs

- [The back image now also exists for solid fills with a strip (Carbon): memory as in `card-chrome-back`] → measured
  together with that change's number on the panel; Carbon's strip without glow could be drawn live instead if it matters.
- [Highlight band on a chamfer: the cut corner's diagonal is in the band] → covered by a golden of Ember with a highlight
  added in the test theme.
- [Marks cut at the card's edge until `card-underlay`] → Carbon shows half brackets and Blueprint no page marks in between;
  both changes are proposed together and land in order.
