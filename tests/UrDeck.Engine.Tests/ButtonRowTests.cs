// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk.Components;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ButtonRowTests
{
    private static readonly SKRect Wide = SKRect.Create(10, 20, 400, 60);

    [Fact]
    public void ThreeButtons_AreCentredAsAGroup_InOrder_TheMiddleOneLarger_AndNeverOverlap()
    {
        SKRect[] rects = ButtonRow.Layout(Wide, 3, [1f, 1.3f, 1f]);

        Assert.Equal(3, rects.Length);
        Assert.True(rects[1].Width > rects[0].Width);
        Assert.Equal(rects[0].Width, rects[2].Width, 3);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(rects[i].Width, rects[i].Height, 3);
            Assert.Equal(Wide.MidY, rects[i].MidY, 3);
            Assert.InRange(rects[i].Height, 0, Wide.Height + 0.001f);
        }

        Assert.True(rects[0].Right < rects[1].Left);
        Assert.True(rects[1].Right < rects[2].Left);
        Assert.Equal(Wide.MidX, (rects[0].Left + rects[2].Right) / 2, 2);
    }

    [Fact]
    public void WithoutWeights_TheButtonsShareOneSize_AndFillTheHeight()
    {
        SKRect[] rects = ButtonRow.Layout(Wide, 3);

        Assert.All(rects, r => Assert.Equal(Wide.Height, r.Height, 3));
    }

    [Fact]
    public void TheGroup_IsAsLargeAsTheHeightAllows_AndSizesDoNotDependOnPosition()
    {
        SKRect[] a = ButtonRow.Layout(SKRect.Create(0, 0, 400, 60), 3);
        SKRect[] b = ButtonRow.Layout(SKRect.Create(500, 700, 400, 60), 3);

        for (int i = 0; i < 3; i++)
            Assert.Equal(a[i].Width, b[i].Width, 3);
    }

    [Fact]
    public void ARectangleTooNarrow_ShrinksTheButtonsToFit_WithoutOverlapOrLeavingIt()
    {
        var narrow = SKRect.Create(0, 0, 100, 60);
        SKRect[] rects = ButtonRow.Layout(narrow, 3);

        Assert.True(rects[0].Left >= narrow.Left - 0.001f);
        Assert.True(rects[2].Right <= narrow.Right + 0.001f);
        Assert.True(rects[0].Right < rects[1].Left);
        Assert.True(rects[1].Right < rects[2].Left);
        Assert.True(rects[0].Width < ButtonRow.MinTouchShare * narrow.Height);
    }

    [Fact]
    public void SmallWeights_AreRaisedToTheMinimumTouchSize_WhileTheGroupFits()
    {
        SKRect[] rects = ButtonRow.Layout(Wide, 3, [0.2f, 1f, 0.2f]);

        Assert.Equal(ButtonRow.MinTouchShare * Wide.Height, rects[0].Width, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void ACountOfZeroOrLess_GivesNoButtons(int count) => Assert.Empty(ButtonRow.Layout(Wide, count));

    [Fact]
    public void AnEmptyOrInvertedRectangle_GivesNoButtons()
    {
        Assert.Empty(ButtonRow.Layout(SKRect.Empty, 3));
        Assert.Empty(ButtonRow.Layout(new SKRect(50, 50, 10, 10), 3));
        Assert.Equal(-1, ButtonRow.HitTest(SKRect.Empty, 3, new SKPoint(1, 1)));
    }

    [Fact]
    public void AButtonsCentre_HitsThatButton()
    {
        SKRect[] rects = ButtonRow.Layout(Wide, 3, [1f, 1.3f, 1f]);

        for (int i = 0; i < 3; i++)
            Assert.Equal(i, ButtonRow.HitTest(Wide, 3, new SKPoint(rects[i].MidX, rects[i].MidY), [1f, 1.3f, 1f]));
    }

    [Fact]
    public void APointInTheGap_HitsTheNearerButton_NeverBoth()
    {
        SKRect[] rects = ButtonRow.Layout(Wide, 3);
        float y = Wide.MidY;
        float nearFirst = rects[0].Right + (rects[1].Left - rects[0].Right) * 0.25f;
        float nearSecond = rects[0].Right + (rects[1].Left - rects[0].Right) * 0.75f;

        Assert.Equal(0, ButtonRow.HitTest(Wide, 3, new SKPoint(nearFirst, y)));
        Assert.Equal(1, ButtonRow.HitTest(Wide, 3, new SKPoint(nearSecond, y)));

        // Every point between the two buttons hits exactly the nearer.
        for (float x = rects[0].Right; x <= rects[1].Left; x += 0.5f)
        {
            int hit = ButtonRow.HitTest(Wide, 3, new SKPoint(x, y));
            float toFirst = Math.Abs(x - rects[0].MidX);
            float toSecond = Math.Abs(x - rects[1].MidX);
            Assert.Equal(toFirst <= toSecond ? 0 : 1, hit);
        }
    }

    [Fact]
    public void APointBeyondTheGroup_HitsNothing()
    {
        SKRect[] rects = ButtonRow.Layout(Wide, 3);
        float halfGap = (rects[1].Left - rects[0].Right) / 2;

        Assert.Equal(-1, ButtonRow.HitTest(Wide, 3, new SKPoint(rects[0].Left - halfGap - 1, Wide.MidY)));
        Assert.Equal(-1, ButtonRow.HitTest(Wide, 3, new SKPoint(rects[2].Right + halfGap + 1, Wide.MidY)));
        Assert.Equal(-1, ButtonRow.HitTest(Wide, 3, new SKPoint(rects[1].MidX, Wide.Bottom + 200)));
        Assert.Equal(-1, ButtonRow.HitTest(Wide, 3, new SKPoint(rects[1].MidX, Wide.Top - 200)));
    }

    [Fact]
    public void ASingleButton_IsHitOnItsRectangle()
    {
        SKRect[] rects = ButtonRow.Layout(Wide, 1);

        Assert.Single(rects);
        Assert.Equal(0, ButtonRow.HitTest(Wide, 1, new SKPoint(rects[0].MidX, rects[0].MidY)));
        Assert.Equal(-1, ButtonRow.HitTest(Wide, 1, new SKPoint(rects[0].Right + 5, rects[0].MidY)));
    }
}
