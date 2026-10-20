// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The page pattern: its block of whole repeats, its snapping and sizes, and what a snapshot shows of it.</summary>
public sealed class PatternTileTests
{
    private const int Width = 1100;
    private const int Height = 3840;
    private const float Cell = Width / 4f;

    private static readonly System.Drawing.Size Window = new(Width, Height);

    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "themes");

    private static LoadedTheme Load(string name) =>
        ThemeStore.BuiltInNames.Contains(name) ? new ThemeStore("").Load(name) : new ThemeStore(FixtureFolder).Load(name);

    private static LoadedTheme With(string kind, double spacing, double thickness, Action<ThemePatternDefinition>? more = null)
    {
        var loaded = Load("carbon");
        loaded.Definition.Pattern = new ThemePatternDefinition { Kind = kind, Spacing = spacing, Thickness = thickness, Color = "#ffffffff" };
        more?.Invoke(loaded.Definition.Pattern);
        return loaded;
    }

    private static PatternTile Make(LoadedTheme loaded, System.Drawing.Size? window = null)
    {
        var size = window ?? Window;
        return PatternTile.Create(loaded, size.Width / 4f, size) ?? throw new InvalidOperationException("no pattern");
    }

    /// <summary>The window shaded with the block, as <see cref="PageRenderer"/> and the host's tiles draw it.</summary>
    private static SKBitmap Shade(PatternTile tile, System.Drawing.Size window)
    {
        var bitmap = new SKBitmap(window.Width, window.Height);
        using var canvas = new SKCanvas(bitmap);
        using var shader = tile.Image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, new SKSamplingOptions(SKFilterMode.Nearest));
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(0, 0, window.Width, window.Height, paint);
        return bitmap;
    }

    private static int Distance(SKColor a, SKColor b) =>
        Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)), Math.Max(Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha)));

    public static TheoryData<string> Themes() => ["carbon", "blueprint", "ember", "rigline"];

    [Theory]
    [MemberData(nameof(Themes))]
    public void TheBlock_IsPeriodic_AcrossItsEdgesInBothDirections(string theme)
    {
        using var tile = Make(Load(theme));
        using var shaded = Shade(tile, Window);

        int checkedPixels = 0;
        for (int y = 0; y < Height - tile.RepeatY; y += 3)
        {
            for (int x = 0; x < Width - tile.RepeatX; x += 3)
            {
                Assert.True(Distance(shaded.GetPixel(x, y), shaded.GetPixel(x + tile.RepeatX, y)) <= 1, $"x {x} y {y}");
                Assert.True(Distance(shaded.GetPixel(x, y), shaded.GetPixel(x, y + tile.RepeatY)) <= 1, $"x {x} y {y}");
                checkedPixels++;
            }
        }

        Assert.True(checkedPixels > 1000);
    }

    [Fact]
    public void Carbon_IsAGridOfDots35PixelsApart()
    {
        using var tile = Make(Load("carbon"));
        Assert.Equal((35, 35), (tile.RepeatX, tile.RepeatY));
        using var shaded = Shade(tile, Window);
        // The first dot is half a repeat in, then every 35 pixels.
        Assert.True(shaded.GetPixel(17, 17).Alpha > 0);
        Assert.True(shaded.GetPixel(17 + 35 * 20, 17 + 35 * 90).Alpha > 0);
        Assert.Equal(0, shaded.GetPixel(0, 0).Alpha);
        Assert.Equal(0, shaded.GetPixel(35, 35).Alpha);
    }

    [Fact]
    public void Blueprint_IsAGridWith50PixelMinorAnd250PixelMajorRepeat()
    {
        using var tile = Make(Load("blueprint"));
        Assert.Equal((250, 250), (tile.RepeatX, tile.RepeatY));
        using var shaded = Shade(tile, Window);
        // Lines are at 25 + 50 k; every fifth one, from the first, is brighter.
        var major = shaded.GetPixel(25, 400);
        var minor = shaded.GetPixel(75, 400);
        Assert.True(minor.Alpha > 0);
        Assert.True(major.Alpha > minor.Alpha);
        Assert.Equal(major, shaded.GetPixel(25 + 250, 400));
        Assert.Equal(0, shaded.GetPixel(50, 410).Alpha);
    }

    [Fact]
    public void Rigline_IsHorizontalLines10PixelsApart()
    {
        using var tile = Make(Load("rigline"));
        Assert.Equal(10, tile.RepeatY);
        using var shaded = Shade(tile, Window);
        int lines = Enumerable.Range(0, 100).Count(y => shaded.GetPixel(500, y).Alpha > 0);
        Assert.Equal(20, lines); // ten lines, each 2 pixels (2.475 -> 2)
        Assert.Equal(shaded.GetPixel(3, 14), shaded.GetPixel(900, 14));
    }

    [Fact]
    public void Ember_IsA45DegreeHatchWith32PixelPeriods_RisingToTheRight()
    {
        using var tile = Make(Load("ember"));
        Assert.Equal((32, 32), (tile.RepeatX, tile.RepeatY));
        using var shaded = Shade(tile, Window);
        // The line through the block's centre rises to the right.
        int cx = tile.PixelWidth / 2, cy = tile.PixelHeight / 2;
        Assert.True(shaded.GetPixel(cx, cy).Alpha > 0);
        Assert.True(shaded.GetPixel(cx + 10, cy - 10).Alpha > 0);
        Assert.True(shaded.GetPixel(cx - 10, cy + 10).Alpha > 0);
        Assert.Equal(0, shaded.GetPixel(cx + 10, cy + 10).Alpha);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(5)]
    [InlineData(-60)]
    [InlineData(135)]
    public void AHatch_IsDrawnAtItsAngleWithinOneDegree(double angle)
    {
        using var tile = Make(With("hatch", 0.15, 0.005, p => p.Angle = angle));
        double drawn = Math.Atan2(tile.RepeatY, tile.RepeatX) * 180 / Math.PI;
        double folded = ((angle % 180) + 180) % 180;
        double want = folded > 90 ? 180 - folded : folded;
        Assert.InRange(Math.Abs(drawn - want), 0, 1);
        using var shaded = Shade(tile, Window);
        // A slanted line's edge is anti-aliased from float endpoints, so a repeat can differ by a few levels (of 255), never by a visible seam.
        for (int x = 0; x < Width - tile.RepeatX; x += 7)
            Assert.True(Distance(shaded.GetPixel(x, 100), shaded.GetPixel(x + tile.RepeatX, 100)) <= 8, $"x {x} d {Distance(shaded.GetPixel(x, 100), shaded.GetPixel(x + tile.RepeatX, 100))} rep {tile.RepeatX}x{tile.RepeatY} block {tile.PixelWidth}x{tile.PixelHeight}");
    }

    [Theory]
    [InlineData(0.5, false)]
    [InlineData(89.5, true)]
    public void AHatchNearAnAxis_IsDrawnAsLinesOnThatAxis(double angle, bool vertical)
    {
        using var tile = Make(With("hatch", 0.1, 0.005, p => p.Angle = angle));
        using var shaded = Shade(tile, Window);
        // Every pixel along a line has the same colour: a horizontal line is constant in x, a vertical one in y.
        for (int i = 0; i < 200; i += 5)
        {
            var a = vertical ? shaded.GetPixel(i, 3) : shaded.GetPixel(3, i);
            var b = vertical ? shaded.GetPixel(i, 900) : shaded.GetPixel(800, i);
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void LineWidths_AreWholePixels_AtLeastOne()
    {
        using var thin = Make(With("scanlines", 0.1, 0.001));
        using var shaded = Shade(thin, Window);
        // Spacing 27.5 rounds to 28 (or 27); the line is 0.275 -> 1 pixel and fully opaque, not a smear.
        var lit = Enumerable.Range(0, thin.RepeatY).Select(y => shaded.GetPixel(10, y)).Where(c => c.Alpha > 0).ToList();
        Assert.Single(lit);
        Assert.Equal(255, lit[0].Alpha);

        using var wide = Make(With("scanlines", 0.1, 0.02));
        using var wideShaded = Shade(wide, Window);
        Assert.Equal(6, Enumerable.Range(0, wide.RepeatY).Count(y => wideShaded.GetPixel(10, y).Alpha == 255)); // 5.5 -> 6
    }

    [Fact]
    public void TheSpacing_IsAtLeastTwoPixels()
    {
        using var tile = Make(With("dots", 0.02, 0.0015), new System.Drawing.Size(40, 40));
        Assert.Equal(2, tile.RepeatX);
    }

    [Fact]
    public void WithoutAColour_ThePatternIsTheMutedTextColourAtASixteenthOfItsAlpha()
    {
        var loaded = With("dots", 0.127, 0.009, p => p.Color = null);
        var muted = SKColor.Parse(loaded.Definition.Colors!.TextMuted);
        var expected = PatternTile.Muted(loaded);
        Assert.Equal(Math.Max(1, muted.Alpha / 16), expected.Alpha);
        Assert.Equal((muted.Red, muted.Green, muted.Blue), (expected.Red, expected.Green, expected.Blue));

        using var tile = Make(loaded);
        using var shaded = Shade(tile, Window);
        Assert.InRange(shaded.GetPixel(17, 17).Alpha, expected.Alpha - 3, expected.Alpha + 3);
    }

    [Fact]
    public void AGridsMajorStyle_FallsBackToThePatternsOwn()
    {
        using var tile = Make(With("grid", 0.2, 0.008, p => p.MajorEvery = 4));
        using var shaded = Shade(tile, Window);
        // A major line (the first of each four) looks like a minor one when there is no major style.
        Assert.Equal(shaded.GetPixel(27, 300), shaded.GetPixel(27 + 55, 300));
    }

    [Fact]
    public void TheBlock_IsTheSmallestMultipleOfTheRepeatOfAtLeast512()
    {
        Assert.Equal(525, PatternTile.BlockSize(35, Width));
        Assert.Equal(750, PatternTile.BlockSize(250, 3840));
        Assert.Equal(520, PatternTile.BlockSize(10, 3840));
        Assert.Equal(512, PatternTile.BlockSize(1, 3840));
        Assert.Equal(512, PatternTile.BlockSize(32, 3840));
    }

    [Fact]
    public void TheBlock_IsCappedAtTheWindow()
    {
        Assert.Equal(300, PatternTile.BlockSize(250, 300));
        Assert.Equal(200, PatternTile.BlockSize(35, 200));
        using var tile = Make(Load("blueprint"), new System.Drawing.Size(300, 400));
        Assert.Equal((300, 400), (tile.PixelWidth, tile.PixelHeight));
        Assert.Equal((1, 1), tile.TileCount(new System.Drawing.Size(300, 400)));
    }

    [Fact]
    public void AThemeWithNoPattern_OrKindNone_HasNoTile()
    {
        Assert.Null(PatternTile.Create(Load("default-dark"), Cell, Window));

        var carbon = Load("carbon");
        carbon.Definition.Pattern!.Kind = "none";
        Assert.Null(PatternTile.Create(carbon, Cell, Window));
    }

    [Fact]
    public void TheKey_IsTheSameForTheSameInputs_AndDiffersWhenTheThemeCellOrWindowChanges()
    {
        using var a = Make(Load("carbon"));
        using var b = Make(Load("carbon"));
        Assert.Equal(a.Key, b.Key);

        using var otherTheme = Make(Load("blueprint"));
        Assert.NotEqual(a.Key, otherTheme.Key);

        using var otherWindow = Make(Load("carbon"), new System.Drawing.Size(1100, 1920));
        Assert.NotEqual(a.Key, otherWindow.Key);

        using var otherCell = Make(Load("carbon"), new System.Drawing.Size(1650, 3840));
        Assert.NotEqual(a.Key, otherCell.Key);
    }

    [Fact]
    public void ThePng_DecodesToTheBlock_AndIsMadeOnce()
    {
        using var tile = Make(Load("carbon"));
        using var decoded = SKBitmap.Decode(tile.Png);
        Assert.Equal((tile.PixelWidth, tile.PixelHeight), (decoded.Width, decoded.Height));
        Assert.Same(tile.Png, tile.Png);
    }

    [Fact]
    public void ASnapshotOfCarbon_ShowsDotsInTheGaps_AndNoneOverAnOpaqueCard()
    {
        using var page = SamplePage.Build();
        var with = Load("carbon");
        var without = Load("carbon");
        without.Definition.Pattern = null;
        using var dotted = PageRenderer.RenderToBitmap(1100, 1920, page.Widgets, with, SamplePage.Time);
        using var plain = PageRenderer.RenderToBitmap(1100, 1920, page.Widgets, without, SamplePage.Time);

        var layout = new GridLayoutManager(1100, 1920, with.Definition.Card!.Gap!.Value, 1920)
            .RenderWidgetLayout(page.Widgets.Select(w => w.Config).ToList(), new System.Drawing.Size(1100, 1920));
        var card = layout[0];
        int differing = 0, overCard = 0;
        for (int y = 0; y < 1920; y++)
        {
            for (int x = 0; x < 1100; x++)
            {
                if (dotted.GetPixel(x, y) == plain.GetPixel(x, y))
                    continue;
                differing++;
                if (x >= card.Position.X + 6 && x < card.Position.X + card.Size.Width - 6 && y >= card.Position.Y + 6 && y < card.Position.Y + card.Size.Height - 6)
                    overCard++;
            }
        }

        Assert.True(differing > 100, "dots are drawn");
        Assert.Equal(0, overCard);
    }
}
