// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public class WidgetCardTests
{
    private static readonly LoadedTheme Dark = new ThemeStore("").Load(ThemeStore.DefaultName);

    private static LoadedTheme WithRadius(double radius)
    {
        var def = new Themes.ThemeDefinition { Card = new ThemeCardDefinition { Radius = radius } }.MergeOver(Dark.Definition);
        return Dark with { Definition = def };
    }

    private static SKBitmap Paint(IWidget widget, LoadedTheme loaded, int width, int height, float cell = 275)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        using var theme = ThemeResolver.Resolve(loaded, cell);
        WidgetPainter.Paint(widget, canvas, new System.Drawing.Size(width, height), theme, DateTime.Now, CancellationToken.None);
        return bitmap;
    }

    private sealed class Probe(Action<WidgetRenderContext> render) : Widget<WidgetConfig>
    {
        public override void Render(WidgetRenderContext context) => render(context);
    }

    [Fact]
    public void EmptyWidget_ShowsTheCardFill()
    {
        using var bitmap = Paint(new Probe(_ => { }), Dark, 275, 275);

        Assert.Equal(SKColor.Parse(Dark.Definition.Colors!.CardFill), bitmap.GetPixel(137, 137));
    }

    [Fact]
    public void RoundedCard_LeavesTheCornerTransparent_AndFlatCardFillsIt()
    {
        using var rounded = Paint(new Probe(_ => { }), Dark, 275, 275);
        using var flat = Paint(new Probe(_ => { }), WithRadius(0), 275, 275);

        Assert.Equal(0, rounded.GetPixel(0, 0).Alpha);
        // The card fill may be translucent: the flat card covers the corner with the fill's own alpha.
        Assert.Equal(SKColor.Parse(Dark.Definition.Colors!.CardFill).Alpha, flat.GetPixel(0, 0).Alpha);
    }

    [Fact]
    public void EdgeToEdgeContent_IsClippedToTheCorners()
    {
        var fill = new Probe(c => c.Canvas.Clear(SKColors.Red));
        using var bitmap = Paint(fill, Dark, 275, 275);

        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(137, 137));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(137, 1));
    }

    [Fact]
    public void ThrowingWidget_ShowsCriticalColourInsideTheCard()
    {
        var boom = new Probe(c =>
        {
            c.Canvas.Clear(SKColors.Lime);
            throw new InvalidOperationException("kaput");
        });
        using var bitmap = Paint(boom, Dark, 550, 275, cell: 275);
        var critical = SKColor.Parse(Dark.Definition.Colors!.Critical);

        bool found = false;
        bool lime = false;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                found |= Math.Abs(p.Red - critical.Red) < 8 && Math.Abs(p.Green - critical.Green) < 8
                         && Math.Abs(p.Blue - critical.Blue) < 8 && p.Alpha == 255;
                lime |= p == SKColors.Lime;
            }

        Assert.True(found, "no critical-coloured pixels");
        Assert.False(lime, "the widget's partial drawing was not wiped");
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
    }

    [Fact]
    public void CardsOfDifferentSizes_HaveTheSameCornerRadius()
    {
        using var big = Paint(new Probe(_ => { }), Dark, 550, 550);
        using var small = Paint(new Probe(_ => { }), Dark, 275, 275);

        static int CornerRun(SKBitmap b)
        {
            int run = 0;
            while (run < b.Width && b.GetPixel(run, 0).Alpha < 128)
                run++;
            return run;
        }

        Assert.InRange(CornerRun(big), 1, 40);
        Assert.Equal(CornerRun(small), CornerRun(big));
    }

    [Fact]
    public void ContentRect_IsTheCardInsetByPadding()
    {
        SKRect seen = default;
        using var bitmap = Paint(new Probe(c => seen = c.ContentRect), Dark, 550, 275);
        using var theme = ThemeResolver.Resolve(Dark, 275);

        Assert.Equal(new SKRect(theme.Padding, theme.Padding, 550 - theme.Padding, 275 - theme.Padding), seen);
    }
}
