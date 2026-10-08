// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.Versioning;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Machine;

/// <summary>
/// Readings about the local machine that need no administrator rights: CPU load (whole and per logical processor), the
/// share of physical memory in use, and the load, temperature, power and clock of each GPU. Each group lives in its own
/// class and is queried only while one of its readings is subscribed to.
/// </summary>
[SupportedOSPlatform("windows")]
[DataProvider("system", "System", DefaultIntervalMs = 2000, MinIntervalMs = 250)]
public sealed class SystemProvider : IDataProvider
{
    /// <summary>What the subscribers want; replaced whole so a sample reads a consistent set.</summary>
    private sealed record Demand(bool Total, bool[] Cores, string[] MissingCores, bool Memory, GpuWant[] Gpu, string[] MissingGpu)
    {
        public bool Cpu => Total || Array.IndexOf(Cores, true) >= 0;
    }

    private static readonly Demand None = new(false, [], [], false, [], []);

    private readonly CpuReadings _cpu = new();
    private readonly MemoryReadings _memory = new();
    private readonly GpuReadings _gpu;
    private IReadingSink? _sink;
    private Demand _demand = None;

    public SystemProvider()
        : this(new WindowsGpuPlatform())
    {
    }

    internal SystemProvider(IGpuPlatform gpuPlatform) => _gpu = new GpuReadings(gpuPlatform);

    internal int CpuQueries => _cpu.Queries;

    internal int MemoryQueries => _memory.Queries;

    internal int GpuLoadQueries => _gpu.LoadQueries;

    internal int GpuTemperatureQueries => _gpu.TemperatureQueries;

    internal int GpuVendorQueries => _gpu.VendorQueries;

    public IReadOnlyList<ReadingDescriptor> Describe()
    {
        var catalog = new List<ReadingDescriptor>(_cpu.CoreCount() + 10);
        _cpu.Describe(catalog);
        MemoryReadings.Describe(catalog);
        _gpu.Describe(catalog);
        return catalog;
    }

    public void Start(IReadingSink sink)
    {
        _sink = sink;
        _cpu.EnsureBuffers();
    }

    public void SetDemand(IReadOnlyCollection<string> paths)
    {
        int cores = _cpu.CoreCount();
        int adapters = _gpu.Count;
        bool total = false;
        bool memory = false;
        bool[] wantedCores = new bool[cores];
        var gpu = new GpuWant[adapters];
        List<string>? missingCores = null;
        List<string>? missingGpu = null;
        foreach (string path in paths)
        {
            if (string.Equals(path, CpuReadings.Total, StringComparison.OrdinalIgnoreCase))
                total = true;
            else if (string.Equals(path, MemoryReadings.Load, StringComparison.OrdinalIgnoreCase))
                memory = true;
            else if (CpuReadings.TryParseCore(path, out int number))
            {
                if (number >= 1 && number <= cores)
                    wantedCores[number - 1] = true;
                else
                    (missingCores ??= []).Add(path);
            }
            else if (GpuReadings.TryParse(path, out int adapter, out var kind, out bool alias))
            {
                if (adapter >= 1 && adapter <= adapters)
                {
                    var current = gpu[adapter - 1];
                    gpu[adapter - 1] = alias ? current with { Alias = current.Alias | kind } : current with { Numbered = current.Numbered | kind };
                }
                else
                    (missingGpu ??= []).Add(path);
            }
        }

        _demand = new Demand(total, wantedCores, missingCores?.ToArray() ?? [], memory, gpu, missingGpu?.ToArray() ?? []);
    }

    public Task SampleAsync(CancellationToken cancellationToken)
    {
        var sink = _sink;
        var demand = _demand;
        if (sink == null)
            return Task.CompletedTask;

        foreach (string path in demand.MissingCores)
            sink.Unavailable(path, "this machine has no such logical processor");
        foreach (string path in demand.MissingGpu)
            sink.Unavailable(path, "this machine has no such GPU");

        if (demand.Cpu)
            _cpu.Sample(sink, demand.Total, demand.Cores);
        else
            _cpu.DropBaseline();

        if (demand.Memory)
            _memory.Sample(sink);

        _gpu.Sample(sink, demand.Gpu);
        return Task.CompletedTask;
    }

    public void Shutdown()
    {
        _sink = null;
        _demand = None;
        _cpu.DropBaseline();
        _gpu.Shutdown();
    }
}
