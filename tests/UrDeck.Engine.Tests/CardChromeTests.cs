// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The card's back of format 2 (card-chrome-back): the resolved style, the order, the shape, the fill and the border.</summary>
public sealed class CardChromeTests
{
    private static readonly LoadedTheme Dark = new ThemeStore("").Load(ThemeStore.DefaultName);

    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "themes");

    private static readonly SKColor[] Tints = [SKColor.Parse("#ff0000"), SKColor.Parse("#00ff00"), SKColor.Parse("#0000ff")];

    /// <summary>The default theme with <paramref name="card"/> and <paramref name="colors"/> over it, as a theme of its own (its own warnings).</summary>
    private static LoadedTheme With(ThemeCardDefinition card, ThemeColorsDefinition? colors = null, string name = "card-test")
    {
        var def = new ThemeDefinition { Card = card, Colors = colors }.MergeOver(Dark.Definition);
        return new LoadedTheme(name, def, Dark.Font);
    }

    /// <summary>Opaque square cards without a border, tinted from <see cref="Tints"/>.</summary>
    private static LoadedTheme Tinted() => With(
        new ThemeCardDefinition { Radius = 0, BorderWidth = 0, Tint = "cycle" },
        new ThemeColorsDefinition { CardTints = Tints.Select(c => c.ToString()).ToList() });

    private sealed class Probe(Action<WidgetRenderContext> render) : Widget<WidgetConfig>
    {
        public override void Render(WidgetRenderContext context) => render(context);
    }

    private static readonly Probe Empty = new(_ => { });

    private static SKBitmap Paint(IWidget widget, LoadedTheme loaded, int width, int height, ResolvedCardStyle? style, int index = 0, float cell = 275)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var theme = ThemeResolver.Resolve(loaded, cell);
        WidgetPainter.Paint(widget, canvas, new System.Drawing.Size(width, height), theme,
            style == null ? null : new CardLook(style, index), DateTime.Now, CancellationToken.None);
        return bitmap;
    }

    private static List<string> Warnings(Action action)
    {
        var lines = new List<string>();
        UrDeckLog.Listener = (level, message) =>
        {
            if (level == "WARN")
                lines.Add(message);
        };
        try
        {
            action();
        }
        finally
        {
            UrDeckLog.Listener = null;
        }
        return lines;
    }

    private static void AssertNear(SKColor expected, SKColor actual, int tolerance = 3)
    {
        Assert.True(Math.Abs(expected.Red - actual.Red) <= tolerance
                    && Math.Abs(expected.Green - actual.Green) <= tolerance
                    && Math.Abs(expected.Blue - actual.Blue) <= tolerance
                    && Math.Abs(expected.Alpha - actual.Alpha) <= tolerance,
            $"expected {expected}, got {actual}");
    }

    private static float Area(SKPath path)
    {
        var p = path.Points;
        float twice = 0;
        for (int i = 0; i < p.Length; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Length];
            twice += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(twice) / 2;
    }

    // ---- 2. Resolved style and order

    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("glass")]
    public void TheBuiltInThemes_ArePlain(string name)
    {
        using var style = ResolvedCardStyle.Resolve(new ThemeStore("").Load(name), 275);

        Assert.True(style.IsPlain);
    }

    [Theory]
    [InlineData("blueprint")]
    [InlineData("carbon")]
    [InlineData("ember")]
    [InlineData("obsidian")]
    [InlineData("paper")]
    [InlineData("pop")]
    [InlineData("prism")]
    [InlineData("rigline")]
    public void EveryDraft_IsNotPlain(string name)
    {
        using var style = ResolvedCardStyle.Resolve(new ThemeStore(FixtureFolder).Load(name), 275);

        Assert.False(style.IsPlain);
    }

    [Theory]
    [InlineData(250f, 2.5f)]
    [InlineData(160f, 2f)]
    [InlineData(60f, 1f)]
    [InlineData(110f, 1f)]
    public void AFormat2BorderOfTwoPixelsOrLess_IsWhole(float cell, float expected)
    {
        var loaded = With(new ThemeCardDefinition { BorderWidth = 0.01, Shape = "round" });
        using var style = ResolvedCardStyle.Resolve(loaded, cell);

        Assert.Equal(expected, style.BorderWidth, 3);
    }

    [Fact]
    public void ABorderUnderAPixel_IsWarnedOnce_AcrossResolves()
    {
        var loaded = With(new ThemeCardDefinition { BorderWidth = 0.01, Shape = "round" }, name: "thin-border");

        var lines = Warnings(() =>
        {
            for (int i = 0; i < 3; i++)
                ResolvedCardStyle.Resolve(loaded, 60).Dispose();
            ResolvedCardStyle.Resolve(loaded, 250).Dispose();
        });

        string line = Assert.Single(lines);
        Assert.Contains("thin-border", line);
        Assert.Contains("card.borderWidth", line);
    }

    [Fact]
    public void APlainStylesBorder_IsNotRounded()
    {
        var loaded = With(new ThemeCardDefinition { BorderWidth = 0.01 });

        var lines = Warnings(() =>
        {
            using var style = ResolvedCardStyle.Resolve(loaded, 160);
            Assert.True(style.IsPlain);
            Assert.Equal(1.6f, style.BorderWidth, 3);
            using var under = ResolvedCardStyle.Resolve(loaded, 60);
            Assert.Equal(0.6f, under.BorderWidth, 3);
        });

        Assert.Empty(lines);
    }

    private static WidgetLayoutItem Item(int col, int row, bool placed = true, bool visible = true) => new()
    {
        CellPosition = new System.Drawing.Point(col * 100, row * 100),
        Placed = placed,
        Visible = visible,
    };

    [Fact]
    public void TheOrder_IsByRowThenColumn_NotByTheList()
    {
        int[] order = CardOrder.Assign([Item(1, 1), Item(0, 0), Item(0, 1), Item(1, 0)]);

        Assert.Equal([3, 0, 2, 1], order);
    }

    [Fact]
    public void AHiddenOrUnplacedItem_TakesNoPlace()
    {
        int[] order = CardOrder.Assign([Item(0, 0), Item(1, 0, visible: false), Item(2, 0), Item(0, 9, placed: false), Item(0, 1)]);

        Assert.Equal([0, -1, 1, -1, 2], order);
    }

    [Fact]
    public void EveryPage_StartsAtZero()
    {
        Assert.Equal(0, CardOrder.Assign([Item(2, 3), Item(3, 3)])[0]);
        Assert.Equal(0, CardOrder.Assign([Item(0, 5)])[0]);
    }

    [Fact]
    public void TheDock_IsBySlotPosition()
    {
        Assert.Equal([0, 1, 2], CardOrder.Dock(3));
    }

    // ---- 3. Shape and painter

    [Fact]
    public void AChamfersArea_IsTheRectangleLessItsTriangles()
    {
        using var path = CardShape.Chamfer(SKRect.Create(200, 100), 30, CardCorners.TopRight | CardCorners.BottomLeft);

        Assert.Equal(200 * 100 - 2 * (30 * 30 / 2f), Area(path), 1);
    }

    [Fact]
    public void AnEmptyCutCorners_IsASquareCard()
    {
        var loaded = With(new ThemeCardDefinition { Shape = "chamfer", Cut = 0.1, CutCorners = [], BorderWidth = 0 });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var shape = CardShape.For(style, new System.Drawing.Size(200, 100));
        using var bitmap = Paint(Empty, loaded, 200, 100, style);

        Assert.Equal(200 * 100, Area(shape.Path!), 1);
        var fill = SKColor.Parse(Dark.Definition.Colors!.CardFill);
        foreach (var (x, y) in new[] { (0, 0), (199, 0), (0, 99), (199, 99) })
            Assert.Equal(fill, bitmap.GetPixel(x, y));
    }

    [Fact]
    public void ACutLargerThanHalfTheSmallerSide_IsLimited()
    {
        var loaded = With(new ThemeCardDefinition { Shape = "chamfer", Cut = 0.5 });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var shape = CardShape.For(style, new System.Drawing.Size(100, 60));

        Assert.Equal(100 * 60 - 2 * (30 * 30 / 2f), Area(shape.Path!), 1);
    }

    [Fact]
    public void TheSchemasDefaults_ApplyToAnAbsentCutAndAngles()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Shape = "chamfer",
            FillGradient = new ThemeGradientDefinition { Colors = ["#ff0000", "#0000ff"] },
            BorderGradient = new ThemeCardBorderGradientDefinition { Colors = ["#ff0000", "#0000ff"] },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 200);

        Assert.Equal(28, style.Cut, 3);
        Assert.Equal(90, style.FillAngle);
        Assert.Equal(135, style.BorderAngle);
    }

    [Fact]
    public void AChamferedCard_ClipsTheContent_AndLeavesTheCutCornersEmpty()
    {
        var loaded = With(new ThemeCardDefinition { Shape = "chamfer", Cut = 0.145, CutCorners = ["tr", "bl"] });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(new Probe(c => c.Canvas.Clear(SKColors.Red)), loaded, 550, 275, style);

        Assert.Equal(0, bitmap.GetPixel(549, 0).Alpha);
        Assert.Equal(0, bitmap.GetPixel(0, 274).Alpha);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(0, 0));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(549, 274));
    }

    [Fact]
    public void APlainStyle_DrawsTheSameBytesAsNoLook()
    {
        using var style = ResolvedCardStyle.Resolve(Dark, 275);
        var text = new Probe(c => c.Canvas.DrawCircle(10, 10, 40, new SKPaint { Color = SKColors.Red, IsAntialias = true }));
        using var with = Paint(text, Dark, 550, 275, style);
        using var without = Paint(text, Dark, 550, 275, null);

        Assert.True(with.Bytes.AsSpan().SequenceEqual(without.Bytes));
    }

    [Fact]
    public void TheErrorCard_OnAChamferedGradientCard_WearsTheChrome()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Shape = "chamfer",
            Cut = 0.145,
            BorderWidth = 0.02,
            FillGradient = new ThemeGradientDefinition { Colors = ["#203040", "#405060"], Angle = 90 },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        var boom = new Probe(c =>
        {
            c.Canvas.Clear(SKColors.Lime);
            throw new InvalidOperationException("kaput");
        });
        using var failed = Paint(boom, loaded, 550, 275, style);
        using var fine = Paint(Empty, loaded, 550, 275, style);

        var critical = SKColor.Parse(Dark.Definition.Colors!.Critical);
        bool found = false;
        for (int y = 0; y < failed.Height; y++)
            for (int x = 0; x < failed.Width; x++)
            {
                var c = failed.GetPixel(x, y);
                Assert.NotEqual(SKColors.Lime, c);
                found |= c == critical;
            }
        Assert.True(found);

        // The cut corners stay empty, and away from the text the card is the card without the error: fill and border.
        Assert.Equal(0, failed.GetPixel(549, 0).Alpha);
        Assert.Equal(0, failed.GetPixel(0, 274).Alpha);
        foreach (var (x, y) in new[] { (540, 270), (275, 272), (547, 200), (500, 250) })
            Assert.Equal(fine.GetPixel(x, y), failed.GetPixel(x, y));
    }

    // ---- 4. Fill and border

    [Fact]
    public void AGradientAt90_RunsFromTheTopToTheBottom()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Radius = 0,
            BorderWidth = 0,
            FillGradient = new ThemeGradientDefinition { Colors = ["#ff0000", "#0000ff"], Angle = 90 },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Empty, loaded, 200, 200, style);

        AssertNear(SKColors.Red, bitmap.GetPixel(100, 0));
        AssertNear(SKColors.Blue, bitmap.GetPixel(100, 199));
        // The same colour across a row: the gradient runs down only.
        Assert.Equal(bitmap.GetPixel(0, 100), bitmap.GetPixel(199, 100));
    }

    [Fact]
    public void AGradient_WinsOverTheTint()
    {
        var loaded = With(
            new ThemeCardDefinition
            {
                Radius = 0,
                BorderWidth = 0,
                Tint = "cycle",
                FillGradient = new ThemeGradientDefinition { Colors = ["#ffffff", "#ffffff"] },
            },
            new ThemeColorsDefinition { CardTints = ["#ff0000"] });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Empty, loaded, 100, 100, style, index: 0);

        Assert.Equal(SKColors.White, bitmap.GetPixel(50, 50));
    }

    [Fact]
    public void ATintWithoutCardTints_IsTheCardFill()
    {
        var loaded = With(new ThemeCardDefinition { Radius = 0, BorderWidth = 0, Tint = "cycle" });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Empty, loaded, 100, 100, style, index: 2);

        Assert.Equal(SKColor.Parse(Dark.Definition.Colors!.CardFill), bitmap.GetPixel(50, 50));
    }

    private static (WidgetConfig, IWidget) Card(int col, int row, bool visible = true) =>
        (new WidgetConfig { WidgetTypeId = "t", Col = col, Row = row, Width = 2, Height = 1, IsVisible = visible }, Empty);

    private static SKColor Centre(SKBitmap bitmap, int col, int row) => bitmap.GetPixel(col * 275 + 275, row * 275 + 137);

    [Fact]
    public void Tints_FollowThePageOrder()
    {
        // Listed out of order: the order is the grid's, row by row.
        using var bitmap = PageRenderer.RenderToBitmap(1100, 1100, [Card(2, 1), Card(0, 0), Card(0, 1), Card(2, 0)], Tinted(), DateTime.Now);

        Assert.Equal(Tints[0], Centre(bitmap, 0, 0));
        Assert.Equal(Tints[1], Centre(bitmap, 2, 0));
        Assert.Equal(Tints[2], Centre(bitmap, 0, 1));
        Assert.Equal(Tints[0], Centre(bitmap, 2, 1));
    }

    [Fact]
    public void AHiddenWidget_TakesNoTint()
    {
        using var bitmap = PageRenderer.RenderToBitmap(1100, 1100, [Card(0, 0), Card(2, 0, visible: false), Card(0, 1)], Tinted(), DateTime.Now);

        Assert.Equal(Tints[0], Centre(bitmap, 0, 0));
        Assert.Equal(Tints[1], Centre(bitmap, 0, 1));
    }

    [Fact]
    public void DockSlots_AreTintedByPosition()
    {
        var loaded = Tinted();
        var frame = ChromeLayout.Compute(new System.Drawing.Size(1100, 3840), IndicatorMode.Off, 1,
            loaded.Definition.Indicator!.BandHeight!.Value, loaded.Definition.Dock!.Height!.Value);
        var slots = DockLayout.Compute(frame.Dock, 3, loaded.Definition.Card!.Gap!.Value);
        (WidgetConfig, IWidget?) docked = (new WidgetConfig { WidgetTypeId = "t" }, Empty);
        (WidgetConfig, IWidget?) empty = (new WidgetConfig { WidgetTypeId = "t" }, null);

        using var bitmap = PageRenderer.RenderToBitmap(1100, 3840, [], loaded, DateTime.Now, null, [docked, empty, docked]);

        SKColor At(int slot) => bitmap.GetPixel(slots[slot].Position.X + slots[slot].Size.Width / 2, slots[slot].Position.Y + slots[slot].Size.Height / 2);
        Assert.Equal(Tints[0], At(0));
        Assert.Equal(Tints[2], At(2));
    }

    [Fact]
    public void TheFittedDash_FillsEachSideWithWholePeriods_AndCentresADashOnEveryCorner()
    {
        float[] corners = [50, 150, 250, 350];
        var (intervals, phase) = SizedCardChrome.FitDash(400, corners, 15, 10);

        Assert.Equal(400, intervals.Sum(), 2);
        Assert.Equal(0, intervals.Length % 2);
        // The pattern starts half a dash before the first corner.
        Assert.Equal((400 - (50 - intervals[0] / 2)) % 400, phase, 2);
        // Each side of 100 takes four periods of 25: the dash is unscaled.
        Assert.Equal(15, intervals[0], 3);
        Assert.Equal(10, intervals[1], 3);
    }

    [Fact]
    public void AnUnevenSide_ScalesItsOwnDash()
    {
        var (intervals, _) = SizedCardChrome.FitDash(240, [10, 110, 130, 230], 15, 10);

        Assert.Equal(240, intervals.Sum(), 2);
        // The short side of 20 takes one period, scaled down: dash 12, gap 8.
        Assert.Contains(intervals, v => Math.Abs(v - 8) < 0.01);
    }

    private static LoadedTheme Dashed(string? shape) => With(new ThemeCardDefinition
    {
        Radius = 0,
        BorderWidth = 0.008,
        BorderStyle = "dashed",
        BorderDash = new ThemeCardBorderDashDefinition { On = 0.055, Off = 0.036 },
        Shape = shape,
        Cut = 0.145,
    }, new ThemeColorsDefinition { CardFill = "#000000", CardBorder = "#ffffff" });

    [Theory]
    [InlineData(550, 275)]
    [InlineData(275, 275)]
    [InlineData(1100, 275)]
    public void ADashedSquareCard_HasADashOnEveryCorner(int width, int height)
    {
        var loaded = Dashed(null);
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Empty, loaded, width, height, style);

        foreach (var (x, y) in new[] { (0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1) })
            Assert.Equal(SKColors.White, bitmap.GetPixel(x, y));
        // The dash runs on round the corner the same way at each.
        Assert.Equal(bitmap.GetPixel(10, 0), bitmap.GetPixel(width - 11, height - 1));
        Assert.Equal(bitmap.GetPixel(0, 10), bitmap.GetPixel(width - 1, height - 11));
    }

    [Fact]
    public void ADashedChamfer_HasADashOnEachCutsEnds()
    {
        var loaded = Dashed("chamfer");
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Empty, loaded, 550, 275, style);
        int cut = (int)Math.Round(0.145 * 275);

        // Where the top edge meets the cut, and the cut meets the right edge, a dash turns the corner.
        Assert.Equal(SKColors.White, bitmap.GetPixel(550 - cut - 1, 0));
        Assert.Equal(SKColors.White, bitmap.GetPixel(549, cut + 1));
        Assert.Equal(SKColors.White, bitmap.GetPixel(0, 0));
        Assert.Equal(SKColors.White, bitmap.GetPixel(549, 274));
    }

    [Fact]
    public void BorderStyleNone_AndWidthZero_DrawNoBorder()
    {
        var fill = SKColor.Parse("#000000");
        foreach (var card in new[]
        {
            new ThemeCardDefinition { Radius = 0, BorderWidth = 0.02, BorderStyle = "none" },
            new ThemeCardDefinition { Radius = 0, BorderWidth = 0, BorderStyle = "dashed" },
            new ThemeCardDefinition
            {
                Radius = 0,
                BorderWidth = 0,
                BorderGradient = new ThemeCardBorderGradientDefinition { Colors = ["#ff0000", "#00ff00"] },
            },
        })
        {
            var loaded = With(card, new ThemeColorsDefinition { CardFill = "#000000", CardBorder = "#ffffff" });
            using var style = ResolvedCardStyle.Resolve(loaded, 275);
            using var bitmap = Paint(Empty, loaded, 100, 100, style);

            Assert.Equal(fill, bitmap.GetPixel(0, 0));
            Assert.Equal(fill, bitmap.GetPixel(50, 0));
        }
    }

    [Fact]
    public void ALinearBorderGradient_ColoursTheBorderAcrossTheCard()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Radius = 0,
            BorderWidth = 0.02,
            BorderGradient = new ThemeCardBorderGradientDefinition { Colors = ["#ff0000", "#0000ff"], Kind = "linear", Angle = 0 },
        }, new ThemeColorsDefinition { CardFill = "#000000" });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Empty, loaded, 400, 100, style);

        AssertNear(SKColors.Red, bitmap.GetPixel(0, 50), 6);
        AssertNear(SKColors.Blue, bitmap.GetPixel(399, 50), 6);
        Assert.Equal(SKColor.Parse("#000000"), bitmap.GetPixel(200, 50));
    }

    [Fact]
    public void AConicBorder_StartsAtTheRight_AndDoesNotChangeBetweenPaints()
    {
        var loaded = new ThemeStore(FixtureFolder).Load("rigline");
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var first = Paint(Empty, loaded, 550, 275, style);
        using var second = Paint(Empty, loaded, 550, 275, style);

        Assert.True(first.Bytes.AsSpan().SequenceEqual(second.Bytes));
        // The sweep's first colour (#ff3ea5) at the right, on the border at the card's middle height.
        AssertNear(SKColor.Parse("#ff3ea5"), first.GetPixel(548, 137), 12);
    }

    [Fact]
    public void TheGradientBack_IsMadeOnce_ForAHundredPaints()
    {
        var loaded = new ThemeStore(FixtureFolder).Load("ember");
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var theme = ThemeResolver.Resolve(loaded, 275);
        using var bitmap = new SKBitmap(550, 275);
        using var canvas = new SKCanvas(bitmap);
        var size = new System.Drawing.Size(550, 275);

        for (int i = 0; i < 100; i++)
            WidgetPainter.Paint(Empty, canvas, size, theme, new CardLook(style, i), DateTime.Now, CancellationToken.None);

        Assert.Equal(1, style.BackImagesMade);
    }

    [Fact]
    public void ASolidBack_IsNotAnImage()
    {
        var loaded = new ThemeStore(FixtureFolder).Load("blueprint");
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Empty, loaded, 550, 275, style);

        Assert.Equal(0, style.BackImagesMade);
    }

    [Fact]
    public void ADisposedStyle_ReleasesItsSizes()
    {
        var loaded = new ThemeStore(FixtureFolder).Load("ember");
        var style = ResolvedCardStyle.Resolve(loaded, 275);
        style.For(0, new System.Drawing.Size(100, 100));
        style.Dispose();

        Assert.Throws<ObjectDisposedException>(() => style.For(0, new System.Drawing.Size(100, 100)));
    }
}
