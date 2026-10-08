// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Machine;

/// <summary>The share of physical memory in use.</summary>
internal sealed class MemoryReadings
{
    public const string Load = "memory/load";

    /// <summary>Counted so a test can see that an unwanted group is not queried.</summary>
    public int Queries { get; private set; }

    public static void Describe(List<ReadingDescriptor> catalog) =>
        catalog.Add(new(Load, ReadingKind.Percent, "Memory", "Memory load") { Min = 0, Max = 100 });

    public void Sample(IReadingSink sink)
    {
        Queries++;
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
            throw new InvalidOperationException($"GlobalMemoryStatusEx failed ({Marshal.GetLastWin32Error()})");
        sink.Publish(Load, status.MemoryLoad);
    }
}
