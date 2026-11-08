// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>Which readings the editor's preview shows.</summary>
public enum PreviewReadingMode
{
    /// <summary>The deck's own readings; a provider that cannot start for a missing setting shows its sample.</summary>
    Live,

    /// <summary>The providers' sample files; a provider without one shows its live readings.</summary>
    Sample,
}

/// <summary>
/// The readings of the editor's preview: the host's hub (live) and a second hub of its own whose providers are the samples
/// the loaded providers carry, as <c>--demo</c> builds them. In <see cref="PreviewReadingMode.Live"/> a reading comes from the
/// host's hub unless its provider is waiting for a required setting, when its sample is shown, so the preview is never a page
/// of dashes; in <see cref="PreviewReadingMode.Sample"/> every provider with a sample shows it. The deck is never affected:
/// the preview subscribes to the host's hub as another user of the same readings. Thread-safe as the hubs are.
/// </summary>
public sealed class PreviewReadings : IReadingSource, Deck.IDeckReadings, IDisposable
{
    private readonly ReadingHub _live;
    private readonly Func<IReadOnlyCollection<ProviderDescriptor>> _providers;
    private readonly Func<ProviderDescriptor, SampleFile?> _findSample;
    private readonly ProviderRegistry _sampleProviders = new();
    private readonly ReadingHub _sample;
    private readonly Func<DateTime> _now;
    private readonly DateTime _start;
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _subscribed = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _sampled = new(StringComparer.OrdinalIgnoreCase);
    private volatile PreviewReadingMode _mode;
    private bool _disposed;

    /// <param name="live">The host's hub.</param>
    /// <param name="providers">The loaded providers, read again by <see cref="ReloadSamples"/>.</param>
    /// <param name="findSample">The sample a provider carries; by default its assembly's (<see cref="ProviderSamples.Find"/>).</param>
    /// <param name="time">The clock of the sample hub's loop.</param>
    /// <param name="now">The clock the samples are read at; by default the local time.</param>
    /// <param name="sampling">How the sample hub samples: its own loop, or only when told (tests).</param>
    public PreviewReadings(
        ReadingHub live,
        Func<IReadOnlyCollection<ProviderDescriptor>> providers,
        Func<ProviderDescriptor, SampleFile?>? findSample = null,
        TimeProvider? time = null,
        Func<DateTime>? now = null,
        ReadingHubSampling sampling = ReadingHubSampling.Loop)
    {
        _live = live;
        _providers = providers;
        _findSample = findSample ?? ProviderSamples.Find;
        _now = now ?? (() => DateTime.Now);
        _start = _now();
        _sample = new ReadingHub(_sampleProviders, time, live.RegionUsesFahrenheit, live.RegionUses24HourClock, sampling);
        RegisterSamples();
        _live.ReadingsChanged += Forward;
        _sample.ReadingsChanged += Forward;
    }

    public event Action<IReadOnlyCollection<string>>? ReadingsChanged;

    /// <summary>Live or sample readings; a change tells every subscriber that all its readings changed.</summary>
    public PreviewReadingMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
                return;
            _mode = value;
            string[] ids;
            lock (_gate)
                ids = [.. _subscribed.Keys];
            if (ids.Length > 0)
                ReadingsChanged?.Invoke(ids);
        }
    }

    public bool RegionUsesFahrenheit => _live.RegionUsesFahrenheit;

    public bool RegionUses24HourClock => _live.RegionUses24HourClock;

    /// <summary>Whether the provider carries a sample the preview can show.</summary>
    public bool HasSample(string providerId)
    {
        lock (_gate)
            return _sampled.Contains(providerId);
    }

    public Reading Read(string id) => SourceFor(id).Read(id);

    public ReadingDescriptor? Describe(string id) => SourceFor(id).Describe(id) ?? _live.Describe(id);

    private ReadingHub SourceFor(string id)
    {
        if (!ReadingId.TryParse(id, out string provider, out _, out _) || !HasSample(provider))
            return _live;
        return _mode == PreviewReadingMode.Sample || _live.IsWaitingForSettings(provider) ? _sample : _live;
    }

    /// <summary>Counts a user of each reading in the host's hub, and in the sample hub when its provider has a sample.</summary>
    public void Subscribe(IEnumerable<string> ids)
    {
        var list = ids.ToList();
        List<string> sampled;
        lock (_gate)
        {
            if (_disposed)
                return;
            foreach (string id in list)
                _subscribed[id] = _subscribed.GetValueOrDefault(id) + 1;
            sampled = list.Where(Sampled).ToList();
        }

        _live.Subscribe(list);
        _sample.Subscribe(sampled);
    }

    public void Unsubscribe(IEnumerable<string> ids)
    {
        var list = new List<string>();
        List<string> sampled;
        lock (_gate)
        {
            if (_disposed)
                return;
            foreach (string id in ids)
            {
                if (!_subscribed.TryGetValue(id, out int count))
                    continue;
                if (count > 1)
                    _subscribed[id] = count - 1;
                else
                    _subscribed.Remove(id);
                list.Add(id);
            }

            sampled = list.Where(Sampled).ToList();
        }

        _live.Unsubscribe(list);
        _sample.Unsubscribe(sampled);
    }

    /// <summary>
    /// The plugins were reloaded: the sample hub drops its providers (they hold plugin types) and takes the samples of the new
    /// ones; what the preview has subscribed is subscribed again.
    /// </summary>
    public void ReloadSamples()
    {
        _sample.ReleaseAll();
        List<string> again;
        lock (_gate)
        {
            if (_disposed)
                return;
            _sampleProviders.Clear();
            RegisterSamplesUnderGate();
            again = [];
            foreach (var (id, count) in _subscribed)
            {
                if (Sampled(id))
                    again.AddRange(Enumerable.Repeat(id, count));
            }
        }

        _sample.Subscribe(again);
    }

    /// <summary>Samples the sample hub once (its manual mode).</summary>
    internal void SampleNow() => _sample.SampleNow();

    private void RegisterSamples()
    {
        lock (_gate)
            RegisterSamplesUnderGate();
    }

    private void RegisterSamplesUnderGate()
    {
        var sampled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in _providers())
        {
            if (_findSample(provider) is not { } file)
                continue;
            if (_sampleProviders.TryRegister(ProviderSamples.Replace(provider, file, _now, _start), out _))
                sampled.Add(provider.Id);
        }

        _sampled = sampled;
    }

    private bool Sampled(string id) => ReadingId.TryParse(id, out string provider, out _, out _) && _sampled.Contains(provider);

    private void Forward(IReadOnlyCollection<string> ids) => ReadingsChanged?.Invoke(ids);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        _live.ReadingsChanged -= Forward;
        _sample.ReadingsChanged -= Forward;
        string[] ids;
        lock (_gate)
            ids = [.. _subscribed.SelectMany(p => Enumerable.Repeat(p.Key, p.Value))];
        _live.Unsubscribe(ids);
        _sample.Dispose();
    }
}
