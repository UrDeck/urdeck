// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// What a pushed provider tells the hub beyond values: data-providers, "Interruptions", "Descriptions Refined While Running",
/// "Natural Action And Icon" and "Listing Concrete Readings".
/// </summary>
public sealed class ReadingHubRefinementTests : IDisposable
{
    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _registry = new();
    private readonly string _logPath = Path.Combine(Path.GetTempPath(), $"urdeck-refine-{Guid.NewGuid():N}.log");
    private readonly List<IReadOnlyCollection<string>> _changes = [];

    public ReadingHubRefinementTests()
    {
        UrDeckLog.LogPath = _logPath;
        Provider = new FakeProvider(_time)
        {
            Catalog =
            [
                new ReadingDescriptor("a", ReadingKind.Number, "A", "Reading a"),
                new ReadingDescriptor("b", ReadingKind.Number, "B", "Reading b"),
                new ReadingDescriptor("{entity}", ReadingKind.Text, "{entity}", "An entity's state"),
            ],
            OnSample = (_, _, _) => Task.CompletedTask,
        };
        _registry.TryRegister(Provider.Descriptor(), out _);
        Hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false);
        Hub.ReadingsChanged += ids =>
        {
            lock (_changes)
                _changes.Add(ids);
        };
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

    private IReadingSink Started()
    {
        // Settled: the first sample is committed, so what the test publishes next is not buffered into it.
        Assert.True(Wait(() => Provider.Sink != null && Provider.Samples > 0 && Provider.IsSettled));
        return Provider.Sink!;
    }

    private bool Changed(string id)
    {
        lock (_changes)
            return _changes.Any(c => c.Contains(id, StringComparer.OrdinalIgnoreCase));
    }

    private void ForgetChanges()
    {
        lock (_changes)
            _changes.Clear();
    }

    [Fact]
    public void AnInterruption_MakesAReadingWithAValueStale_AndOneWithoutUnavailableWithTheReason()
    {
        Hub.Subscribe(["fake:a", "fake:b"]);
        var sink = Started();
        sink.Publish("a", 7);
        Assert.Equal(ReadingState.Ok, Hub.Read("fake:a").State);

        sink.Interrupted("connection lost");

        Assert.Equal(ReadingState.Stale, Hub.Read("fake:a").State);
        Assert.Equal(7, Hub.Read("fake:a").Value!.Value.Number);
        Assert.Equal(ReadingState.Unavailable, Hub.Read("fake:b").State);
        Assert.Equal("connection lost", Hub.Read("fake:b").Reason);
        Assert.Contains("Provider 'fake' is interrupted: connection lost", Log());
    }

    [Fact]
    public void AnInterruptionReportedTwice_ChangesNothingAndLogsNothingNew()
    {
        Hub.Subscribe(["fake:a"]);
        var sink = Started();
        sink.Publish("a", 7);
        sink.Interrupted("connection lost");
        string logged = Log();
        ForgetChanges();

        sink.Interrupted("connection lost");
        sink.Interrupted("still lost");

        Assert.Equal(logged, Log());
        Assert.False(Changed("fake:a"));
        Assert.Equal(ReadingState.Stale, Hub.Read("fake:a").State);
    }

    [Fact]
    public void APublicationAfterAnInterruption_MakesThatReadingOk_AndLeavesTheOthers()
    {
        Hub.Subscribe(["fake:a", "fake:b"]);
        var sink = Started();
        sink.PublishBatch([ReadingUpdate.Of("a", 1), ReadingUpdate.Of("b", 2)]);
        sink.Interrupted("connection lost");

        sink.Publish("a", 3);

        Assert.Equal(ReadingState.Ok, Hub.Read("fake:a").State);
        Assert.Equal(3, Hub.Read("fake:a").Value!.Value.Number);
        Assert.Equal(ReadingState.Stale, Hub.Read("fake:b").State);

        // A second outage is a new change of state, logged again.
        sink.Interrupted("lost again");
        Assert.Contains("is interrupted: lost again", Log());
    }

    [Fact]
    public void ARefinedDescription_ReplacesThePatternsAndRedrawsTheReading()
    {
        Hub.Subscribe(["fake:sensor.office_temperature"]);
        var sink = Started();
        Assert.Equal(ReadingKind.Text, Hub.Describe("fake:sensor.office_temperature")!.Kind);
        ForgetChanges();

        sink.Describe("sensor.office_temperature", new ReadingDescriptor("ignored", ReadingKind.Temperature, "Office temperature", "sensor.office_temperature"));

        var described = Hub.Describe("fake:sensor.office_temperature")!;
        Assert.Equal(ReadingKind.Temperature, described.Kind);
        Assert.Equal("Office temperature", described.Label);
        Assert.Equal("sensor.office_temperature", described.Path);
        Assert.True(Changed("fake:sensor.office_temperature"));
    }

    [Fact]
    public void DescribingAgain_ReplacesTheDescription_AndTheSameOneIsNoChange()
    {
        Hub.Subscribe(["fake:sensor.x"]);
        var sink = Started();
        var first = new ReadingDescriptor("sensor.x", ReadingKind.Temperature, "X", "sensor.x");
        sink.Describe("sensor.x", first);
        ForgetChanges();

        sink.Describe("sensor.x", first);
        Assert.False(Changed("fake:sensor.x"));

        sink.Describe("sensor.x", first with { Kind = ReadingKind.Number, Unit = "W" });
        Assert.Equal(ReadingKind.Number, Hub.Describe("fake:sensor.x")!.Kind);
        Assert.Equal("W", Hub.Describe("fake:sensor.x")!.Unit);
        Assert.True(Changed("fake:sensor.x"));
    }

    [Fact]
    public void ADescriptionForAPathNoPatternMatches_IsIgnoredAndLoggedOnce()
    {
        Hub.Subscribe(["fake:a"]);
        var sink = Started();

        sink.Describe("a", new ReadingDescriptor("a", ReadingKind.Text, "Other", "Other"));
        sink.Describe("x/y", new ReadingDescriptor("x/y", ReadingKind.Text, "Other", "Other"));
        sink.Describe("x/y", new ReadingDescriptor("x/y", ReadingKind.Text, "Other", "Other"));

        Assert.Equal("A", Hub.Describe("fake:a")!.Label);
        Assert.Null(Hub.Describe("fake:x/y"));
        string log = Log();
        Assert.Contains("described 'a', which no pattern of its catalog matches; ignored", log);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(log, "described 'x/y'"));
    }

    [Fact]
    public void ADescriptionForAPathNobodyShows_IsNotKept()
    {
        Hub.Subscribe(["fake:a"]);
        var sink = Started();

        sink.Describe("sensor.elsewhere", new ReadingDescriptor("sensor.elsewhere", ReadingKind.Temperature, "Elsewhere", "sensor.elsewhere"));

        Assert.Null(Hub.Describe("fake:sensor.elsewhere"));
    }

    [Fact]
    public void ARefinedDescription_IsDroppedWhenTheReadingIsNoLongerShown()
    {
        Hub.Subscribe(["fake:sensor.x"]);
        var sink = Started();
        sink.Describe("sensor.x", new ReadingDescriptor("sensor.x", ReadingKind.Temperature, "X", "sensor.x"));

        Hub.Unsubscribe(["fake:sensor.x"]);

        Assert.Equal(ReadingKind.Text, Hub.Describe("fake:sensor.x")!.Kind);
    }

    [Fact]
    public void ADescriptionsActionAndIcon_ReachTheWidget()
    {
        Hub.Subscribe(["fake:light.desk"]);
        var sink = Started();

        sink.Describe("light.desk", new ReadingDescriptor("light.desk", ReadingKind.OnOff, "Desk", "light.desk")
        {
            Action = "light.desk/toggle",
            Icon = "home/light",
        });

        var described = Hub.Describe("fake:light.desk")!;
        Assert.Equal("light.desk/toggle", described.Action);
        Assert.Equal("home/light", described.Icon);
    }

    [Fact]
    public void APatternsAction_IsFilledInLikeItsLabel()
    {
        Provider.Catalog = [new ReadingDescriptor("{entity}", ReadingKind.OnOff, "{entity}", "{entity}") { Action = "{entity}/toggle" }];
        Hub.Subscribe(["fake:light.desk"]);
        Started();

        Assert.Equal("light.desk/toggle", Hub.Describe("fake:light.desk")!.Action);
    }

    [Fact]
    public async Task AProviderWithoutAListing_AnswersWithAMessage()
    {
        var listing = await Hub.BrowseAsync("fake", TestContext.Current.CancellationToken);

        Assert.False(listing.Succeeded);
        Assert.Equal("provider 'fake' cannot list its readings", listing.Message);
        Assert.Equal("no data provider 'nosuch' is registered", (await Hub.BrowseAsync("nosuch", TestContext.Current.CancellationToken)).Message);
    }

    [Fact]
    public async Task AListing_StartsTheProvider_AndItsDescriptionsAreKeptWhileItRuns()
    {
        var browser = new BrowsingProvider();
        _registry.TryRegister(browser.Descriptor(), out _);

        var listing = await Hub.BrowseAsync("browse", TestContext.Current.CancellationToken);

        Assert.True(listing.Succeeded, listing.Message);
        Assert.Equal(["light.desk", "sensor.office_temperature"], listing.Readings!.Select(r => r.Path));
        Assert.Equal(1, browser.Starts);
        Assert.Equal(ReadingKind.Temperature, Hub.Describe("browse:sensor.office_temperature")!.Kind);

        // Nothing subscribes to it: it lingers and then stops, and the listing is forgotten.
        _time.Advance(TimeSpan.FromSeconds(6));
        Assert.True(Wait(() => browser.Stops == 1));
        Assert.Null(Hub.Describe("browse:sensor.office_temperature"));
    }

    [Fact]
    public async Task AReadingSubscribedAfterAListing_KeepsTheListedDescription()
    {
        var browser = new BrowsingProvider();
        _registry.TryRegister(browser.Descriptor(), out _);
        await Hub.BrowseAsync("browse", TestContext.Current.CancellationToken);

        Hub.Subscribe(["browse:light.desk"]);

        Assert.Equal(ReadingKind.OnOff, Hub.Describe("browse:light.desk")!.Kind);
        Assert.Equal("light.desk/toggle", Hub.Describe("browse:light.desk")!.Action);
    }

    [Fact]
    public async Task AFailingListing_AnswersWithItsReason()
    {
        var browser = new BrowsingProvider { Error = new InvalidOperationException("Home Assistant did not answer") };
        _registry.TryRegister(browser.Descriptor(), out _);

        var listing = await Hub.BrowseAsync("browse", TestContext.Current.CancellationToken);

        Assert.False(listing.Succeeded);
        Assert.Equal("provider 'browse' could not list its readings: Home Assistant did not answer", listing.Message);
    }

    [Fact]
    public void AProviderThatAsksForALongerLinger_KeepsRunningThatLong()
    {
        var browser = new BrowsingProvider();
        _registry.TryRegister(browser.Descriptor() with { LingerMs = 30_000 }, out _);
        Hub.Subscribe(["browse:light.desk"]);
        Assert.True(Wait(() => browser.Starts == 1));

        Hub.Unsubscribe(["browse:light.desk"]);
        _time.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(0, browser.Stops);

        _time.Advance(TimeSpan.FromSeconds(11));
        Assert.True(Wait(() => browser.Stops == 1));
    }

    [Fact]
    public void ASampleReadingsActionAndIcon_BecomeItsDescriptions()
    {
        var file = SampleFile.Parse(
            """
            {
              "version": 1,
              "readings": {
                "light.living_room": { "kind": "onOff", "label": "Living room", "name": "light.living_room",
                  "action": "light.living_room/toggle", "icon": "home/light", "value": true }
              }
            }
            """, out var findings);

        Assert.Empty(findings);
        var described = file!.Describe().Single();
        Assert.Equal("light.living_room/toggle", described.Action);
        Assert.Equal("home/light", described.Icon);
        Assert.False(typeof(IActionProvider).IsAssignableFrom(typeof(SampleProvider)));
    }

    [Fact]
    public void ASampleReadingsEmptyIcon_IsAFinding()
    {
        var file = SampleFile.Parse(
            """{ "version": 1, "readings": { "x": { "kind": "text", "label": "X", "name": "X", "icon": " ", "action": 3, "value": "" } } }""",
            out var findings);

        Assert.Null(file);
        Assert.Contains("'x': icon must not be empty", findings);
        Assert.Contains("'x': action must be a text", findings);
    }

    /// <summary>A provider with an <c>{entity}</c> pattern that lists two entities.</summary>
    private sealed class BrowsingProvider : IDataProvider, IReadingBrowser
    {
        private int _starts;
        private int _stops;

        public int Starts => Volatile.Read(ref _starts);

        public int Stops => Volatile.Read(ref _stops);

        public Exception? Error { get; init; }

        public IReadOnlyList<ReadingDescriptor> Describe() => [new ReadingDescriptor("{entity}", ReadingKind.Text, "{entity}", "An entity")];

        public void Start(IReadingSink sink, ProviderSettings settings) => Interlocked.Increment(ref _starts);

        public void SetDemand(IReadOnlyCollection<string> paths)
        {
        }

        public Task SampleAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Shutdown() => Interlocked.Increment(ref _stops);

        public Task<IReadOnlyList<ReadingDescriptor>> BrowseAsync(CancellationToken cancellationToken) =>
            Error != null
                ? Task.FromException<IReadOnlyList<ReadingDescriptor>>(Error)
                : Task.FromResult<IReadOnlyList<ReadingDescriptor>>(
                [
                    new ReadingDescriptor("light.desk", ReadingKind.OnOff, "Desk", "light.desk") { Action = "light.desk/toggle" },
                    new ReadingDescriptor("sensor.office_temperature", ReadingKind.Temperature, "Office", "sensor.office_temperature"),
                ]);

        public ProviderDescriptor Descriptor() =>
            new("browse", "Browse", typeof(BrowsingProvider), 1000, 250, "tests") { Factory = () => this };
    }
}
