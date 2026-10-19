// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// A recessed rounded rectangle behind a group of content, as <see cref="Theme.Well"/> says: its fill, corner radius (at most
/// half the smaller side) and border. A theme with no well draws nothing.
/// </summary>
public static class Well
{
    public static void Draw(SKCanvas canvas, Theme theme, SKRect rect)
    {
        if (theme.Well is not { } well || rect.Width <= 0 || rect.Height <= 0)
            return;

        float radius = Math.Min(well.Radius, Math.Min(rect.Width, rect.Height) / 2);
        if (well.Fill is { } fill)
        {
            using var paint = new SKPaint { Color = fill, IsAntialias = true };
            canvas.DrawRoundRect(rect, radius, radius, paint);
        }

        if (well.Border is { } border)
        {
            float stroke = Math.Max(1f, theme.CardBorderWidth);
            using var paint = new SKPaint { Color = border, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke };
            var inner = rect;
            inner.Inflate(-stroke / 2, -stroke / 2);
            float innerRadius = Math.Max(0, radius - stroke / 2);
            canvas.DrawRoundRect(inner, innerRadius, innerRadius, paint);
        }
    }
}
