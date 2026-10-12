// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using UrDeck.Engine.Config;
using UrDeck.Engine.Display;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public class DisplayRowsTests
{
    private static readonly DisplayInfo[] Three = [TestDisplays.Panel(), TestDisplays.Primary(), TestDisplays.Side()];

    private static UrDeckConfig Config(RememberedDisplay? remembered = null, string monitorName = "tallest") =>
        new() { MonitorName = monitorName, Display = remembered };

    [Fact]
    public void ThreeDisplays_AreNumberedByPosition_AndDescribedAgainstTheirNearestNeighbour()
    {
        var list = DisplayRows.Build(Config(), Three);

        Assert.Equal(["HP Z23n G2", "LG ULTRAGEAR", "HYTE Y70Touch"], list.Rows.Select(r => r.Name));
        Assert.Equal([1, 2, 3], list.Rows.Select(r => r.Number));
        Assert.Equal(["Left of Display 2", "Right of Display 1", "Above Display 2"], list.Rows.Select(r => r.RelativePosition));
        Assert.Equal([DisplayOrientation.Portrait, DisplayOrientation.Landscape, DisplayOrientation.Portrait], list.Rows.Select(r => r.Orientation));
        Assert.Equal([false, true, false], list.Rows.Select(r => r.IsPrimary));
        Assert.Equal([1080, 3840, 1100], list.Rows.Select(r => r.Width));
        Assert.Equal([1920, 2160, 3840], list.Rows.Select(r => r.Height));
    }

    [Fact]
    public void TheDeckRow_IsTheOnePlacementWouldUse()
    {
        var list = DisplayRows.Build(Config(), Three);

        Assert.Equal(["HYTE Y70Touch"], list.Rows.Where(r => r.IsDeck).Select(r => r.Name));
        Assert.DoesNotContain(list.Rows, r => r.IsWaiting);

        list = DisplayRows.Build(Config(monitorName: "primary"), Three);
        Assert.Equal(["LG ULTRAGEAR"], list.Rows.Where(r => r.IsDeck).Select(r => r.Name));
    }

    [Fact]
    public void StackedDisplays_AreAboveAndBelowEachOther()
    {
        var top = TestDisplays.Primary() with { Bounds = new Rectangle(0, 0, 1920, 1080) };
        var bottom = TestDisplays.Side() with { IsPrimary = false, Bounds = new Rectangle(0, 1080, 1920, 1080) };

        var list = DisplayRows.Build(Config(monitorName: "primary"), [bottom, top]);

        // The same x: the order falls to y, and each describes the other.
        Assert.Equal(["LG ULTRAGEAR", "HP Z23n G2"], list.Rows.Select(r => r.Name));
        Assert.Equal(["Above Display 2", "Below Display 1"], list.Rows.Select(r => r.RelativePosition));
    }

    [Fact]
    public void OneDisplay_HasNoRelativePosition()
    {
        var list = DisplayRows.Build(Config(monitorName: "primary"), [TestDisplays.Primary()]);

        var row = Assert.Single(list.Rows);
        Assert.Null(row.RelativePosition);
        Assert.True(row.IsDeck);
        Assert.Equal(1, row.Number);
    }

    [Fact]
    public void TwoDisplaysWithTheSameName_AreTwoRows_WithDifferentNumbersAndPositions()
    {
        var left = TestDisplays.Panel(@"\\.\DISPLAY5", @"\\?\DISPLAY#RTD1100#a") with { Bounds = new Rectangle(0, 0, 1100, 3840) };
        var right = TestDisplays.Panel(@"\\.\DISPLAY6", @"\\?\DISPLAY#RTD1100#b") with { Bounds = new Rectangle(1100, 0, 1100, 3840) };

        var list = DisplayRows.Build(Config(), [right, left]);

        Assert.Equal(["HYTE Y70Touch", "HYTE Y70Touch"], list.Rows.Select(r => r.Name));
        Assert.Equal(["Left of Display 2", "Right of Display 1"], list.Rows.Select(r => r.RelativePosition));
        Assert.Equal([left, right], list.Rows.Select(r => r.Display));
    }

    [Fact]
    public void ADisplayWithNoName_IsShownAsDisplayNumber_NotAsADeviceName()
    {
        var nameless = new DisplayInfo("DISPLAY4", "", @"\\.\DISPLAY4", @"\\.\DISPLAY4", new Rectangle(2560, 0, 1920, 1080), 1, false);
        var named = TestDisplays.Primary();

        var list = DisplayRows.Build(Config(monitorName: "primary"), [named, nameless]);

        Assert.Equal(["LG ULTRAGEAR", "Display 2"], list.Rows.Select(r => r.Name));
        Assert.DoesNotContain(list.Rows, r => r.Name.Contains("DISPLAY"));
        Assert.Equal(1920, list.Rows[1].Width);
    }

    [Fact]
    public void ARememberedDisplayThatIsNotConnected_IsAnExtraRowThatCanNotBeChosen()
    {
        var panel = TestDisplays.Panel();
        var list = DisplayRows.Build(Config(TestDisplays.Remembered(panel)), [TestDisplays.Primary(), TestDisplays.Side()]);

        Assert.Equal(["HP Z23n G2", "LG ULTRAGEAR", "HYTE Y70Touch"], list.Rows.Select(r => r.Name));
        var waiting = list.Rows[^1];
        Assert.True(waiting.IsWaiting);
        Assert.Null(waiting.Number);
        Assert.Null(waiting.Display);
        Assert.Null(waiting.RelativePosition);
        Assert.DoesNotContain(list.Rows, r => r.IsDeck);
        Assert.Equal([1, 2], list.Rows.Where(r => !r.IsWaiting).Select(r => r.Number));
    }

    [Fact]
    public void ARememberedDisplayWithoutAnyDisplay_IsTheOnlyRow()
    {
        var list = DisplayRows.Build(Config(TestDisplays.Remembered(TestDisplays.Panel())), []);

        var row = Assert.Single(list.Rows);
        Assert.True(row.IsWaiting);
        Assert.Equal("HYTE Y70Touch", row.Name);
    }

    [Fact]
    public void ARememberedDisplayFoundByModelAtANewPath_IsConnected_NotWaiting()
    {
        var remembered = TestDisplays.Remembered(TestDisplays.Panel());
        var moved = TestDisplays.Panel(path: TestDisplays.PanelPathOtherOutput);

        var list = DisplayRows.Build(Config(remembered), [TestDisplays.Primary(), moved]);

        Assert.DoesNotContain(list.Rows, r => r.IsWaiting);
        Assert.Equal(["HYTE Y70Touch"], list.Rows.Where(r => r.IsDeck).Select(r => r.Name));
    }

    [Fact]
    public void NothingRemembered_IsNeverAWaitingRow()
    {
        Assert.DoesNotContain(DisplayRows.Build(Config(), []).Rows, r => r.IsWaiting);
        Assert.Empty(DisplayRows.Build(Config(), []).Rows);
        Assert.DoesNotContain(DisplayRows.Build(Config(new RememberedDisplay { Name = "x" }), Three).Rows, r => r.IsWaiting);
    }

    [Theory]
    [InlineData("tallest")]
    [InlineData("index:1")]
    public void ChooseAutomatically_IsOffWhileTheSelectorsAreInCharge(string selector)
    {
        var config = Config(TestDisplays.Remembered(TestDisplays.Panel(), selector));

        Assert.False(DisplayRows.Build(config, Three).CanChooseAutomatically);
    }

    [Fact]
    public void ChooseAutomatically_IsOffWithoutARememberedDisplay_AndOnForAnExplicitChoice()
    {
        Assert.False(DisplayRows.Build(Config(), Three).CanChooseAutomatically);
        Assert.False(DisplayRows.Build(Config(new RememberedDisplay()), Three).CanChooseAutomatically);
        Assert.True(DisplayRows.Build(Config(TestDisplays.Remembered(TestDisplays.Side())), Three).CanChooseAutomatically);
        Assert.True(DisplayRows.Build(Config(TestDisplays.Remembered(TestDisplays.Side())), []).CanChooseAutomatically);
    }
}
