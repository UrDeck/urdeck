# Page pattern (page-pattern)

Measured 2026-10-19 on the owner's panel (HYTE Y70 Touch, 1100x3840 at 100 %), Release build copied to a scratch folder, by the
S1 method of `docs/perf/theme-look.md`: 15 s of warm-up, the median of 30 one-second samples, three runs of each case
interleaved. Each pattern is compared with a copy of the same theme with `"pattern": {"kind": "none"}` (Carbon's copy is a
user theme with Carbon's own fonts; the Blueprint, Ember and Rigline fixtures have no font files, so both of a pair fall back
alike). Every theme runs its own moving background at scale 0.5.

## What is drawn

| Theme | Pattern | Repeat | Block | Tiles |
|---|---|---|---|---|
| Carbon | dots | 35x35 px | 525x525 px | 3x8 |
| Blueprint | grid, major every 5 | 250x250 px | 750x750 px | 2x6 |
| Ember | hatch, 45 degrees | 32x32 px | 512x512 px | 3x8 |
| Rigline | scanlines | 1x10 px | 512x520 px | 3x8 |

## Memory (median of three runs)

| Theme | Private bytes: with / without | Working set: with / without | Private delta | Working set delta |
|---|---|---|---|---|
| Carbon | 186.4 / 184.5 MB | 225.7 / 220.0 MB | +1.9 MB | +5.7 MB |
| Blueprint | 184.3 / 178.6 MB | 223.1 / 215.0 MB | +5.7 MB | +8.1 MB |
| Ember | 195.8 / 190.5 MB | 232.2 / 226.4 MB | +5.3 MB | +5.8 MB |
| Rigline | 195.9 / 192.9 MB | 235.4 / 231.8 MB | +3.0 MB | +3.6 MB |

(The medians are of the three runs' own medians; the runs' range is about 2 to 4 MB, so the deltas are good to a few MB.)
All four are inside the 10 MB budget; the page-sized surface it replaces measured +33 MB (S1). The 256 px minimum block was not
needed.

## Time to make

First in a process (the log line, cold): 30 to 40 ms for every kind (the first Skia surface, PNG encode and
`LoadedImageSurface` of the process, as in S1). Made again after a theme switch (Carbon to `default-dark` and back, warm):
**6.3 ms**, inside the 20 ms budget.

## Made once

One minute of Carbon with its moving background and ticking clocks, then an unrelated configuration save: exactly one
`Pattern:` line. Carbon to `default-dark` logged `Pattern: none` and back made the dots again, without a restart. A page swipe
was not injected; the layer is not part of the pager, so nothing in a swipe reaches it.

## Panel look

Carbon on the panel (2026-10-19, the owner): the dots match the design board at 100 %, and no seam shows between tiles at 125 %
or 150 % display scaling. The whole-pixel placement needed no fix and the page-sized canvas fallback was not used.
