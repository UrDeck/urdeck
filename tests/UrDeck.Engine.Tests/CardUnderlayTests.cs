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
/// What a theme draws outside its cards (card-underlay): shadow, glow and the outside part of the corner marks, the reach limit
/// on translucent cards, the pictures the host stretches, and the snapshot's pass under the cards.
/// </summary>
public sealed class CardUnderlayTests
{
    private const float Cell = 275;

    private static readonly LoadedTheme Dark = new ThemeStore("").Load(ThemeStore.DefaultName);

    private static readonly SKColor Red = SKColor.Parse("#ff0000");

    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "themes");

    private static LoadedTheme Draft(string name) => new ThemeStore(ThemeStore.BuiltInNames.Contains(name) ? "" : FixtureFolder).Load(name);

    /// <summary>The default theme with <paramref name="card"/> and <paramref name="colors"/> over it, as a theme of its own.</summary>
    private static LoadedTheme With(ThemeCardDefinition card, ThemeColorsDefinition? colors = null, string name = "underlay-test")
    {
        var def = new ThemeDefinition { Card = card, Colors = colors }.MergeOver(Dark.Definition);
        return new LoadedTheme(name, def, Dark.Font);
    }

    /// <summary>Square opaque red cards without a border, and <paramref name="shadow"/>.</summary>
    private static LoadedTheme Shadowed(ThemeCardShadowDefinition shadow, string fill = "#ff0000", string name = "shadowed") => With(
        new ThemeCardDefinition { Radius = 0, BorderWidth = 0, Shape = "round", Shadow = shadow },
        new ThemeColorsDefinition { CardFill = fill },
        name);

    private sealed class Probe(Action<WidgetRenderContext> render) : Widget<WidgetConfig>
    {
        public override void Render(WidgetRenderContext context) => render(context);
    }

    private static (WidgetConfig, IWidget) Card(int col, int row, int width, int height) =>
        (new WidgetConfig { WidgetTypeId = "t", Col = col, Row = row, Width = width, Height = height }, new Probe(_ => { }));

    private static WidgetLayoutItem LayoutOf(LoadedTheme loaded, int size, params (WidgetConfig Config, IWidget Widget)[] widgets) =>
        new GridLayoutManager(size, size, loaded.Definition.Card!.Gap!.Value, size)
            .RenderWidgetLayout(widgets.Select(w => w.Config).ToList(), new System.Drawing.Size(size, size))[0];

    private static List<string> Lines(string level, Action action)
    {
        var lines = new List<string>();
        UrDeckLog.Listener = (l, message) =>
        {
            if (l == level)
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

    // ---- The resolved style: bleed, zone, reach

    /// <summary>The bleed and corner zone of each draft at a 275 px cell, as <c>docs/perf/card-chrome.md</c> lists them (Prism limited).</summary>
    [Theory]
    [InlineData("carbon", 8, 30)]
    [InlineData("blueprint", 17, 27)]
    [InlineData("paper", 95, 163)]
    [InlineData("pop", 16, 51)]
    [InlineData("prism", 25, 99)]
    [InlineData("rigline", 99, 139)]
    [InlineData("ember", 0, 0)]
    [InlineData("obsidian", 0, 0)]
    public void EachDraft_HasTheBleedAndZoneOfThePerfNotes(string name, int bleed, int zone)
    {
        using var style = ResolvedCardStyle.Resolve(Draft(name), Cell);

        Assert.Equal(bleed, style.Bleed);
        Assert.Equal(zone, style.CornerZone);
        Assert.Equal(bleed > 0, style.HasOutside);
    }

    [Fact]
    public void PrismWithOpaqueCards_KeepsItsOwnShadow_BecauseItAlreadyFitsTheGap()
    {
        var loaded = Draft("prism");
        loaded.Definition.Card!.FillGradient = null;
        loaded.Definition.Colors!.CardFill = "#202040";

        using var style = ResolvedCardStyle.Resolve(loaded, Cell);

        Assert.Equal(25, style.Bleed);
        Assert.Equal(99, style.CornerZone);
    }

    [Fact]
    public void PrismsShadow_FitsTheGapOnItsOwn_SoNothingIsLimitedAndNothingIsLogged()
    {
        var loaded = Draft("prism");
        ResolvedCardStyle? style = null;

        var lines = Lines("INFO", () => style = ResolvedCardStyle.Resolve(loaded, Cell));

        using (style)
        {
            var shadow = style!.Shadow!;
            Assert.True(shadow.Reach <= 0.09f * Cell, "the shadow reaches no further than the gap");
            Assert.Equal(0.02f / 0.046f, shadow.Y / shadow.Blur, 3);
        }
        Assert.DoesNotContain(lines, l => l.Contains("translucent"));
    }

    [Fact]
    public void AnOpaqueThemesSoftShadow_KeepsItsReach_WithoutALine()
    {
        ResolvedCardStyle? style = null;
        var lines = Lines("INFO", () => style = ResolvedCardStyle.Resolve(Draft("paper"), Cell));

        using (style)
            Assert.Equal(95, style!.Bleed);
        Assert.DoesNotContain(lines, l => l.Contains("translucent"));
    }

    [Fact]
    public void ATranslucentTint_LimitsTheReach()
    {
        var loaded = With(
            new ThemeCardDefinition { Shape = "round", Tint = "cycle", Shadow = new ThemeCardShadowDefinition { Y = 0.2, Blur = 0.2 } },
            new ThemeColorsDefinition { CardFill = "#202020", CardTints = ["#ff0000", "#8000ff00"] },
            "translucent-tint");

        using var style = ResolvedCardStyle.Resolve(loaded, Cell);

        Assert.Equal((int)MathF.Ceiling((float)loaded.Definition.Card!.Gap!.Value * Cell), style.Bleed);
    }

    [Fact]
    public void TheGlow_TakesTheBorderColour_AtItsAlpha()
    {
        var loaded = With(
            new ThemeCardDefinition { Shape = "round", Glow = new ThemeGlowDefinition { Blur = 0.1, Alpha = 0.5 } },
            new ThemeColorsDefinition { CardFill = "#202020", CardBorder = "#00ff00" });

        using var style = ResolvedCardStyle.Resolve(loaded, Cell);

        Assert.Equal(new SKColor(0, 255, 0, 128), style.Glow!.Color);
        Assert.Equal((int)MathF.Ceiling(0.15f * Cell), style.Bleed);
    }

    [Fact]
    public void AGlowWithoutBlur_AndMarksInside_DrawNothingOutside()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Shape = "round",
            Glow = new ThemeGlowDefinition { Alpha = 1 },
            CornerMarks = new ThemeCardCornerMarksDefinition { Style = "bracket", Inset = 0.02 },
        });

        using var style = ResolvedCardStyle.Resolve(loaded, Cell);

        Assert.False(style.HasOutside);
        Assert.Null(style.Underlay(new System.Drawing.Size(500, 250)));
    }

    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("glass")]
    public void AVersion1Theme_MakesNothing(string name)
    {
        using var style = ResolvedCardStyle.Resolve(new ThemeStore("").Load(name), Cell);

        Assert.False(style.HasOutside);
        Assert.Null(style.Underlay(new System.Drawing.Size(250, 250)));
        Assert.Equal(0, style.UnderlayPicturesMade);
    }

    // ---- Pictures

    [Fact]
    public void BigCards_ShareOneNineGridPicture_AndASmallCardGetsItsOwn()
    {
        using var style = ResolvedCardStyle.Resolve(Draft("paper"), Cell);

        var wide = style.Underlay(new System.Drawing.Size(1075, 525))!;
        var square = style.Underlay(new System.Drawing.Size(525, 525))!;
        var small = style.Underlay(new System.Drawing.Size(250, 250))!;
        var again = style.Underlay(new System.Drawing.Size(250, 250))!;

        Assert.Same(wide, square);
        Assert.True(wide.IsNineGrid);
        Assert.False(small.IsNineGrid);
        Assert.Same(small, again);
        Assert.Equal(2, style.UnderlayPicturesMade);
        Assert.Equal(new System.Drawing.Size(250, 250), small.Card);
    }

    [Fact]
    public void ASoftPicture_IsHalfSize_AndItsInsetIsInItsOwnPixels()
    {
        using var style = ResolvedCardStyle.Resolve(Draft("paper"), Cell);

        var picture = style.Underlay(new System.Drawing.Size(1075, 525))!;

        // 518 px round a card of 2 x 163 + 2 at full size (docs/perf/card-chrome.md), half of it here.
        Assert.Equal(0.5f, picture.Scale);
        Assert.Equal(259, picture.PixelWidth);
        Assert.Equal(259, picture.PixelHeight);
        Assert.Equal((95 + 163) / 2f, picture.Inset);
        using var bitmap = picture.ToBitmap();
        Assert.Equal(259, bitmap.Width);
    }

    [Theory]
    [InlineData("pop")]
    [InlineData("blueprint")]
    public void APictureWithAHardShadowOrMarks_IsFullSize(string name)
    {
        using var style = ResolvedCardStyle.Resolve(Draft(name), Cell);

        var picture = style.Underlay(new System.Drawing.Size(250, 250))!;

        Assert.Equal(1f, picture.Scale);
        Assert.Equal(2 * style.CornerZone + 2 + 2 * style.Bleed, picture.PixelWidth);
    }

    [Fact]
    public void ATranslucentCardsOwnShadow_IsCutOutOfItsPicture()
    {
        using var style = ResolvedCardStyle.Resolve(Draft("prism"), Cell);
        var picture = style.Underlay(new System.Drawing.Size(250, 250))!;

        using var bitmap = picture.ToBitmap();
        float s = picture.Scale;
        int bleed = picture.Bleed, side = picture.Card.Width;

        // The card's middle is empty; just below the card the shadow shows.
        Assert.Equal(0, bitmap.GetPixel((int)((bleed + side / 2) * s), (int)((bleed + side / 2) * s)).Alpha);
        Assert.True(bitmap.GetPixel((int)((bleed + side / 2) * s), (int)((bleed + side + 6) * s)).Alpha > 0);
    }

    // ---- The snapshot

    [Fact]
    public void AHardShadow_ShowsBesideTheCard_AndNotUnderItsContent()
    {
        var loaded = Shadowed(new ThemeCardShadowDefinition { X = 0.055, Y = 0.055, Blur = 0, Color = "#000000" });
        var widget = Card(0, 0, 2, 1);
        var item = LayoutOf(loaded, 1100, widget);
        int offset = (int)MathF.Round(0.055f * Cell);

        using var bitmap = PageRenderer.RenderToBitmap(1100, 1100, [widget], loaded, DateTime.Now);
        var background = SKColor.Parse(loaded.Definition.Colors!.Background);
        int right = item.Position.X + item.Size.Width, bottom = item.Position.Y + item.Size.Height;

        Assert.Equal(SKColors.Black, bitmap.GetPixel(right + offset / 2, item.Position.Y + item.Size.Height / 2));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(item.Position.X + item.Size.Width / 2, bottom + offset / 2));
        Assert.Equal(background, bitmap.GetPixel(right + offset / 2, item.Position.Y + offset / 2));
        Assert.Equal(Red, bitmap.GetPixel(right - 2, bottom - 2));
    }

    [Fact]
    public void AMarkAcrossTheEdge_IsDrawnUnbroken()
    {
        var loaded = With(
            new ThemeCardDefinition
            {
                Radius = 0,
                BorderWidth = 0,
                Shape = "round",
                CornerMarks = new ThemeCardCornerMarksDefinition { Style = "bracket", Inset = -0.02, Thickness = 0.04, Size = 0.1, Color = "#00ff00" },
            },
            new ThemeColorsDefinition { CardFill = "#ff0000" });
        var widget = Card(1, 1, 2, 1);
        var item = LayoutOf(loaded, 1100, widget);

        using var bitmap = PageRenderer.RenderToBitmap(1100, 1100, [widget], loaded, DateTime.Now);

        // The top-left bracket's horizontal arm is 11 px thick and centred on the card's top edge.
        int x = item.Position.X + 15;
        for (int y = item.Position.Y - 4; y <= item.Position.Y + 4; y++)
            Assert.Equal(SKColors.Lime, bitmap.GetPixel(x, y));
    }

    [Fact]
    public void TheDocksShadow_StaysInsideItsBand()
    {
        var loaded = Shadowed(new ThemeCardShadowDefinition { Y = -0.4, Blur = 0, Color = "#000000" }, name: "shadow-up");
        var dock = new List<(WidgetConfig, IWidget?)> { (new WidgetConfig { WidgetTypeId = "t" }, new Probe(_ => { })) };
        var frame = ChromeLayout.Compute(new System.Drawing.Size(1100, 3840), IndicatorMode.Off, 1,
            loaded.Definition.Indicator!.BandHeight!.Value, loaded.Definition.Dock!.Height!.Value);
        var slot = DockLayout.Compute(frame.Dock, 1, loaded.Definition.Card!.Gap!.Value)[0];

        using var bitmap = PageRenderer.RenderToBitmap(1100, 3840, [], loaded, DateTime.Now, null, dock);
        var background = SKColor.Parse(loaded.Definition.Colors!.Background);
        int x = slot.Position.X + slot.Size.Width / 2;

        Assert.Equal(SKColors.Black, bitmap.GetPixel(x, frame.Dock.Top + 1));
        Assert.Equal(background, bitmap.GetPixel(x, frame.Dock.Top - 2));
    }

}
