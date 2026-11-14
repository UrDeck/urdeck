// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Serialization;
using UrDeck.Sdk;
using UrDeck.Sdk.Settings;

namespace UrDeck.Widgets.Steam;

/// <summary>
/// The Steam widget's settings: flat, typed and each with a default. Properties this version does not know stay in
/// <see cref="WidgetConfig.ExtensionData"/> and are kept across load and save.
/// </summary>
public class SteamConfig : WidgetConfig
{
    /// <summary>Which friends the list shows: <c>online</c> or <c>playing</c>. Anything else counts as <c>online</c>.</summary>
    [JsonPropertyName("friends")]
    [Setting("Friends shown", Help = "Which friends the list shows: everyone who is online (those playing first) or only those playing a game.")]
    [Option("online", "Online")]
    [Option("playing", "Playing")]
    public string Friends { get; set; } = "online";

    /// <summary>Show a download or update in progress on the card.</summary>
    [JsonPropertyName("showDownloads")]
    [Setting("Show downloads", Help = "While Steam downloads or updates a game, the card shows the game and its progress instead of your state.")]
    public bool ShowDownloads { get; set; } = true;

    /// <summary>At the largest size, show recent games below the friends.</summary>
    [JsonPropertyName("showRecent")]
    [Setting("Show recent games", Help = "At the largest size, the games you played lately with their hours, below the friends.")]
    public bool ShowRecent { get; set; } = true;
}
