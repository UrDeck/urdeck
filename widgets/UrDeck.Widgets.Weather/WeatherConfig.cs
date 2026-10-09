// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk;

namespace UrDeck.Widgets.Weather;

/// <summary>
/// The weather card's settings. Properties this version does not know stay in <see cref="WidgetConfig.ExtensionData"/> and
/// are kept across load and save.
/// </summary>
public class WeatherConfig : WidgetConfig
{
    /// <summary>Free text naming a place (a city, a postal code, <c>City, ST</c>); no default.</summary>
    public string? Location { get; set; }

    /// <summary>With <see cref="Longitude"/>, replaces <see cref="Location"/>.</summary>
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>
    /// Null (absent) shows the place's resolved name, a text shows that text, and an empty text shows no place and
    /// reserves no space for it.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>Display unit for temperatures: <c>celsius</c> or <c>fahrenheit</c>; null is the region's.</summary>
    public string? Unit { get; set; }

    /// <summary>
    /// <c>periodic</c> (the default) plays the icon's loop once every <see cref="PeriodSeconds"/> and rests on its first frame
    /// in between; <c>full</c> loops it all the time; <c>off</c> only shows the first frame. Anything else counts as <c>periodic</c>.
    /// </summary>
    public string? Motion { get; set; }

    /// <summary>Seconds from one start of the loop to the next in the periodic mode; default 60, never less than a loop and a second.</summary>
    public double? PeriodSeconds { get; set; }
}
