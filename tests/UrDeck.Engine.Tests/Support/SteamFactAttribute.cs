// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.CompilerServices;
using Xunit;

namespace UrDeck.Engine.Tests.Support;

/// <summary>
/// A test against the real Steam. Skipped unless <c>URDECK_STEAM_E2E=1</c> and <c>URDECK_SECRET_STEAM_REFRESHTOKEN</c> (a refresh
/// token from Connect Steam) are set, which only a developer does; CI never sets them.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class SteamFactAttribute : FactAttribute
{
    public const string TokenVariable = "URDECK_SECRET_STEAM_REFRESHTOKEN";

    public SteamFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (Environment.GetEnvironmentVariable("URDECK_STEAM_E2E") != "1" || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TokenVariable)))
            Skip = $"against the real Steam: set URDECK_STEAM_E2E=1 and {TokenVariable} (a refresh token from Connect Steam)";
    }
}
