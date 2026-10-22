// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The pictures the host builds a moving border from (motion): the conic source, the ring and the pulse brush.</summary>
public sealed class BorderPictureTests
{
    private static readonly LoadedTheme Dark = new ThemeStore("").Load(ThemeStore.DefaultName);
    private static readonly string[] Colors = ["#ff3ea5", "#ffb020", "#3dff8a", "#19e6ff", "#7a5cff", "#ff3ea5"];

    private static ResolvedCardStyle Resolve(ThemeCardDefinition card)
    {
        // A card with no animation of its own is Rigline's: a spinning conic border.
        if (card.BorderAnimate == null)
        {
            card.BorderGradient ??= new ThemeCardBorderGradientDefinition { Colors = [.. Colors], Kind = "conic" };
            card.BorderAnimate = new ThemeCardBorderAnimateDefinition { Mode = "spin", Period = 7 };
        }
        var def = new ThemeDefinition { Card = card }.MergeOver(Dark.Definition);
        return ResolvedCardStyle.Resolve(new LoadedTheme("border-picture", def, Dark.Font), 100);
    }

    public static TheoryData<string> Shapes => new() { "round", "chamfer", "circle" };

    private static ResolvedCardStyle ForShape(string shape) => shape switch
    {
        "chamfer" => Resolve(new ThemeCardDefinition { Shape = "chamfer", Cut = 0.2, BorderWidth = 0.04, CutCorners = ["tr", "bl"] }),
        "circle" => Resolve(new ThemeCardDefinition { Radius = 0.1, BorderWidth = 0.04, Compact = new ThemeCardCompactDefinition { Shape = "circle" } }).Compact,
        _ => Resolve(new ThemeCardDefinition { Radius = 0.25, BorderWidth = 0.04 }),
    };

    private static System.Drawing.Size CardFor(string shape) => shape == "circle" ? new(100, 100) : new(300, 180);

    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheRing_StretchedAsANineGrid_MatchesTheStillBorder_AtEveryCorner(string shape)
    {
        using var style = ForShape(shape);
        var size = CardFor(shape);

        // The still border: the card's shape stroked under its clip, as the card back draws it. Alpha is its coverage.
        using var still = new SKBitmap(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(still))
        {
            var chrome = style.For(0, size);
            canvas.Clear(SKColors.Transparent);
            chrome.Shape.Clip(canvas);
            chrome.Shape.Draw(canvas, chrome.BorderPaint!);
        }

        // The compositor's ring: the small picture stretched round a card of this size.
        var ring = style.BorderRing(size)!;
        using var picture = ring.ToBitmap();
        using var image = SKImage.FromBitmap(picture);
        using var stretched = new SKBitmap(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(stretched))
        {
            canvas.Clear(SKColors.Transparent);
            if (ring.IsNineGrid)
            {
                int inset = (int)ring.Inset;
                canvas.DrawImageNinePatch(image, new SKRectI(inset, inset, ring.PixelWidth - inset, ring.PixelHeight - inset),
                    SKRect.Create(size.Width, size.Height), SKFilterMode.Nearest, null);
            }
            else
            {
                canvas.DrawImage(image, 0, 0, new SKSamplingOptions(), null);
            }
        }

        int zone = ring.IsNineGrid ? (int)ring.Inset : Math.Min(size.Width, size.Height) / 2;
        int worst = 0;
        foreach (var (x0, y0) in new[] { (0, 0), (size.Width - zone, 0), (0, size.Height - zone), (size.Width - zone, size.Height - zone) })
        {
            for (int y = y0; y < y0 + zone; y++)
            {
                for (int x = x0; x < x0 + zone; x++)
                    worst = Math.Max(worst, Math.Abs(still.GetPixel(x, y).Alpha - stretched.GetPixel(x, y).Alpha));
            }
        }
        // Anti-aliasing of the same path at the same pixels: they should be identical, a little slack for resampling.
        Assert.True(worst <= 12, $"{shape}: the ring differs from the still border by {worst}/255 in a corner");

        // The straight runs between the corners match too.
        Assert.Equal(still.GetPixel(size.Width / 2, 0).Alpha, stretched.GetPixel(size.Width / 2, 0).Alpha);
        Assert.Equal(still.GetPixel(size.Width / 2, size.Height / 2).Alpha, stretched.GetPixel(size.Width / 2, size.Height / 2).Alpha);
    }

    [Fact]
    public void TheConicSource_AtRotationZero_MatchesTheStillBorder_AtTheFourEdgeMidpoints()
    {
        using var style = Resolve(new ThemeCardDefinition { Radius = 0.25, BorderWidth = 0.04 });
        var size = new System.Drawing.Size(300, 300);
        var chrome = style.For(0, size);
        using var still = new SKBitmap(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(still))
        {
            canvas.Clear(SKColors.Transparent);
            chrome.Shape.Clip(canvas);
            chrome.Shape.Draw(canvas, chrome.BorderPaint!);
        }

        using var source = style.BorderConicSource()!.ToBitmap();
        int half = size.Width / 2;
        int edge = (int)style.BorderWidth / 2;
        // A square card shows the picture without cropping, scaled to its size.
        foreach (var (x, y) in new[] { (half, edge), (size.Width - 1 - edge, half), (half, size.Height - 1 - edge), (edge, half) })
        {
            var expected = still.GetPixel(x, y);
            var actual = source.GetPixel(x * source.Width / size.Width, y * source.Height / size.Height);
            Assert.InRange(Math.Abs(expected.Red - actual.Red), 0, 10);
            Assert.InRange(Math.Abs(expected.Green - actual.Green), 0, 10);
            Assert.InRange(Math.Abs(expected.Blue - actual.Blue), 0, 10);
        }
    }

    [Fact]
    public void ThePictures_AreKeptOnTheStyle_OnePerShapeKey()
    {
        using var style = Resolve(new ThemeCardDefinition { Radius = 0.25, BorderWidth = 0.04 });

        Assert.Same(style.BorderConicSource(), style.BorderConicSource());
        // Two cards big enough for the full radius share a ring; a card too small to hold it has its own.
        Assert.Same(style.BorderRing(new(300, 180)), style.BorderRing(new(500, 400)));
        Assert.NotSame(style.BorderRing(new(300, 180)), style.BorderRing(new(30, 30)));
        Assert.False(style.BorderRing(new(30, 30))!.IsNineGrid);
        Assert.Equal(30, style.BorderRing(new(30, 30))!.PixelWidth);
    }

    [Fact]
    public void TheKey_DiffersPerShape()
    {
        using var round = ForShape("round");
        using var chamfer = ForShape("chamfer");

        Assert.NotEqual(round.BorderRing(new(300, 180))!.Png, chamfer.BorderRing(new(300, 180))!.Png);
    }

    [Fact]
    public void AStyleWithoutAnAnimatedBorder_MakesNoPictures()
    {
        using var style = Resolve(new ThemeCardDefinition { Radius = 0.25, BorderWidth = 0.04, BorderAnimate = new ThemeCardBorderAnimateDefinition { Mode = "none" } });

        Assert.Null(style.BorderConicSource());
        Assert.Null(style.BorderRing(new(300, 180)));
        Assert.Null(style.BorderBrush(new(300, 180)));
    }

    [Fact]
    public void AConicPulse_UsesTheConicPicture_NotABrush()
    {
        using var style = Resolve(new ThemeCardDefinition
        {
            BorderWidth = 0.04,
            BorderGradient = new ThemeCardBorderGradientDefinition { Colors = [.. Colors], Kind = "conic" },
            BorderAnimate = new ThemeCardBorderAnimateDefinition { Mode = "pulse", Period = 3 },
        });

        Assert.NotNull(style.BorderConicSource());
        Assert.Null(style.BorderBrush(new(200, 100)));
    }

    [Fact]
    public void ThePulseBrush_IsASolidColour_OrTheLinearGradientTheStillBorderUses()
    {
        using var solid = Resolve(new ThemeCardDefinition
        {
            BorderWidth = 0.04,
            BorderGradient = null,
            BorderAnimate = new ThemeCardBorderAnimateDefinition { Mode = "pulse", Period = 3 },
        });
        Assert.True(solid.BorderBrush(new(200, 100))!.IsSolid);

        using var linear = Resolve(new ThemeCardDefinition
        {
            BorderWidth = 0.04,
            BorderGradient = new ThemeCardBorderGradientDefinition { Colors = ["#ff0000", "#0000ff"], Kind = "linear", Angle = 0 },
            BorderAnimate = new ThemeCardBorderAnimateDefinition { Mode = "pulse", Period = 3 },
        });
        var spec = linear.BorderBrush(new(200, 100))!;

        Assert.False(spec.IsSolid);
        Assert.Equal(2, spec.Colors.Length);
        // Angle 0 runs left to right across the card's middle.
        Assert.Equal(new SKPoint(0, 50), spec.Start);
        Assert.Equal(new SKPoint(200, 50), spec.End);
    }
}
