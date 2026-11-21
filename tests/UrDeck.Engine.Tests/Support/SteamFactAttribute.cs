// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.CompilerServices;
using Xunit;

namespace UrDeck.Engine.Tests.Support;

/// <summary>
/// A test against the real Steam. Skipped unless <c>URDECK_STEAM_E2E=1</c>, <c>URDECK_SECRET_STEAM_APIKEY</c> (the developer's own
/// Web API key) and <c>URDECK_STEAM_E2E_STEAMID</c> (a SteamID64 whose friends list is public) are set, which only a developer
/// does; CI never sets them.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class SteamFactAttribute : FactAttribute
{
    public const string KeyVariable = "URDECK_SECRET_STEAM_APIKEY";
    public const string SteamIdVariable = "URDECK_STEAM_E2E_STEAMID";

    public SteamFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (Environment.GetEnvironmentVariable("URDECK_STEAM_E2E") != "1"
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(KeyVariable))
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(SteamIdVariable)))
            Skip = $"against the real Steam: set URDECK_STEAM_E2E=1, {KeyVariable} (your Web API key) and {SteamIdVariable}";
    }
}
