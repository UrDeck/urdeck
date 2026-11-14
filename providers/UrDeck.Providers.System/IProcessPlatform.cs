// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Providers.Machine;

/// <summary>One process as one enumeration saw it.</summary>
/// <param name="Id">The process id.</param>
/// <param name="Name">The executable's name without its extension.</param>
/// <param name="CpuTime">The processor time the process has used in total.</param>
/// <param name="WorkingSetBytes">The physical memory the process holds.</param>
internal readonly record struct ProcessSample(int Id, string Name, TimeSpan CpuTime, long WorkingSetBytes);

/// <summary>
/// What the top-processes reading asks of the machine. Windows implements it for real; tests supply a fake one. Nothing is
/// kept between calls, so nothing has to be released when the provider stops.
/// </summary>
internal interface IProcessPlatform
{
    /// <summary>How many logical processors share the load; the denominator of a process's share.</summary>
    int ProcessorCount { get; }

    /// <summary>A monotonic clock; the difference of two readings is the time between two enumerations.</summary>
    TimeSpan Now();

    /// <summary>Every process whose times can be read (the idle process and those that deny access are left out).</summary>
    IReadOnlyList<ProcessSample> Enumerate();
}
