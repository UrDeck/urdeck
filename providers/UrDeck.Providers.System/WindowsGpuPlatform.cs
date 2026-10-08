// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace UrDeck.Providers.Machine;

/// <summary>
/// The GPU platform on Windows: the graphics kernel for adapters, load and temperature, NVML for NVIDIA. The structure
/// offsets and sizes are the ones the spike measured (docs/perf/gpu-readings.md); the kernel insists on exact sizes.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class WindowsGpuPlatform : IGpuPlatform
{
    // D3DKMT_QUERYADAPTERINFO types.
    private const int TypeSegmentSize = 3;
    private const int TypeAdapterAddress = 6;
    private const int TypeRegistryInfo = 8;
    private const int TypeAdapterType = 15;
    private const int TypeDeviceIds = 31;
    private const int TypePerformanceData = 62;

    // D3DKMT_QUERYSTATISTICS.
    private const int StatisticsAdapter = 0;
    private const int StatisticsNode = 5;
    private const int StatisticsSize = 1024; // the structure is 808 bytes on x64
    private const int StatisticsResult = 24;
    private const int StatisticsNodeId = 800;
    private const int StatisticsNodeCount = 28;

    private const int SoftwareDevice = 4;
    private const uint MicrosoftVendor = 0x1414;
    private const int PerformanceDataSize = 64;
    private const int PerformanceDataTemperature = 56;

    public IReadOnlyList<GpuAdapter> Adapters()
    {
        var request = new KmtEnumAdapters();
        if (NativeMethods.D3DKMTEnumAdapters2(ref request) != 0 || request.Count == 0)
            return [];

        var infos = new KmtAdapterInfo[request.Count];
        var adapters = new List<GpuAdapter>(infos.Length);
        fixed (KmtAdapterInfo* pointer = infos)
        {
            request.Adapters = pointer;
            if (NativeMethods.D3DKMTEnumAdapters2(ref request) != 0)
                return [];

            for (int i = 0; i < request.Count; i++)
            {
                adapters.Add(Describe(infos[i]));
                Close(infos[i].Handle);
            }
        }

        return adapters;
    }

    public long Now() => (long)(Stopwatch.GetTimestamp() * (10_000_000.0 / Stopwatch.Frequency));

    public int EngineCount(GpuAdapter adapter)
    {
        byte* query = stackalloc byte[StatisticsSize];
        Prepare(query, StatisticsAdapter, adapter);
        return NativeMethods.D3DKMTQueryStatistics(query) == 0 ? (int)*(uint*)(query + StatisticsNodeCount) : 0;
    }

    public bool TryReadRunningTime(GpuAdapter adapter, int engine, out long runningTime)
    {
        byte* query = stackalloc byte[StatisticsSize];
        Prepare(query, StatisticsNode, adapter);
        *(uint*)(query + StatisticsNodeId) = (uint)engine;
        if (NativeMethods.D3DKMTQueryStatistics(query) != 0)
        {
            runningTime = 0;
            return false;
        }

        runningTime = *(long*)(query + StatisticsResult);
        return true;
    }

    public bool TryReadTemperature(GpuAdapter adapter, out double celsius)
    {
        // D3DKMT_ADAPTER_PERFDATA: the temperature is a uint in tenths of a degree. Zero means the driver reports none.
        celsius = 0;
        var open = new KmtOpenAdapter { Luid = Luid.From(adapter.Luid) };
        if (NativeMethods.D3DKMTOpenAdapterFromLuid(ref open) != 0)
            return false;

        byte* data = stackalloc byte[PerformanceDataSize];
        bool read = Query(open.Handle, TypePerformanceData, data, PerformanceDataSize);
        Close(open.Handle);
        if (!read)
            return false;

        uint tenths = *(uint*)(data + PerformanceDataTemperature);
        celsius = tenths / 10.0;
        return tenths > 0;
    }

    public IGpuVendorReader? CreateVendorReader(GpuAdapter adapter) =>
        adapter.VendorId == NvmlReader.NvidiaVendorId ? new NvmlReader() : null;

    private static void Prepare(byte* query, int type, GpuAdapter adapter)
    {
        new Span<byte>(query, StatisticsSize).Clear();
        *(int*)query = type;
        *(Luid*)(query + 4) = Luid.From(adapter.Luid);
    }

    private static GpuAdapter Describe(KmtAdapterInfo info)
    {
        byte* data = stackalloc byte[2080]; // the registry info is four WCHAR[260]
        string name = "GPU";
        uint flags = 0;
        ulong dedicated = 0;
        uint vendor = 0;
        int bus = 0;
        int device = 0;
        int function = 0;

        if (Query(info.Handle, TypeRegistryInfo, data, 2080) && Marshal.PtrToStringUni((IntPtr)data) is { Length: > 0 } text)
            name = text.Trim();
        if (Query(info.Handle, TypeAdapterType, data, 4))
            flags = *(uint*)data;
        if (Query(info.Handle, TypeSegmentSize, data, 24))
            dedicated = *(ulong*)data;
        if (Query(info.Handle, TypeDeviceIds, data, 28))
            vendor = ((uint*)data)[1];
        if (Query(info.Handle, TypeAdapterAddress, data, 12))
        {
            bus = (int)((uint*)data)[0];
            device = (int)((uint*)data)[1];
            function = (int)((uint*)data)[2];
        }

        bool software = (flags & SoftwareDevice) != 0 || vendor == MicrosoftVendor;
        return new GpuAdapter(info.Luid.Value, name, dedicated, vendor, software, bus, device, function);
    }

    private static void Close(uint handle)
    {
        var close = new KmtCloseAdapter { Handle = handle };
        _ = NativeMethods.D3DKMTCloseAdapter(ref close); // nothing to do if it fails: the handle is not used again
    }

    private static bool Query(uint handle, int type, byte* data, int size)
    {
        new Span<byte>(data, size).Clear();
        var query = new KmtQueryAdapterInfo { Handle = handle, Type = type, Data = data, Size = size };
        return NativeMethods.D3DKMTQueryAdapterInfo(ref query) == 0;
    }
}
