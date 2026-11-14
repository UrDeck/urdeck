// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using System.Runtime.Versioning;

namespace UrDeck.Providers.Machine;

/// <summary>The processes of this Windows machine, through <see cref="Process"/>. Names are never logged.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsProcessPlatform : IProcessPlatform
{
    public int ProcessorCount => Environment.ProcessorCount;

    public TimeSpan Now() => Stopwatch.GetElapsedTime(0);

    public IReadOnlyList<ProcessSample> Enumerate()
    {
        var result = new List<ProcessSample>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == 0)
                    continue;
                try
                {
                    result.Add(new ProcessSample(process.Id, process.ProcessName, process.TotalProcessorTime, process.WorkingSet64));
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // A protected process, or one that ended while it was read: it is not busy as far as this reading can tell.
                }
            }
        }

        return result;
    }
}
