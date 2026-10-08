// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ReadingHubTests : IDisposable
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(10);

    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _registry = new();
    private readonly List<(string[] Ids, ReadingState StateOfA)> _changes = [];
    private readonly List<IDisposable> _disposables = [];

    public ReadingHubTests()
    {
        Provider = new FakeProvider(_time);
        _registry.TryRegister(Provider.Descriptor(), out _);
        Hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false);
        Hub.ReadingsChanged += ids =>
        {
            lock (_changes)
                _changes.Add((ids.ToArray(), Hub.Read("fake:a").State));
        };
        _disposables.Add(Hub);
    }

    private FakeProvider Provider { get; }

    private ReadingHub Hub { get; }

    public void Dispose()
    {
        foreach (var d in _disposables)
            d.Dispose();
    }

    private bool Pump(Func<bool> condition, int virtualSeconds = 30) =>
        _time.AdvanceUntil(condition, Step, TimeSpan.FromSeconds(virtualSeconds), settled: () => Provider.IsSettled);

    /// <summary>Waits for the first sample before moving the clock, so every gap is measured from a known start.</summary>
    private bool PumpSamples(int count, int virtualSeconds = 30) =>
        Wait(() => Provider.Samples >= 1) && Pump(() => Provider.Samples >= count, virtualSeconds);

    private static bool Wait(Func<bool> condition) => HubTestExtensions.WaitFor(condition);

    private bool FirstSampleDone(string id = "fake:a") => Hub.Read(id).State == ReadingState.Ok;

    private static ProviderSettings Interval(int ms) => new() { IntervalMs = ms };

    private static double Ms(TimeSpan span) => span.TotalMilliseconds;

    private TimeSpan[] SampleTimes() => Provider.SampleTimes.ToArray();

    // Lifetime

    [Fact]
    public void NoSubscription_NoProviderInstanceAndNoTimer()
    {
        int created = 0;
        var registry = new ProviderRegistry();
        registry.TryRegister(Provider.Descriptor("other") with { Factory = () => { created++; return Provider; } }, out _);
        using var hub = new ReadingHub(registry, _time);

        _ = hub.Read("other:a");
        _ = hub.Describe("other:a");
        hub.Subscribe(["nosuch:value"]);
        _time.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(0, created);
        Assert.Equal(0, _time.ActiveTimers);
    }

    [Fact]
    public void FirstSubscription_CreatesStartsAndTellsTheProviderWhatIsWanted()
    {
        Hub.Subscribe(["fake:a"]);

        Assert.True(Wait(() => FirstSampleDone()));
        Assert.Equal(["describe", "start", "demand:a", "sample"], Provider.Calls.Take(4));
    }

    [Fact]
    public void ReadingsTheProviderIsNotAskedFor_AreNotInTheDemand()
    {
        Hub.Subscribe(["fake:a", "fake:nosuchpath"]);

        Assert.True(Wait(() => Provider.Calls.Contains("sample")));
        Assert.Equal("demand:a", Provider.Calls.First(c => c.StartsWith("demand", StringComparison.Ordinal)));
    }

    [Fact]
    public void DemandIsUpdatedWhenTheSubscribedSetChanges()
    {
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));

        Hub.Subscribe(["fake:b"]);
        Assert.True(Wait(() => Provider.Calls.Contains("demand:a,b")));

        Hub.Unsubscribe(["fake:a"]);
        Assert.True(Wait(() => Provider.Calls.Contains("demand:b")));
    }

    [Fact]
    public void TwoSubscribersShareOneReading_AndTheProviderIsSampledOnce()
    {
        Hub.Subscribe(["fake:a"]);
        Hub.Subscribe(["fake:a"]);
        Assert.True(PumpSamples(3));

        Assert.Equal(1, Provider.Count("start"));
        Assert.Equal(1, Provider.MaxConcurrentSamples);
        Assert.Equal(Hub.Read("fake:a"), Hub.Read("FAKE:A"));

        // One of the two leaves: the reading is still wanted and nothing stops.
        Hub.Unsubscribe(["fake:a"]);
        _time.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(0, Provider.Count("stop"));
    }

    [Fact]
    public void LastSubscriptionEnding_StopsTheProviderAfterTheLinger()
    {
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));

        var left = _time.Elapsed;
        Hub.Unsubscribe(["fake:a"]);
        _time.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(0, Provider.Count("stop"));

        Assert.True(Pump(() => Provider.Count("stop") == 1));
        Assert.InRange(Ms(_time.Elapsed - left), 5000, 5500);

        int samples = Provider.Samples;
        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(samples, Provider.Samples);
    }

    [Fact]
    public void ResubscribingWithinTheLinger_DoesNotRestartTheProvider()
    {
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));

        Hub.Unsubscribe(["fake:a"]);
        _time.Advance(TimeSpan.FromSeconds(2));
        Hub.Subscribe(["fake:a"]);
        _time.Advance(TimeSpan.FromSeconds(20));

        Assert.Equal(1, Provider.Count("start"));
        Assert.Equal(1, Provider.Count("describe"));
        Assert.Equal(0, Provider.Count("stop"));
    }

    [Fact]
    public void AReadingKeepsItsStateAcrossAPageRebuildWithinTheLinger()
    {
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));

        Hub.Unsubscribe(["fake:a"]);
        Hub.Subscribe(["fake:a"]);

        Assert.Equal(ReadingState.Ok, Hub.Read("fake:a").State);
    }

    [Fact]
    public void TheLastValueIsKept_AndIsPendingWhenSubscribedAgainAfterAStop()
    {
        Provider.OnSample = (_, sink, _) =>
        {
            sink.Publish("a", 42);
            return Task.CompletedTask;
        };
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));
        Hub.Unsubscribe(["fake:a"]);
        Assert.True(Pump(() => Provider.Count("stop") == 1));

        Assert.Equal(Reading.Ok(42), Hub.Read("fake:a"));

        Hub.Subscribe(["fake:a"]);

        Assert.Equal(Reading.Pending(42), Hub.Read("fake:a"));
        Assert.True(Wait(() => FirstSampleDone()));
        Assert.Equal(2, Provider.Count("start"));
    }

    [Fact]
    public void CatalogWithoutSubscribers_ListsTheReadingsAndDoesNoSampling()
    {
        var catalog = Hub.GetCatalog("fake");

        Assert.Equal(["a", "b", "c"], catalog!.Select(e => e.Path));
        Assert.Equal("A", Hub.Describe("fake:a")?.Label);
        _time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(["describe"], Provider.Calls);
        Assert.Null(Hub.GetCatalog("nosuch"));
    }

    [Fact]
    public void ReleaseAll_StopsEveryProviderAndDropsValuesAndCatalogs()
    {
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));

        Hub.ReleaseAll();

        Assert.Equal(1, Provider.Count("stop"));
        Assert.Equal(ReadingState.Unavailable, Hub.Read("fake:a").State);
        Assert.Null(Hub.Describe("fake:a"));

        // The page rebuild that follows resubscribes against fresh state; old unsubscribes are ignored.
        Hub.Unsubscribe(["fake:a"]);
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));
        Assert.Equal(2, Provider.Count("start"));
    }

    [Fact]
    public void Dispose_StopsTheProvider()
    {
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));

        Hub.Dispose();

        Assert.Equal(1, Provider.Count("stop"));
    }

    // Sampling

    [Fact]
    public void ASampleIsTakenAtOnce_AgainAfter250Ms_ThenPerInterval()
    {
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => Provider.Samples >= 1));
        Assert.True(PumpSamples(3));

        var times = SampleTimes();
        Assert.InRange(Ms(times[1] - times[0]), 250, 530);
        Assert.InRange(Ms(times[2] - times[1]), 1000, 1330);
    }

    [Fact]
    public void ValuesOfOneSample_ArriveTogether_InOneNotification()
    {
        Hub.Subscribe(["fake:a", "fake:b", "fake:c"]);
        Provider.OnSample = (_, sink, _) =>
        {
            sink.Publish("a", 1);
            sink.Publish("b", 2);
            sink.Publish("c", 3);
            return Task.CompletedTask;
        };
        Assert.True(Wait(() => FirstSampleDone()));

        (string[] Ids, ReadingState StateOfA)[] batches;
        lock (_changes)
            batches = _changes.Where(c => c.StateOfA == ReadingState.Ok).ToArray();

        // Exactly one notification saw reading a become ok, and it carried all three.
        Assert.Single(batches);
        Assert.Contains("fake:b", batches[0].Ids, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("fake:c", batches[0].Ids, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnchangedValue_RaisesNothing()
    {
        Provider.OnSample = (_, sink, _) =>
        {
            sink.Publish("a", 5);
            return Task.CompletedTask;
        };
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));
        int before;
        lock (_changes)
            before = _changes.Count;

        Assert.True(PumpSamples(4));
        Thread.Sleep(20);

        lock (_changes)
            Assert.Equal(before, _changes.Count);
    }

    [Fact]
    public void APublicationBetweenSamples_IsCommittedOnItsOwn()
    {
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));
        Assert.True(Wait(() => Provider.SamplesRunning == 0));
        int before;
        lock (_changes)
            before = _changes.Count;

        Provider.Sink!.Publish("a", 99);

        Assert.Equal(Reading.Ok(99), Hub.Read("fake:a"));
        lock (_changes)
            Assert.Equal(before + 1, _changes.Count);
    }

    [Fact]
    public void ASlowSample_IsNeverOverlapped_AndOtherProvidersAreSampledOnTime()
    {
        Provider.OnSample = async (_, _, ct) => await Task.Delay(TimeSpan.FromSeconds(3), _time, ct);
        var other = new FakeProvider(_time);
        _registry.TryRegister(other.Descriptor("other"), out _);

        Hub.Subscribe(["fake:a", "other:a"]);
        _time.AdvanceUntil(() => other.Samples >= 12, Step, TimeSpan.FromSeconds(30), settled: () => other.IsSettled);

        Assert.True(other.Samples >= 12);
        Assert.Equal(1, Provider.MaxConcurrentSamples);
        Assert.True(Provider.Samples < other.Samples);
    }

    // Rate settings

    [Fact]
    public void TheConfiguredInterval_ReplacesTheDefault()
    {
        Hub.ApplySettings(new Dictionary<string, ProviderSettings> { ["fake"] = Interval(500) });
        Hub.Subscribe(["fake:a"]);
        Assert.True(PumpSamples(3));

        var times = SampleTimes();
        Assert.InRange(Ms(times[2] - times[1]), 500, 780);
    }

    [Fact]
    public void AnIntervalBelowTheMinimum_IsRaisedToIt()
    {
        Hub.ApplySettings(new Dictionary<string, ProviderSettings> { ["FAKE"] = Interval(10) });
        Hub.Subscribe(["fake:a"]);
        Assert.True(PumpSamples(3));

        var times = SampleTimes();
        Assert.InRange(Ms(times[2] - times[1]), 250, 530);
    }

    [Fact]
    public void ChangingTheIntervalWhileRunning_TakesEffectWithoutARestart()
    {
        Hub.Subscribe(["fake:a"]);
        Assert.True(PumpSamples(3));

        Hub.ApplySettings(new Dictionary<string, ProviderSettings> { ["fake"] = Interval(500) });
        Assert.True(PumpSamples(5));

        var times = SampleTimes();
        Assert.InRange(Ms(times[4] - times[3]), 500, 780);
        Assert.Equal(1, Provider.Count("start"));
    }

    [Fact]
    public void SettingsForAProviderThatIsNotRegistered_AreIgnored()
    {
        Hub.ApplySettings(new Dictionary<string, ProviderSettings> { ["nosuch"] = Interval(500) });
        Hub.Subscribe(["fake:a"]);

        Assert.True(Wait(() => FirstSampleDone()));
    }

    // States and faults

    [Fact]
    public void AnUnknownProvider_IsUnavailable()
    {
        Hub.Subscribe(["nosuch:value"]);

        var reading = Hub.Read("nosuch:value");
        Assert.Equal(ReadingState.Unavailable, reading.State);
        Assert.Contains("nosuch", reading.Reason);
    }

    [Fact]
    public void AMalformedId_IsUnavailable()
    {
        Hub.Subscribe(["nocolon", "fake:"]);

        Assert.Equal(ReadingState.Unavailable, Hub.Read("nocolon").State);
        Assert.Equal(ReadingState.Unavailable, Hub.Read("fake:").State);
        Assert.Equal(ReadingState.Unavailable, Hub.Read("neverasked").State);
    }

    [Fact]
    public void APathNotInTheCatalog_IsUnavailable_WhileTheRestStillWorks()
    {
        Hub.Subscribe(["fake:a", "fake:zzz"]);

        Assert.True(Wait(() => Hub.Read("fake:zzz").State == ReadingState.Unavailable));
        Assert.Contains("zzz", Hub.Read("fake:zzz").Reason);
        Assert.True(Wait(() => FirstSampleDone()));
    }

    [Fact]
    public void ASubscribedReading_IsPendingUntilTheProviderReports()
    {
        Provider.OnSample = (_, _, _) => Task.Delay(Timeout.Infinite, CancellationToken.None);
        Hub.Subscribe(["fake:a"]);

        Assert.Equal(ReadingState.Pending, Hub.Read("fake:a").State);
        Assert.True(Hub.AnyPending(["fake:a"]));
    }

    [Fact]
    public void AProviderThatReportsAReadingUnavailable_GivesItsReason()
    {
        Provider.OnSample = (n, sink, _) =>
        {
            if (n == 0)
                sink.Unavailable("a", "no sensor");
            else
                sink.Publish("a", 7);
            return Task.CompletedTask;
        };
        Hub.Subscribe(["fake:a"]);

        Assert.True(Wait(() => Hub.Read("fake:a").State == ReadingState.Unavailable));
        Assert.Equal("no sensor", Hub.Read("fake:a").Reason);
        Assert.True(Pump(() => FirstSampleDone()));
        Assert.Equal(Reading.Ok(7), Hub.Read("fake:a"));
    }

    [Fact]
    public void AFailingSampleAfterASuccess_MakesTheReadingsStaleWithTheirLastValue_AndTheyRecover()
    {
        Provider.OnSample = (n, sink, _) =>
        {
            if (n == 1)
                throw new InvalidOperationException("boom");
            sink.Publish("a", n == 0 ? 5 : 8);
            return Task.CompletedTask;
        };
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));

        Assert.True(Pump(() => Hub.Read("fake:a").State == ReadingState.Stale));
        Assert.Equal(Reading.Stale(5), Hub.Read("fake:a"));

        Assert.True(Pump(() => FirstSampleDone()));
        Assert.Equal(Reading.Ok(8), Hub.Read("fake:a"));
    }

    [Fact]
    public void AProviderThatFailsBeforeAnyValue_MakesItsReadingsUnavailable()
    {
        Provider.OnSample = (_, _, _) => throw new InvalidOperationException("boom");
        Hub.Subscribe(["fake:a"]);

        Assert.True(Wait(() => Hub.Read("fake:a").State == ReadingState.Unavailable));
        Assert.Contains("boom", Hub.Read("fake:a").Reason);
    }

    [Fact]
    public void AThrowingProvider_DoesNotAffectAnother()
    {
        Provider.OnSample = (_, _, _) => throw new InvalidOperationException("boom");
        var other = new FakeProvider(_time);
        _registry.TryRegister(other.Descriptor("other"), out _);

        Hub.Subscribe(["fake:a", "other:a"]);

        Assert.True(Wait(() => Hub.Read("other:a").State == ReadingState.Ok));
        Assert.True(Wait(() => Hub.Read("fake:a").State == ReadingState.Unavailable));
    }

    [Fact]
    public void AfterFailures_TheDelayDoublesEachTime()
    {
        Provider.OnSample = (_, _, _) => throw new InvalidOperationException("boom");
        Hub.Subscribe(["fake:a"]);
        Assert.True(PumpSamples(5));

        var times = SampleTimes();
        Assert.InRange(Ms(times[1] - times[0]), 1000, 1330);
        Assert.InRange(Ms(times[2] - times[1]), 2000, 2330);
        Assert.InRange(Ms(times[3] - times[2]), 4000, 4330);
        Assert.InRange(Ms(times[4] - times[3]), 8000, 8330);
    }

    [Fact]
    public void TheBackoff_IsCappedAtAMinute()
    {
        _registry.Clear();
        _registry.TryRegister(Provider.Descriptor(defaultMs: 40000), out _);
        Provider.OnSample = (_, _, _) => throw new InvalidOperationException("boom");
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => Provider.Samples >= 1));
        Assert.True(_time.AdvanceUntil(() => Provider.Samples >= 4, TimeSpan.FromMilliseconds(250), TimeSpan.FromMinutes(10), settled: () => Provider.IsSettled));

        var times = SampleTimes();
        Assert.InRange(Ms(times[1] - times[0]), 40000, 40750);
        Assert.InRange(Ms(times[2] - times[1]), 60000, 60750);
        Assert.InRange(Ms(times[3] - times[2]), 60000, 60750);
    }

    [Fact]
    public void AfterARecovery_TheProviderReturnsToItsInterval()
    {
        Provider.OnSample = (n, sink, _) =>
        {
            if (n == 0)
                throw new InvalidOperationException("boom");
            sink.Publish("a", n);
            return Task.CompletedTask;
        };
        Hub.Subscribe(["fake:a"]);
        Assert.True(PumpSamples(4));

        var times = SampleTimes();
        // After the failure comes a success at the retry; then the early sample, then the interval.
        Assert.InRange(Ms(times[3] - times[2]), 1000, 1330);
    }

    [Fact]
    public void AHangingSample_TimesOut_IsAskedToCancel_AndIsNeverOverlapped()
    {
        CancellationToken seen = default;
        Provider.OnSample = async (n, sink, ct) =>
        {
            if (n == 0)
            {
                sink.Publish("a", 5);
                return;
            }

            seen = ct;
            await new TaskCompletionSource().Task; // ignores cancellation
        };
        _registry.TryRegister(new FakeProvider(_time).Descriptor("other"), out _);
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => FirstSampleDone()));

        Assert.True(Pump(() => Hub.Read("fake:a").State == ReadingState.Stale));
        Assert.Equal(Reading.Stale(5), Hub.Read("fake:a"));
        Assert.True(seen.IsCancellationRequested);

        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(2, Provider.Samples);
        Assert.Equal(1, Provider.MaxConcurrentSamples);
    }

    [Fact]
    public void AHangingSample_HoldsUpOnlyItsOwnProvider()
    {
        Provider.OnSample = async (_, _, _) => await new TaskCompletionSource().Task;
        var other = new FakeProvider(_time);
        _registry.TryRegister(other.Descriptor("other"), out _);
        Hub.Subscribe(["fake:a", "other:a"]);

        Assert.True(_time.AdvanceUntil(() => other.Samples >= 20, Step, TimeSpan.FromSeconds(60), settled: () => other.IsSettled));
        Assert.Equal(1, Provider.Samples);
        Assert.Equal(ReadingState.Ok, Hub.Read("other:a").State);
    }

    [Fact]
    public void ADescribeThatThrows_MakesTheProvidersReadingsUnavailable()
    {
        Provider.DescribeError = new InvalidOperationException("no catalog");
        Hub.Subscribe(["fake:a"]);

        Assert.True(Wait(() => Hub.Read("fake:a").State == ReadingState.Unavailable));
        Assert.Contains("no catalog", Hub.Read("fake:a").Reason);
    }

    [Fact]
    public void AStartThatThrows_IsRetriedWithABackoff()
    {
        Provider.StartError = new InvalidOperationException("no device");
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => Hub.Read("fake:a").State == ReadingState.Unavailable));

        Provider.StartError = null;
        Assert.True(Pump(() => FirstSampleDone()));
        Assert.True(Provider.Count("start") >= 2);
    }

    [Fact]
    public void ThreeSamplesWithoutAReport_MakeAReadingStale()
    {
        Provider.OnSample = (n, sink, _) =>
        {
            sink.Publish("b", n);
            if (n == 0)
                sink.Publish("a", 5);
            return Task.CompletedTask;
        };
        Hub.Subscribe(["fake:a", "fake:b"]);
        Assert.True(Wait(() => FirstSampleDone()));

        Assert.True(Pump(() => Hub.Read("fake:a").State == ReadingState.Stale));

        // Samples 1, 2 and 3 skipped it; the third made it stale.
        Assert.Equal(4, Provider.Samples);
        Assert.Equal(Reading.Stale(5), Hub.Read("fake:a"));
        Assert.Equal(ReadingState.Ok, Hub.Read("fake:b").State);
    }

    [Fact]
    public void AnEmptySample_IsAPushedProvidersIdleTick_NotASkippedReading()
    {
        Provider.OnSample = (_, _, _) => Task.CompletedTask;
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => Provider.Sink != null));
        Provider.Sink!.Publish("a", 1);

        _time.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(ReadingState.Ok, Hub.Read("fake:a").State);
    }

    [Fact]
    public void SubscribeAndUnsubscribe_ToleratesIdsNobodyAskedFor()
    {
        Hub.Unsubscribe(["fake:a", "nocolon", "nosuch:x"]);
        Hub.Unsubscribe(["fake:a"]);

        Assert.Equal(0, _time.ActiveTimers);
    }

    [Fact]
    public void RegionFahrenheit_IsReportedForTheFormatter()
    {
        using var hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: true);

        Assert.True(hub.RegionUsesFahrenheit);
        Assert.False(Hub.RegionUsesFahrenheit);
    }
}
