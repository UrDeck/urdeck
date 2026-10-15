# The twelve added built-in shaders on the real host (2026-10-15)

Cost of `horizon`, `spectrum`, `contour`, `nebula`, `streams`, `ripple`, `embers`, `plotter`, `shapes`, `warpgate`,
`equalizer` and `vortex` at the default scale (surface 550x1920 on the 1100x3840 panel), against the same page without a
background. Budget: `backgrounds` spec, "Background Cost" (5% of a core, 2% of the 3D engine, 60 MB private, 60 MB video).

## Method

Method of `nexus-baseline.md` and `backgrounds.md`, shortened: for each case the host was restarted from a copy of the
build output (clock, stats, weather, shortcut in the dock; `default-dark`), 25 s settle, then 20 s: CPU from the change in
`TotalProcessorTime` as a share of one core, GPU 3D as the mean of five samples four seconds apart, private bytes at the
end, dedicated video memory as the mean of the five samples. Windows 11 Pro 10.0.26200, RTX 4090, machine otherwise idle.
One run per case; `none` was run first and last to show the noise (CPU 1.5 and 1.0, GPU 0 and 1.06, memory 135 and 136 MB).
The 20 s window is shorter than the weather icon's once-a-minute animation, so CPU is good to about a point.

## Results

| Case | CPU % of one core | GPU 3D % | Private MB | Video MB |
|---|---|---|---|---|
| none (mean of two) | 1.3 | 0.5 | 136 | 125 |
| `nebula` | 4.3 | 1.5 | 173 | 155 |
| `shapes` | 4.0 | 1.2 | 174 | 155 |
| `horizon` | 4.1 | 1.3 | 175 | 155 |
| `spectrum` | 3.8 | 1.3 | 176 | 155 |
| `contour` | 4.0 | 1.1 | 175 | 155 |
| `streams` | 4.9 | 1.4 | 180 | 177 |
| `ripple` | 4.6 | 1.3 | 173 | 155 |
| `embers` | 4.7 | 1.1 | 176 | 155 |
| `plotter` | 4.2 | 1.2 | 174 | 155 |
| `warpgate` | 4.3 | 1.3 | 176 | 155 |
| `equalizer` | 4.3 | 1.1 | 176 | 155 |
| `vortex` | 5.1 | 1.1 | 173 | 155 |

Over the page without a background, the worst of each: CPU +3.8 (`vortex`, budget 5), GPU 3D +1.0 (`nebula`, budget 2),
private memory +44 MB (`streams`, budget 60), video memory +52 MB (`streams`, budget 60). **Every shader is within
budget.** `nebula` and `shapes`, the two expected to be heavy, cost no more than the rest (the GPU draws all twelve in
about the same time; the CPU raster numbers in the design handoff do not carry over), so no default was lightened.
`streams` has the largest memory, about 22 MB of video memory more than the others, which is still inside the budget.

Frames: every case logged about 900 frames per 30 s (30 per second). The log had no `SHADERnnn` line.

## Looked at

All twelve were shown on the panel for 30 s each: no banding, no seam in `warpgate` or `vortex`, nothing blurry or
broken. `nebula` and `embers` were also rendered by `urdeck-cli` at time 14,399: no hash artefacts, and `nebula` looks
as at time 0. A run on the GPU past the wrap was not made.
