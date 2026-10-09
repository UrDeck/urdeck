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
/// What the forecast service says about one place today; a value the answer did not hold is null. <c>UtcOffset</c> is in
/// whole minutes, <c>Sunrise</c> and <c>Sunset</c> are Unix seconds (null when the sun does not rise or set that day).
/// </summary>
internal sealed record PlaceWeather(
    TimeSpan UtcOffset,
    double? Temperature,
    double? Apparent,
    bool? IsDay,
    int? WeatherCode,
    double? High,
    double? Low,
    long? Sunrise,
    long? Sunset);

/// <summary>Reads the services' JSON answers. Anything that is not what the service documents throws.</summary>
internal static class WeatherParser
{
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

        double? temperature = null, apparent = null;
        bool? isDay = null;
        int? code = null;
        if (item.TryGetProperty("current", out JsonElement current) && current.ValueKind == JsonValueKind.Object)
        {
            temperature = Number(current, "temperature_2m");
            apparent = Number(current, "apparent_temperature");
            isDay = Number(current, "is_day") is { } day ? day != 0 : null;
            code = Number(current, "weather_code") is { } weather ? (int)weather : null;
        }

        double? high = null, low = null;
        long? sunrise = null, sunset = null;
        if (item.TryGetProperty("daily", out JsonElement daily) && daily.ValueKind == JsonValueKind.Object)
        {
            high = First(daily, "temperature_2m_max");
            low = First(daily, "temperature_2m_min");
            sunrise = First(daily, "sunrise") is { } rise ? (long)rise : null;
            sunset = First(daily, "sunset") is { } set ? (long)set : null;
        }

        return new PlaceWeather(offset, temperature, apparent, isDay, code, high, low, sunrise, sunset);
    }

    private static void ThrowIfError(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.True)
            throw new InvalidDataException("The service reported an error: " + (Text(element, "reason") ?? "no reason given"));
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    /// <summary>The first entry of an array property (today's value); null when it is missing, empty or null.</summary>
    private static double? First(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
            return null;
        JsonElement first = array[0];
        return first.ValueKind == JsonValueKind.Number ? first.GetDouble() : null;
    }
}
