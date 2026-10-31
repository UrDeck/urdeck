// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Providers.HomeAssistant;

/// <summary>The settings of <see cref="HomeAssistantProvider"/>: <c>providers.homeassistant</c> in <c>urdeck-config.json</c>.</summary>
public sealed class HomeAssistantSettings : ProviderSettings
{
    [Setting("Address", Help = "The address you open Home Assistant at, for example http://homeassistant.local:8123.", Required = true)]
    public string? Url { get; set; }

    [Setting("Token", Help = "A long-lived access token, made on your Home Assistant profile page under Security.", Required = true)]
    [Secret(ContextSetting = "url")]
    public SecretValue? Token { get; set; }

    /// <summary>
    /// The address as an absolute <c>http</c> or <c>https</c> address without a trailing slash; null with <paramref name="error"/>
    /// saying what is wrong with it.
    /// </summary>
    internal Uri? Address(out string? error)
    {
        error = null;
        string text = (Url ?? "").Trim().TrimEnd('/');
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = $"the Home Assistant address '{Url}' is not an http or https address; fix it in Settings";
            return null;
        }

        return uri;
    }
}
