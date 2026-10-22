// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Motion;
using Xunit;

namespace UrDeck.Engine.Tests;

public class MotionPolicyTests
{
    [Theory]
    // config, windows animations, theme ceiling => effective, user, source
    [InlineData(null, true, null, MotionLevel.Full, MotionLevel.Full, MotionSource.Windows)]
    [InlineData(null, false, null, MotionLevel.Off, MotionLevel.Off, MotionSource.Windows)]
    [InlineData(null, false, "full", MotionLevel.Off, MotionLevel.Off, MotionSource.Windows)]
    [InlineData("full", false, "full", MotionLevel.Full, MotionLevel.Full, MotionSource.Configuration)]
    [InlineData("full", false, null, MotionLevel.Full, MotionLevel.Full, MotionSource.Configuration)]
    [InlineData("full", true, "reduced", MotionLevel.Reduced, MotionLevel.Full, MotionSource.Theme)]
    [InlineData("reduced", true, "full", MotionLevel.Reduced, MotionLevel.Reduced, MotionSource.Configuration)]
    [InlineData("reduced", true, "off", MotionLevel.Off, MotionLevel.Reduced, MotionSource.Theme)]
    [InlineData("off", true, "full", MotionLevel.Off, MotionLevel.Off, MotionSource.Configuration)]
    [InlineData("off", true, "reduced", MotionLevel.Off, MotionLevel.Off, MotionSource.Configuration)]
    [InlineData(null, true, "reduced", MotionLevel.Reduced, MotionLevel.Full, MotionSource.Theme)]
    [InlineData("  FULL ", true, null, MotionLevel.Full, MotionLevel.Full, MotionSource.Configuration)]
    [InlineData("sideways", false, null, MotionLevel.Off, MotionLevel.Off, MotionSource.Windows)]
    [InlineData("", true, null, MotionLevel.Full, MotionLevel.Full, MotionSource.Windows)]
    public void DecideFollowsTheTruthTable(string? config, bool windows, string? theme, MotionLevel effective, MotionLevel user, MotionSource source)
    {
        var decision = MotionPolicy.Decide(config, windows, theme);

        Assert.Equal(effective, decision.Effective);
        Assert.Equal(user, decision.User);
        Assert.Equal(source, decision.Source);
    }

    [Fact]
    public void TheLogWordingNamesTheLevelAndWhatDecidedIt()
    {
        Assert.Equal("off (Windows animation setting)", MotionPolicy.Decide(null, false, null).ToString());
        Assert.Equal("reduced (theme)", MotionPolicy.Decide("full", true, "reduced").ToString());
        Assert.Equal("full (configuration)", MotionPolicy.Decide("full", false, null).ToString());
    }

    [Fact]
    public void AnUnknownValueIsAbsent()
    {
        Assert.Null(MotionPolicy.Parse("sideways"));
        Assert.Null(MotionPolicy.Parse(null));
        Assert.Equal(MotionLevel.Reduced, MotionPolicy.Parse(" Reduced"));
    }
}
