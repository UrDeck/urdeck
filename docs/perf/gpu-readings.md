# GPU readings: sources and what they cost

The spike of the `gpu-readings` change, measured on 2026-10-08 on the owner's PC (NVIDIA GeForce RTX 4090, 24 GB, Windows 11
Pro 10.0.26200), as a standard user, Release build of a throwaway console program (not
merged). The program called the Windows graphics kernel (`gdi32.dll`) and `nvml.dll` directly, with no COM and no package.

## Sources (all work without elevation)

| Reading | Source | Result |
|---|---|---|
| Adapters | `D3DKMTEnumAdapters2`, then per adapter `D3DKMTQueryAdapterInfo` and `D3DKMTCloseAdapter` | Two adapters: the RTX 4090 and the Microsoft Basic Render Driver |
| Name | query type 8 (adapter registry info), size 2080: first `WCHAR[260]` is the adapter string | `NVIDIA GeForce RTX 4090`; the software adapter reports `STATUS_OBJECT_NAME_NOT_FOUND` (use a fallback name) |
| Software adapter | query type 15 (adapter type), 4 bytes: bit 2 is `SoftwareDevice` | RTX 4090 `0x31B` (hardware), Basic Render `0x105` (bit 2 set) |
| Vendor, device | query type 31 (physical adapter device ids), size 28: `{index, vendor, device, subvendor, subsystem, revision, bustype}` | `0x10DE` / `0x2684` for the RTX 4090; the software adapter says `0x1414` (Microsoft) |
| PCI location | query type 6 (adapter address), 12 bytes: `{bus, device, function}` | `1:0.0`, matching `nvidia-smi` (`00000000:01:00.0`); the software adapter reports `-1` |
| Dedicated video memory | query type 3 (segment size), 24 bytes: `{dedicatedVideo, dedicatedSystem, shared}` | 24 138 MB; the software adapter 0 |
| Temperature | query type 62 (adapter performance data), 64 bytes: `uint` at offset 56 is the temperature in tenths of a degree Celsius | 47.6 °C against `nvidia-smi` 48 °C. The driver fills it, so Windows is the temperature source on NVIDIA |
| Load | `D3DKMTQueryStatistics`, type 0 (adapter) gives the node count at result offset 4 (offset 28 of the structure); type 5 (node) with the node number as a `uint` at offset 800 gives `RunningTime` (100 ns units) as the first `long` of the result (offset 24) | 14 nodes. Running times are stable per node and differ between nodes, so offset 800 is right |
| Power, clock | `nvml.dll` from the system directory: `nvmlInit_v2`, `nvmlDeviceGetHandleByPciBusId_v2` (`00000000:BB:DD.F`), `nvmlDeviceGetPowerUsage` (mW), `nvmlDeviceGetClockInfo(device, 0, ...)` (MHz) | 21.6 W and 210 MHz idle against `nvidia-smi` 21.6 W and 210 MHz; no elevation |

Structure layout used for `D3DKMTQueryStatistics` (x64): `Type` (int) at 0, `AdapterLuid` at 4, `hProcess` at 16, the result
union at 24 and the query union (node number) at 800. Only these offsets are used; the buffer is 1024 bytes, zeroed
before each call.

## Cost

| Step | Time | Memory |
|---|---|---|
| Kernel statistics, one node | 435 µs (CPU, in the kernel) | none |
| Kernel statistics, one load sample of 14 nodes | 5.7 ms CPU (5.9 ms wall) | 0 allocations in the steady state (2 760 bytes over 15 samples, all console output) |
| `GPU Engine` counters, first read | 837 ms | +18.8 MB private |
| `GPU Engine` counters, one read of 913 instances | 1.6 ms CPU | +40 MB private after 20 reads |
| `nvml.dll` load, init and device lookup | 19 ms | +20 MB private bytes |
| NVML first power and clock read | 0.8 ms | |
| NVML steady power and clock read | below 1 µs (measured 0.000 ms over 100 calls) | |

## Pass or fail (design decision 8)

- Every reading has a source that works unelevated: **pass**.
- Temperature: Windows reports it on NVIDIA, no vendor fallback needed: **pass**.
- A sample costs well under a millisecond: **fail for load**. A load sample is 14 kernel calls and costs about 6 ms of
  CPU. The listed fallback (performance counters) is cheaper per sample (1.6 ms) but costs 840 ms on the first call and
  40 MB of private bytes, so it is worse for a widget host. The kernel statistics stay the source for load. At the
  provider's default interval of 2 s the load costs about 0.3% of one core, and nothing when no GPU load is shown.
  Temperature (one call, microseconds) and NVML (microseconds) are cheap.
- The 20 MB that NVML adds is why the library is loaded only when a power or clock reading is shown.

Not yet verified (tasks 6.3 and 6.4, on the panel): that the load follows Task Manager under a real GPU load, and the
temperature levels under heat. At idle `nvidia-smi` showed 3 to 9% and the kernel statistics 1.7 to 5.5%.

## Cost of GPU load: options for later

Decision (2026-10-08): keep the spec's definition (`gpu/load` is the busiest engine, as in Task Manager) and sample all
engines. A sample is about 6 ms of CPU on a 14-engine card, roughly 0.3% of one core at the 2 s default, paid only while a
GPU load reading is shown. Ideas if that ever needs to come down, cheapest first:

1. **Skip engines that have never run.** 8 of the owner's 14 engines had a running time of exactly zero. Sample only the
   others and re-check the rest every ~30 samples (about 2.5 ms per sample). Keeps the definition; an engine that first
   becomes busy shows up after up to ~30 samples.
2. **Per-engine readings** (`gpu/3d/load`, `gpu/video/load`, ...). Demand then is per engine, so a card that shows only
   `gpu/3d/load` pays for one or two engines (about 0.4 to 1.3 ms). Needs each engine's type (a one-time adapter query
   for the node metadata) and a new change, since per-engine load was a non-goal here.
3. **`gpu/load` as 3D and compute only.** Same saving as 2 without new ids, but it no longer matches Task Manager for
   video decode/encode or copy workloads, so it overrides design decision 4 and the spec.
4. **NVML utilization on NVIDIA.** A few microseconds per call, but a second definition of "GPU load" by vendor, which
   design decision 3 rejected.
5. **A longer interval** for GPU load only (`providers.system.intervalMs` applies to the whole provider today).

Not measured: how HYTE Nexus samples. `nexus-baseline.md` records its overall CPU and GPU use, not its internals.

## Panel check (2026-10-08, standard user, scratch Release build, full 4x4 card)

- Owner compared GPU temperature, load, power and clock with Task Manager and `nvidia-smi`: they match.
- Host with the full card (CPU, GPU temperature, GPU load, memory as gauges; GPU power, GPU clock, core 1 as text):
  129 MB private, 1.33% of one core (20 s window, 15 s after start), no warnings in `urdeck.log`. The log names the
  adapter and both sources once. The extra memory over `data-providers.md` (105 MB, five cards) is mostly NVML (+20 MB).
- A page with only CPU and memory slots logs no GPU line and starts no vendor library.
- Levels: with the slot's `warning` 52 and `critical` 60 the owner saw the gauge change to the warning and then the
  critical colour while playing a game. The catalog's 80 and 90 degrees were not reached and stay as designed.
