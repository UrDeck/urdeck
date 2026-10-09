// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Widgets.Weather;

/// <summary>Which Meteocons animation shows a condition by day or by night, and the words for the condition.</summary>
internal static class WeatherIcons
{
    /// <summary>The icon of a place whose condition is not known.</summary>
    public const string Neutral = "not-available";

    /// <summary>
    /// The poster frame as a fraction of the animation's length: frame 0, where the sun is whole and the rain, snow and
    /// lightning are at their fullest (chosen by eye, see docs/perf/weather.md).
    /// </summary>
    public const double PosterFraction = 0.0;

    /// <summary>The name of the animation for a condition class (<c>clear</c>, <c>rain</c> ...) by day or by night.</summary>
    public static string Key(string? condition, bool isDay) => condition switch
    {
        "clear" => isDay ? "clear-day" : "clear-night",
        "partly-cloudy" => isDay ? "partly-cloudy-day" : "partly-cloudy-night",
        "cloudy" => "overcast",
        "fog" => "fog",
        "drizzle" => "drizzle",
        "rain" => "rain",
        "sleet" => "sleet",
        "snow" => "snow",
        "thunder" => isDay ? "thunderstorms-day" : "thunderstorms-night",
        _ => Neutral,
    };

    /// <summary>The condition in words (English; localisation is parked).</summary>
    public static string Words(string condition) => condition switch
    {
        "clear" => "Clear",
        "partly-cloudy" => "Partly cloudy",
        "cloudy" => "Cloudy",
        "fog" => "Fog",
        "drizzle" => "Drizzle",
        "rain" => "Rain",
        "sleet" => "Sleet",
        "snow" => "Snow",
        "thunder" => "Thunderstorm",
        _ => "Unknown",
    };
}
