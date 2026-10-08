// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Machine;

/// <summary>
/// The load of the whole CPU and of each logical processor, from processor times: no handle and no per-sample allocation.
/// A load needs two samples, so the first one after a pause only sets the baseline.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class CpuReadings
{
    public const string Total = "cpu/load";
    private const string CorePrefix = "cpu/core/";
    private const string CoreSuffix = "/load";

    private int _coreCount;
    private ProcessorTimes[] _buffer = [];
    private long[] _previousIdle = [];
    private long[] _previousBusy = [];
    private bool _hasBaseline;

    /// <summary>Counted so a test can see that an unwanted group is not queried.</summary>
    public int Queries { get; private set; }

    public void Describe(List<ReadingDescriptor> catalog)
    {
        int cores = CoreCount();
        string cpu = CpuName();
        catalog.Add(new(Total, ReadingKind.Percent, "CPU", "CPU load") { Device = cpu, Min = 0, Max = 100 });
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
    }

    public static bool TryParseCore(string path, out int number)
    {
        number = 0;
        return path.StartsWith(CorePrefix, StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(CoreSuffix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(path.AsSpan(CorePrefix.Length, path.Length - CorePrefix.Length - CoreSuffix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    /// <summary>The number of logical processors Windows reports times for.</summary>
    public int CoreCount()
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

    public void EnsureBuffers()
    {
        int cores = CoreCount();
        if (_buffer.Length == cores)
            return;
        _buffer = new ProcessorTimes[cores];
        _previousIdle = new long[cores];
        _previousBusy = new long[cores];
        _hasBaseline = false;
    }

    /// <summary>Times measured before a pause say nothing about the load after it.</summary>
    public void DropBaseline() => _hasBaseline = false;

    public void Sample(IReadingSink sink, bool total, bool[] wantedCores)
    {
        EnsureBuffers();
        Queries++;
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
                if (i < wantedCores.Length && wantedCores[i])
                    Publish(sink, $"{CorePrefix}{(i + 1).ToString(CultureInfo.InvariantCulture)}{CoreSuffix}", deltaIdle, deltaBusy);
            }

            _previousIdle[i] = idle;
            _previousBusy[i] = busy;
        }

        // The first query only sets the baseline: a load needs two samples.
        if (_hasBaseline && total)
            Publish(sink, Total, totalIdle, totalBusy);
        _hasBaseline = true;
    }

    private static void Publish(IReadingSink sink, string path, long deltaIdle, long deltaBusy)
    {
        long elapsed = deltaIdle + deltaBusy;
        if (elapsed <= 0)
            return;
        sink.Publish(path, Math.Clamp(100.0 * deltaBusy / elapsed, 0, 100));
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
