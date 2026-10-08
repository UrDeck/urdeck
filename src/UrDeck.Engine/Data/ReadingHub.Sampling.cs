// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

// Provider lifetime and the sampling loop. One loop per provider on the thread pool; a loop awaits its own sample, so a
// provider is never sampled twice at once and a slow or hung provider cannot delay another one.
public sealed partial class ReadingHub
{
    private static readonly TimeSpan EarlySample = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MinSampleTimeout = TimeSpan.FromSeconds(5);

    // A provider that publishes in a sample but skips a wanted reading this many samples in a row makes it stale.
    private const int StaleAfterMisses = 3;

    // Under the gate.

    private void EnsureLoop(ProviderRuntime rt)
    {
        if (rt.Loop != null)
            return;
        rt.Cts = new CancellationTokenSource();
        int generation = rt.Generation;
        var token = rt.Cts.Token;
        rt.Loop = Task.Run(() => RunAsync(rt, generation, token));
    }

    private void StartLinger(ProviderRuntime rt)
    {
        if (rt.Loop == null || rt.LingerTimer != null)
            return;
        rt.LingerTimer = _time.CreateTimer(_ => OnLinger(rt), null, Linger, Timeout.InfiniteTimeSpan);
    }

    private void MarkDemandDirty(ProviderRuntime rt)
    {
        rt.DemandDirty = true;
        if (!rt.Started || rt.DemandPumpRunning)
            return;
        rt.DemandPumpRunning = true;
        Task.Run(() =>
        {
            try
            {
                PumpDemand(rt);
            }
            finally
            {
                lock (_gate)
                    rt.DemandPumpRunning = false;
            }
        });
    }

    /// <summary>
    /// Ends the provider's current run: the loop is cancelled and the instance forgotten, so the next subscription
    /// starts a fresh one. Returns what must be done outside the gate (waiting for the loop, calling Stop).
    /// </summary>
    private static Action Detach(ProviderRuntime rt)
    {
        var instance = rt.Started ? rt.Instance : null;
        var cts = rt.Cts;
        var loop = rt.Loop;
        var timer = rt.LingerTimer;
        string providerId = rt.Descriptor.Id;

        rt.Instance = null;
        rt.Started = false;
        rt.Cts = null;
        rt.Loop = null;
        rt.LingerTimer = null;
        rt.Generation++;
        rt.Failures = 0;
        rt.DemandDirty = false;
        rt.InSample = false;
        rt.Buffer.Clear();
        rt.Misses.Clear();

        return () =>
        {
            timer?.Dispose();
            cts?.Cancel();
            bool finished = true;
            if (loop != null)
            {
                try
                {
                    finished = loop.Wait(TimeSpan.FromMilliseconds(500));
                }
                catch (AggregateException)
                {
                    // The loop logs its own failures.
                }
            }

            if (finished)
            {
                StopInstance(providerId, instance);
                cts?.Dispose();
            }
            else
            {
                // A hung sample: do not hold up a reload for it. The thread it occupies is the provider's cost.
                UrDeckLog.Warn($"Provider '{providerId}' is still sampling; stopping it without waiting");
                Task.Run(() => StopInstance(providerId, instance));
            }
        };
    }

    private static void StopInstance(string providerId, IDataProvider? instance)
    {
        if (instance == null)
            return;
        try
        {
            instance.Shutdown();
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Provider '{providerId}' failed to stop", ex);
        }
    }

    private void OnLinger(ProviderRuntime rt)
    {
        Action stop;
        lock (_gate)
        {
            if (_disposed || rt.Wanted.Count > 0 || rt.LingerTimer == null
                || !_runtimes.TryGetValue(rt.Descriptor.Id, out var current) || !ReferenceEquals(current, rt))
                return;
            stop = Detach(rt);
        }

        UrDeckLog.Info($"Provider '{rt.Descriptor.Id}' stopped: nothing subscribes to it any more");
        stop();
    }

    // Outside the gate.

    private IDataProvider? EnsureInstance(ProviderRuntime rt, out string? error)
    {
        error = null;
        int generation;
        lock (_gate)
        {
            if (rt.Instance != null)
                return rt.Instance;
            generation = rt.Generation;
        }

        IDataProvider instance;
        try
        {
            instance = rt.Descriptor.CreateInstance();
        }
        catch (Exception ex)
        {
            error = $"could not be created: {ex.GetBaseException().Message}";
            UrDeckLog.Error($"Provider '{rt.Descriptor.Id}' {error}", ex);
            return null;
        }

        lock (_gate)
        {
            if (rt.Instance != null)
                return rt.Instance;
            if (rt.Generation != generation || _disposed)
                return null;
            rt.Instance = instance;
            return instance;
        }
    }

    private Dictionary<string, ReadingDescriptor>? CacheCatalog(ProviderRuntime rt, IDataProvider instance, out string? error)
    {
        error = null;
        lock (_gate)
        {
            if (rt.Catalog != null)
                return rt.Catalog;
        }

        var catalog = new Dictionary<string, ReadingDescriptor>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var entry in instance.Describe())
            {
                if (entry != null && !string.IsNullOrEmpty(entry.Path) && !catalog.TryAdd(entry.Path, entry))
                    UrDeckLog.Warn($"Provider '{rt.Descriptor.Id}' lists '{entry.Path}' twice; keeping the first");
            }
        }
        catch (Exception ex)
        {
            error = $"could not describe its readings: {ex.GetBaseException().Message}";
            UrDeckLog.Error($"Provider '{rt.Descriptor.Id}' {error}", ex);
            return null;
        }

        var changed = new List<string>();
        lock (_gate)
        {
            if (rt.Catalog != null)
                return rt.Catalog;
            if (_disposed || !_runtimes.TryGetValue(rt.Descriptor.Id, out var current) || !ReferenceEquals(current, rt))
                return catalog;

            rt.Catalog = catalog;
            foreach (var entry in catalog.Values)
                _descriptors[$"{rt.Descriptor.Id}:{entry.Path}"] = entry;

            // Subscribers now get their labels and units, and a path the provider does not have is found out.
            foreach (string path in rt.Wanted)
            {
                string id = $"{rt.Descriptor.Id}:{path}";
                CheckPath(rt, id, path, changed);
                if (!changed.Contains(id, StringComparer.OrdinalIgnoreCase))
                    changed.Add(id);
            }
        }

        Raise(changed);
        return catalog;
    }

    private void PumpDemand(ProviderRuntime rt)
    {
        lock (rt.DemandLock)
        {
            while (true)
            {
                IDataProvider instance;
                string[] paths;
                lock (_gate)
                {
                    if (!rt.DemandDirty || !rt.Started || rt.Instance == null)
                        return;
                    rt.DemandDirty = false;
                    instance = rt.Instance;
                    paths = rt.Wanted
                        .Select(p => rt.Catalog != null && rt.Catalog.TryGetValue(p, out var d) ? d.Path : null)
                        .OfType<string>()
                        .ToArray();
                }

                try
                {
                    instance.SetDemand(paths);
                }
                catch (Exception ex)
                {
                    UrDeckLog.Error($"Provider '{rt.Descriptor.Id}' failed in SetDemand", ex);
                }
            }
        }
    }

    private async Task RunAsync(ProviderRuntime rt, int generation, CancellationToken stop)
    {
        int samples = 0;
        try
        {
            while (true)
            {
                stop.ThrowIfCancellationRequested();

                bool started;
                lock (_gate)
                    started = rt.Started;
                if (!started)
                {
                    long attempt = _time.GetTimestamp();
                    if (!TryStart(rt, generation, out string? error))
                    {
                        stop.ThrowIfCancellationRequested();
                        FailProvider(rt, generation, error ?? "failed to start", null);
                        await WaitAsync(rt, attempt, () => Backoff(rt), stop);
                        continue;
                    }

                    samples = 0;
                }

                PumpDemand(rt);
                long begin = _time.GetTimestamp();
                bool ok = await SampleOnceAsync(rt, generation, stop);
                samples++;
                Func<TimeSpan> due = !ok ? () => Backoff(rt)
                    : samples == 1 ? () => Min(EarlySample, IntervalOf(rt))
                    : () => IntervalOf(rt);
                await WaitAsync(rt, begin, due, stop);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // Stopped.
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Sampling loop of provider '{rt.Descriptor.Id}' ended unexpectedly", ex);
        }
    }

    private bool TryStart(ProviderRuntime rt, int generation, out string? error)
    {
        var instance = EnsureInstance(rt, out error);
        if (instance == null)
        {
            error ??= "was stopped while starting";
            return false;
        }

        if (CacheCatalog(rt, instance, out error) == null)
            return false;

        try
        {
            instance.Start(new RuntimeSink(this, rt, generation));
        }
        catch (Exception ex)
        {
            error = $"failed to start: {ex.GetBaseException().Message}";
            UrDeckLog.Error($"Provider '{rt.Descriptor.Id}' {error}", ex);
            lock (_gate)
            {
                if (ReferenceEquals(rt.Instance, instance))
                    rt.Instance = null;
            }

            StopInstance(rt.Descriptor.Id, instance);
            return false;
        }

        lock (_gate)
        {
            if (rt.Generation != generation)
            {
                error = "was stopped while starting";
                return false;
            }

            rt.Started = true;
            rt.DemandDirty = true;
        }

        UrDeckLog.Info($"Provider '{rt.Descriptor.Id}' started ({rt.IntervalMs} ms)");
        return true;
    }

    private async Task<bool> SampleOnceAsync(ProviderRuntime rt, int generation, CancellationToken stop)
    {
        IDataProvider provider;
        TimeSpan timeout;
        lock (_gate)
        {
            if (rt.Generation != generation || rt.Instance == null)
                return true;
            provider = rt.Instance;
            rt.Buffer.Clear();
            rt.InSample = true;
            timeout = Max(MinSampleTimeout, TimeSpan.FromMilliseconds(5.0 * rt.IntervalMs));
        }

        using var sampleCts = CancellationTokenSource.CreateLinkedTokenSource(stop);
        using var timeoutCts = new CancellationTokenSource();

        // On its own pool thread: a provider that blocks inside SampleAsync must not take the loop (and the timeout) with it.
        var sample = Task.Run(() => provider.SampleAsync(sampleCts.Token), CancellationToken.None);
        var timeoutTask = Task.Delay(timeout, _time, timeoutCts.Token);
        var first = await Task.WhenAny(sample, timeoutTask);

        if (first == sample)
        {
            await timeoutCts.CancelAsync();
            try
            {
                await sample;
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                DiscardSample(rt, generation);
                return true;
            }
            catch (Exception ex)
            {
                FailProvider(rt, generation, $"sample failed: {ex.GetBaseException().Message}", ex);
                return false;
            }

            CommitSample(rt, generation);
            return true;
        }

        await sampleCts.CancelAsync();
        FailProvider(rt, generation, $"sample did not finish within {timeout.TotalSeconds:0.#} s", null);

        // Park on the sample: a second one is never started while this one is still running.
        try
        {
            await sample.WaitAsync(stop);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // It ended in an error after the timeout; the failure is already recorded.
        }

        return false;
    }

    private async Task WaitAsync(ProviderRuntime rt, long from, Func<TimeSpan> due, CancellationToken stop)
    {
        while (true)
        {
            var remaining = due() - _time.GetElapsedTime(from);
            if (remaining <= TimeSpan.Zero)
                return;

            Task signal;
            lock (_gate)
                signal = rt.Signal.Task;
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(stop);
            var delay = Task.Delay(remaining, _time, waitCts.Token);
            var done = await Task.WhenAny(delay, signal);
            await waitCts.CancelAsync();
            stop.ThrowIfCancellationRequested();
            if (done == delay)
                return;

            // The interval changed: wait again against the new one.
            lock (_gate)
            {
                if (rt.Signal.Task.IsCompleted)
                    rt.Signal = ProviderRuntime.NewSignal();
            }
        }
    }

    private TimeSpan IntervalOf(ProviderRuntime rt)
    {
        lock (_gate)
            return TimeSpan.FromMilliseconds(rt.IntervalMs);
    }

    /// <summary>The interval doubled for each failure after the first, up to a minute.</summary>
    private TimeSpan Backoff(ProviderRuntime rt)
    {
        lock (_gate)
        {
            double ms = rt.IntervalMs * Math.Pow(2, Math.Max(1, rt.Failures) - 1);
            return Min(TimeSpan.FromMilliseconds(ms), MaxBackoff);
        }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private void DiscardSample(ProviderRuntime rt, int generation)
    {
        lock (_gate)
        {
            if (rt.Generation != generation)
                return;
            rt.Buffer.Clear();
            rt.InSample = false;
        }
    }

    private void CommitSample(ProviderRuntime rt, int generation)
    {
        var changed = new List<string>();
        lock (_gate)
        {
            if (rt.Generation != generation)
                return;
            var items = rt.Buffer.ToArray();
            rt.Buffer.Clear();
            rt.InSample = false;
            CommitLocked(rt, items, isSample: true, changed);
            if (rt.Failures > 0)
            {
                rt.Failures = 0;
                UrDeckLog.Info($"Provider '{rt.Descriptor.Id}' recovered");
            }
        }

        Raise(changed);
    }

    private void FailProvider(ProviderRuntime rt, int generation, string message, Exception? ex)
    {
        var changed = new List<string>();
        lock (_gate)
        {
            if (rt.Generation != generation)
                return;
            rt.Buffer.Clear();
            rt.InSample = false;
            rt.Failures++;

            string providerId = rt.Descriptor.Id;
            foreach (string path in rt.Wanted)
            {
                string canonical = path;
                if (rt.Catalog != null)
                {
                    if (!rt.Catalog.TryGetValue(path, out var entry))
                        continue;
                    canonical = entry.Path;
                }

                string id = $"{providerId}:{canonical}";
                bool had = _store.TryGetValue(id, out var current);
                Reading next = !had ? Reading.Unavailable($"provider '{providerId}' {message}")
                    : current.State == ReadingState.Unavailable ? current
                    : current.Value is { } last ? Reading.Stale(last)
                    : Reading.Unavailable($"provider '{providerId}' {message}");
                Set(id, next, changed);
            }
        }

        if (ex == null)
            UrDeckLog.Warn($"Provider '{rt.Descriptor.Id}' {message}");
        else
            UrDeckLog.Error($"Provider '{rt.Descriptor.Id}' {message}", ex);
        Raise(changed);
    }

    private void PublishFromProvider(ProviderRuntime rt, int generation, string path, Publication publication)
    {
        if (string.IsNullOrEmpty(path))
            return;
        var changed = new List<string>();
        lock (_gate)
        {
            if (_disposed || rt.Generation != generation)
                return;
            if (rt.InSample)
            {
                rt.Buffer[path] = publication;
                return;
            }

            CommitLocked(rt, [new KeyValuePair<string, Publication>(path, publication)], isSample: false, changed);
        }

        Raise(changed);
    }

    /// <summary>Under the gate: stores what a provider published and, after a sample, counts who was left out.</summary>
    private void CommitLocked(ProviderRuntime rt, IReadOnlyCollection<KeyValuePair<string, Publication>> items, bool isSample, List<string> changed)
    {
        string providerId = rt.Descriptor.Id;
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, publication) in items)
        {
            if (rt.Catalog == null || !rt.Catalog.TryGetValue(path, out var entry))
            {
                if (_loggedBad.Add($"{providerId}:{path}"))
                    UrDeckLog.Warn($"Provider '{providerId}' published '{path}', which is not in its catalog; ignored");
                continue;
            }

            reported.Add(entry.Path);
            rt.Misses[entry.Path] = 0;
            string id = $"{providerId}:{entry.Path}";
            Set(id, publication.Value is { } value ? Reading.Ok(value) : Reading.Unavailable(publication.Reason ?? "unavailable"), changed);
        }

        // A sample that published nothing is a pushed provider's idle tick, not a skipped reading.
        if (!isSample || reported.Count == 0 || rt.Catalog == null)
            return;
        foreach (string path in rt.Wanted)
        {
            if (!rt.Catalog.TryGetValue(path, out var entry) || reported.Contains(entry.Path))
                continue;
            int misses = rt.Misses.GetValueOrDefault(entry.Path) + 1;
            rt.Misses[entry.Path] = misses;
            string id = $"{providerId}:{entry.Path}";
            if (misses >= StaleAfterMisses && _store.TryGetValue(id, out var current)
                && current.State is ReadingState.Ok or ReadingState.Pending && current.Value is { } last)
                Set(id, Reading.Stale(last), changed);
        }
    }

    private sealed class RuntimeSink(ReadingHub hub, ProviderRuntime runtime, int generation) : IReadingSink
    {
        public void Publish(string path, ReadingValue value) =>
            hub.PublishFromProvider(runtime, generation, path, new Publication(value, null));

        public void Unavailable(string path, string reason) =>
            hub.PublishFromProvider(runtime, generation, path, new Publication(null, reason));

        public void Log(string message) =>
            UrDeckLog.Info($"Provider '{runtime.Descriptor.Id}': {message}");
    }
}
