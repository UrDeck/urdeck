// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Serialization;
using UrDeck.Sdk;
using UrDeck.Sdk.Settings;

namespace UrDeck.Widgets.Shortcut;

/// <summary>
/// The shortcut's settings: flat, typed and each with a default. Properties this version does not know stay in
/// <see cref="WidgetConfig.ExtensionData"/> and are kept across load and save.
/// </summary>
public class ShortcutConfig : WidgetConfig
{
    /// <summary>
    /// What to open: a rooted path to a file, a folder, an executable or a <c>.lnk</c> (<c>%NAME%</c> is expanded), a
    /// bare name Windows resolves (<c>notepad</c>), a <c>shell:</c> item or an address. No default: without one the card
    /// shows the placeholder and does not react.
    /// </summary>
    [JsonPropertyName("target")]
    [Setting("Opens", Help = "What to open: an application, a file, a folder or a web address. With none the card shows an empty placeholder and does not react.")]
    [LaunchTarget]
    public string? Target { get; set; }

    /// <summary>Arguments for a file target; ignored for an address.</summary>
    [JsonPropertyName("arguments")]
    [Setting("Arguments", Help = "Arguments for a file target; ignored for an address. They are never written to the log.", Fallback = "None")]
    public string? Arguments { get; set; }

    /// <summary>Where the icon comes from instead of <see cref="Target"/>: a local image file, or any other icon source.</summary>
    [JsonPropertyName("icon")]
    [Setting("Icon", Help = "Where the icon comes from instead of the target: a glyph of the theme's icon set or an image file on this PC.", Fallback = "From the target")]
    [Icon]
    public string? Icon { get; set; }

    /// <summary>A text shown under the icon. Not set and empty both show none at 1x1.</summary>
    [JsonPropertyName("label")]
    [Setting("Label", Help = "A text under the icon. With none, the icon takes the whole card.", Fallback = "None")]
    public string? Label { get; set; }
}
