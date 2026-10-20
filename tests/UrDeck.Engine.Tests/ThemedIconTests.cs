// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ThemedIconTests
{
    private static readonly SKColor AccentColor = new(0x10, 0xA0, 0x40);
    private static readonly SKColor Last = new(0xD0, 0x20, 0x20);

    /// <summary>A set that answers <c>test/line</c> with a horizontal line and <c>test/dot</c> with a filled circle.</summary>
    internal sealed class TestSet : IIconSet, IDisposable
    {
        private readonly IconGlyph _line;
        private readonly IconGlyph _dot;

        public TestSet()
        {
            using var line = new SKPathBuilder();
            line.MoveTo(2, 12);
            line.LineTo(22, 12);
            _line = new IconGlyph([new IconShape(line.Detach(), false)]);
            using var dot = new SKPathBuilder();
            dot.AddCircle(12, 12, 4);
            _dot = new IconGlyph([new IconShape(dot.Detach(), true)]);
        }

        public bool TryGet(string key, out IconGlyph glyph)
        {
            glyph = key switch { "test/line" => _line, "test/dot" => _dot, _ => null! };
            return glyph != null;
        }

        public void Dispose()
        {
            foreach (var shape in _line.Shapes.Concat(_dot.Shapes))
                shape.Path.Dispose();
        }
    }

    private static Theme ThemeWith(IIconSet set, IconTint tint = IconTint.Accent, IconGlow? glow = null) => new()
    {
        Accent = AccentColor,
        Ramp = [SKColors.Blue, Last],
        Icons = new IconTheme { Set = set, NamesSet = true, Tint = tint, Glow = glow },
    };

    private static SKBitmap Paint(Theme theme, SKRect rect, string key, SKColor? tint = null, float ramp = 0.5f, int size = 200, Action<bool>? result = null)
    {
        var bitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        bool drew = ThemedIcon.Draw(canvas, theme, rect, key, tint, ramp);
        result?.Invoke(drew);
        return bitmap;
    }

    private static bool Close(SKColor a, SKColor b) =>
        Math.Abs(a.Red - b.Red) < 6 && Math.Abs(a.Green - b.Green) < 6 && Math.Abs(a.Blue - b.Blue) < 6 && a.Alpha > 240;

    private static int Opaque(SKBitmap bitmap, int x, int fromY, int toY)
    {
        int count = 0;
        for (int y = fromY; y < toY; y++)
            if (bitmap.GetPixel(x, y).Alpha > 240)
                count++;
        return count;
    }

    [Fact]
    public void Glyph_IsStrokedInTheAccentWithTheThemesWidthAndReturnsTrue()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set);
        bool drew = false;
        using var bitmap = Paint(theme, new SKRect(0, 0, 96, 96), "test/line", result: r => drew = r);

        Assert.True(drew);
        Assert.True(Close(AccentColor, bitmap.GetPixel(48, 48)));
        // 1.6/24 of 96 pixels is 6.4 pixels wide, centred on the line at y = 48.
        int across = Opaque(bitmap, 48, 0, 96);
        Assert.InRange(across, 5, 7);
    }

    [Fact]
    public void WideRectangle_DrawsASquareCentredInIt()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set);
        using var bitmap = Paint(theme, new SKRect(0, 0, 200, 100), "test/line");

        // The line spans x 2..22 of 24 in a 100 pixel square starting at x = 50.
        Assert.True(Close(AccentColor, bitmap.GetPixel(100, 50)));
        Assert.Equal(0, bitmap.GetPixel(40, 50).Alpha);
        Assert.Equal(0, bitmap.GetPixel(160, 50).Alpha);
    }

    [Fact]
    public void RampTint_DrawsTheColourAtThePosition()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set, IconTint.Ramp);
        using var bitmap = Paint(theme, new SKRect(0, 0, 96, 96), "test/line", ramp: 1f);

        Assert.True(Close(Last, bitmap.GetPixel(48, 48)));
    }

    [Fact]
    public void NoneTint_DrawsTheTextColour()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set, IconTint.None);
        using var bitmap = Paint(theme, new SKRect(0, 0, 96, 96), "test/line");

        Assert.True(Close(theme.Text, bitmap.GetPixel(48, 48)));
    }

    [Fact]
    public void Override_BeatsTheTint()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set);
        using var bitmap = Paint(theme, new SKRect(0, 0, 96, 96), "test/line", tint: SKColors.Magenta);

        Assert.True(Close(SKColors.Magenta, bitmap.GetPixel(48, 48)));
    }

    [Fact]
    public void FilledShape_IsFilledInside()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set);
        using var bitmap = Paint(theme, new SKRect(0, 0, 96, 96), "test/dot");

        Assert.True(Close(AccentColor, bitmap.GetPixel(48, 48)));
    }

    [Fact]
    public void Glow_IsBlurredOnceForTheSameGlyphSizeAndColour()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set, glow: new IconGlow(6f, null, 0.8f));
        using var first = Paint(theme, new SKRect(0, 0, 96, 96), "test/line");
        using var second = Paint(theme, new SKRect(0, 0, 96, 96), "test/line");

        Assert.Equal(1, theme.Icons.GlowImages.Renders);
        // The glow reaches above the stroke, where the plain glyph has nothing.
        Assert.True(first.GetPixel(48, 40).Alpha > 0);
        Assert.Equal(first.GetPixel(48, 40), second.GetPixel(48, 40));
    }

    [Fact]
    public void UnknownKey_DrawsNothingAndReturnsFalse()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set);
        bool drew = true;
        using var bitmap = Paint(theme, new SKRect(0, 0, 96, 96), "test/nosuch", result: r => drew = r);

        Assert.False(drew);
        Assert.Equal(0, bitmap.GetPixel(48, 48).Alpha);
    }

    [Fact]
    public void EmptyRectangle_DrawsNothingAndReturnsFalse()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set);
        bool drew = true;
        using var bitmap = Paint(theme, new SKRect(10, 10, 10, 50), "test/line", result: r => drew = r);

        Assert.False(drew);
    }

    [Fact]
    public void DefaultTheme_NamesNoSetAndAnswersNothing()
    {
        using var theme = new Theme();

        Assert.False(theme.Icons.NamesSet);
        Assert.False(theme.Icons.Set.TryGet("app/file", out _));
        Assert.Equal(IconTheme.DefaultStroke, theme.Icons.Stroke);
        Assert.Equal(IconTint.Accent, theme.Icons.Tint);
        Assert.Null(theme.Icons.Glow);
    }

    [Fact]
    public void SharedSet_SurvivesADisposedTheme()
    {
        using var set = new TestSet();
        var first = ThemeWith(set);
        using var second = ThemeWith(set);
        first.Dispose();

        using var bitmap = Paint(second, new SKRect(0, 0, 96, 96), "test/line");
        Assert.True(Close(AccentColor, bitmap.GetPixel(48, 48)));
    }

    [Fact]
    public void ImageTile_DrawsTheGlyphInPlaceOfThePlaceholder()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set);
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        ImageTile.Draw(canvas, theme, new SKRect(0, 0, 100, 100), null, placeholder: "homeassistant", glyphKey: "test/line");

        Assert.True(Close(AccentColor, bitmap.GetPixel(50, 50)));
        // No placeholder square: the corner of it is untouched.
        Assert.Equal(0, bitmap.GetPixel(3, 3).Alpha);
    }

    [Fact]
    public void ImageTile_UnknownGlyphKeyDrawsThePlaceholder()
    {
        using var set = new TestSet();
        using var theme = ThemeWith(set);
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        ImageTile.Draw(canvas, theme, new SKRect(0, 0, 100, 100), null, placeholder: "homeassistant", glyphKey: "test/nosuch");

        Assert.True(bitmap.GetPixel(50, 10).Alpha > 0 || bitmap.GetPixel(10, 50).Alpha > 0);
        Assert.True(bitmap.GetPixel(12, 50).Alpha > 0);
    }
}
