// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Numerics;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Machine;

/// <summary>The four kinds of GPU reading; a set of them says which are wanted.</summary>
[Flags]
internal enum GpuKinds
{
    None = 0,
    Load = 1,
    Temperature = 2,
    Power = 4,
    Clock = 8,
}

/// <summary>What is wanted of one adapter: through its numbered paths (<c>gpu/1/load</c>) and through the main-adapter paths (<c>gpu/load</c>).</summary>
internal readonly record struct GpuWant(GpuKinds Numbered, GpuKinds Alias)
{
    public GpuKinds Kinds => Numbered | Alias;
}

/// <summary>
/// The GPU readings: load and temperature for every vendor from Windows itself, power and clock through the vendor's own
/// library. Adapters are listed once; nothing is queried, and no vendor library is loaded, for a reading nobody shows.
/// </summary>
internal sealed class GpuReadings(IGpuPlatform platform)
{
    private static readonly string[] Parts = ["load", "temperature", "power", "clock"];
    private static readonly GpuKinds[] AllKinds = [GpuKinds.Load, GpuKinds.Temperature, GpuKinds.Power, GpuKinds.Clock];
    private static readonly string[] AliasPaths = Parts.Select(p => $"gpu/{p}").ToArray();

    private enum VendorStatus
    {
        NotTried,
        Open,
        Failed,
    }

    private sealed class AdapterState(GpuAdapter adapter, int number)
    {
        public GpuAdapter Adapter { get; } = adapter;

        public int Number { get; } = number;

        public string[] Paths { get; } = Parts.Select(p => $"gpu/{number.ToString(CultureInfo.InvariantCulture)}/{p}").ToArray();

        public bool Logged { get; set; }

        public int Engines { get; set; } = -1;

        public long[] Previous { get; set; } = [];

        public long PreviousTime { get; set; }

        public bool HasBaseline { get; set; }

        public VendorStatus Vendor { get; set; }

        public IGpuVendorReader? Reader { get; set; }

        public string Failure { get; set; } = string.Empty;
    }

    private List<AdapterState>? _adapters;

    // Counted so a test can see that an unwanted group is not queried.
    public int LoadQueries { get; private set; }

    public int TemperatureQueries { get; private set; }

    public int VendorQueries { get; private set; }

    /// <summary>The number of hardware adapters.</summary>
    public int Count => Adapters().Count;

    public void Describe(List<ReadingDescriptor> catalog)
    {
        var adapters = Adapters();
        if (adapters.Count == 0)
            return;

        for (int i = 0; i < Parts.Length; i++)
            catalog.Add(Descriptor(AliasPaths[i], AllKinds[i], "GPU", $"GPU {Parts[i]}", adapters[0].Adapter.Name));
        foreach (var state in adapters)
        {
            string number = state.Number.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < Parts.Length; i++)
                catalog.Add(Descriptor(state.Paths[i], AllKinds[i], $"GPU {number}", $"GPU {number} {Parts[i]}", state.Adapter.Name));
        }
    }

    /// <summary>Recognises a GPU path. The adapter number is 1 for the main-adapter paths, which are flagged as aliases.</summary>
    public static bool TryParse(string path, out int adapter, out GpuKinds kind, out bool alias)
    {
        adapter = 0;
        kind = GpuKinds.None;
        alias = false;
        if (!path.StartsWith("gpu/", StringComparison.OrdinalIgnoreCase))
            return false;

        ReadOnlySpan<char> rest = path.AsSpan(4);
        int slash = rest.IndexOf('/');
        ReadOnlySpan<char> part = rest;
        if (slash >= 0)
        {
            if (!int.TryParse(rest[..slash], NumberStyles.None, CultureInfo.InvariantCulture, out adapter))
                return false;
            part = rest[(slash + 1)..];
        }
        else
        {
            adapter = 1;
            alias = true;
        }

        for (int i = 0; i < Parts.Length; i++)
        {
            if (part.Equals(Parts[i], StringComparison.OrdinalIgnoreCase))
            {
                kind = AllKinds[i];
                return true;
            }
        }

        return false;
    }

    public void Sample(IReadingSink sink, GpuWant[] wants)
    {
        var adapters = Adapters();
        for (int i = 0; i < adapters.Count && i < wants.Length; i++)
        {
            var state = adapters[i];
            var want = wants[i];
            if ((want.Kinds & GpuKinds.Load) == 0)
                state.HasBaseline = false; // times measured before a pause say nothing about the load after it
            if (want.Kinds == GpuKinds.None)
                continue;

            LogOnce(sink, state, want.Kinds);
            if ((want.Kinds & GpuKinds.Load) != 0)
                SampleLoad(sink, state, want);
            if ((want.Kinds & GpuKinds.Temperature) != 0)
                SampleTemperature(sink, state, want);
            if ((want.Kinds & (GpuKinds.Power | GpuKinds.Clock)) != 0)
                SampleVendor(sink, state, want);
        }
    }

    public void Shutdown()
    {
        if (_adapters == null)
            return;
        foreach (var state in _adapters)
        {
            state.Reader?.Close();
            state.Reader = null;
            state.Vendor = VendorStatus.NotTried;
            state.HasBaseline = false;
            state.Logged = false;
        }
    }

    private List<AdapterState> Adapters()
    {
        if (_adapters != null)
            return _adapters;

        // Largest dedicated video memory first; the sort is stable, so equal memory keeps Windows' order.
        var hardware = platform.Adapters().Where(a => !a.IsSoftware).OrderByDescending(a => a.DedicatedMemory).ToList();
        var list = new List<AdapterState>(hardware.Count);
        for (int i = 0; i < hardware.Count; i++)
            list.Add(new AdapterState(hardware[i], i + 1));
        _adapters = list;
        return list;
    }

    private static ReadingDescriptor Descriptor(string path, GpuKinds kind, string label, string name, string device) => kind switch
    {
        GpuKinds.Load => new(path, ReadingKind.Percent, label, name) { Device = device, Min = 0, Max = 100 },
        // 80 and 90 degrees hold for current desktop and laptop GPUs of all vendors; hardware temperatures are read in Celsius.
        GpuKinds.Temperature => new(path, ReadingKind.Temperature, label, name)
        {
            Device = device,
            Min = 0,
            Max = 100,
            Warning = 80,
            Critical = 90,
            DisplayUnit = DisplayUnit.Celsius,
        },
        GpuKinds.Power => new(path, ReadingKind.Number, label, name) { Device = device, Unit = "W" },
        _ => new(path, ReadingKind.Number, label, name) { Device = device, Unit = "MHz" },
    };

    private static void Publish(IReadingSink sink, AdapterState state, GpuWant want, GpuKinds kind, double value)
    {
        int index = BitOperations.Log2((uint)kind);
        if ((want.Numbered & kind) != 0)
            sink.Publish(state.Paths[index], value);
        if (state.Number == 1 && (want.Alias & kind) != 0)
            sink.Publish(AliasPaths[index], value);
    }

    private static void Unavailable(IReadingSink sink, AdapterState state, GpuWant want, GpuKinds kind, string reason)
    {
        int index = BitOperations.Log2((uint)kind);
        if ((want.Numbered & kind) != 0)
            sink.Unavailable(state.Paths[index], reason);
        if (state.Number == 1 && (want.Alias & kind) != 0)
            sink.Unavailable(AliasPaths[index], reason);
    }

    private static void LogOnce(IReadingSink sink, AdapterState state, GpuKinds wanted)
    {
        if (state.Logged)
            return;
        state.Logged = true;

        var sources = new List<string>(2);
        if ((wanted & GpuKinds.Load) != 0)
            sources.Add("load from the Windows graphics kernel statistics");
        if ((wanted & GpuKinds.Temperature) != 0)
            sources.Add("temperature from the Windows adapter performance data");
        if (sources.Count > 0)
            sink.Log($"GPU {state.Number.ToString(CultureInfo.InvariantCulture)} is {state.Adapter.Name} ({state.Adapter.Vendor}): {string.Join(", ", sources)}");
    }

    private void SampleLoad(IReadingSink sink, AdapterState state, GpuWant want)
    {
        LoadQueries++;
        if (state.Engines < 0)
        {
            state.Engines = platform.EngineCount(state.Adapter);
            state.Previous = new long[state.Engines];
        }

        if (state.Engines == 0)
        {
            Unavailable(sink, state, want, GpuKinds.Load, "the driver lists no engines to measure");
            return;
        }

        long now = platform.Now();
        double busiest = -1;
        for (int engine = 0; engine < state.Engines; engine++)
        {
            if (!platform.TryReadRunningTime(state.Adapter, engine, out long time))
            {
                state.HasBaseline = false;
                Unavailable(sink, state, want, GpuKinds.Load, "the engine times could not be read");
                return;
            }

            long elapsed = now - state.PreviousTime;
            if (state.HasBaseline && elapsed > 0)
                busiest = Math.Max(busiest, 100.0 * (time - state.Previous[engine]) / elapsed);
            state.Previous[engine] = time;
        }

        state.PreviousTime = now;
        // The first query only sets the baseline: a load needs two samples.
        if (state.HasBaseline && busiest >= 0)
            Publish(sink, state, want, GpuKinds.Load, Math.Clamp(busiest, 0, 100));
        state.HasBaseline = true;
    }

    private void SampleTemperature(IReadingSink sink, AdapterState state, GpuWant want)
    {
        TemperatureQueries++;
        if (platform.TryReadTemperature(state.Adapter, out double celsius) && celsius > 0)
            Publish(sink, state, want, GpuKinds.Temperature, celsius);
        else
            Unavailable(sink, state, want, GpuKinds.Temperature, "the driver reports no temperature");
    }

    private void SampleVendor(IReadingSink sink, AdapterState state, GpuWant want)
    {
        if (state.Vendor == VendorStatus.NotTried)
            OpenVendor(sink, state);

        var wanted = want.Kinds & (GpuKinds.Power | GpuKinds.Clock);
        if (state.Vendor != VendorStatus.Open || state.Reader == null)
        {
            // Not retried until the provider restarts: a missing library does not appear while a page is showing.
            if ((wanted & GpuKinds.Power) != 0)
                Unavailable(sink, state, want, GpuKinds.Power, state.Failure);
            if ((wanted & GpuKinds.Clock) != 0)
                Unavailable(sink, state, want, GpuKinds.Clock, state.Failure);
            return;
        }

        VendorQueries++;
        if ((wanted & GpuKinds.Power) != 0)
        {
            if (state.Reader.PowerWatts() is { } watts)
                Publish(sink, state, want, GpuKinds.Power, watts);
            else
                Unavailable(sink, state, want, GpuKinds.Power, $"{state.Reader.Source} returned no power");
        }

        if ((wanted & GpuKinds.Clock) != 0)
        {
            if (state.Reader.ClockMegahertz() is { } megahertz)
                Publish(sink, state, want, GpuKinds.Clock, megahertz);
            else
                Unavailable(sink, state, want, GpuKinds.Clock, $"{state.Reader.Source} returned no clock");
        }
    }

    private void OpenVendor(IReadingSink sink, AdapterState state)
    {
        string number = state.Number.ToString(CultureInfo.InvariantCulture);
        var reader = platform.CreateVendorReader(state.Adapter);
        if (reader == null)
        {
            state.Vendor = VendorStatus.Failed;
            state.Failure = $"no vendor library for this {state.Adapter.Vendor} GPU yet";
            sink.Log($"GPU {number}: power and clock are unavailable: {state.Failure}");
            return;
        }

        if (reader.TryOpen(state.Adapter, out string failure))
        {
            state.Vendor = VendorStatus.Open;
            state.Reader = reader;
            sink.Log($"GPU {number}: power and clock from {reader.Source}");
            return;
        }

        reader.Close();
        state.Vendor = VendorStatus.Failed;
        state.Failure = failure;
        sink.Log($"GPU {number}: power and clock are unavailable: {failure}");
    }
}
