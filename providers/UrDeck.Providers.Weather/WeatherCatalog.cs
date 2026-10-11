// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
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
    public const string Humidity = "current/humidity";
    public const string WindSpeed = "current/wind-speed";
    public const string WindDirection = "current/wind-direction";

    // The fields of a forecast day, as in daily/N/date.
    public const string Date = "date";
    public const string DayCondition = "condition";
    public const string High = "high";
    public const string Low = "low";
    public const string Sunrise = "sunrise";
    public const string Sunset = "sunset";
    public const string PrecipChance = "precip-chance";
    public const string Rain = "rain";
    public const string Snow = "snow";

    /// <summary>The credit Open-Meteo's licence asks for next to any place its data is shown.</summary>
    public static readonly ReadingAttribution Attribution =
        new("Weather data by Open-Meteo.com", "Open-Meteo.com", "https://open-meteo.com/");

    /// <summary>The path below the place of a field of forecast day <paramref name="day"/> (0 is today).</summary>
    public static string Daily(int day, string field) => string.Create(CultureInfo.InvariantCulture, $"daily/{day}/{field}");

    public static IReadOnlyList<ReadingDescriptor> Describe()
    {
        var entries = new List<ReadingDescriptor>
        {
            Entry(Place, ReadingKind.Text, "Place", "Place"),
            TemperatureEntry(Temperature, "Now", "Temperature"),
            TemperatureEntry(Apparent, "Feels like", "Feels like temperature"),
            Entry(Condition, ReadingKind.Text, "Condition", "Weather condition"),
            Entry(IsDay, ReadingKind.OnOff, "Daytime", "Is daytime"),
            Entry(Humidity, ReadingKind.Percent, "Humidity", "Relative humidity") with { Min = 0, Max = 100 },
            Entry(WindSpeed, ReadingKind.Speed, "Wind", "Wind speed") with { Min = 0, Max = 150 },
            Entry(WindDirection, ReadingKind.Number, "Direction", "Wind direction") with { Unit = "°", Min = 0, Max = 360 },
        };

        for (int day = 0; day < WeatherParser.MaxDays; day++)
        {
            string forecast = "Forecast day " + day.ToString(CultureInfo.InvariantCulture) + " ";
            entries.Add(Entry(Daily(day, Date), ReadingKind.Date, "Day", forecast + "date"));
            entries.Add(Entry(Daily(day, DayCondition), ReadingKind.Text, "Condition", forecast + "condition"));
            entries.Add(TemperatureEntry(Daily(day, High), "High", forecast + "high"));
            entries.Add(TemperatureEntry(Daily(day, Low), "Low", forecast + "low"));
            entries.Add(Entry(Daily(day, Sunrise), ReadingKind.Time, "Sunrise", forecast + "sunrise"));
            entries.Add(Entry(Daily(day, Sunset), ReadingKind.Time, "Sunset", forecast + "sunset"));
            entries.Add(Entry(Daily(day, PrecipChance), ReadingKind.Percent, "Chance", forecast + "chance of precipitation") with { Min = 0, Max = 100 });
            entries.Add(Entry(Daily(day, Rain), ReadingKind.Depth, "Rain", forecast + "rain") with { DepthUnit = DepthUnit.Millimetre });
            entries.Add(Entry(Daily(day, Snow), ReadingKind.Depth, "Snow", forecast + "snow") with { DepthUnit = DepthUnit.Centimetre });
        }

        return entries;
    }

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
