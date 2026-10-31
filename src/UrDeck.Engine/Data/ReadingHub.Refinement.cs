// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>What <see cref="ReadingHub.BrowseAsync"/> found: the provider's concrete readings, or why there are none.</summary>
/// <param name="Readings">One description per reading, without values; null when the provider cannot list them.</param>
/// <param name="Message">Why there is no list; null when there is one.</param>
public sealed record ReadingListing(IReadOnlyList<ReadingDescriptor>? Readings, string? Message)
{
    /// <summary>Whether the provider listed its readings.</summary>
    public bool Succeeded => Readings != null;

    internal static ReadingListing Failed(string message) => new(null, message);
}

// What a provider that publishes from its own callbacks tells the hub beyond values: that it lost its source (an interruption),
// what a concrete reading under one of its patterns turned out to be (a refined description), and the list of its concrete
// readings (browsing). See the data-providers spec, "Interruptions", "Descriptions Refined While Running" and "Listing Concrete
// Readings".
public sealed partial class ReadingHub
{
    /// <summary>
    /// Lists the concrete readings of a provider that can enumerate them (<see cref="IReadingBrowser"/>). The provider is started
    /// for it if it is not running, as for an action, and lingers afterwards. Never throws: a provider that is unknown, cannot list,
    /// cannot start or whose listing fails gives a <see cref="ReadingListing"/> with a message and no list. While the provider runs,
    /// the descriptions listed are what <see cref="Describe"/> returns for their ids.
    /// </summary>
    public async Task<ReadingListing> BrowseAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ProviderRuntime rt;
        Task<string?>? ready = null;
        lock (_gate)
        {
            if (_disposed)
                return ReadingListing.Failed("the host is shutting down");
            var descriptor = _providers.Get(providerId);
            if (descriptor == null)
                return ReadingListing.Failed($"no data provider '{providerId}' is registered");
            rt = GetOrCreateRuntime(descriptor);
            if (!(rt.Instance != null ? rt.Instance is IReadingBrowser : descriptor.OffersBrowsing))
                return ReadingListing.Failed($"provider '{providerId}' cannot list its readings");
            if (!rt.Started)
            {
                ready = (rt.StartedSignal ??= ProviderRuntime.NewStartSignal()).Task;
                EnsureLoop(rt);
                TouchLinger(rt);
            }
        }

        var (limit, _) = ActionLimits(providerId);
        if (ready != null)
        {
            if (_manual)
                StartManual(rt);
            string? why = await WaitForStartAsync(ready, limit, cancellationToken);
            if (why != null)
                return ReadingListing.Failed($"provider '{providerId}': {why}");
        }

        IReadingBrowser? browser;
        int generation;
        lock (_gate)
        {
            browser = rt.Instance as IReadingBrowser;
            generation = rt.Generation;
        }

        if (browser == null)
            return ReadingListing.Failed($"provider '{providerId}' cannot list its readings");

        IReadOnlyList<ReadingDescriptor> listed;
        try
        {
            listed = await Task.Run(() => browser.BrowseAsync(cancellationToken), CancellationToken.None).WaitAsync(limit, _time, cancellationToken);
        }
        catch (TimeoutException)
        {
            return ReadingListing.Failed($"provider '{providerId}' did not list its readings within {limit.TotalSeconds:0.#} s");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ReadingListing.Failed("cancelled");
        }
        catch (Exception ex)
        {
            return ReadingListing.Failed($"provider '{providerId}' could not list its readings: {ex.GetBaseException().Message}");
        }

        var readings = (listed ?? []).Where(d => d != null && !string.IsNullOrEmpty(d.Path)).ToList();
        lock (_gate)
        {
            if (!_disposed && rt.Generation == generation)
            {
                foreach (string old in rt.Listed.Where(p => !rt.Wanted.Contains(p)).ToList())
                    DropRefined(rt, old);
                rt.Listed.Clear();
                foreach (var reading in readings)
                {
                    if (!FitsAPattern(rt, reading.Path))
                        continue;
                    rt.Listed.Add(reading.Path);
                    StoreRefined(rt, reading.Path, reading);
                }

                TouchLinger(rt);
            }
        }

        return new ReadingListing(readings, null);
    }

    private void InterruptFromProvider(ProviderRuntime rt, int generation, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            reason = "interrupted";
        var changed = new List<string>();
        bool first;
        lock (_gate)
        {
            if (_disposed || rt.Generation != generation)
                return;
            first = !rt.Interrupted;
            rt.Interrupted = true;

            // The failed-sample rule: a value is kept as stale, a reading without one says why.
            string providerId = rt.Descriptor.Id;
            foreach (string path in rt.Wanted)
            {
                var entry = Resolve(rt, path);
                if (entry == null)
                    continue;
                string id = $"{providerId}:{entry.Path}";
                bool had = _store.TryGetValue(id, out var current);
                Reading next = !had ? Reading.Unavailable(reason)
                    : current.State == ReadingState.Unavailable ? current
                    : current.Value is { } last ? Reading.Stale(last)
                    : Reading.Unavailable(reason);
                Set(id, next, changed);
            }
        }

        if (first && !_manual)
            UrDeckLog.Warn($"Provider '{rt.Descriptor.Id}' is interrupted: {reason}");
        Raise(changed);
    }

    private void RefineFromProvider(ProviderRuntime rt, int generation, string path, ReadingDescriptor? descriptor)
    {
        if (string.IsNullOrEmpty(path) || descriptor == null)
            return;
        var changed = new List<string>();
        lock (_gate)
        {
            if (_disposed || rt.Generation != generation || rt.Catalog == null)
                return;
            string providerId = rt.Descriptor.Id;
            if (!FitsAPattern(rt, path))
            {
                if (_loggedBad.Add($"{providerId}:describe:{path}"))
                    UrDeckLog.Warn($"Provider '{providerId}' described '{path}', which no pattern of its catalog matches; ignored");
                return;
            }

            // Kept only for what a widget shows or a listing returned, so a provider cannot grow the cache.
            if (!rt.Wanted.Contains(path) && !rt.Listed.Contains(path))
                return;
            if (StoreRefined(rt, path, descriptor))
                changed.Add($"{providerId}:{path}");
        }

        Raise(changed);
    }

    // Under the gate.

    /// <summary>Whether a pattern (not an exact entry) of the provider's catalog matches the path.</summary>
    private static bool FitsAPattern(ProviderRuntime rt, string path)
    {
        if (rt.Catalog == null || rt.Catalog.ContainsKey(path))
            return false;
        string[] segments = path.Split('/');
        return rt.Patterns.Any(p => Fits(p, segments));
    }

    /// <summary>Keeps a refined description; false when it equals the one already kept.</summary>
    private bool StoreRefined(ProviderRuntime rt, string path, ReadingDescriptor descriptor)
    {
        var refined = descriptor with { Path = path };
        if (rt.Refined.TryGetValue(path, out var old) && old == refined)
            return false;
        rt.Refined[path] = refined;
        _descriptors[$"{rt.Descriptor.Id}:{path}"] = refined;
        return true;
    }

    /// <summary>Forgets a refined description: the id is described by its pattern again (or not at all until it is resolved).</summary>
    private void DropRefined(ProviderRuntime rt, string path)
    {
        if (!rt.Refined.Remove(path))
            return;
        string id = $"{rt.Descriptor.Id}:{path}";
        if (rt.Instances.TryGetValue(path, out var made))
            _descriptors[id] = made;
        else
            _descriptors.TryRemove(id, out _);
    }
}
