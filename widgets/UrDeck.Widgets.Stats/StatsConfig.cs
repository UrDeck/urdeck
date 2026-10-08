// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Serialization;
using UrDeck.Sdk;

namespace UrDeck.Widgets.Stats;

/// <summary>One reading on a stats card.</summary>
public class StatsSlot
{
    /// <summary>The reading id, for example <c>system:cpu/core/2/load</c>.</summary>
    public string Reading { get; set; } = "";

    /// <summary>
    /// Null (absent) shows the reading's catalog label, a text shows that text, and an empty text shows no label.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>Display unit for readings that have several: <c>celsius</c> or <c>fahrenheit</c>.</summary>
    public string? Unit { get; set; }

    public int? Decimals { get; set; }

    /// <summary>
    /// <c>plain</c>, <c>gauge</c> (the theme's default gauge style), <c>ring</c>, <c>bar</c> or <c>verticalBar</c>. Null or
    /// a name this version does not know counts as not present.
    /// </summary>
    public string? Style { get; set; }

    /// <summary>The gauge's range, replacing the one in the reading's description (canonical unit).</summary>
    public double? Min { get; set; }

    public double? Max { get; set; }

    /// <summary>From this value on the gauge is drawn in the theme's warning colour (canonical unit).</summary>
    public double? Warning { get; set; }

    /// <summary>From this value on the gauge is drawn in the theme's critical colour (canonical unit).</summary>
    public double? Critical { get; set; }

    /// <summary>Properties this version does not know; kept across load and save.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class StatsConfig : WidgetConfig
{
    /// <summary>The readings on the card, in the order of the positions of its size; slots beyond them stay in the file.</summary>
    public List<StatsSlot> Slots { get; set; } = [];
}
