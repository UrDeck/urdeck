// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Serialization;
using UrDeck.Sdk;

namespace UrDeck.Widgets.NowPlaying;

/// <summary>
/// The now-playing widget's settings: flat, typed and each with a default. Properties this version does not know stay in
/// <see cref="WidgetConfig.ExtensionData"/> and are kept across load and save.
/// </summary>
public class NowPlayingConfig : WidgetConfig
{
    /// <summary>Show the album line when there is room.</summary>
    [JsonPropertyName("showAlbum")]
    public bool ShowAlbum { get; set; } = true;

    /// <summary>Show the name of the player as a small line.</summary>
    [JsonPropertyName("showApp")]
    public bool ShowApp { get; set; }

    /// <summary>What the card says when no player has a session.</summary>
    [JsonPropertyName("idleLabel")]
    public string IdleLabel { get; set; } = "Nothing playing";
}
