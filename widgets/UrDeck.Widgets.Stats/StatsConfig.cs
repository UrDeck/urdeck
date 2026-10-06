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

    /// <summary>Properties this version does not know; kept across load and save.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class StatsConfig : WidgetConfig
{
    /// <summary>The readings on the card. At 1x1 and 2x2 only the first is shown; the others stay in the file.</summary>
    public List<StatsSlot> Slots { get; set; } = [];
}
