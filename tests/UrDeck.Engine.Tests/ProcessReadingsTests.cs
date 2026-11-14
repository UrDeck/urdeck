// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.Versioning;
using UrDeck.Engine.Tests.Support;
using UrDeck.Providers.Machine;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>provider-system, "Top Processes Reading", over a fake platform.</summary>
[SupportedOSPlatform("windows")]
public class ProcessReadingsTests
{
    private sealed class FakePlatform : IProcessPlatform
    {
        public int ProcessorCount { get; set; } = 4;

        public TimeSpan Clock { get; set; } = TimeSpan.FromSeconds(100);

        public List<ProcessSample> Processes { get; set; } = [];

        public int Enumerations { get; private set; }

        public TimeSpan Now() => Clock;

        public IReadOnlyList<ProcessSample> Enumerate()
        {
            Enumerations++;
            return [.. Processes];
        }
    }

    private sealed class Sink : IReadingSink
    {
        public List<ReadingList> Lists { get; } = [];

        public void Publish(string path, ReadingValue value)
        {
            Assert.Equal("processes/top", path);
            Lists.Add(value.List);
        }

        public void Unavailable(string path, string reason)
        {
        }

        public List<string> Logged { get; } = [];

        public void Log(string message) => Logged.Add(message);
    }

    private static ProcessSample Proc(int id, string name, double cpuSeconds, long megabytes = 100) =>
        new(id, name, TimeSpan.FromSeconds(cpuSeconds), megabytes * 1024 * 1024);

    private static (SystemProvider Provider, FakePlatform Platform, Sink Sink) Started(bool demanded = true)
    {
        var platform = new FakePlatform();
        var sink = new Sink();
        var provider = new SystemProvider(new NoGpu(), platform);
        provider.Start(sink, new ProviderSettings());
        provider.SetDemand(demanded ? ["processes/top"] : []);
        return (provider, platform, sink);
    }

    private sealed class NoGpu : IGpuPlatform
    {
        public IReadOnlyList<GpuAdapter> Adapters() => [];

        public long Now() => 0;

        public int EngineCount(GpuAdapter adapter) => 0;

        public bool TryReadRunningTime(GpuAdapter adapter, int engine, out long runningTime)
        {
            runningTime = 0;
            return false;
        }

        public bool TryReadTemperature(GpuAdapter adapter, out double celsius)
        {
            celsius = 0;
            return false;
        }

        public IGpuVendorReader? CreateVendorReader(GpuAdapter adapter) => null;
    }

    private static void Sample(SystemProvider provider) => provider.SampleAsync(CancellationToken.None).GetAwaiter().GetResult();

    [WindowsFact]
    public void TheCatalog_DescribesTheListWithItsFieldsAndRoles()
    {
        var descriptor = new SystemProvider().Describe().Single(d => d.Path == "processes/top");

        Assert.Equal(ReadingKind.List, descriptor.Kind);
        Assert.Equal("processes", descriptor.Unit);
        Assert.Equal("No busy processes", descriptor.EmptyText);
        Assert.Equal(["name", "cpu", "memory"], descriptor.Fields.Select(f => f.Name));
        var roles = ListRoles.Of(descriptor);
        Assert.Equal("name", roles.Title!.Name);
        Assert.Equal("cpu", roles.Status!.Name);
        Assert.Equal("memory", roles.Detail!.Name);
    }

    [WindowsFact]
    public void ABusyGame_IsTheFirstRow_AndTheListHasAtMostTenRows()
    {
        var (provider, platform, sink) = Started();
        platform.Processes = [Proc(1, "game", 0), .. Enumerable.Range(2, 20).Select(i => Proc(i, "p" + i, 0))];
        Sample(provider);

        platform.Clock += TimeSpan.FromSeconds(2);
        // 35 % of four processors for two seconds is 2.8 s of processor time; the others use 2 % or less.
        platform.Processes = [Proc(1, "game", 2.8), .. Enumerable.Range(2, 20).Select(i => Proc(i, "p" + i, 0.16))];
        Sample(provider);

        var list = Assert.Single(sink.Lists);
        Assert.Equal(10, list.Count);
        Assert.Equal("game", list[0].Get("name")!.Value.Text);
        Assert.Equal(35.0, list[0].Get("cpu")!.Value.Number);
        Assert.Equal(100, list[0].Get("memory")!.Value.Number);
        Assert.Equal("1", list[0].Key);
    }

    [WindowsFact]
    public void TheFirstSample_PublishesNothing_BecauseAShareNeedsTwo()
    {
        var (provider, platform, sink) = Started();
        platform.Processes = [Proc(1, "game", 50)];

        Sample(provider);

        Assert.Empty(sink.Lists);
    }

    [WindowsFact]
    public void WhenNothingShowsTheReading_NoProcessIsEnumerated()
    {
        var (provider, platform, sink) = Started(demanded: false);
        platform.Processes = [Proc(1, "game", 0)];

        Sample(provider);
        platform.Clock += TimeSpan.FromSeconds(2);
        Sample(provider);

        Assert.Equal(0, platform.Enumerations);
        Assert.Equal(0, provider.ProcessQueries);
        Assert.Empty(sink.Lists);
    }

    [WindowsFact]
    public void AProcessThatEnds_IsGoneFromTheNextList_AndNothingIsLogged()
    {
        var (provider, platform, sink) = Started();
        platform.Processes = [Proc(1, "game", 0), Proc(2, "chat", 0)];
        Sample(provider);
        platform.Clock += TimeSpan.FromSeconds(2);
        platform.Processes = [Proc(1, "game", 2), Proc(2, "chat", 1)];
        Sample(provider);

        platform.Clock += TimeSpan.FromSeconds(2);
        platform.Processes = [Proc(1, "game", 4)];
        Sample(provider);

        Assert.Equal(2, sink.Lists.Count);
        Assert.Equal(2, sink.Lists[0].Count);
        Assert.Equal(["1"], sink.Lists[1].Rows.Select(r => r.Key));
        Assert.Empty(sink.Logged);
    }

    [WindowsFact]
    public void AnIdleMachine_PublishesAnEmptyList()
    {
        var (provider, platform, sink) = Started();
        platform.Processes = [Proc(1, "game", 5)];
        Sample(provider);
        platform.Clock += TimeSpan.FromSeconds(2);

        Sample(provider);

        Assert.Equal(0, Assert.Single(sink.Lists).Count);
    }

    [WindowsFact]
    public void ADroppedSubscription_NeedsTwoFreshSamplesAgain()
    {
        var (provider, platform, sink) = Started();
        platform.Processes = [Proc(1, "game", 0)];
        Sample(provider);
        provider.SetDemand([]);
        Sample(provider);
        provider.SetDemand(["processes/top"]);
        platform.Clock += TimeSpan.FromSeconds(2);
        platform.Processes = [Proc(1, "game", 2)];

        Sample(provider);

        Assert.Empty(sink.Lists);
    }
}
