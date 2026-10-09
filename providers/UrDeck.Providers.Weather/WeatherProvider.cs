// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Weather;

/// <summary>
/// Weather for any number of places, from Open-Meteo. The place is the first segment of the reading id
/// (<c>weather:Portland, Oregon/current/temperature</c>), so the provider needs no settings of its own. A sample fetches
/// every wanted place in one request; a place that was not wanted before is fetched at once, outside the sample, so a
/// widget whose location was just edited shows values in seconds. Nothing is held while no reading is shown.
/// </summary>
[DataProvider("weather", "Weather", DefaultIntervalMs = 900000, MinIntervalMs = 300000)]
public sealed class WeatherProvider : IDataProvider, IDisposable
{
    /// <summary>A place fetched this recently is not fetched again, so a sample right after a new place's fetch makes no second request.</summary>
    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(30);

    /// <summary>For each wanted place, the readings wanted (by their path below the place) and the full path each was asked for as.</summary>
    private sealed class Demand(Dictionary<LocationKey, Dictionary<string, string>> places)
    {
        public Dictionary<LocationKey, Dictionary<string, string>> Places { get; } = places;
    }

    private static readonly HashSet<string> KnownReadings = new(
        WeatherCatalog.Describe().Select(d => d.Path["{location}/".Length..]),
        StringComparer.OrdinalIgnoreCase);

    private readonly Func<IWeatherHttp> _httpFactory;
    private readonly TimeProvider _time;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _fetching = new(1, 1);
    private readonly Dictionary<LocationKey, Place?> _places = [];
    private readonly Dictionary<LocationKey, (PlaceWeather Weather, long At)> _data = [];
    private readonly HashSet<LocationKey> _queue = [];

    private IReadingSink? _sink;
    private IWeatherHttp? _http;
    private GeocodingClient? _geocoding;
    private ForecastClient? _forecast;
    private CancellationTokenSource? _stop;
    private Task? _newPlaces;
    private bool _newPlacesRunning;
    private Demand _demand = new([]);
    private bool _failing;

    public WeatherProvider()
        : this(() => new WeatherHttp(), TimeProvider.System)
    {
    }

    internal WeatherProvider(Func<IWeatherHttp> httpFactory, TimeProvider time)
    {
        _httpFactory = httpFactory;
        _time = time;
    }

    public IReadOnlyList<ReadingDescriptor> Describe() => WeatherCatalog.Describe();

    public void Start(IReadingSink sink)
    {
        _sink = sink;
        _http = _httpFactory();
        _geocoding = new GeocodingClient(_http);
        _forecast = new ForecastClient(_http);
        _stop = new CancellationTokenSource();
    }

    public void SetDemand(IReadOnlyCollection<string> paths)
    {
        var next = new Dictionary<LocationKey, Dictionary<string, string>>();
        foreach (string path in paths)
        {
            if (!LocationKey.TrySplit(path, out string segment, out string reading)
                || !KnownReadings.Contains(reading)
                || !LocationKey.TryParse(segment, out LocationKey? key))
                continue;

            if (!next.TryGetValue(key!, out var readings))
                next[key!] = readings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            readings[reading] = path;
        }

        var toFetch = new List<LocationKey>();
        var toPublish = new List<(LocationKey Key, Dictionary<string, string> Readings, Place? Place, PlaceWeather? Weather)>();
        lock (_lock)
        {
            Demand before = _demand;
            _demand = new Demand(next);
            foreach (LocationKey gone in before.Places.Keys.Where(k => !next.ContainsKey(k)))
                _data.Remove(gone);

            foreach ((LocationKey key, Dictionary<string, string> readings) in next)
            {
                // Readings that were not wanted before are answered from what is already known, without a request.
                var added = before.Places.TryGetValue(key, out var had)
                    ? readings.Where(r => !had.ContainsKey(r.Key)).ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase)
                    : readings;
                bool known = _places.TryGetValue(key, out Place? place);
                if (known && place == null)
                    toPublish.Add((key, added, null, null));
                else if (_data.TryGetValue(key, out var data))
                    toPublish.Add((key, added, place, data.Weather));
                else
                    toFetch.Add(key);
            }
        }

        foreach ((LocationKey key, Dictionary<string, string> readings, Place? place, PlaceWeather? weather) in toPublish)
        {
            if (readings.Count > 0)
                Publish(key, readings, place, weather);
        }

        if (toFetch.Count > 0)
            StartFetchingNewPlaces(toFetch);
    }

    public async Task SampleAsync(CancellationToken cancellationToken)
    {
        LocationKey[] keys;
        lock (_lock)
            keys = [.. _demand.Places.Keys];
        if (keys.Length == 0)
            return;

        await _fetching.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RefreshAsync(keys, cancellationToken).ConfigureAwait(false);
            Succeeded();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Failed(ex);
            throw;
        }
        finally
        {
            _fetching.Release();
        }
    }

    /// <summary>The engine calls <see cref="Shutdown"/>; disposing does the same and also frees the lock.</summary>
    public void Dispose()
    {
        Shutdown();
        _fetching.Dispose();
    }

    public void Shutdown()
    {
        CancellationTokenSource? stop = _stop;
        Task? running;
        lock (_lock)
        {
            _queue.Clear();
            running = _newPlaces;
        }

        stop?.Cancel();
        try
        {
            running?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
            // The fetch ended by cancellation or by its own failure, which it already reported.
        }

        (_http as IDisposable)?.Dispose();
        _http = null;
        stop?.Dispose();
        _stop = null;
    }

    /// <summary>Starts, unless one runs already, the fetch of places that have no data yet; places queued meanwhile join it.</summary>
    private void StartFetchingNewPlaces(IEnumerable<LocationKey> keys)
    {
        lock (_lock)
        {
            if (_stop is not { IsCancellationRequested: false })
                return;
            _queue.UnionWith(keys);
            if (_newPlacesRunning)
                return;
            _newPlacesRunning = true;
            _newPlaces = Task.Run(() => FetchNewPlacesAsync(_stop.Token));
        }
    }

    private async Task FetchNewPlacesAsync(CancellationToken stop)
    {
        try
        {
            while (true)
            {
                LocationKey[] batch;
                lock (_lock)
                {
                    if (_queue.Count == 0 || stop.IsCancellationRequested)
                    {
                        _newPlacesRunning = false;
                        return;
                    }

                    batch = [.. _queue];
                    _queue.Clear();
                }

                await _fetching.WaitAsync(stop).ConfigureAwait(false);
                try
                {
                    // A sample may have fetched them while this waited, and a widget may have dropped them.
                    lock (_lock)
                        batch = [.. batch.Where(k => _demand.Places.ContainsKey(k) && !_data.ContainsKey(k) && !(_places.TryGetValue(k, out Place? p) && p == null))];
                    if (batch.Length == 0)
                        continue;

                    try
                    {
                        await RefreshAsync(batch, stop).ConfigureAwait(false);
                        Succeeded();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !stop.IsCancellationRequested)
                    {
                        Failed(ex);
                        PublishUnavailable(batch, $"could not reach Open-Meteo: {ex.GetBaseException().Message}");
                    }
                }
                finally
                {
                    _fetching.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
        catch (Exception ex)
        {
            _sink?.Log($"fetching new places ended unexpectedly: {ex.GetBaseException().Message}");
        }
        finally
        {
            lock (_lock)
                _newPlacesRunning = false;
        }
    }

    /// <summary>Resolves the places, fetches those without fresh data in one request, and publishes every wanted reading of all of them. Caller holds the fetch lock.</summary>
    private async Task RefreshAsync(LocationKey[] keys, CancellationToken cancellationToken)
    {
        var due = new List<(LocationKey Key, Place Place)>();
        foreach (LocationKey key in keys)
        {
            Place? place = await ResolveAsync(key, cancellationToken).ConfigureAwait(false);
            if (place == null)
                continue;
            if (!IsFresh(key))
                due.Add((key, place));
        }

        if (due.Count > 0)
        {
            PlaceWeather[] weather = await _forecast!.FetchAsync([.. due.Select(d => d.Place)], cancellationToken).ConfigureAwait(false);
            long at = _time.GetTimestamp();
            lock (_lock)
            {
                for (int i = 0; i < due.Count; i++)
                    _data[due[i].Key] = (weather[i], at);
            }
        }

        foreach (LocationKey key in keys)
        {
            Dictionary<string, string>? readings;
            Place? place;
            PlaceWeather? weather = null;
            lock (_lock)
            {
                if (!_demand.Places.TryGetValue(key, out readings))
                    continue;
                _places.TryGetValue(key, out place);
                if (_data.TryGetValue(key, out var data))
                    weather = data.Weather;
            }

            Publish(key, readings, place, weather);
        }
    }

    private bool IsFresh(LocationKey key)
    {
        lock (_lock)
            return _data.TryGetValue(key, out var data) && _time.GetElapsedTime(data.At) < FreshFor;
    }

    /// <summary>The place a location names, found once per session; null when nothing matches (also remembered).</summary>
    private async Task<Place?> ResolveAsync(LocationKey key, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_places.TryGetValue(key, out Place? known))
                return known;
        }

        Place? place;
        if (key.IsPoint)
        {
            place = new Place(key.Text, key.Latitude!.Value, key.Longitude!.Value);
        }
        else
        {
            IReadOnlyList<Place> matches = await _geocoding!.SearchAsync(key.Text, cancellationToken).ConfigureAwait(false);
            place = matches.Count > 0 ? matches[0] : null;
            if (place == null)
                _sink?.Log($"no place matches '{key.Text}'");
            else if (matches.Count > 1)
                _sink?.Log($"'{key.Text}' matches {string.Join("; ", matches.Select(m => m.Name))}; using {place.Name}");
            else
                _sink?.Log($"'{key.Text}' is {place.Name}");
        }

        lock (_lock)
            _places[key] = place;
        return place;
    }

    private void Publish(LocationKey key, Dictionary<string, string> readings, Place? place, PlaceWeather? weather)
    {
        IReadingSink? sink = _sink;
        if (sink == null)
            return;

        foreach ((string reading, string path) in readings)
        {
            if (place == null)
            {
                sink.Unavailable(path, $"no place matches '{key.Text}'");
                continue;
            }

            ReadingValue? value = Value(reading, place, weather, out string? reason);
            if (value is { } v)
                sink.Publish(path, v);
            else
                sink.Unavailable(path, reason ?? "no value");
        }
    }

    private void PublishUnavailable(IEnumerable<LocationKey> keys, string reason)
    {
        IReadingSink? sink = _sink;
        Demand demand;
        lock (_lock)
            demand = _demand;
        foreach (LocationKey key in keys)
        {
            if (!demand.Places.TryGetValue(key, out var readings))
                continue;
            foreach (string path in readings.Values)
                sink?.Unavailable(path, reason);
        }
    }

    private static ReadingValue? Value(string reading, Place place, PlaceWeather? weather, out string? reason)
    {
        reason = null;
        if (string.Equals(reading, WeatherCatalog.Place, StringComparison.OrdinalIgnoreCase))
            return ReadingValue.FromText(place.Name);
        if (weather == null)
        {
            reason = "no forecast yet";
            return null;
        }

        ReadingValue? value = reading.ToLowerInvariant() switch
        {
            WeatherCatalog.Temperature => Number(weather.Temperature),
            WeatherCatalog.Apparent => Number(weather.Apparent),
            WeatherCatalog.Condition => weather.WeatherCode is { } code ? ReadingValue.FromText(ConditionMap.Classify(code)) : NoValue,
            WeatherCatalog.IsDay => weather.IsDay is { } day ? ReadingValue.FromOnOff(day) : NoValue,
            WeatherCatalog.High => Number(weather.High),
            WeatherCatalog.Low => Number(weather.Low),
            WeatherCatalog.Sunrise => Instant(weather.Sunrise, weather),
            WeatherCatalog.Sunset => Instant(weather.Sunset, weather),
            _ => NoValue,
        };
        if (value == null)
        {
            reason = reading.StartsWith("today/sun", StringComparison.OrdinalIgnoreCase)
                ? "the sun does not rise or set at this place today"
                : "the source gave no value";
        }

        return value;
    }

    // A bare null would convert through the string operator of ReadingValue and become an empty text.
    private static ReadingValue? NoValue => null;

    private static ReadingValue? Number(double? number) => number.HasValue ? ReadingValue.FromNumber(number.Value) : NoValue;

    private static ReadingValue? Instant(long? unixSeconds, PlaceWeather weather) =>
        unixSeconds.HasValue ? ReadingValue.FromTime(DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).ToOffset(weather.UtcOffset)) : NoValue;

    /// <summary>Only the first failure after a success is logged, not every attempt.</summary>
    private void Failed(Exception ex)
    {
        if (_failing)
            return;
        _failing = true;
        _sink?.Log($"request failed: {ex.GetBaseException().Message}");
    }

    private void Succeeded()
    {
        if (!_failing)
            return;
        _failing = false;
        _sink?.Log("requests succeed again");
    }
}
