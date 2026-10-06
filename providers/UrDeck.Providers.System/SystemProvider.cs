// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Machine;

/// <summary>
/// Readings about the local machine that need no administrator rights: the load of the whole CPU and of each logical
/// processor (from processor times, no handle and no per-sample allocation) and the share of physical memory in use.
/// </summary>
[SupportedOSPlatform("windows")]
[DataProvider("system", "System", DefaultIntervalMs = 2000, MinIntervalMs = 250)]
public sealed class SystemProvider : IDataProvider
{
    private const string CpuLoad = "cpu/load";
    private const string MemoryLoad = "memory/load";
    private const string CorePrefix = "cpu/core/";
    private const string CoreSuffix = "/load";

    /// <summary>What the subscribers want; replaced whole so a sample reads a consistent set.</summary>
    private sealed record Demand(bool Total, bool[] Cores, string[] MissingCores, bool Memory)
    {
        public bool Cpu => Total || Array.IndexOf(Cores, true) >= 0;
    }

    private static readonly Demand None = new(false, [], [], false);

    private IReadingSink? _sink;
    private Demand _demand = None;
    private int _coreCount;
    private ProcessorTimes[] _buffer = [];
    private long[] _previousIdle = [];
    private long[] _previousBusy = [];
    private bool _hasBaseline;

    // Counted so a test can see that an unwanted group is not queried.
    internal int CpuQueries { get; private set; }

    internal int MemoryQueries { get; private set; }

    public IReadOnlyList<ReadingDescriptor> Describe()
    {
        int cores = CoreCount();
        string cpu = CpuName();
        var catalog = new List<ReadingDescriptor>(cores + 2)
        {
            new(CpuLoad, ReadingKind.Percent, "CPU", "CPU load") { Device = cpu, Min = 0, Max = 100 },
        };
        for (int n = 1; n <= cores; n++)
        {
            string number = n.ToString(CultureInfo.InvariantCulture);
            catalog.Add(new ReadingDescriptor($"{CorePrefix}{number}{CoreSuffix}", ReadingKind.Percent, $"Core {number}", $"CPU core {number} load")
            {
                Device = cpu,
                Min = 0,
                Max = 100,
            });
        }

        catalog.Add(new ReadingDescriptor(MemoryLoad, ReadingKind.Percent, "Memory", "Memory load") { Min = 0, Max = 100 });
        return catalog;
    }

    public void Start(IReadingSink sink)
    {
        _sink = sink;
        EnsureBuffers();
    }

    public void SetDemand(IReadOnlyCollection<string> paths)
    {
        int cores = CoreCount();
        bool total = false;
        bool memory = false;
        bool[] wantedCores = new bool[cores];
        List<string>? missing = null;
        foreach (string path in paths)
        {
            if (string.Equals(path, CpuLoad, StringComparison.OrdinalIgnoreCase))
                total = true;
            else if (string.Equals(path, MemoryLoad, StringComparison.OrdinalIgnoreCase))
                memory = true;
            else if (TryParseCore(path, out int number))
            {
                if (number >= 1 && number <= cores)
                    wantedCores[number - 1] = true;
                else
                    (missing ??= []).Add(path);
            }
        }

        _demand = new Demand(total, wantedCores, missing?.ToArray() ?? [], memory);
    }

    public Task SampleAsync(CancellationToken cancellationToken)
    {
        var sink = _sink;
        var demand = _demand;
        if (sink == null)
            return Task.CompletedTask;

        foreach (string path in demand.MissingCores)
            sink.Unavailable(path, "this machine has no such logical processor");

        if (demand.Cpu)
            SampleCpu(sink, demand);
        else
            _hasBaseline = false; // times measured before a pause say nothing about the load after it

        if (demand.Memory)
            SampleMemory(sink);
        return Task.CompletedTask;
    }

    public void Shutdown()
    {
        _sink = null;
        _demand = None;
        _hasBaseline = false;
    }

    private void SampleCpu(IReadingSink sink, Demand demand)
    {
        EnsureBuffers();
        CpuQueries++;
        int status = NativeMethods.NtQuerySystemInformation(
            NativeMethods.SystemProcessorPerformanceInformation, ref _buffer[0], _buffer.Length * Marshal.SizeOf<ProcessorTimes>(), out _);
        if (status != 0)
            throw new InvalidOperationException($"NtQuerySystemInformation failed with status 0x{status:X8}");

        long totalIdle = 0;
        long totalBusy = 0;
        for (int i = 0; i < _buffer.Length; i++)
        {
            // Kernel time includes the idle time, so busy time is kernel plus user minus idle.
            long idle = _buffer[i].IdleTime;
            long busy = _buffer[i].KernelTime + _buffer[i].UserTime - idle;
            if (_hasBaseline)
            {
                long deltaIdle = idle - _previousIdle[i];
                long deltaBusy = busy - _previousBusy[i];
                totalIdle += deltaIdle;
                totalBusy += deltaBusy;
                if (i < demand.Cores.Length && demand.Cores[i])
                    Publish(sink, $"{CorePrefix}{(i + 1).ToString(CultureInfo.InvariantCulture)}{CoreSuffix}", deltaIdle, deltaBusy);
            }

            _previousIdle[i] = idle;
            _previousBusy[i] = busy;
        }

        // The first query only sets the baseline: a load needs two samples.
        if (_hasBaseline && demand.Total)
            Publish(sink, CpuLoad, totalIdle, totalBusy);
        _hasBaseline = true;
    }

    private static void Publish(IReadingSink sink, string path, long deltaIdle, long deltaBusy)
    {
        long elapsed = deltaIdle + deltaBusy;
        if (elapsed <= 0)
            return;
        sink.Publish(path, Math.Clamp(100.0 * deltaBusy / elapsed, 0, 100));
    }

    private void SampleMemory(IReadingSink sink)
    {
        MemoryQueries++;
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
            throw new InvalidOperationException($"GlobalMemoryStatusEx failed ({Marshal.GetLastWin32Error()})");
        sink.Publish(MemoryLoad, status.MemoryLoad);
    }

    private static bool TryParseCore(string path, out int number)
    {
        number = 0;
        return path.StartsWith(CorePrefix, StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(CoreSuffix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(path.AsSpan(CorePrefix.Length, path.Length - CorePrefix.Length - CoreSuffix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private void EnsureBuffers()
    {
        int cores = CoreCount();
        if (_buffer.Length == cores)
            return;
        _buffer = new ProcessorTimes[cores];
        _previousIdle = new long[cores];
        _previousBusy = new long[cores];
        _hasBaseline = false;
    }

    /// <summary>The number of logical processors Windows reports times for.</summary>
    private int CoreCount()
    {
        if (_coreCount > 0)
            return _coreCount;

        var probe = new ProcessorTimes[256];
        int status = NativeMethods.NtQuerySystemInformation(
            NativeMethods.SystemProcessorPerformanceInformation, ref probe[0], probe.Length * Marshal.SizeOf<ProcessorTimes>(), out int returned);
        int count = status == 0 ? returned / Marshal.SizeOf<ProcessorTimes>() : 0;
        _coreCount = count > 0 ? count : Environment.ProcessorCount;
        return _coreCount;
    }

    private static string CpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            string? name = key?.GetValue("ProcessorNameString") as string;
            return string.IsNullOrWhiteSpace(name) ? "CPU" : name.Trim();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return "CPU";
        }
    }
}
