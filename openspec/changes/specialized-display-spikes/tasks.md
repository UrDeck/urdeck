## 1. Probe scaffolding

- [ ] 1.1 `--direct list|probe` in `Program.Main` after `--snapshot`, before `--quit` and the instance guard; exit codes 0 (done), 1 (failed, reason logged), 2 (bad arguments); `Direct probe:` log lines
- [ ] 1.2 Add the Vortice D3D11, D3D12 and DXGI packages (host only, `Directory.Packages.props`, third-party notices); declare `IDisplayDeviceInterop` with `[GeneratedComInterface]`, IID checked against the installed Windows SDK header
- [ ] 1.3 A message-only window on the probe's thread with a message loop; `Ctrl+C` and `--seconds` end the probe and release the target
- [ ] 1.4 Refuse to acquire when the target is the only display (no desktop display would remain), with a log line

## 2. Listing (`--direct list`)

- [ ] 2.1 Every `DisplayTarget`: usage kind, `IsConnected`, adapter id and name, `DeviceInterfacePath`, `StableMonitorId`, the monitor's name, `DeviceId`, EDID manufacturer and product code, preferred modes
- [ ] 2.2 The desktop displays as `MonitorPlacement` sees them (name, model, path), side by side with 2.1 for S5
- [ ] 2.3 Every HID digitizer (usage page 0x0D): device path, product string, container id, logical X/Y ranges, contact count maximum

## 3. Probe (`--direct probe`)

- [ ] 3.1 Acquire, connect, choose the mode (preferred resolution, closest to 60 Hz, B8G8R8A8), optional rotation argument, `TryApply` with `FailIfStateChanged` in a retry loop; log the chosen mode
- [ ] 3.2 GPU path: D3D12 device on the target's adapter, primaries opened as D3D12 resources, Skia `GRContext.CreateDirect3D`, shared fence, two-surface scanout loop with `WaitForVBlank`
- [ ] 3.3 Raster path (`--raster`): D3D11 device, `OpenSharedResource1`, `SKBitmap` + `UpdateSubresource`
- [ ] 3.4 The test pattern: arrows and "TOP" on each edge, a 100 px grid, a frame counter, the elapsed time, the last touch points with their contact ids
- [ ] 3.5 Frame timing (draw, present; median and p95 over 300 frames) and a "still" mode that presents once and then nothing, logged every 10 s
- [ ] 3.6 Raw Input reader for touch screens (D4), drawing the mapped points; log each contact down and up with raw and mapped coordinates
- [ ] 3.7 `DisplayManager.Start()` and log every `Changed`, `Enabled`, `Disabled`, `PathsFailedOrInvalidated`, and the target's `IsStale` on each
- [ ] 3.8 Log `WM_POWERBROADCAST` (`GUID_CONSOLE_DISPLAY_STATE`) and `WM_WTSSESSION_CHANGE`; a key or argument that disconnects and reconnects the path to compare blanking methods

## 4. Run the spikes (implementer's PC)

- [ ] 4.1 S1 touch routing, with and without the probe, and each mitigation that applies
- [ ] 4.2 S2 digitizer reports, identification and mapping
- [ ] 4.3 S3 both rendering paths, timings, CPU and GPU use (Task Manager or `typeperf`; record the method)
- [ ] 4.4 S4 modes and rotation
- [ ] 4.5 S5 identity across the toggle, a reboot, a replug and another output
- [ ] 4.6 S6 access (build output and an installed copy), a second process, toggling back, unplug, Remote Desktop, a killed probe
- [ ] 4.7 S7 display off, lock, sleep and resume, sign-out

## 5. Record and decide

- [ ] 5.1 `docs/perf/specialized-display.md`: the PC, each question's procedure, observations and answer, photos referenced from `docs/perf/specialized-display/` (small JPEGs), and "Decision: go" or "Decision: no-go, because ..."
- [ ] 5.2 If the answers change anything in `specialized-display-output` (rendering path, rotation default, identity matching, blanking method, touch device selection), update its `design.md` and specs in the same pull request
- [ ] 5.3 `docs/BACKLOG.md` (new item "Specialized displays" with status) and `docs/ROADMAP.md` (later work)

## 6. Verify

- [ ] 6.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, `dotnet format urdeck.slnx --severity warn`, `openspec validate --all --strict`
- [ ] 6.2 The owner checks that a normal start and `--snapshot` behave as before on the panel (the probe code is not reached)
