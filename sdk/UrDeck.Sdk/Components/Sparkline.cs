// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// A polyline of recent values across a rectangle, the oldest at the left, each a fraction of the range (0 at the bottom,
/// 1 at the top). Fewer than two values draw nothing.
/// </summary>
public static class Sparkline
{
    /// <summary>Draws the line across <paramref name="rect"/>.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="rect">Where the line goes.</param>
    /// <param name="fractions">The values, oldest first.</param>
    /// <param name="color">The line's colour; the theme's accent when null.</param>
    /// <param name="strokeWidth">The line's width in pixels; the theme's sparkline width, or a hairline, when null.</param>
    public static void Draw(SKCanvas canvas, Theme theme, SKRect rect, IReadOnlyList<float> fractions, SKColor? color = null, float? strokeWidth = null)
    {
        if (fractions.Count < 2 || rect.Width <= 0 || rect.Height <= 0)
            return;

        float stroke = strokeWidth ?? theme.Gauge.Sparkline?.StrokeWidth ?? Math.Max(1f, rect.Height * 0.04f);
        float top = rect.Top + stroke / 2;
        float height = Math.Max(0, rect.Height - stroke);
        float step = rect.Width / (fractions.Count - 1);

        using var builder = new SKPathBuilder();
        for (int i = 0; i < fractions.Count; i++)
        {
            float x = rect.Left + step * i;
            float y = top + height * (1f - Math.Clamp(fractions[i], 0f, 1f));
            if (i == 0)
                builder.MoveTo(x, y);
            else
                builder.LineTo(x, y);
        }

        using var path = builder.Detach();
        using var paint = new SKPaint
        {
            Color = color ?? theme.Accent,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };
        canvas.DrawPath(path, paint);
    }
}
