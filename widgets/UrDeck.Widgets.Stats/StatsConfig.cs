// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Serialization;
using UrDeck.Sdk;
using UrDeck.Sdk.Settings;

namespace UrDeck.Widgets.Stats;

/// <summary>One reading on a stats card.</summary>
public class StatsSlot
{
    /// <summary>The reading id, for example <c>system:cpu/core/2/load</c>.</summary>
    [Setting("Reading", Help = "The reading to show, written provider:path (system:cpu/core/2/load).")]
    [Reading]
    public string Reading { get; set; } = "";

    /// <summary>
    /// Null (absent) shows the reading's catalog label, a text shows that text, and an empty text shows no label.
    /// </summary>
    [Setting("Label", Help = "The name under or beside the value. Left out, it is the reading's own label; a text replaces it, and none shows no label.",
        AllowsNone = true, FallbackFrom = FallbackSource.Reading, FallbackReading = "reading")]
    public string? Label { get; set; }

    /// <summary>Display unit for readings that have several: <c>celsius</c> or <c>fahrenheit</c>.</summary>
    [Setting("Unit", Help = "The unit to show a reading in when it has several, such as a temperature in Celsius or Fahrenheit. Left out, it is the reading's default, then the region's.",
        FallbackFrom = FallbackSource.Region)]
    [Unit("reading")]
    public string? Unit { get; set; }

    /// <summary>Decimals to show.</summary>
    [Setting("Decimals", Help = "How many decimals to show.", Fallback = "Automatic")]
    [Number(Min = 0, Max = 3)]
    public int? Decimals { get; set; }

    /// <summary>
    /// <c>plain</c>, <c>gauge</c> (the theme's default gauge style), <c>ring</c>, <c>bar</c>, <c>verticalBar</c>, <c>meter</c>, <c>tach</c>,
    /// <c>marker</c> or <c>thickbar</c>. Null or
    /// a name this version does not know counts as not present.
    /// </summary>
    [Setting("Style", Help = "How the value is drawn. Automatic is plain at the small sizes and the theme's gauge in a gauge position. A reading with no range to measure against is always plain.",
        DisabledInRoles = ["text"], DisabledHelp = "Text positions are always plain.")]
    [Option("", "Automatic")]
    [Option("plain", "Plain")]
    [Option("gauge", "Theme's gauge")]
    [Option("ring", "Ring")]
    [Option("bar", "Bar")]
    [Option("verticalBar", "Vertical bar")]
    [Option("meter", "Meter")]
    [Option("tach", "Tach")]
    [Option("marker", "Marker")]
    [Option("thickbar", "Thick bar")]
    public string? Style { get; set; }

    /// <summary>The gauge's range, replacing the one in the reading's description (canonical unit).</summary>
    [Setting("Minimum", Help = "The value where the gauge is empty, replacing the one the reading declares.", Group = "Range",
        FallbackFrom = FallbackSource.Reading, FallbackReading = "reading")]
    [Number(UnitFromReading = "reading")]
    public double? Min { get; set; }

    /// <summary>The gauge's largest value.</summary>
    [Setting("Maximum", Help = "The value where the gauge is full, replacing the one the reading declares.", Group = "Range",
        FallbackFrom = FallbackSource.Reading, FallbackReading = "reading")]
    [Number(UnitFromReading = "reading")]
    public double? Max { get; set; }

    /// <summary>From this value on the gauge is drawn in the theme's warning colour (canonical unit).</summary>
    [Setting("Warning from", Help = "From this value on the gauge is drawn in the theme's warning colour.", Group = "Levels",
        Fallback = "Off", FallbackFrom = FallbackSource.Reading, FallbackReading = "reading")]
    [Number(UnitFromReading = "reading")]
    public double? Warning { get; set; }

    /// <summary>From this value on the gauge is drawn in the theme's critical colour (canonical unit).</summary>
    [Setting("Critical from", Help = "From this value on the gauge is drawn in the theme's critical colour.", Group = "Levels",
        Fallback = "Off", FallbackFrom = FallbackSource.Reading, FallbackReading = "reading")]
    [Number(UnitFromReading = "reading")]
    public double? Critical { get; set; }

    /// <summary>Properties this version does not know; kept across load and save.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>The stats card's settings.</summary>
public class StatsConfig : WidgetConfig
{
    /// <summary>The readings on the card, in the order of the positions of its size; slots beyond them stay in the file.</summary>
    [Setting("Readings", Help = "The readings to show, filling the positions of the card's size in order. Readings beyond the positions stay in the file.")]
    [Positions("1x1", "reading")]
    [Positions("2x1", "reading")]
    [Positions("2x2", "reading")]
    [Positions("4x2", "gauge", "gauge", "text", "text", "text")]
    [Positions("4x4", "gauge", "gauge", "gauge", "gauge", "text", "text", "text")]
    public List<StatsSlot> Slots { get; set; } = [];
}
