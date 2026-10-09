// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using UrDeck.Engine.Tests.Support;
using UrDeck.Providers.Weather;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The weather provider against recorded answers of the services: no test here touches the network.</summary>
public sealed class WeatherProviderTests : IDisposable
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "weather", name + ".json"));

    // Locations

    [Theory]
    [InlineData("Zurich", "Zurich")]
    [InlineData("  zurich  ", "zurich")]
    [InlineData("Portland, OR", "Portland, OR")]
    [InlineData("AC%2FDC Street", "AC/DC Street")]
    [InlineData("AC%2fDC", "AC/DC")]
    [InlineData("100%25 sure", "100% sure")]
    [InlineData("%252F", "%2F")]
    [InlineData("50%", "50%")]
    [InlineData("a%zzb", "a%zzb")]
    public void ALocationSegment_IsReadBackAsTheTextTyped(string segment, string expected)
    {
        Assert.True(LocationKey.TryParse(segment, out LocationKey? key));

        Assert.Equal(expected, key!.Text);
        Assert.False(key.IsPoint);
    }

    [Theory]
    [InlineData("Zurich")]
    [InlineData("AC/DC")]
    [InlineData("100% / 50%")]
    [InlineData("%2F")]
    public void EscapingThenParsing_GivesTheTextBack(string text)
    {
        Assert.True(LocationKey.TryParse(LocationKey.Escape(text), out LocationKey? key));

        Assert.Equal(text, key!.Text);
        Assert.DoesNotContain('/', LocationKey.Escape(text));
    }

    [Fact]
    public void Coordinates_AreAPoint_AndNeedNoSearch()
    {
        Assert.True(LocationKey.TryParse("@47.37,8.54", out LocationKey? key));

        Assert.True(key!.IsPoint);
        Assert.Equal(47.37, key.Latitude);
        Assert.Equal(8.54, key.Longitude);
        Assert.Equal("47.37, 8.54", key.Text);
        Assert.True(LocationKey.TryParse("@-45.52,-122.68", out LocationKey? west));
        Assert.Equal(-122.68, west!.Longitude);
    }

    [Theory]
    [InlineData("@abc")]
    [InlineData("@47.37")]
    [InlineData("@47.37,8.54,1")]
    [InlineData("@91,0")]
    [InlineData("@0,181")]
    public void AnAtSignThatIsNotTwoNumbers_IsASearch(string segment)
    {
        Assert.True(LocationKey.TryParse(segment, out LocationKey? key));

        Assert.False(key!.IsPoint);
        Assert.Equal(segment, key.Text);
    }

    private static LocationKey Key(string segment)
    {
        Assert.True(LocationKey.TryParse(segment, out LocationKey? key));
        return key!;
    }

    [Fact]
    public void LocationsDifferingOnlyInCaseOrWhiteSpace_AreTheSameKey()
    {
        LocationKey a = Key("Zurich");
        LocationKey b = Key(" ZURICH ");
        LocationKey c = Key("Tokyo");
        LocationKey p = Key("@1,2");
        LocationKey q = Key("@1.0,2.0");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
        Assert.Equal(p, q);
        Assert.NotEqual(a, p);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyLocation_IsNotAKey(string segment) => Assert.False(LocationKey.TryParse(segment, out _));

    [Fact]
    public void APath_IsSplitAtTheFirstSlashOnly()
    {
        Assert.True(LocationKey.TrySplit("Portland, OR/current/temperature", out string segment, out string reading));
        Assert.Equal("Portland, OR", segment);
        Assert.Equal("current/temperature", reading);
        Assert.True(LocationKey.TrySplit("AC%2FDC/place", out segment, out reading));
        Assert.Equal("AC%2FDC", segment);
        Assert.False(LocationKey.TrySplit("Zurich", out _, out _));
        Assert.False(LocationKey.TrySplit("/place", out _, out _));
        Assert.False(LocationKey.TrySplit("Zurich/", out _, out _));
    }

    // Conditions

    [Theory]
    [InlineData(0, "clear")]
    [InlineData(1, "clear")]
    [InlineData(2, "partly-cloudy")]
    [InlineData(3, "cloudy")]
    [InlineData(45, "fog")]
    [InlineData(48, "fog")]
    [InlineData(51, "drizzle")]
    [InlineData(53, "drizzle")]
    [InlineData(55, "drizzle")]
    [InlineData(56, "sleet")]
    [InlineData(57, "sleet")]
    [InlineData(66, "sleet")]
    [InlineData(67, "sleet")]
    [InlineData(61, "rain")]
    [InlineData(63, "rain")]
    [InlineData(65, "rain")]
    [InlineData(80, "rain")]
    [InlineData(81, "rain")]
    [InlineData(82, "rain")]
    [InlineData(71, "snow")]
    [InlineData(73, "snow")]
    [InlineData(75, "snow")]
    [InlineData(77, "snow")]
    [InlineData(85, "snow")]
    [InlineData(86, "snow")]
    [InlineData(95, "thunder")]
    [InlineData(96, "thunder")]
    [InlineData(99, "thunder")]
    public void AWmoCode_IsOneOfTheConditionClasses(int code, string expected) =>
        Assert.Equal(expected, ConditionMap.Classify(code));

    [Theory]
    [InlineData(4)]
    [InlineData(44)]
    [InlineData(100)]
    [InlineData(-1)]
    public void ACodeNobodyKnows_IsUnknown(int code) => Assert.Equal("unknown", ConditionMap.Classify(code));

    // Parsing

    [Fact]
    public void AForecastOfOnePlace_IsAnObject_AndGivesEveryValue()
    {
        PlaceWeather[] weather = WeatherParser.ParseForecast(Fixture("forecast-one"), 1);

        PlaceWeather portland = Assert.Single(weather);
        Assert.Equal(TimeSpan.FromHours(-7), portland.UtcOffset);
        Assert.Equal(23.0, portland.Temperature);
        Assert.Equal(23.3, portland.Apparent);
        Assert.True(portland.IsDay);
        Assert.Equal(3, portland.WeatherCode);
        Assert.Equal(23.8, portland.High);
        Assert.Equal(12.2, portland.Low);
        Assert.Equal(1791469092, portland.Sunrise);
        Assert.Equal(1791509850, portland.Sunset);
    }

    [Fact]
    public void AForecastOfSeveralPlaces_IsAnArray_InRequestOrder_EachWithItsOwnOffset()
    {
        PlaceWeather[] weather = WeatherParser.ParseForecast(Fixture("forecast-three"), 3);

        Assert.Equal([TimeSpan.FromHours(2), TimeSpan.FromHours(9), TimeSpan.FromHours(2)], weather.Select(w => w.UtcOffset));
        Assert.Equal([11.3, 19.2, 3.3], weather.Select(w => w.Temperature));
        Assert.Equal([false, true, false], weather.Select(w => w.IsDay));
        Assert.Equal([3, 1, 61], weather.Select(w => w.WeatherCode));
    }

    [Fact]
    public void AnArrayOfOnePlace_IsAcceptedToo()
    {
        string array = "[" + Fixture("forecast-one") + "]";

        Assert.Equal(23.0, Assert.Single(WeatherParser.ParseForecast(array, 1)).Temperature);
    }

    [Fact]
    public void ANullSunriseAndSunset_AreNull_NotZero()
    {
        PlaceWeather polar = Assert.Single(WeatherParser.ParseForecast(Fixture("forecast-polar"), 1));

        Assert.Null(polar.Sunrise);
        Assert.Null(polar.Sunset);
        Assert.Equal(5.4, polar.High);
    }

    [Fact]
    public void AMissingBlock_GivesNullValues_NotAFailure()
    {
        PlaceWeather bare = Assert.Single(WeatherParser.ParseForecast("""{"utc_offset_seconds":3600}""", 1));

        Assert.Equal(TimeSpan.FromHours(1), bare.UtcOffset);
        Assert.Null(bare.Temperature);
        Assert.Null(bare.IsDay);
        Assert.Null(bare.WeatherCode);
        Assert.Null(bare.High);
        Assert.Null(bare.Sunrise);
    }

    [Fact]
    public void AnOffsetWithAStraySecond_IsRoundedToWholeMinutes()
    {
        PlaceWeather odd = Assert.Single(WeatherParser.ParseForecast("""{"utc_offset_seconds":19801}""", 1));

        Assert.Equal(TimeSpan.FromMinutes(330), odd.UtcOffset);
    }

    [Fact]
    public void AnErrorAnswer_ATruncatedAnswer_AndTheWrongNumberOfPlaces_Throw()
    {
        Assert.Throws<InvalidDataException>(() => WeatherParser.ParseForecast("""{"error":true,"reason":"Latitude must be in range"}""", 1));
        Assert.ThrowsAny<JsonException>(() => WeatherParser.ParseForecast(Fixture("forecast-one")[..120], 1));
        Assert.Throws<InvalidDataException>(() => WeatherParser.ParseForecast(Fixture("forecast-three"), 2));
        Assert.Throws<InvalidDataException>(() => WeatherParser.ParseForecast("[1]", 1));
    }

    [Fact]
    public void Geocoding_NamesAPlaceByItsNameAndItsFirstLevelArea()
    {
        Place portland = WeatherParser.ParseGeocoding(Fixture("geo-portland-or"))[0];
        Place zurich = WeatherParser.ParseGeocoding(Fixture("geo-zurich"))[0];

        Assert.Equal("Portland, Oregon", portland.Name);
        Assert.Equal(45.52345, portland.Latitude);
        Assert.Equal(-122.67621, portland.Longitude);
        Assert.Equal("Zurich, Canton of Zurich", zurich.Name);
        Assert.Equal("Portland, Oregon", WeatherParser.ParseGeocoding(Fixture("geo-portland-oregon"))[0].Name);
        Assert.Equal("Beverly Hills, California", WeatherParser.ParseGeocoding(Fixture("geo-90210"))[0].Name);
    }

    [Fact]
    public void Geocoding_KeepsTheNameAlone_WhenTheAreaIsTheSame()
    {
        var places = WeatherParser.ParseGeocoding("""{"results":[{"name":"Tokyo","latitude":35.7,"longitude":139.7,"admin1":"Tokyo"},{"name":"Bern","latitude":46.9,"longitude":7.4}]}""");

        Assert.Equal(["Tokyo", "Bern"], places.Select(p => p.Name));
    }

    [Fact]
    public void Geocoding_ListsEveryCandidate_BestFirst_AndNoneWhenNothingMatches()
    {
        Assert.Equal(
            ["Springfield, Missouri", "Springfield, Illinois", "Springfield, Massachusetts", "Springfield, Ohio", "Springfield, Tennessee"],
            WeatherParser.ParseGeocoding(Fixture("geo-springfield")).Select(p => p.Name));
        Assert.Empty(WeatherParser.ParseGeocoding(Fixture("geo-nomatch")));
    }

    // The network seam

    /// <summary>Answers requests from recorded fixtures (or from a script) and remembers every request.</summary>
    private sealed class FakeHttp : IWeatherHttp, IDisposable
    {
        private readonly object _gate = new();
        private readonly List<Uri> _requests = [];

        public Func<Uri, string>? Override { get; set; }

        public Exception? Failure { get; set; }

        public TaskCompletionSource? Gate { get; set; }

        public bool Disposed { get; private set; }

        public IReadOnlyList<Uri> Requests
        {
            get
            {
                lock (_gate)
                    return [.. _requests];
            }
        }

        public int Geocoding => Requests.Count(r => r.Host.StartsWith("geocoding", StringComparison.Ordinal));

        public int Forecasts => Requests.Count(r => r.Host.StartsWith("api.", StringComparison.Ordinal));

        public async Task<string> GetStringAsync(Uri url, CancellationToken cancellationToken)
        {
            lock (_gate)
                _requests.Add(url);
            if (Gate != null)
                await Gate.Task.WaitAsync(cancellationToken);
            if (Failure != null)
                throw Failure;
            return Override?.Invoke(url) ?? Respond(url);
        }

        public void Dispose() => Disposed = true;

        private static string Respond(Uri url)
        {
            string query = url.Query;
            if (url.Host.StartsWith("geocoding", StringComparison.Ordinal))
                return GeocodingAnswer(Uri.UnescapeDataString(Value(query, "name")));

            double[] latitudes = [.. Value(query, "latitude").Split(',').Select(l => double.Parse(l, System.Globalization.CultureInfo.InvariantCulture))];
            JsonArray three = JsonNode.Parse(Fixture("forecast-three"))!.AsArray();
            JsonNode?[] places = latitudes.Select(latitude => JsonNode.Parse(
                latitude < 36 ? three[1]!.ToJsonString()
                : latitude is > 47 and < 48 ? three[0]!.ToJsonString()
                : latitude is > 45 and < 46 ? Fixture("forecast-one")
                : three[2]!.ToJsonString())).ToArray();
            return latitudes.Length == 1 ? places[0]!.ToJsonString() : new JsonArray([.. places]).ToJsonString();
        }

        private static string GeocodingAnswer(string name) => name.ToLowerInvariant() switch
        {
            "portland, or" or "portland, oregon" => Fixture("geo-portland-or"),
            "zurich" => Fixture("geo-zurich"),
            "springfield" => Fixture("geo-springfield"),
            "90210" => Fixture("geo-90210"),
            "tokyo" => """{"results":[{"name":"Tokyo","latitude":35.6895,"longitude":139.69171,"admin1":"Tokyo"}]}""",
            "oslo" => """{"results":[{"name":"Oslo","latitude":59.91273,"longitude":10.74609,"admin1":"Oslo"}]}""",
            _ => Fixture("geo-nomatch"),
        };

        private static string Value(string query, string name) =>
            query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).First(p => p[0] == name)[1];
    }

    private sealed class Sink : IReadingSink
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, ReadingValue> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _unavailable = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _log = [];

        public void Publish(string path, ReadingValue value)
        {
            lock (_gate)
            {
                _values[path] = value;
                _unavailable.Remove(path);
            }
        }

        public void Unavailable(string path, string reason)
        {
            lock (_gate)
            {
                _unavailable[path] = reason;
                _values.Remove(path);
            }
        }

        public void Log(string message)
        {
            lock (_gate)
                _log.Add(message);
        }

        public bool Has(string path)
        {
            lock (_gate)
                return _values.ContainsKey(path);
        }

        public bool IsUnavailable(string path)
        {
            lock (_gate)
                return _unavailable.ContainsKey(path);
        }

        public ReadingValue Value(string path)
        {
            lock (_gate)
                return _values[path];
        }

        public string Reason(string path)
        {
            lock (_gate)
                return _unavailable[path];
        }

        public string[] Lines()
        {
            lock (_gate)
                return [.. _log];
        }
    }

    private readonly FakeTime _time = new();
    private readonly FakeHttp _http = new();
    private readonly Sink _sink = new();
    private readonly ConcurrentBag<WeatherProvider> _providers = [];

    public void Dispose()
    {
        foreach (WeatherProvider provider in _providers)
            provider.Dispose();
    }

    private WeatherProvider Started(params string[] demand)
    {
        var provider = new WeatherProvider(() => _http, _time);
        _providers.Add(provider);
        provider.Start(_sink);
        provider.SetDemand(demand);
        return provider;
    }

    private static void Sample(WeatherProvider provider) => provider.SampleAsync(CancellationToken.None).GetAwaiter().GetResult();

    private static bool Wait(Func<bool> condition) => HubTestExtensions.WaitFor(condition);

    private static string[] AllReadings(string location) =>
        [.. WeatherCatalog.Describe().Select(d => location + "/" + d.Path["{location}/".Length..])];

    // The catalog

    [Fact]
    public void TheCatalog_HasTheNineReadings_AsPatterns_WithTheCreditOnEach()
    {
        IReadOnlyList<ReadingDescriptor> catalog = new WeatherProvider().Describe();

        Assert.Equal(
            [
                "{location}/place", "{location}/current/temperature", "{location}/current/apparent", "{location}/current/condition",
                "{location}/current/is-day", "{location}/today/high", "{location}/today/low", "{location}/today/sunrise", "{location}/today/sunset",
            ],
            catalog.Select(d => d.Path));
        Assert.All(catalog, d =>
        {
            Assert.Equal("{location}", d.Device);
            Assert.Equal("Weather data by Open-Meteo.com", d.Attribution!.Text);
            Assert.Equal("Open-Meteo.com", d.Attribution.ShortText);
            Assert.Equal("https://open-meteo.com/", d.Attribution.Url);
        });
        Assert.All(catalog.Where(d => d.Kind == ReadingKind.Temperature), d => Assert.Null(d.DisplayUnit));
        Assert.Equal(
            [ReadingKind.Text, ReadingKind.Temperature, ReadingKind.Temperature, ReadingKind.Text, ReadingKind.OnOff, ReadingKind.Temperature, ReadingKind.Temperature, ReadingKind.Time, ReadingKind.Time],
            catalog.Select(d => d.Kind));
        Assert.Equal(["Place", "Now", "Feels like", "Condition", "Daytime", "High", "Low", "Sunrise", "Sunset"], catalog.Select(d => d.Label));
        Assert.Equal(
            ["Place", "Temperature", "Feels like temperature", "Weather condition", "Is daytime", "Today's high", "Today's low", "Sunrise", "Sunset"],
            catalog.Select(d => d.Name));
    }

    [Fact]
    public void TheProvider_IsDeclaredAsWeather_WithAFifteenMinuteDefaultAndAFiveMinuteFloor()
    {
        var descriptor = UrDeck.Engine.Data.ProviderDescriptor.TryCreate(typeof(WeatherProvider), out string? reason);

        Assert.NotNull(descriptor);
        Assert.Null(reason);
        Assert.Equal("weather", descriptor!.Id);
        Assert.Equal("Weather", descriptor.Name);
        Assert.Equal(900000, descriptor.DefaultIntervalMs);
        Assert.Equal(300000, descriptor.MinIntervalMs);
    }

    [Fact]
    public void TheCatalog_NeedsNoNetwork()
    {
        _ = new WeatherProvider().Describe();

        Assert.Empty(_http.Requests);
    }

    // Values

    [Fact]
    public void APlaceTypedAsText_IsResolvedAndEveryReadingPublishedUnderTheAskedPath()
    {
        WeatherProvider provider = Started(AllReadings("Portland, OR"));
        Sample(provider);

        Assert.Equal("Portland, Oregon", _sink.Value("Portland, OR/place").Text);
        Assert.Equal(23.0, _sink.Value("Portland, OR/current/temperature").Number);
        Assert.Equal(23.3, _sink.Value("Portland, OR/current/apparent").Number);
        Assert.Equal("cloudy", _sink.Value("Portland, OR/current/condition").Text);
        Assert.True(_sink.Value("Portland, OR/current/is-day").IsOn);
        Assert.Equal(23.8, _sink.Value("Portland, OR/today/high").Number);
        Assert.Equal(12.2, _sink.Value("Portland, OR/today/low").Number);
    }

    [Fact]
    public void SunriseAndSunset_AreInstantsInThePlacesOwnOffset()
    {
        WeatherProvider provider = Started("Portland, OR/today/sunrise", "Portland, OR/today/sunset", "Tokyo/today/sunrise");
        Sample(provider);

        DateTimeOffset sunrise = _sink.Value("Portland, OR/today/sunrise").Time;
        Assert.Equal(TimeSpan.FromHours(-7), sunrise.Offset);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791469092), sunrise);
        Assert.Equal(TimeSpan.FromHours(-7), _sink.Value("Portland, OR/today/sunset").Time.Offset);
        Assert.Equal(TimeSpan.FromHours(9), _sink.Value("Tokyo/today/sunrise").Time.Offset);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791492136), _sink.Value("Tokyo/today/sunrise").Time);
    }

    [Fact]
    public void ThePlaceOfCoordinates_IsTheCoordinates_AndNoSearchIsMade()
    {
        WeatherProvider provider = Started("@47.37,8.54/place", "@47.37,8.54/current/temperature");
        Sample(provider);

        Assert.Equal("47.37, 8.54", _sink.Value("@47.37,8.54/place").Text);
        Assert.Equal(11.3, _sink.Value("@47.37,8.54/current/temperature").Number);
        Assert.Equal(0, _http.Geocoding);
        Assert.Contains("latitude=47.37&longitude=8.54", _http.Requests.Single(r => r.Host.StartsWith("api.", StringComparison.Ordinal)).Query);
    }

    [Fact]
    public void ASlashInAPlaceName_IsLookedUpAsASlash()
    {
        WeatherProvider provider = Started("AC%2FDC Street/place");
        Sample(provider);

        Assert.Contains("name=AC%2FDC%20Street", _http.Requests[0].Query);
    }

    [Fact]
    public void TheTextIsSentToTheServiceAsTyped_OverHttpsOnly()
    {
        WeatherProvider provider = Started("Portland, OR/place");
        Sample(provider);

        Uri geocoding = _http.Requests.Single(r => r.Host.StartsWith("geocoding", StringComparison.Ordinal));
        Assert.Equal("https", geocoding.Scheme);
        Assert.Contains("name=Portland%2C%20OR", geocoding.Query);
        Assert.Contains("count=5", geocoding.Query);
        Assert.All(_http.Requests, r => Assert.Equal("https", r.Scheme));
    }

    [Fact]
    public void TheForecastAsksForTheCurrentAndDailyFieldsInUnixTime()
    {
        Started("Portland, OR/place");

        Assert.True(Wait(() => _http.Forecasts == 1));
        string query = Uri.UnescapeDataString(_http.Requests.Single(r => r.Host.StartsWith("api.", StringComparison.Ordinal)).Query);
        Assert.Contains("current=temperature_2m,apparent_temperature,is_day,weather_code", query);
        Assert.Contains("daily=temperature_2m_max,temperature_2m_min,sunrise,sunset", query);
        Assert.Contains("timezone=auto", query);
        Assert.Contains("forecast_days=1", query);
        Assert.Contains("timeformat=unixtime", query);
    }

    [Fact]
    public void TwoPlaces_AreFetchedInOneRequest_AndPublishIndependently()
    {
        WeatherProvider provider = Started("Zurich/current/temperature", "Tokyo/current/temperature", "Zurich/place", "Tokyo/place");
        Sample(provider);

        Assert.Equal(11.3, _sink.Value("Zurich/current/temperature").Number);
        Assert.Equal(19.2, _sink.Value("Tokyo/current/temperature").Number);
        Assert.Equal("Zurich, Canton of Zurich", _sink.Value("Zurich/place").Text);
        Assert.Equal("Tokyo", _sink.Value("Tokyo/place").Text);
        Assert.Equal(1, _http.Forecasts);
        Assert.Equal(2, _http.Geocoding);
    }

    [Fact]
    public void TheSamePlaceWrittenTwice_IsLookedUpOnceAndFetchedOnce()
    {
        WeatherProvider provider = Started("zurich/current/temperature", "ZURICH/place", " Zurich /current/condition");
        Sample(provider);

        Assert.Equal(1, _http.Geocoding);
        Assert.Equal(1, _http.Forecasts);
        Assert.Equal(11.3, _sink.Value("zurich/current/temperature").Number);
        Assert.Equal("Zurich, Canton of Zurich", _sink.Value("ZURICH/place").Text);
        Assert.Equal("cloudy", _sink.Value(" Zurich /current/condition").Text);
    }

    [Fact]
    public void OnlyTheReadingsAskedFor_ArePublished()
    {
        WeatherProvider provider = Started("Zurich/current/temperature");
        Sample(provider);

        Assert.True(_sink.Has("Zurich/current/temperature"));
        Assert.False(_sink.Has("Zurich/place"));
        Assert.False(_sink.Has("Zurich/today/high"));
    }

    [Fact]
    public void AReadingTheProviderDoesNotHave_IsNotPublished()
    {
        WeatherProvider provider = Started("Zurich/current/humidity", "Zurich/current/temperature");
        Sample(provider);

        Assert.False(_sink.Has("Zurich/current/humidity"));
        Assert.False(_sink.IsUnavailable("Zurich/current/humidity"));
        Assert.True(_sink.Has("Zurich/current/temperature"));
    }

    [Fact]
    public void ANoSunrise_IsUnavailableWithAReason_AndTheRestStillPublishes()
    {
        _http.Override = url => url.Host.StartsWith("api.", StringComparison.Ordinal) ? Fixture("forecast-polar") : new FakeHttp().GetStringAsync(url, default).Result;
        WeatherProvider provider = Started("Oslo/today/sunrise", "Oslo/today/sunset", "Oslo/today/high");
        Sample(provider);

        Assert.True(_sink.IsUnavailable("Oslo/today/sunrise"));
        Assert.Contains("sun", _sink.Reason("Oslo/today/sunrise"));
        Assert.True(_sink.IsUnavailable("Oslo/today/sunset"));
        Assert.Equal(5.4, _sink.Value("Oslo/today/high").Number);
    }

    [Fact]
    public void AValueTheSourceDidNotSupply_IsUnavailable_NeverZero()
    {
        _http.Override = url => url.Host.StartsWith("api.", StringComparison.Ordinal) ? """{"utc_offset_seconds":0}""" : new FakeHttp().GetStringAsync(url, default).Result;
        WeatherProvider provider = Started("Zurich/current/temperature", "Zurich/current/condition", "Zurich/place");
        Sample(provider);

        Assert.True(_sink.IsUnavailable("Zurich/current/temperature"));
        Assert.True(_sink.IsUnavailable("Zurich/current/condition"));
        Assert.True(_sink.Has("Zurich/place"));
    }

    [Fact]
    public void ANoMatch_IsUnavailableWithTheTextInTheReason_AndIsNotAskedAgain()
    {
        WeatherProvider provider = Started("Xyzzyville/current/temperature", "Zurich/current/temperature");
        Sample(provider);
        Sample(provider);
        Sample(provider);

        Assert.True(_sink.IsUnavailable("Xyzzyville/current/temperature"));
        Assert.Contains("Xyzzyville", _sink.Reason("Xyzzyville/current/temperature"));
        Assert.Equal(1, _http.Requests.Count(r => r.Query.Contains("Xyzzyville", StringComparison.Ordinal)));
        Assert.Equal(11.3, _sink.Value("Zurich/current/temperature").Number);
        // The place that matched nothing is left out of the forecast request.
        Assert.All(_http.Requests.Where(r => r.Host.StartsWith("api.", StringComparison.Ordinal)), r => Assert.DoesNotContain(",", r.Query.Split('&')[0]));
    }

    [Fact]
    public void AnAmbiguousPlace_UsesTheFirstMatch_AndTheLogListsTheOthers()
    {
        WeatherProvider provider = Started("Springfield/place");
        Sample(provider);

        Assert.Equal("Springfield, Missouri", _sink.Value("Springfield/place").Text);
        string line = Assert.Single(_sink.Lines(), l => l.Contains("matches", StringComparison.Ordinal));
        Assert.Contains("Springfield, Illinois", line);
        Assert.Contains("using Springfield, Missouri", line);
    }

    // Network behaviour

    [Fact]
    public void NothingSubscribed_NoRequestIsMade()
    {
        WeatherProvider provider = Started();
        Sample(provider);
        Sample(provider);

        Assert.Empty(_http.Requests);
    }

    [Fact]
    public void ANewPlace_IsFetchedAtOnce_WithoutASample()
    {
        Started("Zurich/current/temperature");

        Assert.True(Wait(() => _sink.Has("Zurich/current/temperature")));
        Assert.Equal(11.3, _sink.Value("Zurich/current/temperature").Number);
    }

    [Fact]
    public void AnEditedLocation_ShowsTheNewPlaceWithoutWaitingForTheInterval_AndTheOldOneIsDropped()
    {
        WeatherProvider provider = Started("Zurich/current/temperature");
        Assert.True(Wait(() => _sink.Has("Zurich/current/temperature")));
        _time.Advance(TimeSpan.FromMinutes(1));

        provider.SetDemand(["Tokyo/current/temperature"]);

        Assert.True(Wait(() => _sink.Has("Tokyo/current/temperature")));
        Sample(provider);
        Assert.Equal(19.2, _sink.Value("Tokyo/current/temperature").Number);
        Uri last = _http.Requests.Where(r => r.Host.StartsWith("api.", StringComparison.Ordinal)).Last();
        Assert.Contains("latitude=35.6895&", last.Query);
        Assert.DoesNotContain("47.36", last.Query);
    }

    [Fact]
    public void ASampleRightAfterTheNewPlaceFetch_ReusesItsData()
    {
        WeatherProvider provider = Started("Zurich/current/temperature");
        Sample(provider);
        Assert.True(Wait(() => _sink.Has("Zurich/current/temperature")));

        Assert.Equal(1, _http.Forecasts);
    }

    [Fact]
    public void ASampleAfterTheInterval_FetchesAgain()
    {
        WeatherProvider provider = Started("Zurich/current/temperature");
        Sample(provider);
        _time.Advance(TimeSpan.FromMinutes(15));
        Sample(provider);

        Assert.Equal(2, _http.Forecasts);
        Assert.Equal(1, _http.Geocoding);
    }

    [Fact]
    public void ANewReadingOfAKnownPlace_IsAnsweredWithoutARequest()
    {
        WeatherProvider provider = Started("Zurich/current/temperature");
        Sample(provider);
        int forecasts = _http.Forecasts;

        provider.SetDemand(["Zurich/current/temperature", "Zurich/today/high", "Zurich/place"]);

        Assert.True(Wait(() => _sink.Has("Zurich/today/high") && _sink.Has("Zurich/place")));
        Assert.Equal(14.6, _sink.Value("Zurich/today/high").Number);
        Assert.Equal(forecasts, _http.Forecasts);
    }

    [Fact]
    public void ANoMatchPlaceThatGainsAReading_AnswersUnavailableWithoutARequest()
    {
        WeatherProvider provider = Started("Xyzzyville/place");
        Sample(provider);
        int requests = _http.Requests.Count;

        provider.SetDemand(["Xyzzyville/place", "Xyzzyville/current/temperature"]);

        Assert.True(Wait(() => _sink.IsUnavailable("Xyzzyville/current/temperature")));
        Assert.Equal(requests, _http.Requests.Count);
    }

    [Fact]
    public void Offline_ASampleFails_AndValuesAppearOnceTheConnectionReturns_WithOnlyTwoLogLines()
    {
        _http.Failure = new HttpRequestException("no connection");
        WeatherProvider provider = Started("Zurich/current/temperature");
        Assert.True(Wait(() => _sink.IsUnavailable("Zurich/current/temperature")));

        Assert.ThrowsAny<Exception>(() => Sample(provider));
        Assert.ThrowsAny<Exception>(() => Sample(provider));
        Assert.Contains("no connection", _sink.Reason("Zurich/current/temperature"));

        _http.Failure = null;
        Sample(provider);
        Sample(provider);

        Assert.Equal(11.3, _sink.Value("Zurich/current/temperature").Number);
        string[] log = _sink.Lines().Where(l => l.Contains("fail", StringComparison.OrdinalIgnoreCase) || l.Contains("again", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Equal(2, log.Length);
        Assert.Contains("no connection", log[0]);
        Assert.Contains("again", log[1]);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void AnErrorStatus_FailsTheSample(HttpStatusCode status)
    {
        _http.Failure = new HttpRequestException("status", null, status);
        WeatherProvider provider = Started("Zurich/current/temperature");

        Assert.Throws<HttpRequestException>(() => Sample(provider));
    }

    [Fact]
    public void ATruncatedAnswer_FailsTheSample_AndKeepsThePlaceResolved()
    {
        string good = Fixture("forecast-three");
        bool truncate = true;
        _http.Override = url => url.Host.StartsWith("api.", StringComparison.Ordinal)
            ? (truncate ? good[..200] : JsonNode.Parse(good)!.AsArray()[0]!.ToJsonString())
            : new FakeHttp().GetStringAsync(url, default).Result;
        WeatherProvider provider = Started("Zurich/current/temperature");

        Assert.ThrowsAny<JsonException>(() => Sample(provider));
        truncate = false;
        Sample(provider);

        Assert.Equal(11.3, _sink.Value("Zurich/current/temperature").Number);
        Assert.Equal(1, _http.Geocoding);
    }

    [Fact]
    public void ACancelledSample_IsNotLoggedAsAFailure()
    {
        _http.Gate = new TaskCompletionSource();
        WeatherProvider provider = Started("Zurich/current/temperature");
        using var cts = new CancellationTokenSource();
        Task sample = provider.SampleAsync(cts.Token);
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => sample.GetAwaiter().GetResult());
        Assert.DoesNotContain(_sink.Lines(), l => l.Contains("failed", StringComparison.Ordinal));
        _http.Gate.SetResult();
    }

    [Fact]
    public void ANewPlaceThatCannotBeFetched_ShowsUnavailableWithAReason_AndLogsOnce()
    {
        _http.Failure = new HttpRequestException("no connection");
        Started("Zurich/current/temperature", "Zurich/place");

        Assert.True(Wait(() => _sink.IsUnavailable("Zurich/current/temperature") && _sink.IsUnavailable("Zurich/place")));
        Assert.Contains("no connection", _sink.Reason("Zurich/place"));
        Assert.Single(_sink.Lines(), l => l.Contains("failed", StringComparison.Ordinal));
    }

    [Fact]
    public void AllPlacesAddedInOneBurst_AreFetchedInAtMostTwoRequests_AndNeverTwoAtOnce()
    {
        int running = 0, most = 0;
        _http.Override = url =>
        {
            int now = Interlocked.Increment(ref running);
            most = Math.Max(most, now);
            Thread.Sleep(20);
            Interlocked.Decrement(ref running);
            return new FakeHttp().GetStringAsync(url, default).Result;
        };
        WeatherProvider provider = Started("Zurich/place");
        provider.SetDemand(["Zurich/place", "Tokyo/place"]);
        provider.SetDemand(["Zurich/place", "Tokyo/place", "Oslo/place"]);

        Assert.True(Wait(() => _sink.Has("Oslo/place") && _sink.Has("Tokyo/place") && _sink.Has("Zurich/place")));
        Assert.Equal(1, most);
    }

    [Fact]
    public void Shutdown_LeavesNothingRunning_AndStartsNothingAfterIt()
    {
        _http.Gate = new TaskCompletionSource();
        WeatherProvider provider = Started("Zurich/place");
        Assert.True(Wait(() => _http.Requests.Count == 1));

        provider.Shutdown();

        Assert.True(_http.Disposed);
        int requests = _http.Requests.Count;
        provider.SetDemand(["Tokyo/place"]);
        Thread.Sleep(50);
        Assert.Equal(requests, _http.Requests.Count);
        Assert.False(_sink.Has("Zurich/place"));
    }

    [Fact]
    public async Task TheRealClient_RefusesAnUnencryptedAddress()
    {
        using var http = new WeatherHttp();

        await Assert.ThrowsAsync<InvalidOperationException>(() => http.GetStringAsync(new Uri("http://api.open-meteo.com/v1/forecast"), CancellationToken.None));
    }

    [Fact]
    public async Task TheRealClient_ReportsAnUnreachableHostAsAFailure_NotAHang()
    {
        using var http = new WeatherHttp();

        await Assert.ThrowsAnyAsync<Exception>(() => http.GetStringAsync(new Uri("https://127.0.0.1:1/"), CancellationToken.None));
    }
}
