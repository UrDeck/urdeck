// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Providers.Machine;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

public class SystemProviderTests
{
    private sealed class Sink : IReadingSink
    {
        public Dictionary<string, double> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Unavailables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Publish(string path, ReadingValue value) => Values[path] = value.Number;

        public void Unavailable(string path, string reason) => Unavailables[path] = reason;
    }

    private static SystemProvider Started(Sink sink, params string[] demand)
    {
        var provider = new SystemProvider();
        provider.Start(sink);
        provider.SetDemand(demand);
        return provider;
    }

    private static void Sample(SystemProvider provider) => provider.SampleAsync(CancellationToken.None).GetAwaiter().GetResult();

    [Fact]
    public void IsDeclaredAsTheSystemProvider()
    {
        var descriptor = UrDeck.Engine.Data.ProviderDescriptor.TryCreate(typeof(SystemProvider), out string? reason);

        Assert.NotNull(descriptor);
        Assert.Null(reason);
        Assert.Equal("system", descriptor!.Id);
        Assert.Equal(2000, descriptor.DefaultIntervalMs);
        Assert.Equal(250, descriptor.MinIntervalMs);
    }

    [Fact]
    public void TheCatalog_ListsOneEntryPerLogicalProcessor_WithLabelsAndTheCpuName()
    {
        var catalog = new SystemProvider().Describe();

        var cores = catalog.Where(e => e.Path.StartsWith("cpu/core/", StringComparison.Ordinal)).ToList();
        Assert.Equal(Environment.ProcessorCount, cores.Count);
        Assert.Equal("cpu/core/1/load", cores[0].Path);
        Assert.Equal("Core 1", cores[0].Label);
        Assert.Equal("CPU core 1 load", cores[0].Name);
        Assert.Equal($"Core {cores.Count}", cores[^1].Label);

        var total = catalog.Single(e => e.Path == "cpu/load");
        Assert.Equal("CPU", total.Label);
        Assert.Equal("CPU load", total.Name);
        Assert.Equal(ReadingKind.Percent, total.Kind);
        Assert.Equal(0, total.Min);
        Assert.Equal(100, total.Max);
        Assert.False(string.IsNullOrWhiteSpace(total.Device));
        Assert.All(cores, c => Assert.Equal(total.Device, c.Device));

        var memory = catalog.Single(e => e.Path == "memory/load");
        Assert.Equal("Memory", memory.Label);
        Assert.Equal("Memory load", memory.Name);
        Assert.Equal(ReadingKind.Percent, memory.Kind);
    }

    [Fact]
    public void ALoadNeedsTwoSamples_AndIsAPercentageThen()
    {
        var sink = new Sink();
        var provider = Started(sink, "cpu/load", "cpu/core/1/load", "memory/load");

        Sample(provider);
        Assert.False(sink.Values.ContainsKey("cpu/load"), "the first sample only sets the baseline");
        Assert.False(sink.Values.ContainsKey("cpu/core/1/load"));
        Assert.InRange(sink.Values["memory/load"], 0, 100);

        Thread.Sleep(250);
        Sample(provider);
        Assert.InRange(sink.Values["cpu/load"], 0, 100);
        Assert.InRange(sink.Values["cpu/core/1/load"], 0, 100);
    }

    [Fact]
    public async Task TheTotalRisesWithTheMachinesCpuUse()
    {
        var sink = new Sink();
        var provider = Started(sink, "cpu/load");
        Sample(provider);

        // Lowest priority: the machine reads as busy, but the other tests running in parallel are not starved by it.
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(600));
        var burners = Enumerable.Range(0, Environment.ProcessorCount)
            .Select(_ => new Thread(() =>
            {
                while (!stop.IsCancellationRequested)
                    Thread.SpinWait(1000);
            })
            { Priority = ThreadPriority.Lowest, IsBackground = true })
            .ToArray();
        foreach (var burner in burners)
            burner.Start();
        await Task.Delay(400);
        Sample(provider);
        await Task.Delay(300);
        foreach (var burner in burners)
            burner.Join();

        Assert.True(sink.Values["cpu/load"] > 25, $"expected a busy machine, got {sink.Values["cpu/load"]:0.#}%");
    }

    [Fact]
    public void ACoreThatDoesNotExist_IsReportedUnavailable()
    {
        var sink = new Sink();
        var provider = Started(sink, "cpu/core/99999/load", "cpu/core/0/load");

        Sample(provider);

        Assert.Contains("cpu/core/99999/load", sink.Unavailables.Keys);
        Assert.Contains("cpu/core/0/load", sink.Unavailables.Keys);
    }

    [Fact]
    public void OnlyMemoryWanted_DoesNotQueryProcessorTimes()
    {
        var sink = new Sink();
        var provider = Started(sink, "memory/load");

        Sample(provider);
        Sample(provider);

        Assert.Equal(0, provider.CpuQueries);
        Assert.Equal(2, provider.MemoryQueries);
        Assert.Equal(["memory/load"], sink.Values.Keys);
    }

    [Fact]
    public void OnlyCpuWanted_DoesNotQueryMemory()
    {
        var sink = new Sink();
        var provider = Started(sink, "cpu/core/1/load");

        Sample(provider);
        Sample(provider);

        Assert.Equal(2, provider.CpuQueries);
        Assert.Equal(0, provider.MemoryQueries);
        Assert.False(sink.Values.ContainsKey("cpu/load"), "only the wanted readings are published");
    }

    [Fact]
    public void NothingWanted_DoesNothing()
    {
        var sink = new Sink();
        var provider = Started(sink);

        Sample(provider);

        Assert.Equal(0, provider.CpuQueries);
        Assert.Equal(0, provider.MemoryQueries);
        Assert.Empty(sink.Values);
    }

    [Fact]
    public void WhenCpuIsWantedAgainAfterAPause_TheFirstSampleIsABaselineAgain()
    {
        var sink = new Sink();
        var provider = Started(sink, "cpu/load");
        Sample(provider);
        Thread.Sleep(100);
        Sample(provider);
        Assert.True(sink.Values.ContainsKey("cpu/load"));

        provider.SetDemand(["memory/load"]);
        Sample(provider);
        sink.Values.Clear();
        provider.SetDemand(["cpu/load"]);
        Sample(provider);

        Assert.False(sink.Values.ContainsKey("cpu/load"));
    }

    [Fact]
    public void AfterStop_SamplingDoesNothing()
    {
        var sink = new Sink();
        var provider = Started(sink, "memory/load");
        provider.Shutdown();

        Sample(provider);

        Assert.Empty(sink.Values);
    }
}
