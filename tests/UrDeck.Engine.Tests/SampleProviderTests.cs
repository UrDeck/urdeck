// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>A sample replacing its provider behind the hub (sample-data, "A Sample Replaces Its Provider"), and the hub's manual sampling.</summary>
public sealed class SampleProviderTests
{
    private static readonly DateTime Start = new(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);

    private const string Json = """
        {
          "version": 1,
          "readings": {
            "cpu/load": { "kind": "percent", "label": "CPU", "name": "CPU load", "min": 0, "max": 100,
                          "keys": [ { "at": 0, "value": 41 }, { "at": 2, "value": 68 } ] },
            "{location}/place": { "kind": "text", "label": "Place", "name": "Place", "device": "{location}", "value": "{location}" },
            "{location}/current/temperature": { "kind": "temperature", "label": "Now", "name": "Temperature in {location}", "value": 18 },
            "gpu/fan": { "kind": "percent", "label": "Fan", "name": "GPU fan", "unavailable": "no fan on this card" }
          }
        }
        """;

    private DateTime _now = Start;

    private (ReadingHub Hub, List<IReadOnlyCollection<string>> Changes) Hub(ReadingHubSampling sampling = ReadingHubSampling.Manual)
    {
        var file = SampleFile.Parse(Json, out _)!;
        var registry = new ProviderRegistry();
        var real = new ProviderDescriptor("test", "Test", typeof(object), 1000, 250, "Test");
        registry.TryRegister(ProviderSamples.Replace(real, file, () => _now, Start), out _);
        var hub = new ReadingHub(registry, regionUsesFahrenheit: false, sampling: sampling);
        var changes = new List<IReadOnlyCollection<string>>();
        hub.ReadingsChanged += ids => changes.Add(ids.ToList());
        return (hub, changes);
    }

    [Fact]
    public void Subscribing_InTheManualMode_StartsTheProvider_ButSamplesNothing()
    {
        var (hub, _) = Hub();
        using var _hub = hub;

        hub.Subscribe(["test:cpu/load"]);

        Assert.Equal(ReadingState.Pending, hub.Read("test:cpu/load").State);
        Assert.Equal("CPU", hub.Describe("test:cpu/load")!.Label);
    }

    [Fact]
    public void SampleNow_PublishesThroughTheCommit_WithChangeNotifications()
    {
        var (hub, changes) = Hub();
        using var _hub = hub;
        hub.Subscribe(["test:cpu/load", "test:gpu/fan"]);
        changes.Clear();

        hub.SampleNow();

        Assert.Equal(Reading.Ok(41), hub.Read("test:cpu/load"));
        Assert.Equal(ReadingState.Unavailable, hub.Read("test:gpu/fan").State);
        Assert.Equal("no fan on this card", hub.Read("test:gpu/fan").Reason);
        Assert.Contains(changes, c => c.Contains("test:cpu/load"));

        _now = Start.AddSeconds(1);
        changes.Clear();
        hub.SampleNow();

        Assert.Equal(54.5, hub.Read("test:cpu/load").Value!.Value.Number);
        Assert.Contains(changes, c => c.Contains("test:cpu/load"));
    }

    [Fact]
    public void APlace_ResolvesThroughTheHubsPatterns_ToTheSample()
    {
        var (hub, _) = Hub();
        using var _hub = hub;
        hub.Subscribe(["test:Zurich/current/temperature", "test:Zurich/place"]);

        hub.SampleNow();

        Assert.Equal(Reading.Ok(18), hub.Read("test:Zurich/current/temperature"));
        Assert.Equal(Reading.Ok("Zurich"), hub.Read("test:Zurich/place"));
        Assert.Equal("Temperature in Zurich", hub.Describe("test:Zurich/current/temperature")!.Name);
        Assert.Equal("Zurich", hub.Describe("test:Zurich/place")!.Device);
    }

    [Fact]
    public void TheSameMoment_GivesTheSameValues_WhateverCameBefore()
    {
        var (hub, _) = Hub();
        using var _hub = hub;
        hub.Subscribe(["test:cpu/load"]);

        _now = Start.AddSeconds(1.5);
        hub.SampleNow();
        var first = hub.Read("test:cpu/load");
        _now = Start.AddSeconds(30);
        hub.SampleNow();
        _now = Start.AddSeconds(1.5);
        hub.SampleNow();

        Assert.Equal(first, hub.Read("test:cpu/load"));
    }

    [Fact]
    public void APathTheSampleDoesNotHave_IsUnavailable()
    {
        var (hub, _) = Hub();
        using var _hub = hub;
        hub.Subscribe(["test:memory/load"]);

        hub.SampleNow();

        Assert.Equal(ReadingState.Unavailable, hub.Read("test:memory/load").State);
    }

    [Fact]
    public void SampleNow_IsRefused_ByAHubWithItsLoop()
    {
        var (hub, _) = Hub(ReadingHubSampling.Loop);
        using var _hub = hub;

        Assert.Throws<InvalidOperationException>(hub.SampleNow);
    }

    [Fact]
    public async Task TheSampleProvider_PublishesOnlyWhatIsDemanded()
    {
        var provider = new SampleProvider(SampleFile.Parse(Json, out _)!, () => Start, Start);
        var sink = new Sink();
        provider.Start(sink);
        provider.SetDemand(["cpu/load", "Bern/place", "nothing/here"]);

        await provider.SampleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Bern/place", "cpu/load"], sink.Published.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Bern", sink.Published["Bern/place"].Text);
    }

    [Fact]
    public void Find_ReadsTheSampleTheAssemblyCarries_AndABrokenOneIsOneWarning()
    {
        var warnings = new List<string>();
        var before = UrDeckLog.Listener;
        UrDeckLog.Listener = (level, message) =>
        {
            if (level == "WARN")
                warnings.Add(message);
        };
        try
        {
            Assert.NotNull(ProviderSamples.Find(Provider("test-good")));
            Assert.Null(ProviderSamples.Find(Provider("test-broken")));
            Assert.Null(ProviderSamples.Find(Provider("test-none")));
        }
        finally
        {
            UrDeckLog.Listener = before;
        }

        Assert.True(ProviderSamples.Carries(typeof(SampleProviderTests).Assembly, "test-good"));
        Assert.False(ProviderSamples.Carries(typeof(SampleProviderTests).Assembly, "test-none"));
        string warning = Assert.Single(warnings);
        Assert.Contains("Provider 'test-broken'", warning);
        Assert.Contains("not valid JSON", warning);
    }

    /// <summary>A provider whose type is in this assembly, which carries the samples in <c>Fixtures/samples</c>.</summary>
    private static ProviderDescriptor Provider(string id) => new(id, id, typeof(SampleProviderTests), 1000, 250, "UrDeck.Engine.Tests");

    private sealed class Sink : IReadingSink
    {
        public Dictionary<string, ReadingValue> Published { get; } = [];

        public void Publish(string path, ReadingValue value) => Published[path] = value;

        public void Unavailable(string path, string reason)
        {
        }
    }
}
