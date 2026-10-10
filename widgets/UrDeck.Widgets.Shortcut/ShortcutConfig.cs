// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Serialization;
using UrDeck.Sdk;

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
    public string? Target { get; set; }

    /// <summary>Arguments for a file target; ignored for an address.</summary>
    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }

    /// <summary>Where the icon comes from instead of <see cref="Target"/>: a local image file, or any other icon source.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>A text shown under the icon. Not set and empty both show none at 1x1.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }
}
