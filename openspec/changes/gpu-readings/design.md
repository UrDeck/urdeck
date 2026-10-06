## Context

The `system` provider (`providers/UrDeck.Providers.System`) offers CPU load, per-core load and memory load. It calls
Windows directly (`NtQuerySystemInformation`, `GlobalMemoryStatusEx`), holds no handles, allocates nothing per sample
and queries only the groups that are subscribed to. Its design promises ids that mean the same on every PC and that
hide where a value comes from.

The owner has an NVIDIA GPU and wants Intel, AMD and NVIDIA supported as far as is reasonable, starting with NVIDIA
if that is a quick unlock, without tech debt. The elevated helper (CPU temperature, and whatever else needs a kernel
driver) is a later change.

**What is known and what is not.** The sources below are taken from Windows and vendor documentation and from how
Task Manager behaves. None has been run from this code base. The first task group is a spike that confirms or
replaces each of them; the specs describe behaviour (what a reading means, when it is unavailable) and hold whatever
the spike finds.

## Goals / Non-Goals

**Goals:**

- GPU load and temperature on any vendor through one code path.
- GPU power and clock on NVIDIA, behind ids that AMD and Intel can fill later with no page change.
- No elevation, no driver, no helper process, no new package.
- No cost for pages that show no GPU reading.

**Non-Goals:**

- CPU temperature, CPU power, fan speeds: they need a kernel driver.
- ADLX (AMD) and IGCL (Intel). Added behind the same ids when someone can test them.
- Video memory use, per-engine load (3D, video decode), fan speed, per-process GPU use.
- Showing the clock in GHz. It needs a display unit beyond Celsius and Fahrenheit, which is formatter work.
- Hot-plugged GPUs (an external GPU dock). The catalog is read once, as `data-providers` decided.

## Decisions

### 1. Readings and ids

```
 path                    kind          unit   label   name              range     warning / critical
 gpu/load                Percent              GPU     GPU load          0..100    none
 gpu/temperature         Temperature   °C     GPU     GPU temperature   0..100    80 / 90
 gpu/power               Number        W      GPU     GPU power         none      none
 gpu/clock               Number        MHz    GPU     GPU clock         none      none
 gpu/<n>/<same four>     as above; label "GPU <n>", name "GPU <n> load", ...
```

- `gpu/...` is the main adapter (decision 2); `gpu/<n>/...` is adapter `n`, counted from 1 like the cores.
- The device name of every GPU reading is the adapter's name as Windows reports it.
- Temperature declares Celsius as its default display unit: the owner decided in `data-providers` that hardware
  temperatures are read in Celsius even where weather is read in Fahrenheit.
- Load declares no levels: a busy GPU is not a fault. 80 and 90 degrees are conservative values that hold for current
  desktop and laptop GPUs of all three vendors; a slot can override them, and the owner checks them on a real load
  (task 6.4).
- Power and clock declare no range, so they have no gauge fraction and draw plain. That matches the reference, where
  they are text stats. A slot can give them a range.
- A PC without a GPU adapter lists no GPU readings; `system:gpu/load` is then an unknown path and unavailable.

### 2. Which adapter is "the GPU"

A PC with an integrated and a discrete GPU has two adapters, and a portable id must pick one.

- Adapters are enumerated once, when the catalog is built. Software adapters (the Microsoft Basic Render Driver) are
  left out.
- The main adapter is the one with the most dedicated video memory. A tie goes to the first one enumerated.
- Adapter numbers follow dedicated video memory, largest first, so `gpu/1/...` is the main adapter and the numbering
  does not depend on enumeration order.

Alternative considered: the adapter that drives the primary monitor. Rejected: on a laptop that is the integrated GPU,
which is not the one the user means by "GPU temperature".

Alternative considered: a setting for the main adapter. Not needed: `gpu/2/...` already addresses the other one.

### 3. One source per reading

```
 reading       source                                             vendors      elevation
 load          Windows graphics kernel statistics (per engine)    all          none
 temperature   Windows adapter performance data                   all          none
 power         vendor library (NVML first)                        NVIDIA       none
 clock         vendor library (NVML first)                        NVIDIA       none
```

NVML also reports load and temperature. They are still read from Windows on NVIDIA, so that the vendor-neutral path
is the one the owner's machine exercises every day and there are not two definitions of "GPU load" that differ by
vendor. A vendor library is used only for what Windows does not offer.

The rule bends in one case: if the spike finds that Windows reports no temperature for the NVIDIA adapter (the driver
may leave the field at zero), temperature moves to the "vendor library, Windows as fallback" column and the spec's
wording already allows it, because it names behaviour and not the source.

Alternative considered: NVML for everything on NVIDIA and nothing for other vendors. A quicker unlock, but it leaves
AMD and Intel with four dashes and makes the later vendor-neutral path a second implementation of the same readings.

Alternative considered: LibreHardwareMonitorLib for all of it. It bundles a kernel driver and its licence, driver and
security-tool status are still unverified (roadmap item 7). That question belongs to the elevated change.

### 4. Load

Task Manager's GPU percentage is the busiest engine of the adapter (3D, compute, video decode, ...), not their sum.
`gpu/load` means the same: the highest share of time any engine of the adapter was busy since the previous sample.

Two ways to read it, to be decided by the spike on cost:

- **Graphics kernel statistics** (`D3DKMTQueryStatistics`, per adapter node: running time). Two samples give a share
  of time, the same shape as the CPU load code: a baseline first, then deltas, no handles kept, a reused buffer.
  Preferred.
- **Performance counters** (`\GPU Engine(*)\Utilization Percentage`). One instance per process and engine, so
  hundreds of instances to sum per sample, and `data-providers` already noted an unmeasured memory cost and
  first-call delay for the performance counter library. The fallback if the first way needs rights it does not have.

Like CPU load, the first sample only sets the baseline and publishes nothing.

### 5. Temperature

Windows keeps performance data per adapter (`D3DKMTQueryAdapterInfo` with the adapter performance data type, WDDM 2.4
and later), which includes the temperature in tenths of a degree Celsius. A driver that does not report it leaves
zero. The provider publishes a value above zero and reports the reading unavailable with a reason otherwise, once per
transition as the hub already logs.

### 6. Power and clock through a vendor seam

A small internal interface inside the provider assembly, not an SDK concept:

```
 IGpuVendorReader           (internal to UrDeck.Providers.System)
     bool TryOpen(adapter)   loads the library and finds the device for this adapter
     double? PowerWatts()
     double? ClockMegahertz()
     void Close()

 NvmlReader : IGpuVendorReader     nvml.dll from the Windows system directory
```

- `nvml.dll` is loaded with `NativeLibrary.TryLoad` from the system directory only, the first time a power or clock
  reading of an NVIDIA adapter is in demand. It is initialised once and shut down in the provider's `Shutdown`.
- The NVML device is matched to the Windows adapter by PCI location, so a PC with two NVIDIA cards reads the right one.
- Power is NVML's power draw (milliwatts) in watts; clock is NVML's current graphics clock in MHz.
- If the library is missing, fails to initialise, or the adapter is not NVIDIA, power and clock are unavailable with a
  reason that says so (`no vendor library for this GPU yet`), and the provider does not try again until it is
  restarted. Nothing else is affected.
- AMD and Intel are later implementations of the same interface. The seam costs one interface now and is what keeps
  NVIDIA-first from becoming NVIDIA-only.
- The interface is also the test seam: provider tests run with a fake reader and with none.

A vendor library is native code in the host process. A crash inside it takes the host down, which a managed exception
would not. NVML is mature and is what `nvidia-smi` uses, and the reads are simple getters; the risk is accepted and
noted for the helper process of the elevated change, which is where vendor code could move later.

### 7. Demand and cost

`Demand` gains one record per adapter (load, temperature, power, clock wanted). As with CPU and memory:

- No GPU subscriber: no GPU call of any kind, and the load baseline is dropped so that a pause does not produce a
  wrong first value.
- Each reading group is queried only when one of its readings is wanted.
- No per-sample allocation in the steady state; buffers are reused; paths are prebuilt strings.
- Query counters per group (as `CpuQueries` and `MemoryQueries` today) let a test assert that an unwanted group is
  not queried.

`SystemProvider.cs` is 220 lines for two groups. With GPUs it is split by group (`CpuReadings`, `MemoryReadings`,
`GpuReadings`) behind the one `SystemProvider`, without changing behaviour.

### 8. The spike

Before any provider change, a throwaway console program on the owner's PC, run as a standard user, answers:

1. Do the graphics kernel statistics give a load that tracks Task Manager within a few points, idle and under load?
2. Does the adapter performance data report a temperature for the NVIDIA adapter, and does it match the NVIDIA
   overlay or `nvidia-smi`?
3. Do NVML power and clock read without elevation, and how long do the first call and a steady call take?
4. What do a sample of all four cost in time and allocations, and what does loading NVML add to private bytes?
5. Can adapters be enumerated with their name, dedicated memory and PCI location without COM?

Pass: all four readings have a source that works unelevated and a sample costs well under a millisecond of CPU.
Any source that fails is replaced by its listed fallback (performance counters for load, NVML for temperature) and
measured again. The results go to `docs/perf/gpu-readings.md`; the spike's code is not merged.

## Risks / Trade-offs

- The Windows sources are little-documented and may differ by driver → the spike confirms them on NVIDIA first, each
  has a named fallback, and a source that reports nothing yields an unavailable reading with a reason, never a wrong
  number.
- AMD and Intel are untested → the log names each adapter and each reading's source at provider start, and the README
  says what is verified. No claim of support beyond "should work" for load and temperature.
- A native vendor library can crash the host → accepted for NVML (decision 6); loaded only on demand.
- "Busiest engine" differs from what some overlays show (often the 3D engine only) → it matches Task Manager, which is
  the number users compare against; the owner's CPU load decision took the other side of this trade for cost, and
  here the Task Manager number costs nothing extra.
- Fixed 80 and 90 degree levels are wrong for some hardware → a slot overrides them; NVML's own thresholds could
  refine them later for NVIDIA.

## Open Questions

- Whether the load from kernel statistics needs any right a standard user lacks: spike question 1.
- Whether the driver fills the temperature field on NVIDIA: spike question 2.
- Whether laptop NVIDIA GPUs that are powered down report through NVML without waking the GPU. Not testable on the
  owner's desktop; noted in the README until someone reports it.
