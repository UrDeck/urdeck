## Context

See `proposal.md` and `docs/handoff/2026-10-31-specialized-displays.md` for why. The facts known from Microsoft's
documentation (not yet seen on hardware):

- A specialized display is a `Windows.Devices.Display.Core.DisplayTarget` whose `UsageKind` is
  `DisplayMonitorUsageKind.SpecialDesign`. It is not an `HMONITOR`; `EnumDisplayMonitors`, `MonitorPlacement` and the
  monitor picker do not see it.
- Drawing on it follows the sample `DisplayCoreCustomCompositor`: `DisplayManager.Create`,
  `TryAcquireTargetsAndCreateEmptyState`, `ConnectTarget`, a mode from `FindModes`, `TryApply`, then
  `CreateDisplayDevice(target.Adapter)`, `CreateScanoutSource`, two `CreatePrimary` surfaces, `CreateSimpleScanout`,
  D3D interop through `IDisplayDeviceInterop` (`CreateSharedHandle` / `OpenSharedHandle`), a shared fence, and per frame
  `CreateTask`, `SetScanout`, `SetWait(fence, value)`, `ExecuteTask`, `WaitForVBlank`.
- `IDisplayDeviceInterop` is a classic COM interface (`windows.devices.display.core.interop.h`), not in the WinRT
  metadata; C# has to declare it. Its IID is believed to be `64338358-366A-471B-BD56-DD8EF48E439B`; check it against
  the header in the installed Windows SDK.
- Windows does not route touch or mouse input to a specialized display, and does not power it on by itself.
- SkiaSharp 4.153.1 (the repo's version) has `GRContext.CreateDirect3D(GRD3DBackendContext)` and
  `GRD3DTextureResourceInfo`, so Skia can draw on the GPU through Direct3D 12.
- The host's widgets draw on an `SKCanvas`; `PageRenderer.Render` draws a whole page (background, pattern, cards,
  dock, indicator, moving borders) on one canvas; that is what `--snapshot`, the goldens and the site use.

## Goals / Non-Goals

**Goals:**
- Answer S1 to S7 below on the implementer's PC with the touch panel, and record the answers and the go or no-go.
- Leave probe code behind that `specialized-display-output` can reuse (interop declarations, mode selection, the Raw
  Input reader), but without polishing it for that.

**Non-Goals:**
- Showing the real deck on a specialized display (that is `specialized-display-output`).
- Any change to the normal start, the window, the settings window or `--snapshot`.
- Tests in CI for the probe. The probe is exercised by hand; the code that survives into the next change gets tests there.

## Decisions

### D1. The probe lives in the host, reached like `--snapshot`

`Program.Main` handles `--direct` after `--snapshot` and before `--quit`, the instance guard and the XAML application,
and returns its exit code. It needs no data folder beyond the log (it logs to `urdeck.log` as usual, with a
`Direct probe:` prefix). Living in the host means the `net10.0-windows10.0.19041.0` projection of
`Windows.Devices.Display.Core` and the host's packaging are what is tested, not a lookalike console app.
*Alternative:* a separate console project under `tools/`. Rejected: a different TFM and packaging could behave
differently (for example in what an unpackaged process may acquire), and the code would have to move later.

The probe needs a message loop for Raw Input, session and power notifications: it creates one message-only window
(`HWND_MESSAGE`) on its thread and pumps messages; rendering runs on a second thread that waits for the vertical blank.

### D2. Direct3D bindings: Vortice.Windows

The probe needs D3D11, D3D12 and DXGI from C#. Use the `Vortice.Direct3D11`, `Vortice.Direct3D12` and `Vortice.DXGI`
packages (MIT, maintained, used with SkiaSharp elsewhere), in the host only, pinned in `Directory.Packages.props`,
listed in the third-party notices. `IDisplayDeviceInterop` is declared by hand with `[GeneratedComInterface]`.
*Alternative:* `Microsoft.Windows.CsWin32` source generation. Workable, fewer runtime assemblies, but its D3D12 output
is harder to read and an agent produces it less reliably. Either is acceptable if Vortice has a blocker; record why.

### D3. Two rendering paths, both tried

- **GPU (preferred):** a D3D12 device on the target's adapter (DXGI `EnumAdapterByLuid` with the Display.Core adapter
  id), the primaries opened with `ID3D12Device.OpenSharedHandle`, wrapped as `GRBackendRenderTarget` with
  `GRD3DTextureResourceInfo`, and drawn with a Skia `GRContext.CreateDirect3D`. The fence is a shared `ID3D12Fence`
  opened as a `DisplayFence`.
- **Raster (fallback, as in the sample):** a D3D11 device; the primaries opened with `OpenSharedResource1`; Skia draws
  into an `SKBitmap` and `UpdateSubresource` copies it to the primary; fence `ID3D11Fence`.

`--raster` selects the fallback. Both are timed (S3). A moving SkSL background on the CPU at 1100x3840 is expected to
be too slow, so if only the raster path works, moving backgrounds would be drawn still on a specialized display: an
acceptable outcome, but it must be measured, not assumed.

### D4. Touch is read with Raw Input

Register `RAWINPUTDEVICE { UsagePage = 0x0D (digitizer), Usage = 0x04 (touch screen), Flags = RIDEV_INPUTSINK |
RIDEV_DEVNOTIFY }` on the message-only window, and parse `WM_INPUT` reports with `HidP_GetUsages` /
`HidP_GetUsageValue` and the device's preparsed data: contact count (0x0D/0x54), and per contact the contact id
(0x0D/0x51), tip switch (0x0D/0x42), X (0x01/0x30) and Y (0x01/0x31) with their logical ranges. The probe draws the
points where it believes the finger is, so a wrong axis, mirror or rotation is visible at once.
*Alternatives, only if S1 shows Windows also acts on the touches:* `RegisterPointerInputTarget` (redirects all touch
of a type to one window; needs UIAccess, which needs a signed binary in a secure folder, which the per-user installer
is not), or a Windows setting that unmaps the digitizer (Tablet PC Settings, the `Wisp\Pen\Digimon` mapping). Record
what each does; no driver is written.

## The questions

Each answer goes in `docs/perf/specialized-display.md` with what was run and what was seen.

**S1. Where do the panel's touches go once it is specialized? (go/no-go)** With the panel a desktop monitor, touch it:
the deck's area responds (baseline). Remove it from the desktop and, with no UrDeck running, touch it: does the
cursor move or click on another display, does the touch keyboard or any gesture appear, does nothing happen? Repeat
with the probe running. Then repeat after each mitigation in D4 that applies. *Go* needs a setup in which touches on
the panel have no effect on any desktop display, reachable by an ordinary user (a documented Windows setting counts;
admin rights once, for a setting, count; a driver does not).

**S2. Can the digitizer be read? (go/no-go)** With the probe running, do `WM_INPUT` reports arrive for the panel's
digitizer while the panel is specialized, while another window has focus, and while the session is on the lock screen
(record; reading while locked is not needed)? Are multiple contacts reported? How is the panel's digitizer told from
another touch screen (the HID device's container id against the monitor's, the Digimon mapping, the name)? How do
the logical X/Y ranges map to the panel's pixels in its native scanout orientation? *Go* needs reliable single-touch
reports mapped to the right pixel within a few pixels; multi-touch is recorded, not required.

**S3. Which rendering path works, and what does it cost?** Run the probe in GPU mode and in `--raster` mode. Record for
each: does it show, any error, the time to draw and present one full frame (median and 95th percentile over 300
frames), CPU and GPU use while presenting 30 frames per second, and while presenting nothing (the last frame stays on
the panel?). Also: do the primaries open in D3D12 at all (if not, record the HRESULT), and does the GPU path need the
primary created with particular flags? *Decides* the rendering path of `specialized-display-output`.

**S4. Orientation and mode.** What modes does `FindModes(OnlyPreferredResolution)` offer for the panel (record all)?
Is the preferred resolution landscape (3840x1100) or portrait? Does `DisplayPath.Rotation` take 90 or 270 degrees on
this driver, and does the result look right? Does Windows keep any orientation for the target from when it was on the
desktop? *Decides* whether UrDeck rotates with the path or in Skia, and the default orientation.

**S5. Identity across the toggle.** For the panel, log the desktop identity UrDeck remembers today (`display.name`,
`model`, `path` from `MonitorPlacement`) and the specialized target's `TryGetMonitor()` `DisplayName`, `DeviceId`,
`StableMonitorId`, EDID manufacturer and product code, and `DisplayTarget.DeviceInterfacePath` and
`StableMonitorId`. Which of these equals the remembered `path` or `model`? Do they survive a reboot, a replug, and
moving the cable to another output? *Decides* how a remembered display is matched to a specialized target.

**S6. Access and events.** Does an unpackaged, non-elevated `UrDeck.Host.exe` (the build output and a copy installed
with `scripts/Install-Local.ps1`) acquire the target? What does a second acquire from another process return? What
happens to the probe when the user puts the panel back on the desktop while it runs, unplugs it, or plugs it back
(which `DisplayManager` events fire after `Start()`: `Changed`, `Enabled`, `Disabled`, `PathsFailedOrInvalidated`;
does `IsStale` turn true)? Over Remote Desktop (`RemoteSessionNotSupported` expected)? When the probe is killed, is the
target released (another probe can take it)?

**S7. Power.** While the probe shows the pattern: let Windows switch the displays off (power settings, one minute);
lock the session; sleep and resume; sign out. For each, what does the panel show, does `GUID_CONSOLE_DISPLAY_STATE` /
`WTS_SESSION_LOCK` arrive on the message window, does the panel go dark if the probe disconnects its path
(`TryApply` with the target disconnected, then reconnect) versus presenting a black frame, and does the target need to
be acquired again after resume? *Decides* how `specialized-display-output` blanks the panel.

## Risks / Trade-offs

- [Windows sends the panel's touches to the primary display and nothing an ordinary user can do stops it] → S1 is
  first; the result is a no-go recorded with the evidence, and the work stops at a small diagnostic change.
- [The GPU path does not open the primaries in D3D12] → the raster path is the sample's own; S3 measures whether it is
  good enough without moving backgrounds.
- [The driver behaves differently on another GPU vendor] → record the vendor and driver; the owner's audience is
  mostly NVIDIA and AMD, and the site will say what was tested.
- [The probe is run on a PC with only one display] → never specialize the only display; the probe refuses a target
  when no desktop display would remain, and the docs say how to undo the setting (it is in Settings on the remaining
  display; Windows restores it when the setting is turned off).

## Migration Plan

None: nothing user-visible ships. The probe stays in the code until `specialized-display-output` replaces it, or is
removed in a no-go follow-up.

## Open Questions

None that change this change; the questions are its content.
