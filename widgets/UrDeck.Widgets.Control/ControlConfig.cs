// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Serialization;
using UrDeck.Sdk;
using UrDeck.Sdk.Settings;

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
    [Setting("Reading", Help = "The reading whose state the tile shows. Without one the tile is neutral, neither on nor off: a button that only acts, such as a scene.", Fallback = "None")]
    [Reading(Filter = ReadingFilter.HasAction)]
    public string? Reading { get; set; }

    /// <summary>What a tap does, an action id. A bare path (<c>light.desk/toggle</c>) is taken under the reading's provider. Absent: the reading's own action.</summary>
    [JsonPropertyName("action")]
    [Setting("Tap action", Help = "What a tap does. Left out, it is the reading's own action (a light's toggle); none makes the tile display-only.", Fallback = "The reading's own action", AllowsNone = true)]
    [Action("reading")]
    public string? Action { get; set; }

    /// <summary>The name shown. Absent: the reading's label.</summary>
    [JsonPropertyName("label")]
    [Setting("Label", Help = "The name on the tile. Left out, it is the reading's label; a text replaces it, and none shows no name.", AllowsNone = true, FallbackFrom = FallbackSource.Reading, FallbackReading = "reading")]
    public string? Label { get; set; }

    /// <summary>The icon shown, a canonical icon key (<c>home/light</c>) or a local image file. Absent: the reading's icon.</summary>
    [JsonPropertyName("icon")]
    [Setting("Icon", Help = "The icon: a glyph of the theme's icon set or an image file on this PC. Left out, it is the reading's own icon; none shows no icon.", Fallback = "The reading's own icon", AllowsNone = true)]
    [Icon]
    public string? Icon { get; set; }

    /// <summary>How the tile is drawn: <c>switch</c> or <c>dimmer</c>. Anything else counts as <c>switch</c>.</summary>
    [JsonPropertyName("style")]
    [Setting("Style", Help = "A switch shows the icon, the name and the state; a dimmer adds a bar for a level and needs a 2x1 tile and a level reading.")]
    [Option("switch", "Switch")]
    [Option("dimmer", "Dimmer")]
    public string Style { get; set; } = "switch";

    /// <summary><c>dimmer</c> only: the reading shown on the bar, 0 to 100 (<c>homeassistant:light.desk/brightness</c>).</summary>
    [JsonPropertyName("level")]
    [Setting("Level reading", Help = "The reading shown on the bar, 0 to 100. Without it a dimmer is drawn as a switch.")]
    [Reading]
    [ShownWhen("style", "dimmer")]
    public string? Level { get; set; }

    /// <summary><c>dimmer</c> only: what a tap on the bar does, with the level as the argument. Absent: the level reading's own action.</summary>
    [JsonPropertyName("levelAction")]
    [Setting("Level action", Help = "What a tap on the bar does, with the level as its argument. Left out, it is the level reading's own action.", Fallback = "The level reading's own action", AllowsNone = true)]
    [Action("level")]
    [ShownWhen("style", "dimmer")]
    public string? LevelAction { get; set; }
}
