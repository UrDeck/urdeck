// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Startup;
using Xunit;

namespace UrDeck.Engine.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("0.2.0-rc.1+abc1234", "0.2.0-rc.1")]
    [InlineData("0.1.0+abc", "0.1.0")]
    [InlineData("0.0.0-dev", "0.0.0-dev")]
    public void WithoutMetadataDropsTheBuildMetadata(string informational, string expected) =>
        Assert.Equal(expected, AppVersion.WithoutMetadata(informational));

    [Fact]
    public void OfReadsTheAssembliesVersion() =>
        Assert.False(string.IsNullOrWhiteSpace(AppVersion.Of(typeof(AppVersion).Assembly)));
}
