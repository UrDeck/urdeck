# Backgrounds on the real host (2026-10-12)

The cost of the `backgrounds` change on the owner's panel, and two things measured while verifying it. The spike that
chose the approach is `backgrounds-spike.md`; the budget is design decision 14 of the change.

## Cost table (task 9.4)

Method of `nexus-baseline.md`, with a longer window so the weather icon's once-a-minute animation lands in every case alike:
the host started from a copy of the build output with the sample configuration (clock, stats, weather, shortcut in the
dock; `default-dark`), 15 s settle, then 61 s: CPU from `TotalProcessorTime`, GPU 3D as the mean of 12 samples four seconds
apart, private bytes and dedicated video memory at the end. 2026-10-12, Windows 11 Pro 10.0.26200, RTX 4090, panel
1100x3840 at 100%, machine otherwise idle (0.7% GPU in all). One run per case.

| Case | CPU % of one core | GPU 3D % | Private MB | Dedicated video MB |
|---|---|---|---|---|
| No `background` | 1.3 | 1.1 | 132 | 125 |
| Image (wallpaper, still) | 1.3 | 1.2 | 180 | 176 |
| `plasma`, default scale (surface 550x1920) | 3.6 | 1.9 | 167 | 155 |
| `julia`, default scale | 3.9 | 2.1 | 168 | 155 |
| `julia`, `scale` 1 (surface 1100x3840) | 3.9 | 2.9 | 193 | 203 |
| `plasma` with a game running beside the deck | 4.3 | 0.2 | 183 | 269 |

Over the same page without a background:

| Case | CPU | GPU 3D | Private | Video memory | Budget |
|---|---|---|---|---|---|
| Image | +0.0 | +0.1 | +48 | +51 | still: no CPU or GPU difference once drawn: **met** |
| `plasma`, default | +2.3 | +0.8 | +35 | +30 | at most 5%, 2%, 60 MB, 60 MB: **met** |
| `julia`, default | +2.6 | +1.0 | +36 | +30 | **met** |
| `julia`, `scale` 1 | +2.6 | +1.8 | +61 | +78 | not budgeted (the budget is for the default scale); the video memory is above 60 |

**With a game.** The game kept the GPU's 3D engine at about 99% (all processes) through the run. The deck's own 3D
share was 0.2% (the game takes the time slices), its CPU 4.3% and its memory 183 MB private and 269 MB video, so the
budget's CPU and memory lines still hold (the machine has no same-page baseline under that load). The cost shows elsewhere:
the background produced **1792 frames in 75.4 s, 23.8 per second** instead of 30 (the log's stop line), so under a
saturated GPU the motion is a little choppier. It did not stall, and the game's frame rate was not measured. A heavier
shader or `scale` 1 would lose more; `scale` is the remedy, as documented.

The moving background costs about what the spike measured (3 to 4% of one core, under 1% GPU, 25 to 35 MB). The picture is
the largest memory cost: 48 MB private, against the 16 MB design decision 9 expected for the scaled copy. The rest is
probably the canvas's own bitmap and decode garbage not yet collected; it was not investigated, and there is no budget for
a still picture's memory.

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
