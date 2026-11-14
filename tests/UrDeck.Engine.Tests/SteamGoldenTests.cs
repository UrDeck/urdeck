// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The Steam widget drawn from the Steam provider's sample (a persona playing a game with an update downloading, friends online
/// and playing, recent games) with the built-in themes, compared with the pictures in <c>Goldens/</c>: a 4x4 in every theme, and
/// the other sizes in the default dark one. Windows only: FreeType and DirectWrite rasterise text differently. See CONTRIBUTING.md,
/// "Golden images".
/// </summary>
public sealed class SteamGoldenTests
{
    private static SKBitmap Render(string theme, int cellsWide, int cellsHigh)
    {
        var request = new PreviewRequest
        {
            Widget = new PreviewWidget("urdeck.widgets.steam", new System.Drawing.Size(cellsWide, cellsHigh)),
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
    public void FourByFour(string theme)
    {
        using var bitmap = Render(theme, 4, 4);
        GoldenImage.Compare(bitmap, $"steam-4x4-{theme}");
    }

    [WindowsGlyphTheory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 2)]
    public void OtherSizes(int cellsWide, int cellsHigh)
    {
        using var bitmap = Render("default-dark", cellsWide, cellsHigh);
        GoldenImage.Compare(bitmap, $"steam-{cellsWide}x{cellsHigh}-default-dark");
    }

    [Fact]
    public void TheSample_DrawsAtEverySizeOnAnySystem()
    {
        foreach ((int w, int h) in new[] { (1, 1), (2, 1), (2, 2), (4, 2), (4, 4) })
        {
            using var bitmap = Render("default-dark", w, h);
            Assert.Equal(275 * w, bitmap.Width);
            var first = bitmap.GetPixel(0, 0);
            bool ink = false;
            for (int y = 0; y < bitmap.Height && !ink; y += 3)
                for (int x = 0; x < bitmap.Width && !ink; x += 3)
                    ink = bitmap.GetPixel(x, y) != first;
            Assert.True(ink, $"the {w}x{h} card drew nothing");
        }
    }
}
