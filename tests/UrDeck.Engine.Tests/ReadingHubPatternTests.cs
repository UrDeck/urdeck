// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>A provider whose catalog has parameter segments (the weather provider's locations): see data-providers, "Reading Catalog".</summary>
public sealed class ReadingHubPatternTests : IDisposable
{
    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _registry = new();
    private readonly string _logPath = Path.Combine(Path.GetTempPath(), $"urdeck-pattern-{Guid.NewGuid():N}.log");

    public ReadingHubPatternTests()
    {
        UrDeckLog.LogPath = _logPath;
        Provider = new FakeProvider(_time)
        {
            Catalog =
            [
                new ReadingDescriptor("{location}/temp", ReadingKind.Temperature, "Now", "Temperature in {location}") { Device = "{location}" },
                new ReadingDescriptor("{location}/place", ReadingKind.Text, "Place", "Place") { Device = "{location}" },
            ],
            OnSample = (_, _, _) => Task.CompletedTask,
        };
        _registry.TryRegister(Provider.Descriptor(), out _);
        Hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false);
    }

    private FakeProvider Provider { get; }

    private ReadingHub Hub { get; }

    public void Dispose()
    {
        Hub.Dispose();
        if (File.Exists(_logPath))
            File.Delete(_logPath);
    }

    private string Log() => File.Exists(_logPath) ? File.ReadAllText(_logPath) : "";

    private static bool Wait(Func<bool> condition) => HubTestExtensions.WaitFor(condition);

    private bool Started() => Wait(() => Provider.Calls.Contains("sample"));

    [Fact]
    public void APathThatFitsAPattern_IsServedAndDescribedWithTheLocationFilledIn()
    {
        Hub.Subscribe(["fake:Zurich/temp"]);
        Assert.True(Started());

        var description = Hub.Describe("fake:Zurich/temp");
        Assert.NotNull(description);
        Assert.Equal("Zurich/temp", description.Path);
        Assert.Equal("Zurich", description.Device);
        Assert.Equal("Temperature in Zurich", description.Name);
        Assert.Equal("Now", description.Label);
        Assert.Equal(ReadingKind.Temperature, description.Kind);
        Assert.NotEqual(ReadingState.Unavailable, Hub.Read("fake:Zurich/temp").State);
    }

    [Fact]
    public void ThePatternsValue_IsPublishedUnderTheConcretePath()
    {
        Provider.OnSample = (_, sink, _) =>
        {
            sink.Publish("Zurich/temp", 14.5);
            return Task.CompletedTask;
        };
        Hub.Subscribe(["fake:Zurich/temp", "fake:Tokyo/temp"]);

        Assert.True(Wait(() => Hub.Read("fake:Zurich/temp").State == ReadingState.Ok));
        Assert.Equal(14.5, Hub.Read("fake:Zurich/temp").Value!.Value.Number);
        Assert.NotEqual(ReadingState.Ok, Hub.Read("fake:Tokyo/temp").State);
    }

    [Fact]
    public void TwoLocations_AreDescribedEachWithItsOwnDevice()
    {
        Hub.Subscribe(["fake:Zurich/temp", "fake:Tokyo/temp"]);
        Assert.True(Started());

        Assert.Equal("Zurich", Hub.Describe("fake:Zurich/temp")!.Device);
        Assert.Equal("Tokyo", Hub.Describe("fake:Tokyo/temp")!.Device);
    }

    [Fact]
    public void AnExactEntry_BeatsAPattern()
    {
        Provider.Catalog =
        [
            .. Provider.Catalog,
            new ReadingDescriptor("home/place", ReadingKind.Text, "Mine", "My place"),
        ];
        Hub.Subscribe(["fake:home/place", "fake:away/place"]);
        Assert.True(Started());

        Assert.Equal("Mine", Hub.Describe("fake:home/place")!.Label);
        Assert.Equal("Place", Hub.Describe("fake:away/place")!.Label);
    }

    [Fact]
    public void OfSeveralPatterns_TheOneWithTheMostLiteralSegmentsWins()
    {
        Provider.Catalog =
        [
            new ReadingDescriptor("{a}/{b}", ReadingKind.Number, "none", "No literal"),
            new ReadingDescriptor("{a}/temp", ReadingKind.Number, "tail", "Literal tail"),
            new ReadingDescriptor("home/{b}", ReadingKind.Number, "head", "Literal head"),
            new ReadingDescriptor("home/temp/{c}", ReadingKind.Number, "three", "Three segments"),
        ];
        Hub.Subscribe(["fake:x/y", "fake:x/temp", "fake:home/y", "fake:home/temp/z"]);
        Assert.True(Started());

        Assert.Equal("none", Hub.Describe("fake:x/y")!.Label);
        Assert.Equal("tail", Hub.Describe("fake:x/temp")!.Label);
        Assert.Equal("head", Hub.Describe("fake:home/y")!.Label);
        Assert.Equal("three", Hub.Describe("fake:home/temp/z")!.Label);
    }

    [Fact]
    public void ATieInLiteralSegments_UsesTheFirstListedAndLogsOnce()
    {
        Provider.Catalog =
        [
            new ReadingDescriptor("{a}/temp", ReadingKind.Number, "first", "First"),
            new ReadingDescriptor("home/{b}", ReadingKind.Number, "second", "Second"),
        ];
        Hub.Subscribe(["fake:home/temp"]);
        Assert.True(Started());
        Hub.Subscribe(["fake:home/temp"]);
        Hub.Unsubscribe(["fake:home/temp"]);

        Assert.Equal("first", Hub.Describe("fake:home/temp")!.Label);
        Assert.Equal(1, Count(Log(), "several catalog patterns fit"));
    }

    private static int Count(string text, string part)
    {
        int count = 0;
        for (int at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + 1, StringComparison.Ordinal))
            count++;
        return count;
    }

    [Fact]
    public void APathThatFitsNothing_IsUnavailableWithTheReason()
    {
        Hub.Subscribe(["fake:Zurich/humidity", "fake:Zurich/temp/extra", "fake:Zurich"]);

        Assert.True(Wait(() => Hub.Read("fake:Zurich/humidity").State == ReadingState.Unavailable));
        var reading = Hub.Read("fake:Zurich/humidity");
        Assert.Contains("no reading", reading.Reason);
        Assert.Contains("Zurich/humidity", reading.Reason);
        Assert.Equal(ReadingState.Unavailable, Hub.Read("fake:Zurich/temp/extra").State);
        Assert.Null(Hub.Describe("fake:Zurich/humidity"));
    }

    [Fact]
    public void AMalformedPattern_IsSkippedAndLogged_AndTheOthersStillWork()
    {
        Provider.Catalog =
        [
            new ReadingDescriptor("{a/temp", ReadingKind.Number, "bad1", "Unclosed"),
            new ReadingDescriptor("pre{a}/temp", ReadingKind.Number, "bad2", "Inside a segment"),
            new ReadingDescriptor("{}/temp", ReadingKind.Number, "bad3", "Empty name"),
            new ReadingDescriptor("{a}/{a}", ReadingKind.Number, "bad4", "Name twice"),
            new ReadingDescriptor("{a}/ok", ReadingKind.Number, "good", "Good"),
        ];
        Hub.Subscribe(["fake:x/temp", "fake:x/ok"]);
        Assert.True(Started());

        Assert.Equal("good", Hub.Describe("fake:x/ok")!.Label);
        Assert.Null(Hub.Describe("fake:x/temp"));
        Assert.Equal(4, Count(Log(), "malformed pattern"));
        Assert.Equal(["{a}/ok"], Hub.GetCatalog("fake")!.Select(d => d.Path));
    }

    [Fact]
    public void APathNobodyAskedFor_IsIgnoredWhenTheProviderPublishesIt()
    {
        Provider.OnSample = (_, sink, _) =>
        {
            sink.Publish("Zurich/temp", 14.5);
            sink.Publish("Nobody/temp", 99);
            return Task.CompletedTask;
        };
        Hub.Subscribe(["fake:Zurich/temp"]);

        Assert.True(Wait(() => Hub.Read("fake:Zurich/temp").State == ReadingState.Ok));
        Assert.Null(Hub.Describe("fake:Nobody/temp"));
        Assert.Equal(ReadingState.Unavailable, Hub.Read("fake:Nobody/temp").State);
        Assert.Contains("Nobody/temp", Log());
    }

    [Fact]
    public void TheCatalog_ListsAPatternOnceAsWritten_NotOncePerLocation()
    {
        Hub.Subscribe(["fake:Zurich/temp", "fake:Tokyo/temp", "fake:Oslo/temp"]);
        Assert.True(Started());

        string[] paths = Hub.GetCatalog("fake")!.Select(d => d.Path).ToArray();

        Assert.Equal(["{location}/temp", "{location}/place"], paths);
    }

    [Fact]
    public void TheCatalog_IsAvailableWithoutASubscriber_AndListsThePatterns()
    {
        var catalog = Hub.GetCatalog("fake");

        Assert.NotNull(catalog);
        Assert.Equal(2, catalog.Count);
        Assert.DoesNotContain("sample", Provider.Calls);
    }

    [Fact]
    public void IdsDifferingOnlyInCase_AreOneReading_AndTheProviderIsToldOnce()
    {
        Hub.Subscribe(["fake:zurich/temp", "fake:Zurich/temp"]);
        Assert.True(Started());

        Assert.Equal(Hub.Read("fake:zurich/temp"), Hub.Read("fake:ZURICH/TEMP"));
        Assert.Single(Provider.Demand);
    }

    [Fact]
    public void AReload_ClearsTheDescriptionsMadeFromPatterns()
    {
        Hub.Subscribe(["fake:Zurich/temp"]);
        Assert.True(Started());
        Assert.NotNull(Hub.Describe("fake:Zurich/temp"));

        Hub.ReleaseAll();

        Assert.Null(Hub.Describe("fake:Zurich/temp"));
    }

    [Fact]
    public void AnUnsubscribedLocation_IsLeftOutOfTheProvidersDemand()
    {
        Hub.Subscribe(["fake:Zurich/temp", "fake:Tokyo/temp"]);
        Assert.True(Wait(() => Provider.Calls.Contains("demand:Tokyo/temp,Zurich/temp")));

        Hub.Unsubscribe(["fake:Tokyo/temp"]);

        Assert.True(Wait(() => Provider.Calls.Contains("demand:Zurich/temp")));
    }
}
