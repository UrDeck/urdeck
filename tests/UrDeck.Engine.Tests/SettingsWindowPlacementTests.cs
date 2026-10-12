// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using UrDeck.Engine.Config;
using UrDeck.Engine.Display;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public class SettingsWindowPlacementTests
{
    private static readonly DisplayInfo[] Three = [TestDisplays.Panel(), TestDisplays.Primary(), TestDisplays.Side()];

    private static DisplayInfo? Choose(
        IReadOnlyList<DisplayInfo> displays, Point cursor, string monitorName = "tallest", RememberedDisplay? remembered = null) =>
        SettingsWindowPlacement.Choose(DisplayRows.Build(new UrDeckConfig { MonitorName = monitorName, Display = remembered }, displays), cursor);

    [Fact]
    public void ThePointerOnAnotherDisplay_OpensThere()
    {
        Assert.Equal("HP Z23n G2", Choose(Three, new Point(-500, 500))!.Name);
        Assert.Equal("LG ULTRAGEAR", Choose(Three, new Point(100, 100))!.Name);
    }

    [Fact]
    public void ThePointerOnTheDeck_OpensOnThePrimaryDisplay()
    {
        Assert.Equal("LG ULTRAGEAR", Choose(Three, new Point(4000, -100))!.Name);
    }

    [Fact]
    public void ThePointerOnTheDeck_WhichIsThePrimary_OpensOnTheFirstOtherDisplay()
    {
        Assert.Equal("HP Z23n G2", Choose(Three, new Point(100, 100), "primary")!.Name);
    }

    [Fact]
    public void ThePointerOnNoDisplay_OpensOnThePrimaryDisplay()
    {
        Assert.Equal("LG ULTRAGEAR", Choose(Three, new Point(-20000, 0))!.Name);
    }

    [Fact]
    public void TheOnlyDisplay_IsTheDecks_AndTheWindowOpensThere()
    {
        Assert.Equal("LG ULTRAGEAR", Choose([TestDisplays.Primary()], new Point(10, 10), "primary")!.Name);
    }

    [Fact]
    public void WithTheDecksDisplayAbsent_ThePointersDisplayIsUsed()
    {
        var remembered = TestDisplays.Remembered(TestDisplays.Panel());

        Assert.Equal("HP Z23n G2", Choose([TestDisplays.Primary(), TestDisplays.Side()], new Point(-500, 500), remembered: remembered)!.Name);
    }

    [Fact]
    public void WithoutAnyDisplay_ThereIsNoChoice()
    {
        Assert.Null(Choose([], Point.Empty));
    }
}
