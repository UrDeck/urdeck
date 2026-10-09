// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections.Concurrent;
using System.Globalization;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>
/// Owns the data providers and the latest value of every reading. Widgets read it through <see cref="IReadingSource"/>
/// (lock-free, never blocking); the host subscribes the readings of the widgets it shows. A provider is created when a
/// reading of it is first subscribed to, sampled on the thread pool and stopped after a linger once nothing uses it.
/// </summary>
public sealed partial class ReadingHub : IReadingSource, IDisposable
{
    private static readonly TimeSpan Linger = TimeSpan.FromSeconds(5);

    private readonly ProviderRegistry _providers;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, Reading> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ReadingDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProviderRuntime> _runtimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loggedBad = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _intervals = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public ReadingHub(ProviderRegistry providers, TimeProvider? time = null, bool? regionUsesFahrenheit = null, bool? regionUses24HourClock = null)
    {
        _providers = providers;
        _time = time ?? TimeProvider.System;
        RegionUsesFahrenheit = regionUsesFahrenheit ?? !RegionIsMetric();
        RegionUses24HourClock = regionUses24HourClock
            ?? !CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("tt", StringComparison.Ordinal);
    }

    /// <summary>
    /// Raised on a sampling or pool thread, once per batch, with the ids whose reading (or catalog entry) changed.
    /// Marshalling to the UI thread is the subscriber's job.
    /// </summary>
    public event Action<IReadOnlyCollection<string>>? ReadingsChanged;

    public bool RegionUsesFahrenheit { get; }

    public bool RegionUses24HourClock { get; }

    private static bool RegionIsMetric()
    {
        try
        {
            return RegionInfo.CurrentRegion.IsMetric;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    public Reading Read(string id)
    {
        if (_store.TryGetValue(id, out var reading))
            return reading;
        return ReadingId.TryParse(id, out _, out _, out string? error)
            ? Reading.Unavailable("not subscribed")
            : Reading.Unavailable(error!);
    }

    public ReadingDescriptor? Describe(string id) =>
        _descriptors.TryGetValue(id, out var descriptor) ? descriptor : null;

    /// <summary>Whether any of the readings is pending (used by <c>--snapshot</c> to know when to paint).</summary>
    public bool AnyPending(IEnumerable<string> ids) =>
        ids.Any(id => _store.TryGetValue(id, out var r) && r.State == ReadingState.Pending);

    /// <summary>
    /// The catalog of a registered provider, creating the instance if needed but not starting it or sampling. Null when
    /// the provider is unknown or cannot describe itself.
    /// </summary>
    public IReadOnlyList<ReadingDescriptor>? GetCatalog(string providerId)
    {
        ProviderRuntime rt;
        lock (_gate)
        {
            if (_disposed)
                return null;
            var descriptor = _providers.Get(providerId);
            if (descriptor == null)
                return null;
            rt = GetOrCreateRuntime(descriptor);
            if (rt.Catalog != null)
                return Listed(rt);
        }

        var instance = EnsureInstance(rt, out _);
        if (instance == null || !CacheCatalog(rt, instance, out _))
            return null;
        lock (_gate)
            return rt.Catalog == null ? null : Listed(rt);
    }

    /// <summary>The exact entries and then the patterns as the provider wrote them; under the gate.</summary>
    private static List<ReadingDescriptor> Listed(ProviderRuntime rt)
    {
        var list = rt.Catalog!.Values.ToList();
        list.AddRange(rt.Patterns.Select(p => p.Descriptor));
        return list;
    }

    /// <summary>Counts one more subscriber for each id; the first subscriber of an id makes its reading pending.</summary>
    public void Subscribe(IEnumerable<string> ids)
    {
        var changed = new List<string>();
        lock (_gate)
        {
            if (_disposed)
                return;
            foreach (string id in ids)
            {
                _counts.TryGetValue(id, out int count);
                _counts[id] = count + 1;
                if (count == 0)
                    SubscribeFirst(id, changed);
            }
        }

        Raise(changed);
    }

    /// <summary>Counts one subscriber less for each id; an id nobody is subscribed to is ignored.</summary>
    public void Unsubscribe(IEnumerable<string> ids)
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            foreach (string id in ids)
            {
                if (!_counts.TryGetValue(id, out int count))
                    continue;
                if (count > 1)
                {
                    _counts[id] = count - 1;
                    continue;
                }

                _counts.Remove(id);
                if (!ReadingId.TryParse(id, out string providerId, out string path, out _) || !_runtimes.TryGetValue(providerId, out var rt))
                    continue;
                rt.Wanted.Remove(path);
                if (rt.Wanted.Count == 0)
                    StartLinger(rt);
                else
                    MarkDemandDirty(rt);
            }
        }
    }

    /// <summary>Applies the <c>providers</c> section of the configuration; running providers change rate at once.</summary>
    public void ApplySettings(IReadOnlyDictionary<string, ProviderSettings>? settings)
    {
        var intervals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (settings != null)
        {
            foreach (var (providerId, setting) in settings)
            {
                if (setting.IntervalMs is { } ms)
                    intervals[providerId] = ms;
            }
        }

        lock (_gate)
        {
            _intervals = intervals;
            foreach (var rt in _runtimes.Values)
            {
                int interval = ComputeInterval(rt);
                if (interval == rt.IntervalMs)
                    continue;
                rt.IntervalMs = interval;
                if (rt.Loop != null)
                    UrDeckLog.Info($"Provider '{rt.Descriptor.Id}' is now sampled every {interval} ms");
                rt.Signal.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Stops and releases every provider without the linger and drops all values, catalogs and subscriptions. Called
    /// before plugins are unloaded; the host resubscribes when it rebuilds the page.
    /// </summary>
    public void ReleaseAll()
    {
        var stops = new List<Action>();
        lock (_gate)
        {
            foreach (var rt in _runtimes.Values)
                stops.Add(Detach(rt));
            _runtimes.Clear();
            _store.Clear();
            _descriptors.Clear();
            _counts.Clear();
            _loggedBad.Clear();
        }

        foreach (var stop in stops)
            stop();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        ReleaseAll();
    }

    // The members below run under the gate.

    private void SubscribeFirst(string id, List<string> changed)
    {
        if (!ReadingId.TryParse(id, out string providerId, out string path, out string? error))
        {
            SetBad(id, error!, changed);
            return;
        }

        var descriptor = _providers.Get(providerId);
        if (descriptor == null)
        {
            SetBad(id, $"no data provider '{providerId}' is registered", changed);
            return;
        }

        var rt = GetOrCreateRuntime(descriptor);
        rt.Wanted.Add(path);
        rt.LingerTimer?.Dispose();
        rt.LingerTimer = null;

        // A provider still running (lingering) keeps reporting, so its readings keep their state across a page rebuild.
        // Otherwise the last value is shown as pending until the provider reports.
        bool had = _store.TryGetValue(id, out var existing);
        if (!(rt.Loop != null && had && existing.State != ReadingState.Unavailable))
        {
            var pending = Reading.Pending(had && existing.State != ReadingState.Unavailable ? existing.Value : null);
            Set(id, pending, changed);
        }

        if (rt.Catalog != null)
            CheckPath(rt, id, path, changed);
        MarkDemandDirty(rt);
        EnsureLoop(rt);
    }

    private ProviderRuntime GetOrCreateRuntime(ProviderDescriptor descriptor)
    {
        if (!_runtimes.TryGetValue(descriptor.Id, out var rt))
        {
            rt = new ProviderRuntime(descriptor) { IntervalMs = 0 };
            _runtimes[descriptor.Id] = rt;
            rt.IntervalMs = ComputeInterval(rt);
        }

        return rt;
    }

    private int ComputeInterval(ProviderRuntime rt)
    {
        var descriptor = rt.Descriptor;
        if (!_intervals.TryGetValue(descriptor.Id, out int configured))
        {
            rt.LoggedClampedFrom = null;
            return descriptor.DefaultIntervalMs;
        }

        if (configured >= descriptor.MinIntervalMs)
        {
            rt.LoggedClampedFrom = null;
            return configured;
        }

        if (rt.LoggedClampedFrom != configured)
        {
            rt.LoggedClampedFrom = configured;
            UrDeckLog.Warn($"providers.{descriptor.Id}.intervalMs {configured} is below the provider's minimum; using {descriptor.MinIntervalMs} ms");
        }

        return descriptor.MinIntervalMs;
    }

    /// <summary>Marks a subscribed path that the provider's catalog does not contain as unavailable.</summary>
    private void CheckPath(ProviderRuntime rt, string id, string path, List<string> changed)
    {
        if (Resolve(rt, path) != null)
            return;
        SetBad(id, $"provider '{rt.Descriptor.Id}' has no reading '{path}'", changed);
    }

    private void SetBad(string id, string reason, List<string> changed)
    {
        if (_loggedBad.Add(id))
            UrDeckLog.Warn($"Reading {reason}");
        Set(id, Reading.Unavailable(reason), changed, logTransition: false);
    }

    /// <summary>Stores a reading, logs the transition once and notes the id as changed when it differs from the stored one.</summary>
    private void Set(string id, Reading reading, List<string> changed, bool logTransition = true)
    {
        bool had = _store.TryGetValue(id, out var old);
        if (had && old.Equals(reading))
            return;
        _store[id] = reading;
        changed.Add(id);
        if (!logTransition || (had && old.State == reading.State))
            return;

        switch (reading.State)
        {
            case ReadingState.Unavailable:
                UrDeckLog.Warn($"Reading {id} is unavailable: {reading.Reason}");
                break;
            case ReadingState.Stale:
                UrDeckLog.Warn($"Reading {id} is stale");
                break;
            case ReadingState.Ok when had && old.State is ReadingState.Stale or ReadingState.Unavailable:
                UrDeckLog.Info($"Reading {id} is back");
                break;
        }
    }

    private void Raise(List<string> changed)
    {
        if (changed.Count == 0)
            return;
        try
        {
            ReadingsChanged?.Invoke(changed);
        }
        catch (Exception ex)
        {
            UrDeckLog.Error("A ReadingsChanged handler failed", ex);
        }
    }
}
