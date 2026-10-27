// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.CompilerServices;
using Xunit;

namespace UrDeck.Engine.Tests.Support;

/// <summary>
/// A test against a real Home Assistant. Skipped unless <c>URDECK_HA_E2E=1</c>, <c>URDECK_HA_URL</c> and
/// <c>URDECK_SECRET_HOMEASSISTANT_TOKEN</c> are set, which only a developer does (<c>scripts/Start-HomeAssistantDemo.ps1</c>
/// starts a disposable one in Docker); CI never sets them.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class HomeAssistantFactAttribute : FactAttribute
{
    public const string UrlVariable = "URDECK_HA_URL";
    public const string TokenVariable = "URDECK_SECRET_HOMEASSISTANT_TOKEN";

    public HomeAssistantFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (Environment.GetEnvironmentVariable("URDECK_HA_E2E") != "1"
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(UrlVariable))
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TokenVariable)))
            Skip = $"against a real Home Assistant: set URDECK_HA_E2E=1, {UrlVariable} and {TokenVariable} (scripts/Start-HomeAssistantDemo.ps1)";
    }
}
