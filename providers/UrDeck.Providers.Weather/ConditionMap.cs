// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Providers.Weather;

/// <summary>Turns WMO weather codes (what Open-Meteo reports) into the portable condition classes the provider publishes.</summary>
internal static class ConditionMap
{
    public const string Clear = "clear";
    public const string PartlyCloudy = "partly-cloudy";
    public const string Cloudy = "cloudy";
    public const string Fog = "fog";
    public const string Drizzle = "drizzle";
    public const string Sleet = "sleet";
    public const string Rain = "rain";
    public const string Snow = "snow";
    public const string Thunder = "thunder";
    public const string Unknown = "unknown";

    public static string Classify(int code) => code switch
    {
        0 or 1 => Clear,
        2 => PartlyCloudy,
        3 => Cloudy,
        45 or 48 => Fog,
        51 or 53 or 55 => Drizzle,
        56 or 57 or 66 or 67 => Sleet,
        61 or 63 or 65 or 80 or 81 or 82 => Rain,
        71 or 73 or 75 or 77 or 85 or 86 => Snow,
        95 or 96 or 99 => Thunder,
        _ => Unknown,
    };
}
