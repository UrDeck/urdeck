// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

public class SecretValueTests
{
    private sealed class Settings : ProviderSettings
    {
        public string? Url { get; set; }

        public SecretValue? Token { get; set; }
    }

    [Fact]
    public void ToString_IsTheMask_NotTheTextOrItsLength()
    {
        var secret = new SecretValue("abcdef123456");

        Assert.Equal("•••", secret.ToString());
        Assert.Equal("•••", $"{secret}");
        Assert.DoesNotContain("abcdef", secret.ToString());
    }

    [Fact]
    public void Reveal_GivesTheText()
    {
        Assert.Equal("abc", new SecretValue("abc").Reveal());
        Assert.True(new SecretValue("").IsEmpty);
    }

    [Fact]
    public void ASettingsObject_DoesNotWriteTheSecret()
    {
        var settings = new Settings { Url = "https://ha.home", Token = new SecretValue("topsecret") };

        string json = JsonSerializer.Serialize(settings);

        Assert.DoesNotContain("topsecret", json);
        Assert.Contains("ha.home", json);
    }

    [Fact]
    public void ASecretInJson_IsNotReadIntoTheSettingsObject()
    {
        var settings = JsonSerializer.Deserialize<Settings>("""{"Url":"https://ha.home","Token":"typed-in"}""");

        Assert.NotNull(settings);
        Assert.Equal("https://ha.home", settings.Url);
        Assert.Null(settings.Token);
    }

    [Fact]
    public void LoggingASettingsObject_ShowsTheMask()
    {
        var settings = new Settings { Token = new SecretValue("topsecret") };

        Assert.DoesNotContain("topsecret", $"{settings.Token} {settings.Token!.ToString()}");
    }

    [Fact]
    public void TheBaseSettings_CarryTheActionLimits()
    {
        var settings = JsonSerializer.Deserialize<ProviderSettings>("""{"intervalMs":500,"actionTimeoutMs":5000,"actionWarnMs":1500,"other":1}""")!;

        Assert.Equal(500, settings.IntervalMs);
        Assert.Equal(5000, settings.ActionTimeoutMs);
        Assert.Equal(1500, settings.ActionWarnMs);
        Assert.True(settings.ExtensionData!.ContainsKey("other"));
    }
}
