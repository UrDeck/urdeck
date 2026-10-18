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

## Findings during apply

- **The highlight is drawn after the clip is restored, as a path made per size.** The design had it stroked under the shape's
  clip with only its shaders cached; that cost Prism 0.16 ms per paint for two highlights. The outline stroked twice the
  thickness, intersected with the shape and the band once per size (`SKPath.Op`), costs 0.09 ms and draws the same pixels
  within the golden tolerance. Prism still measures 1.83 times `default-dark` against the budget of 1.5, mostly its back
  (1.39 alone): `docs/perf/card-chrome.md`, "As built (card-chrome-front)". **The owner accepted the cost** (2026-10-18).
- **On the panel:** Obsidian and Carbon looked at by the owner on the Y70 (2026-10-18), with their own fonts: approved.
- **Defaults the schema did not give:** a corner mark's `size` 0.08 and `thickness` 0.01 (added to the schema); a sheen without
  `color` and a strip glow without `blur` draw nothing. `cornerMarks.style` keeps the schema's default `none`, so a theme must
  name `bracket` or `tick`.
- **A mark's arm is `size` long from the mark's outer corner** (the spike measured it from the line's centre). Both arms of a
  bracket are one stroked path with a mitred corner, so a translucent mark does not darken where the arms meet.
- **`fade` on a left or right strip** runs from the top to the bottom; the spec names only the start and the end.
- **A mark that lies wholly outside the card** (Blueprint's page marks, `inset` + `thickness` <= 0) is not drawn by the card's
  surface at all; `card-underlay` draws it.
