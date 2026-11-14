// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;

namespace UrDeck.Providers.Steam;

/// <summary>Reads the two facts the provider needs from Steam's tokens (JWTs): whose they are and when they stop working.</summary>
internal static class SteamJwt
{
    /// <summary>The SteamID64 in the token's <c>sub</c>, or null when the text is not a token of Steam's shape.</summary>
    public static long? SteamId(string token) =>
        Claim(token, "sub") is { ValueKind: JsonValueKind.String } sub
        && long.TryParse(sub.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out long id) && id > 0
            ? id
            : null;

    /// <summary>The instant in the token's <c>exp</c>, or null when it has none.</summary>
    public static DateTimeOffset? Expires(string token) =>
        Claim(token, "exp") is { ValueKind: JsonValueKind.Number } exp && exp.TryGetInt64(out long seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    private static JsonElement? Claim(string token, string name)
    {
        string[] parts = token.Split('.');
        if (parts.Length < 2)
            return null;
        try
        {
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.TryGetProperty(name, out var value) ? value.Clone() : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}
