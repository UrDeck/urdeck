// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Providers.Machine;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

public class GpuReadingsTests
{
    private const uint Nvidia = 0x10DE;
    private const uint Amd = 0x1002;
    private const long Second = 10_000_000;

    private sealed class Sink : IReadingSink
    {
        public Dictionary<string, double> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Unavailables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Logs { get; } = [];

        public void Publish(string path, ReadingValue value) => Values[path] = value.Number;

        public void Unavailable(string path, string reason) => Unavailables[path] = reason;

        public void Log(string message) => Logs.Add(message);
    }

    private sealed class FakeReader : IGpuVendorReader
    {
        public int Opens { get; private set; }

        public int Closes { get; private set; }

        public string? Failure { get; init; }

        public double? Power { get; set; } = 123.4;

        public double? Clock { get; set; } = 2520;

        public string Source => "fake.dll";

        public bool TryOpen(GpuAdapter adapter, out string failure)
        {
            Opens++;
            failure = Failure ?? string.Empty;
            return Failure == null;
        }

        public double? PowerWatts() => Power;

        public double? ClockMegahertz() => Clock;

        public void Close() => Closes++;
    }

    private sealed class FakePlatform(params GpuAdapter[] adapters) : IGpuPlatform
    {
        public long Time { get; set; } = Second;

        /// <summary>Total running time per engine, by adapter.</summary>
        public Dictionary<long, long[]> Engines { get; } = adapters.ToDictionary(a => a.Luid, _ => new long[] { 0, 0 });

        public Dictionary<long, double> Temperatures { get; } = adapters.ToDictionary(a => a.Luid, _ => 45.0);

        public FakeReader? Reader { get; set; } = new();

        public int ReadersCreated { get; private set; }

        public IReadOnlyList<GpuAdapter> Adapters() => adapters;

        public long Now() => Time;

        public int EngineCount(GpuAdapter adapter) => Engines[adapter.Luid].Length;

        public bool TryReadRunningTime(GpuAdapter adapter, int engine, out long runningTime)
        {
            runningTime = Engines[adapter.Luid][engine];
            return true;
        }

        public bool TryReadTemperature(GpuAdapter adapter, out double celsius)
        {
            celsius = Temperatures[adapter.Luid];
            return celsius > 0;
        }

        public IGpuVendorReader? CreateVendorReader(GpuAdapter adapter)
        {
            ReadersCreated++;
            return Reader;
        }
    }

    private static GpuAdapter Adapter(long luid, string name, ulong gigabytes, uint vendor = Nvidia, bool software = false) =>
        new(luid, name, gigabytes * 1024 * 1024 * 1024, vendor, software, 1, 0, 0);

    private static readonly GpuAdapter Discrete = Adapter(1, "Test RTX", 24);

    private static SystemProvider Started(FakePlatform platform, Sink sink, params string[] demand)
    {
        var provider = new SystemProvider(platform);
        provider.Describe();
        provider.Start(sink);
        provider.SetDemand(demand);
        return provider;
    }

    private static void Sample(SystemProvider provider) => provider.SampleAsync(CancellationToken.None).GetAwaiter().GetResult();

    private static List<ReadingDescriptor> GpuCatalog(FakePlatform platform) =>
        new SystemProvider(platform).Describe().Where(d => d.Path.StartsWith("gpu/", StringComparison.Ordinal)).ToList();

    [Fact]
    public void TheCatalog_ListsFourMainPathsAndFourPerAdapter()
    {
        var catalog = GpuCatalog(new FakePlatform(Discrete));

        Assert.Equal(
            ["gpu/load", "gpu/temperature", "gpu/power", "gpu/clock", "gpu/1/load", "gpu/1/temperature", "gpu/1/power", "gpu/1/clock"],
            catalog.Select(d => d.Path));
        Assert.All(catalog, d => Assert.Equal("Test RTX", d.Device));
        Assert.Equal(["GPU", "GPU", "GPU", "GPU", "GPU 1", "GPU 1", "GPU 1", "GPU 1"], catalog.Select(d => d.Label));
        Assert.Equal(
            ["GPU load", "GPU temperature", "GPU power", "GPU clock", "GPU 1 load", "GPU 1 temperature", "GPU 1 power", "GPU 1 clock"],
            catalog.Select(d => d.Name));
    }

    [Fact]
    public void TheCatalog_DescribesEachReadingsKindRangeLevelsAndUnit()
    {
        var catalog = GpuCatalog(new FakePlatform(Discrete));

        var load = catalog.Single(d => d.Path == "gpu/load");
        Assert.Equal(ReadingKind.Percent, load.Kind);
        Assert.Equal((0, 100), (load.Min, load.Max));
        Assert.Null(load.Warning);
        Assert.Null(load.Critical);

        var temperature = catalog.Single(d => d.Path == "gpu/1/temperature");
        Assert.Equal(ReadingKind.Temperature, temperature.Kind);
        Assert.Equal((0, 100), (temperature.Min, temperature.Max));
        Assert.Equal(80, temperature.Warning);
        Assert.Equal(90, temperature.Critical);
        Assert.Equal(DisplayUnit.Celsius, temperature.DisplayUnit);

        var power = catalog.Single(d => d.Path == "gpu/power");
        Assert.Equal((ReadingKind.Number, "W"), (power.Kind, power.Unit));
        Assert.Null(power.Min);
        Assert.Null(power.Warning);

        var clock = catalog.Single(d => d.Path == "gpu/1/clock");
        Assert.Equal((ReadingKind.Number, "MHz"), (clock.Kind, clock.Unit));
    }

    [Fact]
    public void WithoutAHardwareAdapter_TheCatalogHasNoGpuReadings()
    {
        Assert.Empty(GpuCatalog(new FakePlatform()));
        Assert.Empty(GpuCatalog(new FakePlatform(Adapter(7, "Basic Render", 0, software: true))));
    }

    [Fact]
    public void Adapters_AreNumberedByDedicatedMemory_TiesInWindowsOrder_AndSoftwareOnesAreDropped()
    {
        var platform = new FakePlatform(
            Adapter(1, "Integrated", 2),
            Adapter(2, "Software", 100, software: true),
            Adapter(3, "Discrete", 24),
            Adapter(4, "Second small", 2));

        var catalog = GpuCatalog(platform);

        string DeviceOf(string path) => catalog.Single(d => d.Path == path).Device!;
        Assert.Equal("Discrete", DeviceOf("gpu/load"));
        Assert.Equal("Discrete", DeviceOf("gpu/1/load"));
        Assert.Equal("Integrated", DeviceOf("gpu/2/load"));
        Assert.Equal("Second small", DeviceOf("gpu/3/load"));
        Assert.DoesNotContain(catalog, d => d.Path.StartsWith("gpu/4/", StringComparison.Ordinal));
    }

    [Fact]
    public void NoGpuSubscriber_MeansNoGpuQueryAndNoVendorLibrary()
    {
        var platform = new FakePlatform(Discrete);
        var sink = new Sink();
        var provider = Started(platform, sink, "cpu/load", "memory/load");

        Sample(provider);
        Sample(provider);

        Assert.Equal((0, 0, 0), (provider.GpuLoadQueries, provider.GpuTemperatureQueries, provider.GpuVendorQueries));
        Assert.Equal(0, platform.ReadersCreated);
        Assert.Empty(sink.Logs);
    }

    [Fact]
    public void OnlyLoadWanted_QueriesNeitherTemperatureNorTheVendorLibrary()
    {
        var platform = new FakePlatform(Discrete);
        var provider = Started(platform, new Sink(), "gpu/load");

        Sample(provider);
        Sample(provider);

        Assert.Equal(2, provider.GpuLoadQueries);
        Assert.Equal(0, provider.GpuTemperatureQueries);
        Assert.Equal(0, provider.GpuVendorQueries);
        Assert.Equal(0, platform.ReadersCreated);
    }

    [Fact]
    public void OnlyTemperatureWanted_DoesNotQueryLoad()
    {
        var platform = new FakePlatform(Discrete);
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/1/temperature");

        Sample(provider);

        Assert.Equal(0, provider.GpuLoadQueries);
        Assert.Equal(1, provider.GpuTemperatureQueries);
        Assert.Equal(45, sink.Values["gpu/1/temperature"]);
        Assert.Equal(["gpu/1/temperature"], sink.Values.Keys);
    }

    [Fact]
    public void Load_IsTheBusiestEnginesShareOfTimeSincetheLastSample_OnlyForWantedPaths()
    {
        var platform = new FakePlatform(Discrete);
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/load");

        Sample(provider);
        Assert.Empty(sink.Values); // the first sample only sets the baseline

        platform.Time += 2 * Second;
        platform.Engines[1][0] = Second / 2; // 25% of the two seconds
        platform.Engines[1][1] = Second * 3 / 2; // 75%: the busiest engine, not the sum
        Sample(provider);

        Assert.Equal(["gpu/load"], sink.Values.Keys);
        Assert.Equal(75, sink.Values["gpu/load"], 6);
    }

    [Fact]
    public void Load_IsAvailableThroughBothTheMainAndTheNumberedPath()
    {
        var platform = new FakePlatform(Discrete);
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/load", "gpu/1/load");
        Sample(provider);

        platform.Time += Second;
        platform.Engines[1][0] = Second / 5;
        Sample(provider);

        Assert.Equal(20, sink.Values["gpu/load"], 6);
        Assert.Equal(20, sink.Values["gpu/1/load"], 6);
    }

    [Fact]
    public void Load_IsClampedToAHundred()
    {
        var platform = new FakePlatform(Discrete);
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/load");
        Sample(provider);

        platform.Time += Second;
        platform.Engines[1][0] = 3 * Second;
        Sample(provider);

        Assert.Equal(100, sink.Values["gpu/load"]);
    }

    [Fact]
    public void WhenLoadIsWantedAgainAfterAPause_TheFirstSampleIsABaselineAgain()
    {
        var platform = new FakePlatform(Discrete);
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/load");
        Sample(provider);
        platform.Time += Second;
        Sample(provider);
        Assert.True(sink.Values.ContainsKey("gpu/load"));

        provider.SetDemand(["gpu/temperature"]);
        Sample(provider);
        sink.Values.Clear();
        provider.SetDemand(["gpu/load"]);
        platform.Time += 60 * Second;
        Sample(provider);

        Assert.False(sink.Values.ContainsKey("gpu/load"));
    }

    [Fact]
    public void ADriverWithoutATemperature_IsUnavailableAndNeverZero()
    {
        var platform = new FakePlatform(Discrete);
        platform.Temperatures[1] = 0;
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/temperature");

        Sample(provider);

        Assert.Empty(sink.Values);
        Assert.Contains("no temperature", sink.Unavailables["gpu/temperature"]);
    }

    [Fact]
    public void AnAdapterThatDoesNotExist_IsUnavailable()
    {
        var sink = new Sink();
        var provider = Started(new FakePlatform(Discrete, Adapter(2, "Other", 8)), sink, "gpu/3/load", "gpu/0/load", "gpu/2/load");

        Sample(provider);

        Assert.Contains("gpu/3/load", sink.Unavailables.Keys);
        Assert.Contains("gpu/0/load", sink.Unavailables.Keys);
        Assert.DoesNotContain("gpu/2/load", sink.Unavailables.Keys);
    }

    [Fact]
    public void WithoutAGpu_EveryGpuPathIsUnavailable()
    {
        var sink = new Sink();
        var provider = Started(new FakePlatform(), sink, "gpu/load", "gpu/1/power");

        Sample(provider);

        Assert.Contains("gpu/load", sink.Unavailables.Keys);
        Assert.Contains("gpu/1/power", sink.Unavailables.Keys);
    }

    [Fact]
    public void PowerAndClock_ComeFromTheVendorReader_OpenedOnceAndClosedOnShutdown()
    {
        var platform = new FakePlatform(Discrete);
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/power", "gpu/1/clock");

        Sample(provider);
        Sample(provider);

        Assert.Equal(123.4, sink.Values["gpu/power"]);
        Assert.Equal(2520, sink.Values["gpu/1/clock"]);
        Assert.Equal(1, platform.Reader!.Opens);
        Assert.Equal(2, provider.GpuVendorQueries);
        Assert.Equal(0, provider.GpuLoadQueries);

        provider.Shutdown();
        Assert.Equal(1, platform.Reader.Closes);
    }

    [Fact]
    public void AReaderThatFailsToOpen_MakesPowerAndClockUnavailable_WhileLoadKeepsPublishing_AndIsNotRetried()
    {
        var platform = new FakePlatform(Discrete) { Reader = new FakeReader { Failure = "fake.dll could not be loaded" } };
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/load", "gpu/power", "gpu/clock");

        Sample(provider);
        platform.Time += Second;
        platform.Engines[1][0] = Second / 2;
        Sample(provider);
        Sample(provider);

        Assert.Equal("fake.dll could not be loaded", sink.Unavailables["gpu/power"]);
        Assert.Equal("fake.dll could not be loaded", sink.Unavailables["gpu/clock"]);
        Assert.Equal(50, sink.Values["gpu/load"], 6);
        Assert.Equal(1, platform.Reader.Opens);
        Assert.Equal(1, platform.ReadersCreated);
        Assert.Equal(0, provider.GpuVendorQueries);
    }

    [Fact]
    public void AnAdapterWithoutAVendorReader_ReportsThatThereIsNoLibraryForItYet()
    {
        var platform = new FakePlatform(Adapter(1, "Radeon", 16, Amd)) { Reader = null };
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/power", "gpu/temperature");

        Sample(provider);

        Assert.Contains("no vendor library", sink.Unavailables["gpu/power"]);
        Assert.Contains("AMD", sink.Unavailables["gpu/power"]);
        Assert.Equal(45, sink.Values["gpu/temperature"]);
    }

    [Fact]
    public void ThePowerOfAReaderWithoutAValue_IsUnavailable()
    {
        var platform = new FakePlatform(Discrete);
        platform.Reader!.Power = null;
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/power", "gpu/clock");

        Sample(provider);

        Assert.Contains("no power", sink.Unavailables["gpu/power"]);
        Assert.Equal(2520, sink.Values["gpu/clock"]);
    }

    [Fact]
    public void TheLogNamesTheAdapterAndTheSourceOfEachReading_OnceEach()
    {
        var platform = new FakePlatform(Discrete);
        var sink = new Sink();
        var provider = Started(platform, sink, "gpu/load", "gpu/temperature", "gpu/power");

        Sample(provider);
        Sample(provider);

        Assert.Equal(2, sink.Logs.Count);
        Assert.Contains("GPU 1 is Test RTX (NVIDIA)", sink.Logs[0]);
        Assert.Contains("graphics kernel", sink.Logs[0]);
        Assert.Contains("adapter performance data", sink.Logs[0]);
        Assert.Equal("GPU 1: power and clock from fake.dll", sink.Logs[1]);
    }

    [Theory]
    [InlineData("gpu/load", 1, "Load", true)]
    [InlineData("GPU/Temperature", 1, "Temperature", true)]
    [InlineData("gpu/2/power", 2, "Power", false)]
    [InlineData("gpu/12/clock", 12, "Clock", false)]
    public void PathsAreRecognised_WhateverTheirCase(string path, int adapter, string kind, bool alias)
    {
        Assert.True(GpuReadings.TryParse(path, out int parsedAdapter, out var parsedKind, out bool parsedAlias));
        Assert.Equal((adapter, kind, alias), (parsedAdapter, parsedKind.ToString(), parsedAlias));
    }

    [Theory]
    [InlineData("gpu/")]
    [InlineData("gpu/fan")]
    [InlineData("gpu/x/load")]
    [InlineData("gpu/1/fan")]
    [InlineData("cpu/load")]
    public void OtherPathsAreNotGpuPaths(string path) =>
        Assert.False(GpuReadings.TryParse(path, out _, out _, out _));

    [Fact]
    public void OnThisMachine_LoadIsAPercentageAfterTwoSamples()
    {
        var provider = new SystemProvider();
        var catalog = provider.Describe();
        if (!catalog.Any(d => d.Path == "gpu/load"))
            return; // no hardware GPU adapter here: nothing to measure

        var sink = new Sink();
        provider.Start(sink);
        provider.SetDemand(["gpu/load", "gpu/temperature"]);
        Sample(provider);
        Thread.Sleep(250);
        Sample(provider);
        provider.Shutdown();

        Assert.InRange(sink.Values["gpu/load"], 0, 100);
        if (sink.Values.TryGetValue("gpu/temperature", out double celsius))
            Assert.InRange(celsius, 1, 150);
    }
}
