# Data providers: what they cost

What the `data-providers` change costs on the Y70 panel, measured on 2026-10-05 with the method of
`render-host-baseline.md`: Release build, default dark theme, sampled 12 s after start, CPU over the next 20 s.
Windows 11 Pro 10.0.26200. One run per row of the first table was repeated once; the second run is in brackets.

## Memory and CPU of the host

| Page | Private bytes | Working set | Threads | CPU (one core) | Provider in the log |
|---|---|---|---|---|---|
| Clock only | 100.6 MB (100.8) | 135.4 MB (135.5) | 67 | 0.16% (0.08) | none |
| Clock, four core cards, one memory card | 105.2 MB (105.4) | 141.6 MB (142.2) | 69 | 0.86% (1.25) | `system` started once |

- A page with only a Clock matches the baseline (101 MB private; the baseline recorded 0% CPU, the 0.1% here is the
  one-second Clock tick and measurement noise). No provider is created, no provider thread and no reading timer exist:
  the log shows the provider registered at load and never started.
- Five data cards add about 5 MB private, two threads and about 1% of one core: five one-second repaints of a small card
  plus one provider sampling per second. Only cards whose rounded value changed repaint (`NeedsRender`), so an idle
  machine whose cores sit at 0% repaints almost nothing.

## Provider lifetime (live host, from `urdeck.log`)

- Removing the last stats widget from the config logged `Provider 'system' stopped` 5.01 s after the page rebuilt.
- Putting the widgets back started it again; a config reload that only rebuilt the page (same readings) did not restart it.
- `providers.system.intervalMs` set to 500 logged `now sampled every 500 ms` with no restart; a value of 10 logged a
  warning that it is below the provider's minimum and `now sampled every 250 ms`; removing the section returned it to the default (then 1000 ms; the default is now 2000 ms).
- Replacing `UrDeck.Providers.System.dll` and `UrDeck.Widgets.Stats.dll` while running reloaded both: the provider was
  stopped before its plugin unloaded, the old contexts were collected after 434 ms, the provider started again and the
  cards showed values.

## `system:cpu/load` against Windows' own numbers

The provider computes load from the time-based processor counters (`NtQuerySystemInformation`). Compared on this
machine (a CPU that changes frequency) against `Get-Counter`, one sample per second; the first half idle, the second half
with half of the logical processors spinning:

| State | UrDeck `system:cpu/load` | `% Processor Time` | `% Processor Utility` (what Task Manager shows) |
|---|---|---|---|
| Idle | 2 to 4% | 0 to 3% (noisy) | about 5% |
| Half the logical processors busy | 52 to 55% | 52 to 55% | 83 to 87% |

UrDeck agrees with `% Processor Time`, as designed. Task Manager's number is `% Processor Utility`, which is scaled by the
current frequency against the base frequency, so with the CPU boosting it reads much higher under load (84% against 53%
here). The difference is visible under load on this machine, and it grows with how far the CPU boosts.

**Decision (owner, 2026-10-05): keep the time-based load.** It is cheap and has no first-call delay; the cost is that a
card reads lower than Task Manager when the CPU boosts. The alternative (PDH `% Processor Utility`, per core as
`\Processor Information(<group>,<n>)\% Processor Utility`) would match Task Manager at the price of more memory and a
first-call delay, neither of which is measured yet. If it is revisited, measure those two on the panel first. The change
is confined to `providers/UrDeck.Providers.System`: reading ids and the catalog stay the same, so no page config changes.

This is one short sample on one machine, not a benchmark.
