// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Serialization;
using UrDeck.Sdk;

namespace UrDeck.Widgets.Control;

/// <summary>
/// The control tile's settings: flat, typed and each with a default. <see cref="Action"/>, <see cref="Label"/>, <see cref="Icon"/>
/// and <see cref="LevelAction"/> follow one rule: absent (null) takes the value from the reading's description, a text is used
/// as given and an empty text means none. Properties this version does not know stay in <see cref="WidgetConfig.ExtensionData"/>
/// and are kept across load and save.
/// </summary>
public class ControlConfig : WidgetConfig
{
    /// <summary>The state shown, a reading id (<c>homeassistant:light.desk</c>). None: the tile is neutral, an action-only button.</summary>
    [JsonPropertyName("reading")]
    public string? Reading { get; set; }

    /// <summary>What a tap does, an action id. A bare path (<c>light.desk/toggle</c>) is taken under the reading's provider. Absent: the reading's own action.</summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    /// <summary>The name shown. Absent: the reading's label.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>The icon shown, a canonical icon key (<c>home/light</c>) or a local image file. Absent: the reading's icon.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>How the tile is drawn: <c>switch</c> or <c>dimmer</c>. Anything else counts as <c>switch</c>.</summary>
    [JsonPropertyName("style")]
    public string Style { get; set; } = "switch";

    /// <summary><c>dimmer</c> only: the reading shown on the bar, 0 to 100 (<c>homeassistant:light.desk/brightness</c>).</summary>
    [JsonPropertyName("level")]
    public string? Level { get; set; }

    /// <summary><c>dimmer</c> only: what a tap on the bar does, with the level as the argument. Absent: the level reading's own action.</summary>
    [JsonPropertyName("levelAction")]
    public string? LevelAction { get; set; }
}
