## 1. Spike (throwaway code, not merged)

- [x] 1.1 Write a console program outside the solution that enumerates GPU adapters with name, dedicated video memory, vendor and PCI location, without COM if possible, and leaves out software adapters
- [x] 1.2 Read the adapter's load from the graphics kernel statistics (design decision 4) once per second, as a standard user, and compare with Task Manager idle and under a GPU load; if it needs rights or does not track, try the `GPU Engine` performance counters and measure their memory and first-call delay
- [x] 1.3 Read the temperature from the adapter performance data (design decision 5) and compare with `nvidia-smi`; if the NVIDIA driver reports none, mark temperature as a vendor-library reading with Windows as the fallback
- [x] 1.4 Load `nvml.dll` from the system directory, match the device to the adapter by PCI location, read power and graphics clock, and time the first call and a steady call
- [x] 1.5 Measure a sample of all four readings (time, allocations) and the private bytes NVML adds
- [x] 1.6 Record sources, numbers and the pass or fail of design decision 8 in `docs/perf/gpu-readings.md`; update `design.md` where a source changed

## 2. Provider structure

- [x] 2.1 Split `SystemProvider.cs` by reading group (CPU, memory) behind the one provider class, with no behaviour change and the existing tests green
- [x] 2.2 Add the adapter list: enumerate once, drop software adapters, order by dedicated video memory with ties in enumeration order, adapter 1 as the main adapter
- [x] 2.3 Add the GPU catalog entries of design decision 1 (the four main paths and four per adapter), with device names, the temperature's range, levels and Celsius default, and the units of power and clock
- [x] 2.4 Extend `Demand` and `SetDemand` with per-adapter flags for load, temperature, power and clock, including the main-adapter paths and out-of-range adapter numbers

## 3. Load and temperature

- [x] 3.1 Add the native declarations for the source the spike chose for load, and sample it with a reused buffer: baseline first, then the busiest engine's share of time since the previous sample, clamped to 0..100
- [x] 3.2 Drop the load baseline when no load reading is in demand
- [x] 3.3 Sample the temperature; publish a value above zero and report unavailable with a reason otherwise
- [x] 3.4 Add query counters per GPU group, as for CPU and memory

## 4. Power and clock

- [x] 4.1 Add the internal `IGpuVendorReader` seam and a way for tests to supply one
- [x] 4.2 Implement `NvmlReader`: load `nvml.dll` from the system directory on first demand, initialise once, find the device by PCI location, read power in watts and the graphics clock in MHz, shut down in the provider's `Shutdown`
- [x] 4.3 Report power and clock unavailable with a clear reason when the adapter is not NVIDIA, the library is missing or initialisation fails, and do not retry until the provider restarts
- [x] 4.4 Log each adapter and the source of each of its readings once when the provider starts sampling GPU readings

## 5. Tests

- [x] 5.1 Catalog tests with a fake adapter list: four main paths and four per adapter, labels and names, the temperature's range, levels and display unit, no GPU entries without a hardware adapter
- [x] 5.2 Adapter order tests: most dedicated memory first, ties in enumeration order, software adapters dropped
- [x] 5.3 Demand tests: no GPU query without a GPU subscriber; only the wanted group is queried; the vendor reader is not opened unless power or clock is wanted; an adapter number that does not exist is unavailable
- [x] 5.4 Vendor reader tests with a fake: values are published; a reader that fails to open makes power and clock unavailable while load keeps publishing; no second open attempt
- [x] 5.5 A test that runs against the real machine and is skipped without a hardware adapter: load is within 0..100 after two samples

## 6. Verify

- [x] 6.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, and `openspec validate --all --strict`
- [x] 6.2 `UrDeck.Host.exe --snapshot` of a page with a 4x4 stats card using GPU temperature, GPU load, GPU power and GPU clock: values, not dashes
- [x] 6.3 On the panel, as a standard user: compare `system:gpu/load` and `system:gpu/temperature` with Task Manager and `nvidia-smi` idle and under load; record the result, private bytes and CPU of the host with the full card in `docs/perf/gpu-readings.md`
- [x] 6.4 Owner runs a GPU load until the temperature passes 80 degrees (or sets a lower slot `warning`) and looks at the gauge's colour change; adjust the catalog's levels if they are off for this card
- [x] 6.5 On the panel: a page with only CPU and memory cards logs no GPU source and has the memory of `docs/perf/data-providers.md`; removing the GPU slots stops the GPU queries
- [x] 6.6 Rename `nvml.dll` out of reach in a test copy or block the load through the seam: power and clock show the dash with one log line and the other readings keep their values

## 7. Documentation

- [x] 7.1 `README.md`: the GPU readings, what is verified per vendor, and an example 4x4 performance card
- [x] 7.2 `CONTRIBUTING.md` or a note in the provider: how to add a vendor reader (AMD, Intel) behind the existing ids
- [x] 7.3 `docs/ROADMAP.md`: "Current state", item 3 (four changes now, elevation last), the readings list in item 7
