// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ImageTileTests : IDisposable
{
    private static readonly SKColor Ink = new(0xE0, 0x30, 0x90);

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);

    public void Dispose() => _theme.Dispose();

    /// <summary>An opaque picture of one colour; with <paramref name="hole"/>, its middle half is transparent.</summary>
    private static SKImage Picture(int width, int height, bool hole = false)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(Ink);
            if (hole)
            {
                using var clear = new SKPaint { BlendMode = SKBlendMode.Clear };
                canvas.DrawRect(SKRect.Create(width / 4f, height / 4f, width / 2f, height / 2f), clear);
            }
        }

        return SKImage.FromBitmap(bitmap);
    }

    private (SKBitmap Bitmap, SKRect Drawn) Draw(int size, SKRect rect, SKImage? image, string? label = null, string? placeholder = null, Theme? theme = null)
    {
        var bitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var drawn = ImageTile.Draw(canvas, theme ?? _theme, rect, image, label, placeholder);
        return (bitmap, drawn);
    }

    private static int Count(SKBitmap bitmap, Func<SKColor, bool> match, SKRectI? within = null)
    {
        var area = within ?? new SKRectI(0, 0, bitmap.Width, bitmap.Height);
        int count = 0;
        for (int y = Math.Max(0, area.Top); y < Math.Min(bitmap.Height, area.Bottom); y++)
            for (int x = Math.Max(0, area.Left); x < Math.Min(bitmap.Width, area.Right); x++)
                if (match(bitmap.GetPixel(x, y)))
                    count++;
        return count;
    }

    private static bool IsInk(SKColor c) => c.Alpha > 200 && Math.Abs(c.Red - Ink.Red) < 12 && Math.Abs(c.Green - Ink.Green) < 12 && Math.Abs(c.Blue - Ink.Blue) < 12;

    /// <summary>The same colour, give or take the rounding of a translucent pixel stored premultiplied.</summary>
    private static bool Same(SKColor expected, SKColor actual) =>
        Math.Abs(expected.Alpha - actual.Alpha) <= 1 && Math.Abs(expected.Red - actual.Red) <= 3
        && Math.Abs(expected.Green - actual.Green) <= 3 && Math.Abs(expected.Blue - actual.Blue) <= 3;

    private static bool Near(SKColor c, SKColor other) =>
        c.Alpha > 0 && Math.Abs(c.Red - other.Red) < 16 && Math.Abs(c.Green - other.Green) < 16 && Math.Abs(c.Blue - other.Blue) < 16;

    /// <summary>The smallest rectangle holding every pixel that is not fully transparent.</summary>
    private static SKRectI Bounds(SKBitmap bitmap, Func<SKColor, bool>? match = null)
    {
        match ??= c => c.Alpha > 0;
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (!match(bitmap.GetPixel(x, y)))
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x + 1);
                bottom = Math.Max(bottom, y + 1);
            }
        }

        return right < 0 ? SKRectI.Empty : new SKRectI(left, top, right, bottom);
    }

    [Fact]
    public void ASquareIcon_FillsASquareRectangle_AndItsTransparentAreasStayUntouched()
    {
        using var icon = Picture(256, 256, hole: true);
        var (bitmap, drawn) = Draw(240, new SKRect(20, 20, 220, 220), icon);
        using var _ = bitmap;

        Assert.Equal(new SKRect(20, 20, 220, 220), drawn);
        Assert.Equal(new SKRectI(20, 20, 220, 220), Bounds(bitmap));
        Assert.True(IsInk(bitmap.GetPixel(30, 30)));
        // The hole in the icon is still a hole: nothing was painted behind it.
        Assert.Equal(0, bitmap.GetPixel(120, 120).Alpha);
    }

    [Fact]
    public void AWideImage_SpansTheWidth_IsCentredVertically_AndStaysInsideTheRectangle()
    {
        using var wide = Picture(400, 200);
        var (bitmap, drawn) = Draw(240, new SKRect(20, 20, 220, 220), wide);
        using var _ = bitmap;

        Assert.Equal(new SKRect(20, 70, 220, 170), drawn);
        Assert.Equal(new SKRectI(20, 70, 220, 170), Bounds(bitmap));
    }

    [Fact]
    public void ASmallImage_IsDrawnAtTwiceItsSize_InTheCentre()
    {
        using var small = Picture(48, 48);
        var (bitmap, drawn) = Draw(240, new SKRect(0, 0, 240, 240), small);
        using var _ = bitmap;

        Assert.Equal(new SKRect(72, 72, 168, 168), drawn);
        Assert.Equal(new SKRectI(72, 72, 168, 168), Bounds(bitmap));
    }

    [Fact]
    public void TheImage_IsDrawnSmoothly()
    {
        // A checkerboard of single pixels, enlarged: smooth sampling blends neighbours, nearest-neighbour would not.
        using var checks = new SKBitmap(new SKImageInfo(8, 8, SKColorType.Rgba8888, SKAlphaType.Premul));
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                checks.SetPixel(x, y, (x + y) % 2 == 0 ? SKColors.Black : SKColors.White);
        using var image = SKImage.FromBitmap(checks);
        var (bitmap, _) = Draw(16, new SKRect(0, 0, 16, 16), image);
        using var owned = bitmap;

        Assert.True(Count(bitmap, c => c.Red is > 40 and < 215) > 0);
    }

    [Fact]
    public void ALabel_IsDrawnBelowTheImage_InTheMutedColour_AndTheTwoDoNotOverlap()
    {
        using var icon = Picture(256, 256);
        var rect = new SKRect(20, 20, 220, 220);
        var (bitmap, drawn) = Draw(240, rect, icon, label: "Steam");
        using var _ = bitmap;

        var label = Bounds(bitmap, c => Near(c, _theme.TextMuted) && !IsInk(c));
        Assert.False(label.IsEmpty);
        Assert.True(label.Top >= drawn.Bottom, $"label top {label.Top} is above the image bottom {drawn.Bottom}");
        Assert.True(label.Bottom <= rect.Bottom + 0.5f);
        // The image gave up the label's space and is still centred in what remains.
        Assert.True(drawn.Height < rect.Height);
        Assert.Equal(rect.MidX, drawn.MidX, 1);

        using var font = new SKFont(_theme.GetTypeface(TextRole.Label), _theme.LabelSize);
        Assert.Equal(font.MeasureText("Steam"), label.Width, 4f);
    }

    [Fact]
    public void WithoutALabel_NoSpaceIsReserved()
    {
        using var icon = Picture(256, 256);
        var rect = new SKRect(20, 20, 220, 220);

        var (unset, drawnUnset) = Draw(240, rect, icon);
        var (empty, drawnEmpty) = Draw(240, rect, icon, label: "");
        unset.Dispose();
        empty.Dispose();

        Assert.Equal(rect, drawnUnset);
        Assert.Equal(rect, drawnEmpty);
    }

    [Fact]
    public void WithNoImage_ThePlaceholderIsARoundedSquareInTheDimmedAccent_WithTheFirstLetterUpperCased()
    {
        var rect = new SKRect(20, 20, 220, 220);
        var (bitmap, drawn) = Draw(240, rect, null, placeholder: "homeassistant");
        using var _ = bitmap;
        var (blank, _) = Draw(240, rect, null);
        using var ownedBlank = blank;

        Assert.Equal(rect, drawn);
        // The square is there, and its corners are rounded away.
        Assert.True(Same(_theme.AccentDim, bitmap.GetPixel(120, 30)));
        Assert.Equal(0, bitmap.GetPixel(21, 21).Alpha);

        // The letter is drawn in the text colour, in the middle, and it is an "H": two stems and a bar.
        var letter = Bounds(bitmap, c => c == _theme.Text);
        Assert.False(letter.IsEmpty);
        Assert.Equal(120, letter.MidX, 2f);
        Assert.Equal(120, letter.MidY, 2f);
        Assert.Equal(0, Count(blank, c => c == _theme.Text));

        using var expected = new SKBitmap(240, 240);
        using (var canvas = new SKCanvas(expected))
        {
            canvas.Clear(SKColors.Transparent);
            ImageTile.Draw(canvas, _theme, rect, null, null, "H");
        }

        Assert.Equal(expected.Bytes, bitmap.Bytes);
    }

    [Theory]
    [InlineData("  -- 7zip", "7")]
    [InlineData("éclair", "É")]
    public void ThePlaceholderLetter_IsTheFirstLetterOrDigit(string text, string letter)
    {
        var rect = new SKRect(0, 0, 200, 200);
        var (fromText, _) = Draw(200, rect, null, placeholder: text);
        var (fromLetter, _) = Draw(200, rect, null, placeholder: letter);
        using var a = fromText;
        using var b = fromLetter;

        Assert.Equal(fromLetter.Bytes, fromText.Bytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("...")]
    public void WithNoPlaceholderText_TheSquareIsEmpty(string? text)
    {
        var (bitmap, _) = Draw(200, new SKRect(0, 0, 200, 200), null, placeholder: text);
        using var owned = bitmap;

        Assert.True(Same(_theme.AccentDim, bitmap.GetPixel(100, 100)));
        Assert.Equal(0, Count(bitmap, c => c == _theme.Text));
    }

    [Fact]
    public void TheThemeDecidesTheLook_OfThePlaceholderAndTheLabel()
    {
        using var light = ThemeResolver.Resolve(new ThemeStore("").Load("default-light"), 275);
        var rect = new SKRect(0, 0, 200, 200);
        var (dark, _) = Draw(200, rect, null, label: "Steam", placeholder: "s");
        var (other, _) = Draw(200, rect, null, label: "Steam", placeholder: "s", theme: light);
        using var a = dark;
        using var b = other;

        Assert.True(Same(_theme.AccentDim, dark.GetPixel(100, 10)));
        Assert.True(Same(light.AccentDim, other.GetPixel(100, 10)));
        Assert.False(Same(_theme.AccentDim, other.GetPixel(100, 10)));
        Assert.True(Count(other, c => c == light.Text) > 0);
        Assert.True(Count(other, c => Near(c, light.TextMuted)) > 0);
    }

    [Fact]
    public void AnEmptyRectangle_DrawsNothing()
    {
        using var icon = Picture(64, 64);
        var (bitmap, drawn) = Draw(100, new SKRect(10, 10, 10, 60), icon, label: "x", placeholder: "x");
        using var _ = bitmap;

        Assert.Equal(0, drawn.Width);
        Assert.True(Bounds(bitmap).IsEmpty);
    }
}
