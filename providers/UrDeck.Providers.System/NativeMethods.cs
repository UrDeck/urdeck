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

internal static class NativeMethods
{
    public const int SystemProcessorPerformanceInformation = 8;

    [DllImport("ntdll.dll")]
    public static extern int NtQuerySystemInformation(int informationClass, ref ProcessorTimes information, int length, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
