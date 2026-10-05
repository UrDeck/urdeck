# Cost of a Clock flip

What one flap flip of the Clock (`animation` change) costs on the Y70 panel, measured on 2026-10-04 with the method in
`render-host-baseline.md`: Release build, one 4x2 Clock with `style: "flap"` on the default dark theme, Windows 11 Pro
10.0.26200. CPU is the process's `TotalProcessorTime` sampled every 100 ms (its resolution is about 15 ms); GPU is the
`GPU Engine` utilization counters for the process.

| Measure | Result |
|---|---|
| Between flips, CPU | 16 ms over a 20 s sample, one 15 ms tick: 0% of one core |
| Between flips, GPU | 0% (eight samples, 1.5 s apart) |
| One flip, CPU | 31 ms in total, two 15 ms ticks, about 15 frames in 0.5 s |
| One flip, GPU | one non-zero sample, 1.8% (sampled every second or so across the minute change), zero in the rest |
| Frame clock | `urdeck.log` shows "started" at each minute change and "stopped" about 0.5 s later, and nothing in between |
| Memory | 101 MB private, 135 MB working set, as in the baseline |

- A flip costs a few milliseconds of CPU per frame, about 6% of one core while it runs and about 0.05% averaged over
  the minute. Nothing runs between flips.
- This is one short sample, not a benchmark. The weather widget (a continuous loop) is where the sustained cost of the
  frame clock gets measured; see roadmap item 14.
