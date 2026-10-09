# pages-and-pager measurements

## Focus (task 1.3)

Panel: Y70, 1100x3840. `WS_EX_NOACTIVATE` plus `WM_MOUSEACTIVATE` -> `MA_NOACTIVATE` on the top-level window only.

- Tap, drag and long-press on the panel with a non-fullscreen application focused: it keeps focus and stays in front
  (owner, 2026-10-09).
- The same with a fullscreen game (ARC Raiders): no focus was stolen.
- The XAML child window did not need covering (task 1.4 not needed).
- The window has no taskbar button on the owner's machine (extended style `0x08000100`, no `WS_EX_TOOLWINDOW`); not caused
  by this change and not needed.

## Slide spike (tasks 2.1 to 2.5)

- Mechanism: the pointer's dx sets each page host's composition `Offset`; the settle is a 250 ms composition animation
  (ease-out). No `InteractionTracker`.
- By touch on the panel (owner, 2026-10-09): the swipe feels right, the bounce at both ends behaves, dot taps work, and
  values on the incoming page persist (last value shown, no dashes seen).
- The constants kept: slop 0.04 of a cell, commit at half a page or 0.6 pages per second.

## Idle cost at rest (task 8.6)

Release build, Y70 panel, default-dark, page 1 of the test configuration: a Clock, a 4x4 Stats widget and a Weather
widget. Sampled 9 s after start, CPU over 20 s, GPU as the sum of 56 `GPU Engine` readings over 12 s. 2026-10-09.

| | One page in the file | Three pages in the file |
|---|---|---|
| Private bytes | 123 MB | 122 MB |
| Working set | 170 MB | 168 MB |
| CPU | 0.94% of one core | 1.64% |
| GPU (sum / largest single reading) | 4.55 / 1.59 | 4.06 / 1.42 |

- At rest the cost is that of one page: more pages in the file cost nothing. The CPU difference is the Stats gauge
  animation and the 2 s system provider, which are on both pages' widgets and vary between samples.
- The earlier `docs/perf/render-host-baseline.md` figures (one Clock) are not comparable: this page has three widgets.
- An instance that had been swiped, reloaded and hot-reloaded for a while measured 159 MB private; a fresh start with the
  same configuration measured 122 MB: see the next section.
- The frame clock logs `started`/`stopped` pairs about every 2 s on page 1: the Stats widget animates its gauges briefly
  on each new reading. After a swipe settles and after the indicator fade the clock stops.

## Mid-swipe memory and growth over many swipes (task 8.6)

- Mid-swipe, held halfway between two pages: 154 MB private (206 MB working set), against 122 MB at rest. The second
  page costs about 32 MB, as the design estimated.
- Swiping about 80 times by hand, then 100 times with injected mouse drags (pages 1, 2, 3 and back), private bytes
  saw-toothed between 147 and 209 MB without a steady climb: released pages' surfaces are native memory the garbage
  collector does not feel pressure from, so it let them sit.
- Fix: `PagerController` runs one blocking gen 2 collection 1.5 s after a swipe has settled (debounced, so never during
  a drag). Another 100 injected swipes then stayed between 132 and 153 MB, read 2 s after each tenth swipe, about 20 MB
  above the fresh start and not growing.
- The frame clock stops after each swipe settles: the log shows `Frame clock stopped` and the indicator in `always` mode
  never starts it.
