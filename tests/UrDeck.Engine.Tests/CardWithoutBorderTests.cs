// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The style the host paints under a moving border: everything but the ring (motion).</summary>
public sealed class CardWithoutBorderTests
{
    private static readonly LoadedTheme Dark = new ThemeStore("").Load(ThemeStore.DefaultName);

    private sealed class Empty : Widget<WidgetConfig>
    {
        public override void Render(WidgetRenderContext context)
        {
        }
    }

    private static ThemeCardDefinition Card(double borderWidth) => new()
    {
        Radius = 0.2,
        BorderWidth = borderWidth,
        BorderGradient = new ThemeCardBorderGradientDefinition { Colors = ["#ff0000", "#0000ff", "#ff0000"], Kind = "conic" },
        BorderAnimate = new ThemeCardBorderAnimateDefinition { Mode = "spin", Period = 7 },
        FillGradient = new ThemeGradientDefinition { Colors = ["#202020", "#404040"] },
        Compact = new ThemeCardCompactDefinition { Radius = 0.1 },
    };

    private static ResolvedCardStyle Resolve(ThemeCardDefinition card)
    {
        var def = new ThemeDefinition { Card = card }.MergeOver(Dark.Definition);
        return ResolvedCardStyle.Resolve(new LoadedTheme("without-border", def, Dark.Font), 100);
    }

    private static SKBitmap Paint(ResolvedCardStyle style, int width = 200, int height = 100)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var theme = ThemeResolver.Resolve(Dark, 100);
        WidgetPainter.Paint(new Empty(), canvas, new System.Drawing.Size(width, height), theme, new CardLook(style, 0), DateTime.Now, CancellationToken.None);
        return bitmap;
    }

    [Fact]
    public void TheBack_EqualsTheParentsBack_WithABorderWidthOfZero()
    {
        using var parent = Resolve(Card(0.04));
        using var noBorderAtAll = Resolve(Card(0));
        using var derived = Paint(parent.WithoutBorder());
        using var reference = Paint(noBorderAtAll);

        Assert.True(derived.Bytes.AsSpan().SequenceEqual(reference.Bytes));
    }

    [Fact]
    public void TheParentsBack_KeepsItsBorder()
    {
        using var parent = Resolve(Card(0.04));
        using var before = Paint(parent);
        using var derivedPaint = Paint(parent.WithoutBorder());
        using var after = Paint(parent);

        Assert.True(before.Bytes.AsSpan().SequenceEqual(after.Bytes));
        Assert.False(before.Bytes.AsSpan().SequenceEqual(derivedPaint.Bytes));
        Assert.NotNull(parent.BorderAnimation);
        Assert.Null(parent.WithoutBorder().BorderAnimation);
    }

    [Fact]
    public void TheDerivedStyle_IsKeptOnItsParent_AndTheCompactOneHasItsOwn()
    {
        using var parent = Resolve(Card(0.04));

        Assert.Same(parent.WithoutBorder(), parent.WithoutBorder());
        Assert.Same(parent.WithoutBorder().WithoutBorder(), parent.WithoutBorder());
        Assert.NotSame(parent.WithoutBorder(), parent.Compact.WithoutBorder());
        Assert.Equal(parent.Compact.Radius, parent.Compact.WithoutBorder().Radius);
        Assert.Equal(parent.Radius, parent.WithoutBorder().Radius);
    }

    [Fact]
    public void AStyleWithoutABorder_IsItsOwnDerivedStyle()
    {
        using var parent = Resolve(Card(0));

        Assert.Same(parent, parent.WithoutBorder());
    }
}
