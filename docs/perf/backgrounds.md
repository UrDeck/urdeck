# Backgrounds on the real host (2026-10-12)

The cost of the `backgrounds` change on the owner's panel, and two things measured while verifying it. The spike that
chose the approach is `backgrounds-spike.md`; the budget is design decision 14 of the change.

## Cost table (task 9.4): not measured yet

To be filled in by the owner with the method of `nexus-baseline.md` (CPU from `TotalProcessorTime`, GPU Engine and GPU
Process Memory counters, 5 GPU samples four seconds apart after a 15 s settle), on the usual page, once without a
background and once with each case:

| Case | CPU % of one core | GPU 3D % | Private MB | Dedicated video MB |
|---|---|---|---|---|
| No `background` | | | | |
| Image | | | | |
| `plasma`, default scale | | | | |
| `julia`, default scale | | | | |
| `julia`, `scale` 1 | | | | |
| `plasma` with a game running beside the deck | | | | |

Budget over the same page without a background: no background, nothing; still, nothing once drawn; a moving built-in at
the default scale at most 5% of one core, 2% GPU 3D, 60 MB private, 60 MB dedicated video memory. A case over budget is
reported, not tuned.

## What the frame clock delivers (task 9.5)

Question: does `FrameClock` (a `DispatcherQueueTimer` at 1/30 s) deliver 30 frames per second? Method: a temporary log
line in `WidgetView.OnPaintSurface` (removed again) recorded the time between consecutive paints of one animating widget
while it animated. Windows 11 Pro 10.0.26200, no background configured, page of the sample configuration.

| Widget | Asked interval | Measured gap between paints | Paints per second |
|---|---|---|---|
| Clock, `flap` style, at the minute change | none (the clock's own rate) | 47.1 to 47.4 ms (10 paints) | about 21 |
| Weather, animated icon | 66.7 ms (15 fps) | 86.7 to 89.4 ms (120 paints, two minutes) | about 11.3 |

So the clock does not deliver 30 per second but about 21, which is the shortfall the spike saw for a plain dispatcher
timer (22). A widget that asks for a longer interval gets the next tick at or after it: 66.7 ms is two ticks of 44 to 47 ms,
which is why the weather icon runs at about 11 per second and not 15. The moving background does not use `FrameClock`
(it paces from `CompositionTarget.Rendering` and measured 29.7 to 30.4 per second), so it is not affected. `FrameClock` was
not changed in this change; see the backlog.

## The diagonal line under translucent cards

With a moving background and translucent cards (the `glass` theme), Windows 11 drew a bright dotted line along the
window's diagonal. It is not the surface and not the scale; it came from the rounded corners Windows 11 gives a top-level
window, and `WindowCorners.Square` (square corners for the deck window) removes it. The numbers and the reasoning are in
`docs/handoff/2026-10-11-backgrounds-seam.md`. Checked again on 2026-10-12 on the Y70 with `glass` and `julia` (`scale` 0.5):
seam pixels (`seam.py`) 1, against about 570 before, and a contrast-raised crop shows no line.
