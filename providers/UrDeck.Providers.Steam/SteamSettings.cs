// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;

namespace UrDeck.Providers.Steam;

/// <summary>The settings of <see cref="SteamProvider"/>: <c>providers.steam</c> in <c>urdeck-config.json</c>.</summary>
public sealed class SteamSettings : ProviderSettings
{
    /// <summary>The user's own Steam Web API key; stored in Credential Manager, never written to the configuration.</summary>
    [Setting("Web API key", Help = "Your own Steam Web API key (steamcommunity.com/dev/apikey). Shows your friends; the rest works without it.")]
    [Secret]
    public SecretValue? ApiKey { get; set; }

    /// <summary>Whose friends to show; empty means the account Steam is signed in to on this PC.</summary>
    [Setting("SteamID", Help = "Your 17-digit SteamID64. Leave empty to use the account Steam is signed in to on this PC.")]
    public string? SteamId { get; set; }

    /// <summary>The configured SteamID64, or null when the setting is empty or is not a 17-digit id.</summary>
    internal long? ParsedSteamId =>
        long.TryParse((SteamId ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long id) && id > 76561197960265728 ? id : null;
}
