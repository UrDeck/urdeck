// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Sdk;
using UrDeck.Sdk.Settings;

namespace UrDeck.Widgets.Weather;

/// <summary>
/// The weather card's settings. Properties this version does not know stay in <see cref="WidgetConfig.ExtensionData"/> and
/// are kept across load and save.
/// </summary>
public class WeatherConfig : WidgetConfig
{
    /// <summary>Free text naming a place (a city, a postal code, <c>City, ST</c>); no default.</summary>
    [Setting("Place", Help = "The place to show: search for it and pick a match. There is no default: with none the card shows dashes.")]
    [Place("weather", LatitudeSetting = "latitude", LongitudeSetting = "longitude")]
    public string? Location { get; set; }

    /// <summary>With <see cref="Longitude"/>, replaces <see cref="Location"/>.</summary>
    [SettingPart("location")]
    public double? Latitude { get; set; }

    /// <summary>With <see cref="Latitude"/>, replaces <see cref="Location"/>.</summary>
    [SettingPart("location")]
    public double? Longitude { get; set; }

    /// <summary>
    /// Null (absent) shows the place's resolved name, a text shows that text, and an empty text shows no place and
    /// reserves no space for it.
    /// </summary>
    [Setting("Label", Help = "The name shown for the place. Left out, it is the place's name; a text replaces it, and none shows no name.",
        Fallback = "The place name", AllowsNone = true)]
    public string? Label { get; set; }

    /// <summary>
    /// Display unit for the card: <c>celsius</c> or <c>fahrenheit</c>; null is the region's. It chooses the unit of the
    /// temperatures and, with it, the system of wind speeds and precipitation depths.
    /// </summary>
    [Setting("Units", Help = "Celsius means kilometres per hour, millimetres and centimetres; Fahrenheit means miles per hour and inches. Left out, it follows the region.",
        FallbackFrom = FallbackSource.Region)]
    [Option("celsius", "Celsius")]
    [Option("fahrenheit", "Fahrenheit")]
    public string? Unit { get; set; }

    /// <summary>
    /// <c>periodic</c> (the default) plays the icon's loop once every <see cref="PeriodSeconds"/> and rests on its first frame
    /// in between; <c>full</c> loops it all the time; <c>off</c> only shows the first frame. Anything else counts as <c>periodic</c>.
    /// </summary>
    [Setting("Icon motion", Help = "How the weather icon moves: now and then, all the time, or not at all. No effect under a theme that names an icon set, whose icons do not move.",
        Group = "Motion")]
    [Option("periodic", "Now and then")]
    [Option("full", "Always")]
    [Option("off", "Off")]
    public string? Motion { get; set; }

    /// <summary>Seconds from one start of the loop to the next in the periodic mode; default 60, never less than a loop and a second.</summary>
    [Setting("Seconds between loops", Help = "How long from one start of the icon's loop to the next when it moves now and then.",
        Group = "Motion", Fallback = "60")]
    [Number(Min = 1, Unit = "s")]
    [ShownWhen("motion", "periodic")]
    public double? PeriodSeconds { get; set; }

    /// <summary>
    /// At 4x4, how many forecast days the card shows: a whole number from 1 to 7 (5 when absent; a number outside the
    /// range is the nearest end of it). Kept as the JSON it was written as, so that a value that is not a number counts
    /// as absent and is still in the file after a save.
    /// </summary>
    [Setting("Forecast days", Help = "How many forecast days the card shows, from 1 to 7.", Sizes = ["4x4"], Fallback = "5")]
    [Slider(1, 7)]
    public JsonElement? Days { get; set; }

    /// <summary>
    /// At 4x4, <c>true</c> lets the icons of the forecast days move as <see cref="Motion"/> says; anything else shows them
    /// still. Kept as the JSON it was written as, like <see cref="Days"/>.
    /// </summary>
    [Setting("Animate the forecast", Help = "Lets the forecast icons move as the icon motion says. Off by default: each moving icon costs about 2.7 MB of memory. No effect under a theme that names an icon set.",
        Sizes = ["4x4"], Group = "Motion")]
    [Toggle]
    public JsonElement? AnimateForecast { get; set; }
}
