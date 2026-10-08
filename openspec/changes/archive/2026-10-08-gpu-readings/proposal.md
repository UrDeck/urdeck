## Why

The owner's performance card shows GPU temperature, GPU load, GPU power and GPU clock, and the `system` provider
offers none of them, so four of the seven positions of a 4x4 stats card show a dash. Most of these can be read without
administrator rights, so they do not have to wait for the elevated helper. This is an addition to roadmap item 3,
between its second change (`stats-gauges`) and its third (sensors that need elevation).

## What Changes

- **GPU readings in the `system` provider, without elevation:** load and temperature for every GPU vendor through
  Windows itself (the sources Task Manager uses), and power in watts and core clock in MHz through the vendor's own
  library, starting with NVIDIA (`nvml.dll`, which the NVIDIA driver installs).
- **Portable ids.** `system:gpu/load`, `system:gpu/temperature`, `system:gpu/power` and `system:gpu/clock` mean the
  main GPU of whatever PC the page runs on. Each adapter is also addressable as `system:gpu/<n>/...`.
- **One source per reading.** Load and temperature come from Windows on every vendor, NVIDIA included. A vendor
  library is used only for what Windows does not offer. On AMD and Intel, power and clock are in the catalog and
  report unavailable until their libraries are added behind the same ids.
- **The first readings with levels.** GPU temperature declares a range and a warning and a critical value, so a gauge
  changes colour when the GPU runs hot (the catalog members come from `stats-gauges`).
- **A spike comes first.** The Windows sources are known from documentation, not from this machine. The change starts
  with a time-boxed spike on the owner's PC that confirms each source, its cost and that no elevation is needed, and
  the result is recorded before the provider is changed.
- **Nothing is paid for unused readings:** no GPU query runs unless a GPU reading is shown, and the vendor library is
  not loaded unless power or clock is shown.

Deliberately not in this change: CPU temperature and anything else that needs a kernel driver or elevation; AMD and
Intel vendor libraries (ADLX, IGCL); video memory, fan speed and per-engine load; showing the clock in GHz; a
stats widget default that uses these readings; frame rate.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `provider-system`: adds the GPU readings, how the main adapter is chosen, the vendor-library readings and their
  cost rules.

## Impact

- `providers/UrDeck.Providers.System`: `SystemProvider` (catalog, demand, sampling), `NativeMethods` (Windows graphics
  kernel calls, NVML), likely split into one file per reading group.
- Depends on `stats-gauges` for `ReadingDescriptor.Warning` and `Critical`. One small SDK addition: `IReadingSink.Log` (a default interface method, so no provider breaks;
  the engine's sink writes it to `urdeck.log`), which the per-adapter source log needs. No host change, no new package dependency: Windows APIs and `nvml.dll` are called directly.
- Tests: `SystemProviderTests` (catalog, demand, behaviour without a GPU or without NVML through a seam).
- Docs: `docs/perf/gpu-readings.md` (the spike and the cost), `README.md` (the readings and a full 4x4 example),
  `docs/ROADMAP.md`.
- Tested on NVIDIA only. The Windows path is vendor-neutral code, but AMD and Intel are unverified until someone runs
  it there; the log names the adapter and the source of each reading to make such reports useful.
