// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;

namespace UrDeck.Providers.Weather;

/// <summary>
/// A place the geocoding service found. <c>Name</c> is the place's name and, when it differs from the name, its first-level
/// area (<c>Portland, Oregon</c>).
/// </summary>
internal sealed record Place(string Name, double Latitude, double Longitude);

/// <summary>
/// What the forecast service says about one forecast day; a value the answer did not hold is null. <c>Date</c>,
/// <c>Sunrise</c> and <c>Sunset</c> are Unix seconds (<c>Date</c> is the start of the day at the place; a sunrise or
/// sunset is null when the sun does not rise or set that day). Rain is in millimetres and snow in centimetres, as sent.
/// </summary>
internal sealed record DayWeather(
    long? Date,
    int? WeatherCode,
    double? High,
    double? Low,
    long? Sunrise,
    long? Sunset,
    double? PrecipChance,
    double? RainSum,
    double? ShowersSum,
    double? Snow)
{
    /// <summary>The liquid that falls as rain or showers; null when the source supplied neither.</summary>
    public double? Rain => RainSum is null && ShowersSum is null ? null : (RainSum ?? 0) + (ShowersSum ?? 0);
}

/// <summary>
/// What the forecast service says about one place: the conditions now and up to <see cref="WeatherParser.MaxDays"/> days
/// from today; a value the answer did not hold is null. <c>UtcOffset</c> is in whole minutes.
/// </summary>
internal sealed record PlaceWeather(
    TimeSpan UtcOffset,
    double? Temperature,
    double? Apparent,
    bool? IsDay,
    int? WeatherCode,
    double? Humidity,
    double? WindSpeed,
    double? WindDirection,
    IReadOnlyList<DayWeather> Days);

/// <summary>Reads the services' JSON answers. Anything that is not what the service documents throws.</summary>
internal static class WeatherParser
{
    /// <summary>The days of the forecast the provider offers (today and six more).</summary>
    public const int MaxDays = 7;

    /// <summary>The places of a geocoding answer, best match first; none when nothing matched (the answer has no <c>results</c>).</summary>
    public static IReadOnlyList<Place> ParseGeocoding(string json)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        ThrowIfError(root);
        if (!root.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
            return [];

        var places = new List<Place>(results.GetArrayLength());
        foreach (JsonElement result in results.EnumerateArray())
        {
            string? name = Text(result, "name");
            if (string.IsNullOrWhiteSpace(name) || Number(result, "latitude") is not { } latitude || Number(result, "longitude") is not { } longitude)
                continue;

            string? area = Text(result, "admin1");
            string display = !string.IsNullOrWhiteSpace(area) && !string.Equals(area, name, StringComparison.OrdinalIgnoreCase)
                ? name + ", " + area
                : name;
            places.Add(new Place(display, latitude, longitude));
        }

        return places;
    }

    /// <summary>
    /// The answer of a forecast request for <paramref name="expected"/> places: an object for one place and an array of
    /// objects, in request order, for several.
    /// </summary>
    public static PlaceWeather[] ParseForecast(string json, int expected)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        ThrowIfError(root);

        var items = new List<JsonElement>(expected);
        if (root.ValueKind == JsonValueKind.Array)
            items.AddRange(root.EnumerateArray());
        else
            items.Add(root);
        if (items.Count != expected)
            throw new InvalidDataException($"The forecast holds {items.Count} places, not the {expected} that were asked for.");

        var result = new PlaceWeather[expected];
        for (int i = 0; i < expected; i++)
            result[i] = ParsePlace(items[i]);
        return result;
    }

    private static PlaceWeather ParsePlace(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The forecast of a place is not an object.");
        ThrowIfError(item);

        // The offset is in whole minutes in every real zone; a stray second is rounded.
        double offsetSeconds = Number(item, "utc_offset_seconds") ?? 0;
        var offset = TimeSpan.FromMinutes(Math.Round(offsetSeconds / 60));

        double? temperature = null, apparent = null, humidity = null, windSpeed = null, windDirection = null;
        bool? isDay = null;
        int? code = null;
        if (item.TryGetProperty("current", out JsonElement current) && current.ValueKind == JsonValueKind.Object)
        {
            temperature = Number(current, "temperature_2m");
            apparent = Number(current, "apparent_temperature");
            isDay = Number(current, "is_day") is { } day ? day != 0 : null;
            code = Number(current, "weather_code") is { } weather ? (int)weather : null;
            humidity = Number(current, "relative_humidity_2m");
            windSpeed = Number(current, "wind_speed_10m");
            windDirection = Number(current, "wind_direction_10m");
        }

        DayWeather[] days = [];
        if (item.TryGetProperty("daily", out JsonElement daily) && daily.ValueKind == JsonValueKind.Object)
            days = ParseDays(daily);

        return new PlaceWeather(offset, temperature, apparent, isDay, code, humidity, windSpeed, windDirection, days);
    }

    // The days run as long as the longest array says, up to seven; a day or a field the answer lacks is null.
    private static DayWeather[] ParseDays(JsonElement daily)
    {
        string[] names =
        [
            "time", "weather_code", "temperature_2m_max", "temperature_2m_min", "sunrise", "sunset",
            "precipitation_probability_max", "rain_sum", "showers_sum", "snowfall_sum",
        ];
        int count = 0;
        foreach (string name in names)
        {
            if (daily.TryGetProperty(name, out JsonElement array) && array.ValueKind == JsonValueKind.Array)
                count = Math.Max(count, array.GetArrayLength());
        }

        count = Math.Min(count, MaxDays);
        var days = new DayWeather[count];
        for (int i = 0; i < count; i++)
        {
            days[i] = new DayWeather(
                Whole(At(daily, "time", i)),
                At(daily, "weather_code", i) is { } weather ? (int)weather : null,
                At(daily, "temperature_2m_max", i),
                At(daily, "temperature_2m_min", i),
                Whole(At(daily, "sunrise", i)),
                Whole(At(daily, "sunset", i)),
                At(daily, "precipitation_probability_max", i),
                At(daily, "rain_sum", i),
                At(daily, "showers_sum", i),
                At(daily, "snowfall_sum", i));
        }

        return days;
    }

    private static long? Whole(double? number) => number is { } value ? (long)value : null;

    private static void ThrowIfError(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.True)
            throw new InvalidDataException("The service reported an error: " + (Text(element, "reason") ?? "no reason given"));
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    /// <summary>The entry at an index of an array property; null when the array or the entry is missing or null.</summary>
    private static double? At(JsonElement element, string name, int index)
    {
        if (!element.TryGetProperty(name, out JsonElement array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() <= index)
            return null;
        JsonElement entry = array[index];
        return entry.ValueKind == JsonValueKind.Number ? entry.GetDouble() : null;
    }
}
