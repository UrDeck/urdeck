// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>card.borderAnimate resolved on the card style (motion): which borders the host can move.</summary>
public sealed class BorderAnimationResolveTests
{
    private static readonly LoadedTheme Dark = new ThemeStore("").Load(ThemeStore.DefaultName);

    private static ThemeCardDefinition Card(string? mode, double? period = null, string? gradientKind = "conic", double borderWidth = 0.02, string? borderStyle = null) => new()
    {
        BorderWidth = borderWidth,
        BorderStyle = borderStyle,
        BorderGradient = gradientKind == null ? null : new ThemeCardBorderGradientDefinition { Colors = ["#ff0000", "#0000ff", "#ff0000"], Kind = gradientKind },
        BorderAnimate = mode == null ? null : new ThemeCardBorderAnimateDefinition { Mode = mode, Period = period },
    };

    private static ResolvedCardStyle Resolve(ThemeCardDefinition card, ThemeCardCompactDefinition? compact = null)
    {
        card.Compact = compact;
        var def = new ThemeDefinition { Card = card }.MergeOver(Dark.Definition);
        return ResolvedCardStyle.Resolve(new LoadedTheme("anim-test", def, Dark.Font), 100);
    }

    [Fact]
    public void ASpinningConicBorder_ResolvesModeAndPeriod()
    {
        using var style = Resolve(Card("spin", 7));

        Assert.Equal(new BorderAnimation(BorderAnimationMode.Spin, 7), style.BorderAnimation);
    }

    [Fact]
    public void APeriodDefaultsToSeven()
    {
        using var style = Resolve(Card("spin"));

        Assert.Equal(7, style.BorderAnimation!.PeriodSeconds);
    }

    [Theory]
    [InlineData("linear")]
    [InlineData("conic")]
    [InlineData(null)]
    public void APulse_NeedsOnlyABorder(string? kind)
    {
        using var style = Resolve(Card("pulse", 3, kind));

        Assert.Equal(new BorderAnimation(BorderAnimationMode.Pulse, 3), style.BorderAnimation);
    }

    [Fact]
    public void ModeNone_ResolvesNull()
    {
        using var style = Resolve(Card("none"));

        Assert.Null(style.BorderAnimation);
    }

    [Fact]
    public void NoAnimation_ResolvesNull()
    {
        using var style = Resolve(Card(null));

        Assert.Null(style.BorderAnimation);
    }

    [Fact]
    public void ASpinWithoutAConicGradient_ResolvesNull()
    {
        using var style = Resolve(Card("spin", 7, "linear"));

        Assert.Null(style.BorderAnimation);
    }

    [Theory]
    [InlineData("spin")]
    [InlineData("pulse")]
    public void ADashedBorder_ResolvesNull(string mode)
    {
        using var style = Resolve(Card(mode, 7, "conic", borderStyle: "dashed"));

        Assert.Null(style.BorderAnimation);
    }

    [Theory]
    [InlineData("spin")]
    [InlineData("pulse")]
    public void ABorderWidthOfZero_ResolvesNull(string mode)
    {
        using var style = Resolve(Card(mode, 7, "conic", borderWidth: 0));

        Assert.Null(style.BorderAnimation);
    }

    [Fact]
    public void TheCompactStyle_ResolvesItsOwnWidth()
    {
        using var style = Resolve(Card("spin", 7), new ThemeCardCompactDefinition { BorderWidth = 0 });

        Assert.NotNull(style.BorderAnimation);
        Assert.Null(style.Compact.BorderAnimation);
    }

    [Fact]
    public void TheCompactStyle_KeepsTheAnimationOtherwise()
    {
        using var style = Resolve(Card("pulse", 4), new ThemeCardCompactDefinition { Radius = 0.1 });

        Assert.Equal(style.BorderAnimation, style.Compact.BorderAnimation);
    }
}
