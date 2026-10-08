// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace UrDeck.Providers.Machine;

/// <summary>
/// Power and clock of an NVIDIA adapter through <c>nvml.dll</c>, the library the NVIDIA driver installs and
/// <c>nvidia-smi</c> uses. Loaded from the Windows system directory only, so a copy planted elsewhere is never picked up.
/// The device is found by the adapter's PCI location, so a PC with two NVIDIA cards reads each card's own values.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class NvmlReader : IGpuVendorReader
{
    public const uint NvidiaVendorId = 0x10DE;

    private const int NvmlSuccess = 0;
    private const int ClockGraphics = 0;

    private IntPtr _library;
    private bool _initialised;
    private IntPtr _device;
    private delegate* unmanaged<int> _shutdown;
    private delegate* unmanaged<IntPtr, uint*, int> _power;
    private delegate* unmanaged<IntPtr, int, uint*, int> _clock;

    public string Source => "nvml.dll";

    public bool TryOpen(GpuAdapter adapter, out string failure)
    {
        string path = Path.Combine(Environment.SystemDirectory, "nvml.dll");
        if (!NativeLibrary.TryLoad(path, out _library))
        {
            failure = "nvml.dll could not be loaded; the NVIDIA driver installs it";
            return false;
        }

        try
        {
            var init = (delegate* unmanaged<int>)NativeLibrary.GetExport(_library, "nvmlInit_v2");
            var byPci = (delegate* unmanaged<byte*, IntPtr*, int>)NativeLibrary.GetExport(_library, "nvmlDeviceGetHandleByPciBusId_v2");
            _shutdown = (delegate* unmanaged<int>)NativeLibrary.GetExport(_library, "nvmlShutdown");
            _power = (delegate* unmanaged<IntPtr, uint*, int>)NativeLibrary.GetExport(_library, "nvmlDeviceGetPowerUsage");
            _clock = (delegate* unmanaged<IntPtr, int, uint*, int>)NativeLibrary.GetExport(_library, "nvmlDeviceGetClockInfo");

            int status = init();
            if (status != NvmlSuccess)
            {
                failure = $"nvml.dll did not start (status {status})";
                Close();
                return false;
            }

            _initialised = true;
            byte[] id = Encoding.ASCII.GetBytes(adapter.PciBusId + "\0");
            IntPtr device;
            fixed (byte* pointer = id)
                status = byPci(pointer, &device);
            if (status != NvmlSuccess)
            {
                failure = $"nvml.dll does not know the adapter at {adapter.PciBusId} (status {status})";
                Close();
                return false;
            }

            _device = device;
            failure = string.Empty;
            return true;
        }
        catch (EntryPointNotFoundException)
        {
            failure = "nvml.dll is too old: a function is missing";
            Close();
            return false;
        }
    }

    public double? PowerWatts()
    {
        uint milliwatts;
        return _power != null && _power(_device, &milliwatts) == NvmlSuccess ? milliwatts / 1000.0 : null;
    }

    public double? ClockMegahertz()
    {
        uint megahertz;
        return _clock != null && _clock(_device, ClockGraphics, &megahertz) == NvmlSuccess ? megahertz : null;
    }

    public void Close()
    {
        if (_initialised && _shutdown != null)
            _shutdown();
        _initialised = false;
        if (_library != IntPtr.Zero)
            NativeLibrary.Free(_library);
        _library = IntPtr.Zero;
        _device = IntPtr.Zero;
        _power = null;
        _clock = null;
    }
}
