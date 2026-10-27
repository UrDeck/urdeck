// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ReadingHubActionTests : IDisposable
{
    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _registry = new();

    public ReadingHubActionTests()
    {
        Provider = new ActionFakeProvider(_time);
        _registry.TryRegister(Provider.Descriptor(), out _);
        Hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false);
    }

    private ActionFakeProvider Provider { get; }

    private ReadingHub Hub { get; }

    public void Dispose() => Hub.Dispose();

    private static bool Wait(Func<bool> condition) => HubTestExtensions.WaitFor(condition);

    /// <summary>Subscribes a reading and waits until the provider has started, which is when actions are accepted.</summary>
    private void Run()
    {
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => Provider.Count("start") == 1));
        Assert.True(Wait(() => Hub.Invoke("fake:probe").GetAwaiter().GetResult().Succeeded));
    }

    [Fact]
    public async Task Done()
    {
        Run();

        var result = await Hub.Invoke("fake:control/next").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("control/next", Provider.Invoked);
    }

    [Fact]
    public async Task Failed_CarriesTheProvidersMessage()
    {
        Run();
        Provider.Handler = (_, _, _) => Task.FromResult(ActionResult.Failed("no session"));

        var result = await Hub.Invoke("fake:control/next").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("no session", result.Message);
    }

    [Fact]
    public async Task ArgumentReachesTheProvider()
    {
        Run();
        ReadingValue? seen = null;
        Provider.Handler = (_, argument, _) =>
        {
            seen = argument;
            return Task.FromResult(ActionResult.Done);
        };

        await Hub.Invoke("fake:volume/set", 40).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal((ReadingValue)40, seen);
    }

    [Fact]
    public async Task ThrowingProvider_IsAFailedResultAndTheNextActionRuns()
    {
        Run();
        Provider.Handler = (path, _, _) => path == "boom"
            ? throw new InvalidOperationException("kaput")
            : Task.FromResult(ActionResult.Done);

        var failed = await Hub.Invoke("fake:boom").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var next = await Hub.Invoke("fake:fine").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(failed.Succeeded);
        Assert.Equal("kaput", failed.Message);
        Assert.True(next.Succeeded);
    }

    [Fact]
    public async Task TwoInARow_KeepTheirOrderAndNeverOverlap()
    {
        Run();
        var gate = new TaskCompletionSource();
        Provider.Handler = async (path, _, _) =>
        {
            if (path == "play")
                await gate.Task;
            return ActionResult.Done;
        };

        var play = Hub.Invoke("fake:play");
        var pause = Hub.Invoke("fake:pause");
        Assert.True(Wait(() => Provider.Invoked.Contains("play")));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("pause", Provider.Invoked);

        gate.SetResult();
        await Task.WhenAll(play, pause).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(["probe", "play", "pause"], Provider.Invoked);
        Assert.Equal(1, Provider.MaxConcurrentActions);
    }

    [Fact]
    public async Task Hang_CancelsTheTokenFailsWithATimeoutAndRunsTheNext()
    {
        Run();
        CancellationToken seen = default;
        Provider.Handler = async (path, _, token) =>
        {
            if (path != "hang")
                return ActionResult.Done;
            seen = token;
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return ActionResult.Done;
        };

        var hung = Hub.Invoke("fake:hang");
        var next = Hub.Invoke("fake:next");
        Assert.True(Wait(() => Provider.Invoked.Contains("hang")));
        Assert.True(_time.AdvanceUntil(() => hung.IsCompleted, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(30)));

        var result = await hung;
        Assert.False(result.Succeeded);
        Assert.Contains("10 s", result.Message);
        Assert.True(seen.IsCancellationRequested);
        Assert.True((await next.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task QueueFull_NinthIsBusyAndTheEightKeepTheirPlaces()
    {
        Run();
        var gate = new TaskCompletionSource();
        Provider.Handler = async (_, _, _) =>
        {
            await gate.Task;
            return ActionResult.Done;
        };

        var first = Hub.Invoke("fake:a0");
        Assert.True(Wait(() => Provider.Invoked.Contains("a0")));
        var waiting = Enumerable.Range(1, 8).Select(i => Hub.Invoke($"fake:a{i}")).ToArray();
        var ninth = Hub.Invoke("fake:a9");

        Assert.True(ninth.IsCompleted);
        var busy = await ninth;
        Assert.False(busy.Succeeded);
        Assert.Contains("busy", busy.Message);

        gate.SetResult();
        var all = await Task.WhenAll(waiting.Append(first)).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.All(all, r => Assert.True(r.Succeeded));
    }

    [Fact]
    public async Task ProviderNotRunning_IsStartedForTheAction_AndLingersAfterwards()
    {
        var result = await Hub.Invoke("fake:control/next").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, Provider.Count("start"));
        Assert.Contains("control/next", Provider.Invoked);
        Assert.Equal(0, Provider.Count("stop"));
        Assert.True(_time.AdvanceUntil(() => Provider.Count("stop") == 1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task AWidgetSubscribingMeanwhile_KeepsTheProviderAfterTheAction()
    {
        var first = await Hub.Invoke("fake:control/next").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Hub.Subscribe(["fake:a"]);

        _time.Advance(TimeSpan.FromSeconds(20));

        Assert.True(first.Succeeded);
        Assert.Equal(0, Provider.Count("stop"));
        Assert.Equal(1, Provider.Count("start"));
    }

    [Fact]
    public async Task AConfiguredTimeLimit_AppliesToThatProvider()
    {
        Hub.ApplySettings(ConfigStore.Parse("""{"providers":{"fake":{"actionTimeoutMs":5000}}}""").Providers);
        Run();
        Provider.Handler = async (_, _, _) =>
        {
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return ActionResult.Done;
        };

        var hung = Hub.Invoke("fake:hang");
        Assert.True(Wait(() => Provider.Invoked.Contains("hang")));
        Assert.True(_time.AdvanceUntil(() => hung.IsCompleted, TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(30)));

        var result = await hung;
        Assert.False(result.Succeeded);
        Assert.Contains("5 s", result.Message);
        Assert.True(_time.Elapsed < TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task ATimeLimitBelowOneSecond_IsRaisedToOneSecond_AndLoggedOnce()
    {
        var lines = new List<string>();
        UrDeckLog.Listener = (_, message) => lines.Add(message);
        try
        {
            Hub.ApplySettings(ConfigStore.Parse("""{"providers":{"fake":{"actionTimeoutMs":100}}}""").Providers);
            Run();
            Provider.Handler = async (_, _, _) =>
            {
                await Task.Delay(Timeout.Infinite, CancellationToken.None);
                return ActionResult.Done;
            };

            var first = Hub.Invoke("fake:hang");
            Assert.True(Wait(() => Provider.Invoked.Contains("hang")));
            Assert.True(_time.AdvanceUntil(() => first.IsCompleted, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30)));
            var second = Hub.Invoke("fake:hang");
            Assert.True(_time.AdvanceUntil(() => second.IsCompleted, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30)));

            Assert.Contains("1 s", (await first).Message);
        }
        finally
        {
            UrDeckLog.Listener = null;
        }

        Assert.Single(lines, l => l.Contains("actionTimeoutMs 100 is below the minimum"));
    }

    [Fact]
    public async Task ASlowAction_IsDoneAndLogsAWarningNamingTheDurationAndTheThreshold()
    {
        var lines = new List<string>();
        UrDeckLog.Listener = (_, message) => lines.Add(message);
        try
        {
            Run();
            Provider.Handler = async (_, _, token) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(2400), _time, token);
                return ActionResult.Done;
            };

            var slow = Hub.Invoke("fake:slow");
            Assert.True(Wait(() => Provider.Invoked.Contains("slow")));
            Assert.True(_time.AdvanceUntil(() => slow.IsCompleted, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30)));

            Assert.True((await slow).Succeeded);
        }
        finally
        {
            UrDeckLog.Listener = null;
        }

        Assert.Contains(lines, l => l.Contains("fake:slow") && l.Contains("took 2.") && l.Contains("over the 2 s warning threshold"));
    }

    [Fact]
    public async Task AFastAction_LogsNoWarning()
    {
        var lines = new List<string>();
        UrDeckLog.Listener = (_, message) => lines.Add(message);
        try
        {
            Run();
            await Hub.Invoke("fake:quick").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            UrDeckLog.Listener = null;
        }

        Assert.DoesNotContain(lines, l => l.Contains("warning threshold"));
    }

    [Fact]
    public async Task AConfiguredWarningThreshold_IsUsed()
    {
        var lines = new List<string>();
        UrDeckLog.Listener = (_, message) => lines.Add(message);
        try
        {
            Hub.ApplySettings(ConfigStore.Parse("""{"providers":{"fake":{"actionWarnMs":500}}}""").Providers);
            Run();
            Provider.Handler = async (_, _, token) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(700), _time, token);
                return ActionResult.Done;
            };

            var slow = Hub.Invoke("fake:slow");
            Assert.True(Wait(() => Provider.Invoked.Contains("slow")));
            Assert.True(_time.AdvanceUntil(() => slow.IsCompleted, TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(30)));
            await slow;
        }
        finally
        {
            UrDeckLog.Listener = null;
        }

        Assert.Contains(lines, l => l.Contains("over the 0.5 s warning threshold"));
    }

    [Fact]
    public async Task AProviderWhoseRequiredSettingsAreMissing_FailsTheActionWithItsReason()
    {
        var typed = new TypedFakeProvider();
        _registry.TryRegister(typed.Descriptor(), out _);

        var result = await Hub.Invoke("typed:scene/toggle").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("Set up Typed in Settings", result.Message);
        Assert.Empty(typed.Starts);
    }

    [Fact]
    public async Task AnActionAfterTheSettingsAreComplete_StartsTheProviderAndRuns()
    {
        var typed = new TypedFakeProvider();
        _registry.TryRegister(typed.Descriptor(), out _);
        Hub.ApplySettings(ConfigStore.Parse("""{"providers":{"typed":{"url":"https://ha.home","token":"tok"}}}""").Providers);

        var result = await Hub.Invoke("typed:scene/toggle").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Single(typed.Starts);
    }
    [Fact]
    public async Task UnknownProvider_NamesIt()
    {
        var result = await Hub.Invoke("nothing:control/next");

        Assert.False(result.Succeeded);
        Assert.Contains("nothing", result.Message);
    }

    [Fact]
    public async Task MalformedId_FailsWithTheParseMessage()
    {
        var result = await Hub.Invoke("control/next");

        Assert.False(result.Succeeded);
        Assert.Contains("not a reading id", result.Message);
    }

    [Fact]
    public async Task ProviderWithoutActions_FailsAndIsNotTouched()
    {
        var plain = new FakeProvider(_time);
        _registry.TryRegister(plain.Descriptor("plain"), out _);
        Hub.Subscribe(["plain:a"]);
        Assert.True(Wait(() => plain.Count("start") == 1));

        var result = await Hub.Invoke("plain:control/next");

        Assert.False(result.Succeeded);
        Assert.Contains("offers no actions", result.Message);
        Assert.Null(Hub.GetActionCatalog("plain"));
    }

    [Fact]
    public async Task ProviderStops_WaitingActionsFail()
    {
        Run();
        var gate = new TaskCompletionSource();
        Provider.Handler = async (_, _, token) =>
        {
            await gate.Task.WaitAsync(token).ConfigureAwait(false);
            return ActionResult.Done;
        };

        var running = Hub.Invoke("fake:one");
        Assert.True(Wait(() => Provider.Invoked.Contains("one")));
        var waiting = new[] { Hub.Invoke("fake:two"), Hub.Invoke("fake:three") };

        Hub.Unsubscribe(["fake:a"]);
        Assert.True(_time.AdvanceUntil(() => Provider.Count("stop") == 1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)));

        foreach (var action in waiting)
            Assert.False((await action.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await running.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Succeeded);
        Assert.DoesNotContain("two", Provider.Invoked);
    }

    [Fact]
    public async Task ReleaseAll_FailsWaitingActionsAndTheNextRunStartsEmpty()
    {
        Run();
        var gate = new TaskCompletionSource();
        Provider.Handler = async (_, _, token) =>
        {
            await gate.Task.WaitAsync(token).ConfigureAwait(false);
            return ActionResult.Done;
        };

        var running = Hub.Invoke("fake:one");
        Assert.True(Wait(() => Provider.Invoked.Contains("one")));
        var waiting = Hub.Invoke("fake:two");

        Hub.ReleaseAll();

        Assert.False((await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await running.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Succeeded);

        Provider.Handler = null;
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => Provider.Count("start") == 2));
        Assert.True((await Hub.Invoke("fake:three").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Succeeded);
        Assert.DoesNotContain("two", Provider.Invoked);
    }

    [Fact]
    public async Task ActionDuringARunningSample_DoesNotWaitForIt()
    {
        var sampling = new TaskCompletionSource();
        Provider.OnSample = (_, _, token) => sampling.Task.WaitAsync(token);
        Hub.Subscribe(["fake:a"]);
        Assert.True(Wait(() => Provider.Count("start") == 1 && Provider.SamplesRunning == 1));
        Assert.True(Wait(() => Hub.Invoke("fake:probe").GetAwaiter().GetResult().Succeeded));

        var result = await Hub.Invoke("fake:control/next").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, Provider.SamplesRunning);
        sampling.SetResult();
    }

    [Fact]
    public void Catalog_IsReadWithoutStartingTheProviderAndCachedUntilRelease()
    {
        var first = Hub.GetActionCatalog("fake");
        var second = Hub.GetActionCatalog("fake");

        Assert.NotNull(first);
        Assert.Equal("control/next", Assert.Single(first).Path);
        Assert.Equal(first, second);
        Assert.Equal(0, Provider.Count("start"));
        Assert.Equal(1, Provider.Count("describe-actions"));

        Hub.ReleaseAll();
        Hub.GetActionCatalog("fake");

        Assert.Equal(2, Provider.Count("describe-actions"));
        Assert.Null(Hub.GetActionCatalog("nothing"));
    }

    [Fact]
    public async Task Disposed_FailsEveryAction()
    {
        Hub.Dispose();

        Assert.False((await Hub.Invoke("fake:control/next")).Succeeded);
    }

    /// <summary>A provider that reads nothing and takes actions, the handler of which the test sets.</summary>
    private sealed class ActionFakeProvider(TimeProvider time) : IDataProvider, IActionProvider
    {
        private readonly FakeProvider _inner = new(time);
        private readonly object _gate = new();
        private readonly List<string> _invoked = [];
        private readonly List<string> _extra = [];
        private int _running;

        public Func<string, ReadingValue?, CancellationToken, Task<ActionResult>>? Handler { get; set; }

        public Func<int, IReadingSink, CancellationToken, Task>? OnSample
        {
            get => _inner.OnSample;
            set => _inner.OnSample = value;
        }

        public int SamplesRunning => Volatile.Read(ref _inner.SamplesRunning);

        public int MaxConcurrentActions { get; private set; }

        public IReadOnlyList<string> Invoked
        {
            get
            {
                lock (_gate)
                    return _invoked.ToArray();
            }
        }

        public int Count(string call)
        {
            lock (_gate)
                return _inner.Count(call) + _extra.Count(c => c == call);
        }

        public ProviderDescriptor Descriptor(string id = "fake") =>
            new(id, "Fake", typeof(ActionFakeProvider), 1000, 250, "tests") { Factory = () => this };

        public IReadOnlyList<ReadingDescriptor> Describe() => _inner.Describe();

        public void Start(IReadingSink sink, ProviderSettings settings) => _inner.Start(sink, settings);

        public void SetDemand(IReadOnlyCollection<string> paths) => _inner.SetDemand(paths);

        public Task SampleAsync(CancellationToken cancellationToken) => _inner.SampleAsync(cancellationToken);

        public void Shutdown() => _inner.Shutdown();

        public IReadOnlyList<ActionDescriptor> DescribeActions()
        {
            lock (_gate)
                _extra.Add("describe-actions");
            return [new ActionDescriptor("control/next", "Next", "Next track")];
        }

        public async Task<ActionResult> InvokeAsync(string path, ReadingValue? argument, CancellationToken cancellationToken)
        {
            int running = Interlocked.Increment(ref _running);
            try
            {
                lock (_gate)
                {
                    MaxConcurrentActions = Math.Max(MaxConcurrentActions, running);
                    _invoked.Add(path);
                }

                var handler = Handler;
                return handler == null ? ActionResult.Done : await handler(path, argument, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }
    }
}
