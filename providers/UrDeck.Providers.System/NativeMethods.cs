// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;

namespace UrDeck.Providers.Machine;

/// <summary>One entry of SystemProcessorPerformanceInformation: times in 100 ns units, kernel time includes idle time.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ProcessorTimes
{
    public long IdleTime;
    public long KernelTime;
    public long UserTime;
    public long DpcTime;
    public long InterruptTime;
    public uint InterruptCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MemoryStatus
{
    public uint Length;
    public uint MemoryLoad;
    public ulong TotalPhysical;
    public ulong AvailablePhysical;
    public ulong TotalPageFile;
    public ulong AvailablePageFile;
    public ulong TotalVirtual;
    public ulong AvailableVirtual;
    public ulong AvailableExtendedVirtual;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Luid
{
    public uint Low;
    public int High;

    public readonly long Value => ((long)High << 32) | Low;

    public static Luid From(long value) => new() { Low = (uint)value, High = (int)(value >> 32) };
}

/// <summary>D3DKMT_ADAPTERINFO.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KmtAdapterInfo
{
    public uint Handle;
    public Luid Luid;
    public uint VidPnSources;
    public int PresentMoveRegionsPreferred;
}

/// <summary>D3DKMT_ENUMADAPTERS2.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct KmtEnumAdapters
{
    public uint Count;
    public KmtAdapterInfo* Adapters;
}

/// <summary>D3DKMT_OPENADAPTERFROMLUID.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KmtOpenAdapter
{
    public Luid Luid;
    public uint Handle;
}

/// <summary>D3DKMT_QUERYADAPTERINFO.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct KmtQueryAdapterInfo
{
    public uint Handle;
    public int Type;
    public byte* Data;
    public int Size;
}

/// <summary>D3DKMT_CLOSEADAPTER.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KmtCloseAdapter
{
    public uint Handle;
}

internal static unsafe class NativeMethods
{
    public const int SystemProcessorPerformanceInformation = 8;

    [DllImport("ntdll.dll")]
    public static extern int NtQuerySystemInformation(int informationClass, ref ProcessorTimes information, int length, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    // The graphics kernel interface (D3DKMT) that Task Manager's GPU columns sit on. All of it works for a standard user.
    [DllImport("gdi32.dll")]
    public static extern int D3DKMTEnumAdapters2(ref KmtEnumAdapters adapters);

    [DllImport("gdi32.dll")]
    public static extern int D3DKMTOpenAdapterFromLuid(ref KmtOpenAdapter adapter);

    [DllImport("gdi32.dll")]
    public static extern int D3DKMTQueryAdapterInfo(ref KmtQueryAdapterInfo query);

    [DllImport("gdi32.dll")]
    public static extern int D3DKMTCloseAdapter(ref KmtCloseAdapter adapter);

    /// <summary>Takes a D3DKMT_QUERYSTATISTICS: the type at 0, the adapter LUID at 4, the result at 24, the node number at 800.</summary>
    [DllImport("gdi32.dll")]
    public static extern int D3DKMTQueryStatistics(byte* query);
}
