// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using UrDeck.Engine.Layout;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public class WidgetHitTestTests
{
    // 1100 wide: a cell is 275, and a gap of 0.04 insets every card by 5.5 (rounded) on each side.
    private static List<WidgetLayoutItem> Layout(params WidgetConfig[] widgets) =>
        new GridLayoutManager(1100, 1100, 0.04).RenderWidgetLayout([.. widgets], new Size(1100, 1100));

    private static WidgetConfig At(int col, int row, int width = 1, int height = 1, bool visible = true) =>
        new() { WidgetTypeId = "t", Col = col, Row = row, Width = width, Height = height, IsVisible = visible };

    [Fact]
    public void APointOnACard_GivesItsIndex_AndThePointRelativeToTheCard()
    {
        var layout = Layout(At(0, 0), At(1, 0), At(0, 1, width: 2));

        var hit = WidgetHitTest.Find(layout, 400, 100);

        Assert.NotNull(hit);
        Assert.Equal(1, hit!.Value.Index);
        Assert.Equal(400 - layout[1].Position.X, hit.Value.X);
        Assert.Equal(100 - layout[1].Position.Y, hit.Value.Y);
    }

    [Fact]
    public void APointInTheGapBetweenTwoCards_HitsNothing()
    {
        var layout = Layout(At(0, 0), At(1, 0));

        // x = 275 is the border between the two cells: half a gap away from either card.
        Assert.Null(WidgetHitTest.Find(layout, 275, 100));
    }

    [Fact]
    public void APointOnAnEmptyCell_HitsNothing()
    {
        var layout = Layout(At(0, 0));

        Assert.Null(WidgetHitTest.Find(layout, 700, 100));
        Assert.Null(WidgetHitTest.Find([], 10, 10));
    }

    [Fact]
    public void TheCardsEdgePixels_TheFirstIsInside_AndTheOnePastTheLastIsNot()
    {
        var layout = Layout(At(1, 1));
        var card = layout[0];
        int left = card.Position.X;
        int top = card.Position.Y;
        int right = left + card.Size.Width;
        int bottom = top + card.Size.Height;

        Assert.Equal(new WidgetHit(0, 0, 0), WidgetHitTest.Find(layout, left, top));
        Assert.Equal(new WidgetHit(0, card.Size.Width - 1, card.Size.Height - 1), WidgetHitTest.Find(layout, right - 1, bottom - 1));
        Assert.Null(WidgetHitTest.Find(layout, left - 1, top));
        Assert.Null(WidgetHitTest.Find(layout, left, top - 1));
        Assert.Null(WidgetHitTest.Find(layout, right, top));
        Assert.Null(WidgetHitTest.Find(layout, left, bottom));
    }

    [Fact]
    public void AnUnplacedWidget_IsSkipped()
    {
        // Row 9 does not fit a screen four rows high.
        var layout = Layout(At(0, 9));

        Assert.False(layout[0].Placed);
        Assert.Null(WidgetHitTest.Find(layout, layout[0].Position.X + 10, layout[0].Position.Y + 10));
    }

    [Fact]
    public void AnInvisibleWidget_IsSkipped_AndWhatLiesUnderItIsHit()
    {
        var layout = Layout(At(0, 0), At(0, 0, visible: false));

        Assert.Equal(0, WidgetHitTest.Find(layout, 100, 100)!.Value.Index);
        Assert.Null(WidgetHitTest.Find(Layout(At(0, 0, visible: false)), 100, 100));
    }

    [Fact]
    public void WhereCardsOverlap_TheLaterOneWins()
    {
        var layout = Layout(At(0, 0, width: 2), At(1, 0));

        Assert.Equal(1, WidgetHitTest.Find(layout, 400, 100)!.Value.Index);
        Assert.Equal(0, WidgetHitTest.Find(layout, 100, 100)!.Value.Index);
    }
}
