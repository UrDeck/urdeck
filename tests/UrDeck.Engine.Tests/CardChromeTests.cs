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

/// <summary>
/// The card chrome of format 2: the resolved style, the order, the shape, the fill and the border (card-chrome-back); the
/// highlight, sheen, accent strip and corner marks on the card (card-chrome-front).
/// </summary>
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

    // ---- card-chrome-front: highlight, sheen, accent strip, corner marks

    private static readonly SKColor Black = SKColor.Parse("#000000");

    /// <summary>A square black card without a border, with <paramref name="card"/>'s front pieces.</summary>
    private static LoadedTheme Front(ThemeCardDefinition card)
    {
        card.Radius ??= 0;
        card.BorderWidth ??= 0;
        return With(card, new ThemeColorsDefinition { CardFill = "#000000" });
    }

    private static readonly Probe Red = new(c => c.Canvas.Clear(SKColors.Red));

    [Fact]
    public void CornerMarks_DefaultToTopLeftAndBottomRight_InTheAccent()
    {
        var loaded = With(new ThemeCardDefinition { CornerMarks = new ThemeCardCornerMarksDefinition { Style = "bracket" } });
        using var style = ResolvedCardStyle.Resolve(loaded, 200);

        var marks = style.Marks!;
        Assert.Equal(CardCorners.TopLeft | CardCorners.BottomRight, marks.Corners);
        Assert.Equal(SKColor.Parse(Dark.Definition.Colors!.Accent), marks.Color);
        Assert.Equal(0, marks.Inset);
    }

    [Fact]
    public void CornerMarksWithoutAStyle_DrawNothing()
    {
        var loaded = With(new ThemeCardDefinition { CornerMarks = new ThemeCardCornerMarksDefinition { Size = 0.1 } });
        using var style = ResolvedCardStyle.Resolve(loaded, 200);

        Assert.Null(style.Marks);
    }

    [Fact]
    public void TheAccentStrip_DefaultsToTheSchema_InTheAccent()
    {
        var loaded = With(new ThemeCardDefinition { AccentStrip = new ThemeCardAccentStripDefinition() });
        using var style = ResolvedCardStyle.Resolve(loaded, 200);

        var strip = style.Strip!;
        Assert.Equal(CardSide.Bottom, strip.Side);
        Assert.Equal(6, strip.Thickness, 3);
        Assert.Equal(0.72f, strip.Length, 3);
        Assert.Equal([SKColor.Parse(Dark.Definition.Colors!.Accent)], strip.Colors);
        Assert.False(strip.Fade);
        Assert.Equal(0, strip.GlowBlur);
    }

    [Theory]
    [InlineData("card.highlight.thickness", 0.005, 1f)]
    [InlineData("card.accentStrip.thickness", 0.005, 1f)]
    [InlineData("card.cornerMarks.thickness", 0.006, 2f)]
    [InlineData("card.cornerMarks.thickness", 0.02, 5.5f)]
    public void EachThickness_IsAHairline(string key, double fraction, float expected)
    {
        var card = new ThemeCardDefinition();
        if (key == "card.highlight.thickness")
            card.Highlight = new ThemeCardHighlightDefinition { Top = "#ffffff", Thickness = fraction };
        else if (key == "card.accentStrip.thickness")
            card.AccentStrip = new ThemeCardAccentStripDefinition { Thickness = fraction };
        else
            card.CornerMarks = new ThemeCardCornerMarksDefinition { Style = "tick", Thickness = fraction };
        using var style = ResolvedCardStyle.Resolve(With(card), 275);

        float actual = key switch
        {
            "card.highlight.thickness" => style.Highlight!.Thickness,
            "card.accentStrip.thickness" => style.Strip!.Thickness,
            _ => style.Marks!.Thickness,
        };
        Assert.Equal(expected, actual, 3);
    }

    [Fact]
    public void AHighlightUnderAPixel_IsWarnedOnce()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Highlight = new ThemeCardHighlightDefinition { Top = "#ffffff", Thickness = 0.001 },
        }, name: "thin-highlight");

        var lines = Warnings(() =>
        {
            ResolvedCardStyle.Resolve(loaded, 275).Dispose();
            ResolvedCardStyle.Resolve(loaded, 275).Dispose();
        });

        Assert.Contains("card.highlight.thickness", Assert.Single(lines));
    }

    [Fact]
    public void TheStripColour_GoesRoundTheList_ByIndex()
    {
        var strip = new CardStrip(CardSide.Bottom, 4, 1, Tints, false, 0, null, 0.6f);

        Assert.Equal([0, 1, 2, 0, 1, 0], new[] { 0, 1, 2, 3, 4, -1 }.Select(strip.Slot));
    }

    [Fact]
    public void FiveCards_TakeFourStripColoursInOrder_AndTheFifthTheFirst()
    {
        string[] colours = ["#ff0000", "#00ff00", "#0000ff", "#ffff00"];
        var loaded = Front(new ThemeCardDefinition
        {
            AccentStrip = new ThemeCardAccentStripDefinition { Side = "bottom", Thickness = 0.04, Length = 1, Colors = [.. colours] },
        });

        using var bitmap = PageRenderer.RenderToBitmap(1100, 1100, [Card(0, 0), Card(2, 0), Card(0, 1), Card(2, 1), Card(0, 2)], loaded, DateTime.Now);

        // Down from the card's centre to where the fill ends: the strip, 11 pixels thick.
        SKColor Strip(int col, int row)
        {
            int x = col * 275 + 275, y = row * 275 + 137;
            while (bitmap.GetPixel(x, y) == Black)
                y++;
            return bitmap.GetPixel(x, y + 4);
        }
        Assert.Equal(SKColor.Parse(colours[0]), Strip(0, 0));
        Assert.Equal(SKColor.Parse(colours[1]), Strip(2, 0));
        Assert.Equal(SKColor.Parse(colours[2]), Strip(0, 1));
        Assert.Equal(SKColor.Parse(colours[3]), Strip(2, 1));
        Assert.Equal(SKColor.Parse(colours[0]), Strip(0, 2));
    }

    [Fact]
    public void TheBackImage_IsMadePerStripColour()
    {
        var loaded = Front(new ThemeCardDefinition
        {
            AccentStrip = new ThemeCardAccentStripDefinition { Colors = ["#ff0000", "#00ff00"] },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        var size = new System.Drawing.Size(100, 100);

        foreach (int index in new[] { 0, 1, 2, 3, 0 })
            style.For(index, size);

        Assert.Equal(2, style.BackImagesMade);
    }

    [Fact]
    public void TheTopHighlight_IsOverTheContent_AndFadesAtBothEnds()
    {
        var loaded = Front(new ThemeCardDefinition
        {
            Highlight = new ThemeCardHighlightDefinition { Top = "#ffffff", Thickness = 0.02 },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Red, loaded, 400, 200, style);

        Assert.Equal(SKColors.White, bitmap.GetPixel(200, 2));
        Assert.Equal(SKColors.White, bitmap.GetPixel(60, 2));
        // Gone at the very ends, half way through the fade over the first and last eighth.
        AssertNear(SKColors.Red, bitmap.GetPixel(0, 2), 8);
        AssertNear(SKColors.Red, bitmap.GetPixel(399, 2), 8);
        AssertNear(new SKColor(255, 128, 128), bitmap.GetPixel(25, 2), 8);
        // Only along the top: below the line, and along the bottom, the content.
        Assert.Equal(SKColors.Red, bitmap.GetPixel(200, 8));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(200, 198));
    }

    [Fact]
    public void TheBottomHighlight_FollowsARoundedCorner()
    {
        var loaded = Front(new ThemeCardDefinition
        {
            Radius = 0.2,
            Highlight = new ThemeCardHighlightDefinition { Bottom = "#ffffff", Thickness = 0.02 },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Red, loaded, 400, 200, style);

        Assert.Equal(SKColors.White, bitmap.GetPixel(200, 197));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(200, 2));
        // Round the corner (radius 55) the line rises with the edge: at 45 degrees it is inside the arc, lit.
        float r = 55, c = r - r / MathF.Sqrt(2);
        var onArc = bitmap.GetPixel((int)(400 - c - 2), (int)(200 - c - 2));
        Assert.True(onArc.Green > 40, $"got {onArc}");
        // Outside the shape nothing is drawn.
        Assert.Equal(0, bitmap.GetPixel(399, 199).Alpha);
    }

    [Fact]
    public void TheSheen_IsLightestAtItsCentre_AndGoneAtItsSize()
    {
        var loaded = Front(new ThemeCardDefinition
        {
            Sheen = new ThemeCardSheenDefinition { Color = "#ffffffff", X = 0.05, Y = 0, Size = 0.7 },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Empty, loaded, 400, 200, style);

        byte at(int x, int y) => bitmap.GetPixel(x, y).Red;
        Assert.True(at(20, 0) > at(100, 0) && at(100, 0) > at(200, 0), "lightest near the top-left");
        Assert.True(at(20, 0) > at(20, 100));
        // 0.7 of the width from (20, 0) is 280: past it, the fill.
        Assert.Equal(Black, bitmap.GetPixel(310, 0));
        Assert.Equal(Black, bitmap.GetPixel(399, 199));
    }

    [Fact]
    public void TheSheen_IsUnderTheContent()
    {
        var loaded = Front(new ThemeCardDefinition { Sheen = new ThemeCardSheenDefinition { Color = "#ffffffff", Size = 1 } });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Red, loaded, 200, 200, style);

        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 0));
    }

    [Theory]
    [InlineData("top")]
    [InlineData("bottom")]
    [InlineData("left")]
    [InlineData("right")]
    public void TheStrip_RunsAlongItsSide_Centred(string side)
    {
        var loaded = Front(new ThemeCardDefinition
        {
            AccentStrip = new ThemeCardAccentStripDefinition { Side = side, Thickness = 0.04, Length = 0.5, Colors = ["#ff0000"] },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Empty, loaded, 400, 200, style);

        // 11 pixels thick; half the side long, from a quarter to three quarters.
        bool across = side is "top" or "bottom";
        int length = across ? 400 : 200;
        SKColor At(int along, int depth) => side switch
        {
            "top" => bitmap.GetPixel(along, depth),
            "bottom" => bitmap.GetPixel(along, 199 - depth),
            "left" => bitmap.GetPixel(depth, along),
            _ => bitmap.GetPixel(399 - depth, along),
        };
        Assert.Equal(SKColors.Red, At(length / 2, 0));
        Assert.Equal(SKColors.Red, At(length / 4 + 1, 9));
        Assert.Equal(SKColors.Red, At(length * 3 / 4 - 2, 9));
        Assert.Equal(Black, At(length / 4 - 2, 0));
        Assert.Equal(Black, At(length * 3 / 4 + 1, 0));
        Assert.Equal(Black, At(length / 2, 12));
    }

    [Fact]
    public void AFadingStrip_IsStrongestAtItsStart_AndGoneAtItsEnd()
    {
        var loaded = Front(new ThemeCardDefinition
        {
            AccentStrip = new ThemeCardAccentStripDefinition { Side = "bottom", Thickness = 0.04, Length = 1, Colors = ["#ff0000"], Fade = true },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Empty, loaded, 400, 200, style);

        AssertNear(SKColors.Red, bitmap.GetPixel(0, 195), 3);
        AssertNear(Black, bitmap.GetPixel(399, 195), 3);
        AssertNear(new SKColor(128, 0, 0), bitmap.GetPixel(200, 195), 3);
    }

    [Fact]
    public void TheStrip_IsUnderTheContent_AndItsGlowInsideTheCard()
    {
        var loaded = Front(new ThemeCardDefinition
        {
            AccentStrip = new ThemeCardAccentStripDefinition
            {
                Side = "bottom",
                Thickness = 0.02,
                Length = 1,
                Colors = ["#ff0000"],
                Glow = new ThemeGlowDefinition { Blur = 0.1, Alpha = 1 },
            },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var glowing = Paint(Empty, loaded, 400, 200, style);
        using var covered = Paint(Red, loaded, 400, 200, style);

        // Above the strip, the glow tints the fill red.
        var above = glowing.GetPixel(200, 185);
        Assert.True(above.Red > 0 && above.Green == 0, $"got {above}");
        Assert.Equal(SKColors.Red, covered.GetPixel(200, 199));
        Assert.Equal(SKColors.Red, covered.GetPixel(200, 185));
    }

    private static LoadedTheme Marks(double inset, double thickness, string style = "bracket") => Front(new ThemeCardDefinition
    {
        CornerMarks = new ThemeCardCornerMarksDefinition
        {
            Style = style,
            Size = 0.1,
            Thickness = thickness,
            Inset = inset,
            Color = "#00ff00",
        },
    });

    [Fact]
    public void Brackets_AreOverTheContent_AtTopLeftAndBottomRight()
    {
        var loaded = Marks(0.02, 0.02);
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Red, loaded, 400, 200, style);

        // Inset 5.5, thickness 5.5: the line covers 5.5 to 11 from each edge; arms 27.5 long from the outer corner.
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(8, 20));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(20, 8));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(7, 7));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(391, 180));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(380, 191));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(2, 20));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(8, 36));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(391, 8));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(8, 191));
    }

    [Fact]
    public void ATick_IsOneLineAlongTheTopOrBottomEdge()
    {
        var loaded = Marks(0.02, 0.02, "tick");
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Red, loaded, 400, 200, style);

        Assert.Equal(SKColors.Lime, bitmap.GetPixel(20, 8));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(8, 20));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(380, 191));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(391, 180));
    }

    [Fact]
    public void AMarkInsetByMinusHalfItsThickness_IsHalfOnTheCard()
    {
        var loaded = Marks(-0.02, 0.04);
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var bitmap = Paint(Red, loaded, 400, 200, style);

        // Thickness 11, inset -5.5: 5.5 pixels of the line lie on the card, over the content.
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(20, 0));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(20, 4));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 7));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(4, 20));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(7, 20));
    }

    [Fact]
    public void AMarkWhollyOutsideTheCard_IsNotDrawnOnIt()
    {
        var loaded = Marks(-0.05, 0.02);
        using var style = ResolvedCardStyle.Resolve(loaded, 275);
        using var chrome = new SizedCardChrome(style, -1, 0, new System.Drawing.Size(400, 200));

        Assert.Null(chrome.Marks);
    }

    [Fact]
    public void ASnapshot_CutsAMarkAtTheCardsEdge()
    {
        var loaded = Marks(-0.02, 0.04);
        using var bitmap = PageRenderer.RenderToBitmap(1100, 1100, [Card(0, 1)], loaded, DateTime.Now);

        // The card's top and left edges, found from its fill; the half of the bracket outside them belongs to the underlay
        // (card-underlay), not the surface.
        int top = Enumerable.Range(250, 60).First(y => bitmap.GetPixel(150, y) == Black);
        int left = Enumerable.Range(0, 60).First(x => bitmap.GetPixel(x, top + 40) == Black);
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(left + 10, top + 1));
        Assert.NotEqual(SKColors.Lime, bitmap.GetPixel(left + 10, top - 2));
        Assert.NotEqual(SKColors.Lime, bitmap.GetPixel(left - 2, top + 10));
    }
}
