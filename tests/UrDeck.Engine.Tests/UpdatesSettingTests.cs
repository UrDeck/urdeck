// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using Xunit;

namespace UrDeck.Engine.Tests;

public class UpdatesSettingTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("auto", true)]
    [InlineData("off", false)]
    [InlineData("  OFF ", false)]
    [InlineData("sometimes", true)]
    public void ValueIsRead(string? value, bool enabled) =>
        Assert.Equal(enabled, UpdatesSetting.IsEnabled(value));

    [Fact]
    public void AnotherValueWarnsAndChecks()
    {
        var lines = new List<string>();
        UrDeckLog.Listener = (level, message) => lines.Add($"{level} {message}");
        try
        {
            Assert.True(UpdatesSetting.IsEnabled("sometimes", warn: true));
            Assert.False(UpdatesSetting.IsEnabled("off", warn: true));
        }
        finally
        {
            UrDeckLog.Listener = null;
        }

        string warning = Assert.Single(lines);
        Assert.StartsWith("WARN ", warning, StringComparison.Ordinal);
        Assert.Contains("'sometimes'", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void TheConfigurationKeyIsRead()
    {
        Assert.Null(ConfigStore.Parse("{}").Updates);
        Assert.Equal("off", ConfigStore.Parse("""{"updates":"off"}""").Updates);
    }
}
