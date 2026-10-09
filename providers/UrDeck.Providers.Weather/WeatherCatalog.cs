// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Weather;

/// <summary>The readings of one place, as the catalog writes them (the place is the parameter <c>{location}</c>) and as they are published.</summary>
internal static class WeatherCatalog
{
    public const string Place = "place";
    public const string Temperature = "current/temperature";
    public const string Apparent = "current/apparent";
    public const string Condition = "current/condition";
    public const string IsDay = "current/is-day";
    public const string High = "today/high";
    public const string Low = "today/low";
    public const string Sunrise = "today/sunrise";
    public const string Sunset = "today/sunset";

    /// <summary>The credit Open-Meteo's licence asks for next to any place its data is shown.</summary>
    public static readonly ReadingAttribution Attribution =
        new("Weather data by Open-Meteo.com", "Open-Meteo.com", "https://open-meteo.com/");

    public static IReadOnlyList<ReadingDescriptor> Describe() =>
    [
        Entry(Place, ReadingKind.Text, "Place", "Place"),
        TemperatureEntry(Temperature, "Now", "Temperature"),
        TemperatureEntry(Apparent, "Feels like", "Feels like temperature"),
        Entry(Condition, ReadingKind.Text, "Condition", "Weather condition"),
        Entry(IsDay, ReadingKind.OnOff, "Daytime", "Is daytime"),
        TemperatureEntry(High, "High", "Today's high"),
        TemperatureEntry(Low, "Low", "Today's low"),
        Entry(Sunrise, ReadingKind.Time, "Sunrise", "Sunrise"),
        Entry(Sunset, ReadingKind.Time, "Sunset", "Sunset"),
    ];

    // Temperatures declare no display unit: the region decides, unlike hardware readings. The range keeps a readout's size
    // steady whatever the value is.
    private static ReadingDescriptor TemperatureEntry(string path, string label, string name) =>
        Entry(path, ReadingKind.Temperature, label, name) with { Min = -60, Max = 60 };

    private static ReadingDescriptor Entry(string path, ReadingKind kind, string label, string name) =>
        new("{location}/" + path, kind, label, name)
        {
            Device = "{location}",
            Attribution = Attribution,
        };
}
