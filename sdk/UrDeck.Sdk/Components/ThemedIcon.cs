// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// Draws a glyph of the theme's icon set into a rectangle: the largest centred square, every shape stroked with the theme's
/// width and cap, the shapes the file marks as filled also filled, in the theme's tint and with its glow. It reads no file and
/// owns nothing it is given.
/// </summary>
public static class ThemedIcon
{
    /// <summary>The glow's blurred copy is drawn with this many blur widths of margin round the glyph.</summary>
    private const float GlowMargin = 3f;

    /// <summary>Draws the glyph for <paramref name="key"/>; false, drawing nothing, when the theme's lookup has none.</summary>
    /// <param name="canvas">Where to draw.</param>
    /// <param name="theme">The active theme.</param>
    /// <param name="rect">The space for the glyph; it is drawn in the largest centred square.</param>
    /// <param name="key">The icon's key, such as <c>weather/rain</c>.</param>
    /// <param name="tint">A colour that overrides the theme's tint.</param>
    /// <param name="rampPosition">0 to 1 along the theme's ramp, for the <c>ramp</c> tint.</param>
    public static bool Draw(SKCanvas canvas, Theme theme, SKRect rect, string key, SKColor? tint = null, float rampPosition = 0.5f)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return false;
        var icons = theme.Icons;
        if (!icons.Set.TryGet(key, out var glyph) || glyph.Shapes.Count == 0)
            return false;

        float side = Math.Min(rect.Width, rect.Height);
        float left = rect.MidX - side / 2;
        float top = rect.MidY - side / 2;
        var color = tint ?? Resolve(theme, rampPosition);

        if (icons.Glow is { BlurPx: > 0 } glow)
            DrawGlow(canvas, theme, glyph, key, left, top, side, glow, glow.Color ?? color);

        DrawShapes(canvas, theme, glyph, left, top, side, color);
        return true;
    }

    private static SKColor Resolve(Theme theme, float position) => theme.Icons.Tint switch
    {
        IconTint.Accent2 => theme.Accent2,
        IconTint.Text or IconTint.None => theme.Text,
        IconTint.Ramp => ColorMath.Along(theme.Ramp, Math.Clamp(position, 0f, 1f)),
        IconTint.Fixed => theme.Icons.TintColor,
        _ => theme.Accent,
    };

    private static void DrawShapes(SKCanvas canvas, Theme theme, IconGlyph glyph, float left, float top, float side, SKColor color)
    {
        float scale = side / IconGlyph.Size;
        int save = canvas.Save();
        canvas.Translate(left, top);
        canvas.Scale(scale);

        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = theme.Icons.Stroke * IconGlyph.Size,
            StrokeCap = theme.StrokeCap,
            StrokeJoin = theme.StrokeCap == SKStrokeCap.Square ? SKStrokeJoin.Miter : SKStrokeJoin.Round,
        };
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = color };
        foreach (var shape in glyph.Shapes)
        {
            if (shape.Filled)
                canvas.DrawPath(shape.Path, fill);
            canvas.DrawPath(shape.Path, stroke);
        }

        canvas.RestoreToCount(save);
    }

    private static void DrawGlow(SKCanvas canvas, Theme theme, IconGlyph glyph, string key, float left, float top, float side, IconGlow glow, SKColor color)
    {
        int pixels = Math.Max(1, (int)MathF.Ceiling(side));
        int margin = (int)MathF.Ceiling(glow.BlurPx * GlowMargin);
        var cacheKey = new GlowKey(key, pixels, (uint)color, false);
        var image = theme.Icons.GlowImages.Get(cacheKey, () =>
        {
            int extent = pixels + 2 * margin;
            using var surface = SKSurface.Create(new SKImageInfo(extent, extent, SKColorType.Rgba8888, SKAlphaType.Premul));
            using var blur = SKImageFilter.CreateBlur(glow.BlurPx, glow.BlurPx);
            using var layer = new SKPaint { ImageFilter = blur };
            surface.Canvas.SaveLayer(layer);
            DrawShapes(surface.Canvas, theme, glyph, margin, margin, pixels, color);
            surface.Canvas.Restore();
            return surface.Snapshot();
        });

        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White.WithAlpha((byte)Math.Round(255 * Math.Clamp(glow.Opacity, 0f, 1f))) };
        canvas.DrawImage(image, left - margin, top - margin, new SKSamplingOptions(SKFilterMode.Linear), paint);
    }
}
