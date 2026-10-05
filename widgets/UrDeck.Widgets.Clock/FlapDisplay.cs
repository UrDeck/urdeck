// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;

namespace UrDeck.Widgets.Clock;

/// <summary>
/// Draws the four tiles of the split-flap time (and the colon and the 12-hour suffix). Every colour comes from the
/// theme; a flip is drawn with a vertical scale standing in for the flap's rotation, so no 3D and no intermediate bitmap.
/// </summary>
internal static class FlapDisplay
{
    // All ratios of the tile height, so nothing here is a pixel constant.
    private const float TileAspect = 0.72f;
    private const float TileGap = 0.08f;
    private const float ColonWidth = 0.35f;
    private const float SuffixGap = 0.14f;
    private const float DigitCapRatio = 0.6f;
    private const float ColonDotRadius = 0.05f;
    private const float ColonDotOffset = 0.2f;
    private const float HingeRatio = 0.25f;

    // Flaps are opaque (a translucent fill shows the half behind it during a flip), so the look is a deliberate step
    // away from the theme's translucency: a solid colour derived from the theme, a crease and a shadow.
    private const byte TileFillAlpha = 0x24;
    private const float MaxShade = 0.45f;
    private const float CreaseShade = 0.28f;
    private const float CastShadow = 0.4f;
    private const byte HingeAlpha = 0xD0;
    private const float ReferenceSize = 100f;

    private static readonly SKColor[] CreaseColors =
    {
        SKColors.Black.WithAlpha(0),
        SKColors.Black.WithAlpha((byte)(255 * CreaseShade)),
        SKColors.Black.WithAlpha(0),
    };

    private static readonly float[] CreasePositions = { 0f, 0.5f, 1f };

    /// <summary>Where the tiles go: the numbers a draw needs, derived from the slot and the theme.</summary>
    internal readonly record struct Layout(float Left, float TileWidth, float TileHeight, float DigitSize, float UnitSize);

    /// <summary>The characters of the four tiles for a frame: <see cref="From"/> to <see cref="To"/> at <see cref="Progress"/>, or at rest on <see cref="To"/>.</summary>
    internal readonly record struct Flip(string From, string To, float? Progress);

    public static Layout Fit(Theme theme, SKRect slot, bool withSuffix, float scale)
    {
        using var valueFont = new SKFont(theme.GetTypeface(TextRole.Value), ReferenceSize);
        using var unitFont = new SKFont(theme.GetTypeface(TextRole.Unit), ReferenceSize);

        float cap = CapHeight(valueFont);
        float digitPerHeight = DigitCapRatio * ReferenceSize / cap;
        float unitPerHeight = digitPerHeight * theme.UnitRatio;
        float suffixPerHeight = withSuffix
            ? SuffixGap + Math.Max(unitFont.MeasureText("AM"), unitFont.MeasureText("PM")) / ReferenceSize * unitPerHeight
            : 0f;
        float widthPerHeight = 4 * TileAspect + 2 * TileGap + ColonWidth + suffixPerHeight;

        float height = Math.Min(slot.Height, slot.Width / widthPerHeight) * Math.Clamp(scale, 0.1f, 1f);
        height = Math.Max(1f, height);
        float width = widthPerHeight * height;
        return new Layout(
            slot.Left + (slot.Width - width) / 2f,
            TileAspect * height,
            height,
            digitPerHeight * height,
            unitPerHeight * height);
    }

    public static void Draw(SKCanvas canvas, Theme theme, Layout layout, float top, Flip flip, SKColor textColor, int cardHeight, string? suffix)
    {
        float h = layout.TileHeight;
        float w = layout.TileWidth;
        float gap = TileGap * h;
        float colon = ColonWidth * h;
        float radius = Math.Max(2f, theme.CardRadius * h / Math.Max(1, cardHeight));

        using var digitFont = new SKFont(theme.GetTypeface(TextRole.Value), layout.DigitSize) { Subpixel = true, Edging = SKFontEdging.Antialias };
        using var textPaint = new SKPaint { Color = textColor, IsAntialias = true };
        using var fillPaint = new SKPaint { Color = OpaqueTile(theme), IsAntialias = true };
        using var hingePaint = new SKPaint { Color = SKColors.Black.WithAlpha(HingeAlpha), IsAntialias = false };
        using var shadePaint = new SKPaint { IsAntialias = true };
        float cap = CapHeight(digitFont);
        float hinge = Math.Max(1f, theme.StrokeRatio * h * HingeRatio);

        float x = layout.Left;
        float[] lefts =
        {
            x,
            x + w + gap,
            x + 2 * w + gap + colon,
            x + 3 * w + 2 * gap + colon,
        };

        for (int i = 0; i < 4; i++)
        {
            var tile = new SKRect(lefts[i], top, lefts[i] + w, top + h);
            DrawTile(canvas, tile, radius, hinge, cap, digitFont, textPaint, fillPaint, hingePaint, shadePaint,
                flip.From[i], flip.To[i], flip.Progress);
        }

        // Colon between the pairs.
        float colonX = (lefts[1] + w + lefts[2]) / 2f;
        float mid = top + h / 2f;
        canvas.DrawCircle(colonX, mid - ColonDotOffset * h, ColonDotRadius * h, textPaint);
        canvas.DrawCircle(colonX, mid + ColonDotOffset * h, ColonDotRadius * h, textPaint);

        if (suffix != null)
        {
            using var unitFont = new SKFont(theme.GetTypeface(TextRole.Unit), layout.UnitSize) { Subpixel = true, Edging = SKFontEdging.Antialias };
            canvas.DrawText(suffix, lefts[3] + w + SuffixGap * h, top + h, SKTextAlign.Left, unitFont, textPaint);
        }
    }

    private static void DrawTile(
        SKCanvas canvas, SKRect tile, float radius, float hinge, float cap, SKFont font,
        SKPaint textPaint, SKPaint fillPaint, SKPaint hingePaint, SKPaint shadePaint,
        char from, char to, float? progress)
    {
        var rrect = new SKRoundRect(tile, radius);
        float mid = tile.MidY;
        using var creaseShader = SKShader.CreateLinearGradient(
            new SKPoint(0, tile.Top),
            new SKPoint(0, tile.Bottom),
            CreaseColors,
            CreasePositions,
            SKShaderTileMode.Clamp);
        using var creasePaint = new SKPaint { Shader = creaseShader, IsAntialias = true };

        if (progress is not { } p || from == to)
        {
            DrawFace(canvas, rrect, tile, to, cap, font, textPaint, fillPaint, creasePaint, tile);
        }
        else
        {
            var topHalf = new SKRect(tile.Left, tile.Top, tile.Right, mid);
            var bottomHalf = new SKRect(tile.Left, mid, tile.Right, tile.Bottom);

            // The rotation of the flap seen from the front: its height follows the cosine of the angle.
            float scale = MathF.Abs(MathF.Cos(p * MathF.PI));
            bool firstHalf = p < 0.5f;

            DrawFace(canvas, rrect, tile, to, cap, font, textPaint, fillPaint, creasePaint, topHalf);
            DrawFace(canvas, rrect, tile, from, cap, font, textPaint, fillPaint, creasePaint, bottomHalf);

            // The falling flap shades the half below it until it passes the vertical.
            if (firstHalf)
            {
                shadePaint.Color = SKColors.Black.WithAlpha((byte)(255 * CastShadow * (1f - scale)));
                canvas.Save();
                canvas.ClipRoundRect(rrect, SKClipOperation.Intersect, true);
                canvas.DrawRect(bottomHalf, shadePaint);
                canvas.Restore();
            }

            // The moving flap carries the old top half until it is edge-on, then the new bottom half.
            char flapChar = firstHalf ? from : to;
            var flapHalf = firstHalf ? topHalf : bottomHalf;
            int save = canvas.Save();
            canvas.Translate(0, mid);
            canvas.Scale(1f, Math.Max(scale, 0.001f));
            canvas.Translate(0, -mid);
            DrawFace(canvas, rrect, tile, flapChar, cap, font, textPaint, fillPaint, creasePaint, flapHalf);
            shadePaint.Color = SKColors.Black.WithAlpha((byte)(255 * MaxShade * (1f - scale)));
            canvas.ClipRoundRect(rrect, SKClipOperation.Intersect, true);
            canvas.DrawRect(flapHalf, shadePaint);
            canvas.RestoreToCount(save);
        }

        canvas.DrawRect(tile.Left, mid - hinge / 2f, tile.Width, hinge, hingePaint);
    }

    /// <summary>The tile's fill and character, clipped to <paramref name="region"/> (the whole tile or one half).</summary>
    private static void DrawFace(SKCanvas canvas, SKRoundRect rrect, SKRect tile, char ch, float cap, SKFont font, SKPaint textPaint, SKPaint fillPaint, SKPaint creasePaint, SKRect region)
    {
        int save = canvas.Save();
        canvas.ClipRoundRect(rrect, SKClipOperation.Intersect, true);
        canvas.ClipRect(region, SKClipOperation.Intersect, true);
        canvas.DrawRoundRect(rrect, fillPaint);
        canvas.DrawRoundRect(rrect, creasePaint);
        if (ch != ' ')
        {
            string glyph = ch.ToString();
            float x = tile.MidX - font.MeasureText(glyph) / 2f;
            canvas.DrawText(glyph, x, tile.MidY + cap / 2f, SKTextAlign.Left, font, textPaint);
        }
        canvas.RestoreToCount(save);
    }

    /// <summary>The tile colour as a solid: the theme's text colour over the card (itself over the background).</summary>
    private static SKColor OpaqueTile(Theme theme)
    {
        var card = Over(theme.CardFill, theme.Background);
        return Over(theme.Text.WithAlpha(TileFillAlpha), card);
    }

    private static SKColor Over(SKColor top, SKColor bottom)
    {
        float a = top.Alpha / 255f;
        byte Mix(byte t, byte b) => (byte)Math.Round(t * a + b * (1 - a));
        return new SKColor(Mix(top.Red, bottom.Red), Mix(top.Green, bottom.Green), Mix(top.Blue, bottom.Blue), 255);
    }

    private static float CapHeight(SKFont font)
    {
        float cap = font.Metrics.CapHeight;
        return cap > 0 ? cap : -font.Metrics.Ascent * 0.7f;
    }
}
