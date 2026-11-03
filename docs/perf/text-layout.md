# Text layout: cost

The gate of the `text-layout` change: a marquee and a following list must cost next to nothing per frame on the secondary
display's CPU budget, and measuring must happen only when the text or the width changes.

## Method

A throwaway test (not kept) drew each component into a 560x275 CPU raster bitmap with the default theme, after a warm-up,
and divided the elapsed time by the number of draws. Release build, Windows 11, the owner's PC, 2026-11-03. The numbers are
the cost of the component's own drawing and layout; the host's compositing of the widget's surface is not included (see
`docs/perf/animation-flip.md` and `docs/perf/motion.md` for what a repainted widget costs there).

## Result

| | per draw |
|---|---|
| marquee line, 70 characters in a 280 px rectangle, a new time each frame (2000 frames) | 0.007 ms |
| list of 200 two-line items, follow mode, nothing changed (2000 frames) | 0.040 ms |
| list at capacity (200), one item added and the list drawn (500 times) | 0.060 ms |
| list of 200 items laid out again after a width change (20 times) | 2.3 ms |

A marquee at the host's frame rate (about 21 to 30 per second) is under 0.3 ms of drawing per second. A repainting chat
list costs about the same. A width change or a theme change, which a user makes a few times a day, lays out 200 items in
about 2 ms. Nothing is measured on a repaint with the same text, step and width (`TextLayoutCache`, tested).

## Not measured

The host's per-frame cost of repainting a widget's `SKXamlCanvas` for a moving marquee. It is the same path as any
animating widget; `IsAnimating` ends it as soon as the line fits or the motion level is below full.
