# WinUI 3 host baseline

What the WinUI 3 host (`render-path` change) costs on the Y70 panel, measured on 2026-10-04 with the method in
`nexus-baseline.md`: Release build, one Clock widget on the default dark theme, idle, sampled 12 s after start.
Windows 11 Pro 10.0.26200.

| Measure | UrDeck (WinUI 3) | Nexus |
|---|---|---|
| Processes | 1 | 7 |
| Private bytes | 101 MB | about 627 MB |
| Working set | 135 MB | |
| CPU | 0% of one core (20 s sample) | about 20% |
| GPU | 0% (five samples, four seconds apart) | about 3% 3D, 3% video decode |
| Dedicated video memory | 125 MB | 238 MB |

- Mid-minute, with the Clock ticking once a second, the process used no measurable CPU and no GPU: no widget is
  repainted between minute changes (`NeedsRender` returns `false`, so nothing is invalidated).
- The private bytes figure replaces the 60-70 MB written for the WPF host. It is higher by design (the spike measured
  94 MB for an empty window) and accepted against the Nexus bar.
- This is one short sample, not a benchmark. Repeat it before quoting it as a result.
