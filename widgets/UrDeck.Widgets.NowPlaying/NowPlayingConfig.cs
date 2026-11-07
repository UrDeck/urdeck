// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Serialization;
using UrDeck.Sdk;
using UrDeck.Sdk.Settings;

namespace UrDeck.Widgets.NowPlaying;

/// <summary>
/// The now-playing widget's settings: flat, typed and each with a default. Properties this version does not know stay in
/// <see cref="WidgetConfig.ExtensionData"/> and are kept across load and save.
/// </summary>
public class NowPlayingConfig : WidgetConfig
{
    /// <summary>Show the album line when there is room.</summary>
    [JsonPropertyName("showAlbum")]
    [Setting("Show the album", Help = "Shows the album under the artist when the card is tall enough.")]
    public bool ShowAlbum { get; set; } = true;

    /// <summary>Show the name of the player as a small line.</summary>
    [JsonPropertyName("showApp")]
    [Setting("Show the player", Help = "Shows the name of the player (Spotify, chrome) as a small line under the rest. Windows reports only the application's identifier, so the name is that identifier tidied up.")]
    public bool ShowApp { get; set; }

    /// <summary>What the card says when no player has a session.</summary>
    [JsonPropertyName("idleLabel")]
    [Setting("Idle text", Help = "The words on the card when no player has anything to play.", AllowsNone = true)]
    public string IdleLabel { get; set; } = "Nothing playing";
}
