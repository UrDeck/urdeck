// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The list widget drawn from the system provider's sample (the busiest processes) with the built-in themes, compared with the
/// pictures in <c>Goldens/</c>: a 2x2 in every theme, and the other sizes in the default dark one. Windows only: FreeType and
/// DirectWrite rasterise text differently. See CONTRIBUTING.md, "Golden images".
/// </summary>
public sealed class ListGoldenTests
{
    private static SKBitmap Render(string theme, int cellsWide, int cellsHigh)
    {
        var request = new PreviewRequest
        {
            Widget = new PreviewWidget("urdeck.widgets.list", new System.Drawing.Size(cellsWide, cellsHigh)),
            Theme = new ThemeStore("").Load(theme),
            Background = PreviewBackground.None,
        };
        return new PreviewRenderer().Render(request);
    }

    [WindowsGlyphTheory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("glass")]
    [InlineData("carbon")]
    [InlineData("paper")]
    [InlineData("prism")]
    [InlineData("obsidian")]
    [InlineData("ember")]
    [InlineData("blueprint")]
    [InlineData("pop")]
    [InlineData("rigline")]
    public void TwoByTwo(string theme)
    {
        using var bitmap = Render(theme, 2, 2);
        GoldenImage.Compare(bitmap, $"list-2x2-{theme}");
    }

    [WindowsGlyphTheory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(4, 2)]
    [InlineData(4, 4)]
    public void OtherSizes(int cellsWide, int cellsHigh)
    {
        using var bitmap = Render("default-dark", cellsWide, cellsHigh);
        GoldenImage.Compare(bitmap, $"list-{cellsWide}x{cellsHigh}-default-dark");
    }
}
