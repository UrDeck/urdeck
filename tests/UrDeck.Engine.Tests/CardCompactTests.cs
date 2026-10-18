// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>Compact cards (card-compact): dock slots and page cards of one cell take the theme's <c>card.compact</c> values.</summary>
public sealed class CardCompactTests
{
    private static readonly LoadedTheme Dark = new ThemeStore("").Load(ThemeStore.DefaultName);

    private static LoadedTheme With(ThemeCardDefinition card, string name = "compact-test")
    {
        var def = new ThemeDefinition { Card = card }.MergeOver(Dark.Definition);
        return new LoadedTheme(name, def, Dark.Font);
    }

    private static WidgetLayoutItem Item(int cols, int rows) => new() { GridSize = new System.Drawing.Size(cols, rows) };

    [Fact]
    public void ATheme_WithoutCompactValues_MakesNoVariant()
    {
        using var style = ResolvedCardStyle.Resolve(With(new ThemeCardDefinition { Radius = 0.2 }), 100);

        Assert.Same(style, style.Compact);
        Assert.Same(style, style.ForCard(Item(1, 1)));
    }

    [Fact]
    public void EachOverride_ReplacesOnlyItsOwnValue()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Radius = 0.27,
            BorderWidth = 0.02,
            Compact = new ThemeCardCompactDefinition { Radius = 0.1 },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 100);

        Assert.Equal(27f, style.Radius, 0.01f);
        Assert.Equal(10f, style.Compact.Radius, 0.01f);
        Assert.Equal(style.BorderWidth, style.Compact.BorderWidth);

        loaded = With(new ThemeCardDefinition
        {
            Radius = 0.27,
            BorderWidth = 0.02,
            Compact = new ThemeCardCompactDefinition { BorderWidth = 0.04 },
        });
        using var borderStyle = ResolvedCardStyle.Resolve(loaded, 100);

        Assert.Equal(4f, borderStyle.Compact.BorderWidth, 0.01f);
        Assert.Equal(27f, borderStyle.Compact.Radius, 0.01f);
        Assert.Equal(2f, borderStyle.BorderWidth, 0.01f);
    }

    [Fact]
    public void AOneCellCard_IsCompact_AndALargerOneIsNot()
    {
        var loaded = With(new ThemeCardDefinition { Radius = 0.27, Compact = new ThemeCardCompactDefinition { Radius = 0.24 } });
        using var style = ResolvedCardStyle.Resolve(loaded, 100);

        Assert.Same(style.Compact, style.ForCard(Item(1, 1)));
        Assert.Same(style, style.ForCard(Item(2, 2)));
        Assert.Same(style, style.ForCard(Item(2, 1)));
        Assert.Equal(24f, style.ForCard(Item(1, 1)).Radius, 0.01f);
        Assert.Equal(27f, style.ForCard(Item(2, 2)).Radius, 0.01f);
    }

    [Fact]
    public void ACompactStrip_IsMergedKeyByKey_OverThePageStrip()
    {
        var loaded = With(new ThemeCardDefinition
        {
            AccentStrip = new ThemeCardAccentStripDefinition { Side = "top", Thickness = 0.03, Fade = true },
            Compact = new ThemeCardCompactDefinition { AccentStrip = new ThemeCardCompactAccentStripDefinition { Thickness = 0.06 } },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 100);

        Assert.Equal(CardSide.Top, style.Strip!.Side);
        Assert.Equal(3, style.Strip.Thickness);
        var compact = style.Compact.Strip!;
        Assert.Equal(CardSide.Top, compact.Side);
        Assert.Equal(6, compact.Thickness);
        Assert.True(compact.Fade);
    }

    [Fact]
    public void AStripOnCompactCardsOnly_LeavesLargerCardsWithout()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Compact = new ThemeCardCompactDefinition { AccentStrip = new ThemeCardCompactAccentStripDefinition { Colors = ["#ff0000", "#00ff00"] } },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 100);

        Assert.Null(style.Strip);
        Assert.NotNull(style.Compact.Strip);
        Assert.Equal(2, style.Compact.Strip.Colors.Length);
    }

    [Fact]
    public void CompactMarks_AreMergedKeyByKey_OverThePageMarks()
    {
        var loaded = With(new ThemeCardDefinition
        {
            CornerMarks = new ThemeCardCornerMarksDefinition { Style = "bracket", Thickness = 0.014, Color = "#ffff00" },
            Compact = new ThemeCardCompactDefinition
            {
                CornerMarks = new ThemeCardCompactCornerMarksDefinition { Style = "tick", Corners = ["bl"], Size = 0.064, Inset = 0.036 },
            },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 1000);

        Assert.Equal(CornerMarkStyle.Bracket, style.Marks!.Style);
        var compact = style.Compact.Marks!;
        Assert.Equal(CornerMarkStyle.Tick, compact.Style);
        Assert.Equal(CardCorners.BottomLeft, compact.Corners);
        Assert.Equal(64, compact.Size);
        Assert.Equal(36, compact.Inset);
        Assert.Equal(14, compact.Thickness);
        Assert.Equal(SKColor.Parse("#ffff00"), compact.Color);
    }

    [Fact]
    public void ACircle_IsHalfTheCardsSmallerSide_AndAChamferPageKeepsItsCut()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Shape = "chamfer",
            Cut = 0.14,
            Compact = new ThemeCardCompactDefinition { Shape = "circle" },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 100);

        Assert.True(style.Chamfer);
        Assert.False(style.Compact.Chamfer);

        using var round = CardShape.For(style.Compact, new System.Drawing.Size(90, 90));
        Assert.Equal(45, round.Round!.Radii[0].X);
        using var pill = CardShape.For(style.Compact, new System.Drawing.Size(90, 60));
        Assert.Equal(30, pill.Round!.Radii[0].X);
        using var cut = CardShape.For(style, new System.Drawing.Size(200, 200));
        Assert.NotNull(cut.Path);
    }

    [Fact]
    public void ACircleOnACardThatIsNotSquare_LogsOneInformationLine()
    {
        var loaded = With(new ThemeCardDefinition { Compact = new ThemeCardCompactDefinition { Shape = "circle" } }, "capsule-test");
        var lines = new List<string>();
        UrDeckLog.Listener = (level, message) =>
        {
            if (level == "INFO" && message.Contains("capsule", StringComparison.Ordinal))
                lines.Add(message);
        };
        try
        {
            using var style = ResolvedCardStyle.Resolve(loaded, 100);
            style.Compact.For(0, new System.Drawing.Size(90, 90));
            Assert.Empty(lines);
            style.Compact.For(0, new System.Drawing.Size(90, 60));
            style.Compact.For(1, new System.Drawing.Size(80, 50));
        }
        finally
        {
            UrDeckLog.Listener = null;
        }

        Assert.Single(lines);
    }

    [Fact]
    public void ADocksTheme_TakesTheCompactRadiusAndBorderWidth_ButAPagesDoesNot()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Radius = 0.27,
            BorderWidth = 0.02,
            Padding = 0.1,
            Compact = new ThemeCardCompactDefinition { Radius = 0.1, BorderWidth = 0.04 },
        });
        using var page = ThemeResolver.Resolve(loaded, 100);
        using var dock = ThemeResolver.Resolve(loaded, 100, compact: true);

        Assert.Equal(27f, page.CardRadius, 0.01f);
        Assert.Equal(2, page.CardBorderWidth);
        Assert.Equal(10f, dock.CardRadius, 0.01f);
        Assert.Equal(4f, dock.CardBorderWidth, 0.01f);
        Assert.Equal(page.Padding, dock.Padding);
    }

    [Fact]
    public void ADocksTheme_OfACircle_HasHalfTheSlot()
    {
        var loaded = With(new ThemeCardDefinition { Compact = new ThemeCardCompactDefinition { Shape = "circle" } });
        using var dock = ThemeResolver.Resolve(loaded, 80, compact: true);

        Assert.Equal(40f, dock.CardRadius, 0.01f);
    }

    [Fact]
    public void ADocksTheme_WithoutCompactValues_IsThePagesTheme()
    {
        var loaded = With(new ThemeCardDefinition { Radius = 0.2 });
        using var dock = ThemeResolver.Resolve(loaded, 100, compact: true);

        Assert.Equal(20f, dock.CardRadius, 0.01f);
    }

    [Fact]
    public void TheCompactVariant_HasItsOwnUnderlay()
    {
        var loaded = With(new ThemeCardDefinition
        {
            Shadow = new ThemeCardShadowDefinition { Blur = 0.1, Y = 0.05 },
            Compact = new ThemeCardCompactDefinition { Radius = 0.5 },
        });
        using var style = ResolvedCardStyle.Resolve(loaded, 200);

        var page = style.Underlay(new System.Drawing.Size(400, 400));
        var compact = style.Compact.Underlay(new System.Drawing.Size(180, 180));

        Assert.NotNull(page);
        Assert.NotNull(compact);
        Assert.NotSame(page, compact);
        Assert.Equal(1, style.UnderlayPicturesMade);
        Assert.Equal(1, style.Compact.UnderlayPicturesMade);
    }
}
