# stats-gauges: panel measurements

Measured 2026-10-08 on the owner's panel (Release build, WinUI 3 host, theme `default-dark`), with the page of the baseline
`src/UrDeck.Host/urdeck-config.json`: a 4x2 Clock above a 4x4 stats card (four gauges and three text stats, styles ring,
bar and vertical bar). Process sampled for 30 s after 10 s of warm-up.

| Case | Private bytes | Working set | CPU (share of all logical processors) |
|---|---|---|---|
| Idle system | 114 MB | 153 MB | 0.06% |
| All cores busy (readings change every sample) | 112 MB | 152 MB | 0.04% |

- The 4x4 card repainted as one surface is cheap enough that no clip-and-redraw (design decision 8) is needed.
- Memory is in line with the single-Clock host (about 101 MB private, `render-host-baseline.md`) plus the card's layer.
- The frame clock runs only for the 300 ms ease after a changed value: `urdeck.log` shows `Frame clock started` followed by
  `Frame clock stopped` about 330 ms later, once per sample that changed a value, and nothing in between.
