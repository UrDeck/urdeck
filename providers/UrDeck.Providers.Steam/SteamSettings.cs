// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;

namespace UrDeck.Providers.Steam;

/// <summary>The settings of <see cref="SteamProvider"/>: <c>providers.steam</c> in <c>urdeck-config.json</c>.</summary>
public sealed class SteamSettings : ProviderSettings
{
    /// <summary>The refresh token of the Steam sign-in; set by Connect, renewed by the provider, never written to the configuration.</summary>
    [Setting("Steam sign-in", Help = "Connect Steam with the Steam mobile app to show your friends.")]
    [Secret(SetBySignIn = true)]
    public SecretValue? RefreshToken { get; set; }
}
