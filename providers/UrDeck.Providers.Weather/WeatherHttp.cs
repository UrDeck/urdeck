// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Net;
using System.Text;

namespace UrDeck.Providers.Weather;

/// <summary>The one door to the network, so that tests feed recorded answers and never touch it.</summary>
internal interface IWeatherHttp
{
    /// <summary>The body of the answer to a GET. Throws on a transport error, a timeout or a status that is not a success.</summary>
    Task<string> GetStringAsync(Uri url, CancellationToken cancellationToken);
}

/// <summary>
/// The real <see cref="IWeatherHttp"/>: one client, encrypted connections only, a timeout on every request, and a
/// <c>User-Agent</c> that says who is asking. Nothing is static, so a plugin context holding it can be collected once it
/// is disposed.
/// </summary>
internal sealed class WeatherHttp : IWeatherHttp, IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _client;

    public WeatherHttp()
    {
        var handler = new SocketsHttpHandler
        {
            // A short pooled-connection lifetime follows DNS changes and leaves no connection open between samples.
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };
        _client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout,
            MaxResponseContentBufferSize = 1024 * 1024,
        };
        string version = typeof(WeatherHttp).Assembly.GetName().Version?.ToString(3) ?? "0";
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(string.Create(CultureInfo.InvariantCulture, $"UrDeck/{version} (+https://github.com/UrDeck/urdeck)"));
    }

    public async Task<string> GetStringAsync(Uri url, CancellationToken cancellationToken)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Only encrypted (https) requests are made.");

        using HttpResponseMessage response = await _client.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{url.Host} answered {(int)response.StatusCode} {response.ReasonPhrase}", null, response.StatusCode);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _client.Dispose();
}

/// <summary>Finds a place by the text a user typed, with Open-Meteo's geocoding service.</summary>
internal sealed class GeocodingClient(IWeatherHttp http)
{
    private const string Endpoint = "https://geocoding-api.open-meteo.com/v1/search";

    public const int Candidates = 5;

    /// <summary>The places that match the text, best first; none when nothing does. The text is sent as it was typed.</summary>
    public async Task<IReadOnlyList<Place>> SearchAsync(string text, CancellationToken cancellationToken)
    {
        var url = new Uri($"{Endpoint}?name={Uri.EscapeDataString(text)}&count={Candidates}&language=en");
        string json = await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return WeatherParser.ParseGeocoding(json);
    }
}

/// <summary>Asks Open-Meteo for the current conditions and today's values of several places in one request.</summary>
internal sealed class ForecastClient(IWeatherHttp http)
{
    private const string Endpoint = "https://api.open-meteo.com/v1/forecast";

    private const string Query =
        "current=temperature_2m,apparent_temperature,is_day,weather_code"
        + "&daily=temperature_2m_max,temperature_2m_min,sunrise,sunset"
        + "&timezone=auto&forecast_days=1&timeformat=unixtime";

    /// <summary>One answer per place, in the order given.</summary>
    public async Task<PlaceWeather[]> FetchAsync(IReadOnlyList<Place> places, CancellationToken cancellationToken)
    {
        var url = new StringBuilder(Endpoint).Append("?latitude=");
        AppendNumbers(url, places, place => place.Latitude);
        url.Append("&longitude=");
        AppendNumbers(url, places, place => place.Longitude);
        url.Append('&').Append(Query);

        string json = await http.GetStringAsync(new Uri(url.ToString()), cancellationToken).ConfigureAwait(false);
        return WeatherParser.ParseForecast(json, places.Count);
    }

    private static void AppendNumbers(StringBuilder url, IReadOnlyList<Place> places, Func<Place, double> pick)
    {
        for (int i = 0; i < places.Count; i++)
        {
            if (i > 0)
                url.Append(',');
            url.Append(pick(places[i]).ToString("0.######", CultureInfo.InvariantCulture));
        }
    }
}
