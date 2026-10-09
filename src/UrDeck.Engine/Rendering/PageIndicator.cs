// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Rendering;

/// <summary>The page indicator's theme values in physical pixels.</summary>
public sealed record IndicatorStyle(
    float BandHeight,
    float DotSize,
    float PillLength,
    float Spacing,
    SKColor Active,
    SKColor Inactive,
    SKColor Backdrop)
{
    /// <summary>Resolves the theme's indicator group for a grid cell that is <paramref name="cellPx"/> pixels wide.</summary>
    public static IndicatorStyle Resolve(LoadedTheme loaded, float cellPx)
    {
        var i = loaded.Definition.Indicator!;
        return new IndicatorStyle(
            (float)i.BandHeight!.Value * cellPx,
            (float)i.DotSize!.Value * cellPx,
            (float)i.PillLength!.Value * cellPx,
            (float)i.Spacing!.Value * cellPx,
            SKColor.Parse(i.Active),
            SKColor.Parse(i.Inactive),
            SKColor.Parse(i.Backdrop));
    }
}

/// <summary>
/// Draws the page indicator: one dot per page and the current page as a pill, from a fractional position so the pill
/// sits between two dots mid-swipe. The window and the snapshot both call it.
/// </summary>
public static class PageIndicator
{
    /// <summary>Each page's touch target is at least this fraction of a grid cell wide.</summary>
    public const float MinSlotCells = 1f / 3f;

    /// <summary>The distance between neighbouring marks' centres: a pill's half-length, a dot's half and the spacing.</summary>
    public static float Pitch(IndicatorStyle style) => (style.PillLength + style.DotSize) / 2 + style.Spacing;

    /// <summary>The x of page <paramref name="index"/>'s mark in a row of <paramref name="count"/> centred in <paramref name="area"/>.</summary>
    public static float CenterX(RectangleF area, int count, IndicatorStyle style, int index) =>
        area.Left + area.Width / 2 + (index - (count - 1) / 2f) * Pitch(style);

    /// <param name="canvas">Where to draw, in the same pixels as <paramref name="area"/>.</param>
    /// <param name="area">The indicator's rectangle; the marks are centred in it.</param>
    /// <param name="count">The number of pages.</param>
    /// <param name="position">The page shown as a fraction, 0 to count-1; the pill is drawn here.</param>
    /// <param name="opacity">0 draws nothing, 1 draws fully.</param>
    /// <param name="backdrop">Draws the translucent pill behind the marks (the floating mode).</param>
    /// <param name="style">The theme's values.</param>
    public static void Paint(SKCanvas canvas, RectangleF area, int count, double position, float opacity, bool backdrop, IndicatorStyle style)
    {
        if (count <= 0 || opacity <= 0)
            return;
        opacity = Math.Min(opacity, 1);
        position = Math.Clamp(position, 0, count - 1);

        float cy = area.Top + area.Height / 2;
        float d = style.DotSize;
        float pitch = Pitch(style);
        float first = CenterX(area, count, style, 0);
        float last = CenterX(area, count, style, count - 1);

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        if (backdrop)
        {
            float pad = d * 1.5f;
            var rect = new SKRect(
                first - style.PillLength / 2 - pad,
                cy - d / 2 - pad,
                last + style.PillLength / 2 + pad,
                cy + d / 2 + pad);
            paint.Color = Fade(style.Backdrop, opacity);
            canvas.DrawRoundRect(rect, rect.Height / 2, rect.Height / 2, paint);
        }

        paint.Color = Fade(style.Inactive, opacity);
        for (int i = 0; i < count; i++)
            canvas.DrawCircle(first + i * pitch, cy, d / 2, paint);

        float pillCx = first + (float)position * pitch;
        var pill = new SKRect(pillCx - style.PillLength / 2, cy - d / 2, pillCx + style.PillLength / 2, cy + d / 2);
        paint.Color = Fade(style.Active, opacity);
        canvas.DrawRoundRect(pill, d / 2, d / 2, paint);
    }

    /// <summary>
    /// The page a point selects, or -1. A point selects the nearest mark if it lies in the indicator's rectangle and
    /// within a slot of it; a slot is at least <see cref="MinSlotCells"/> of a cell wide, so it is bigger than the dot.
    /// </summary>
    public static int HitTest(RectangleF area, int count, IndicatorStyle style, float cellPx, PointF point)
    {
        if (count <= 0 || !area.Contains(point))
            return -1;

        float slot = Math.Max(Pitch(style), MinSlotCells * cellPx);
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            float distance = Math.Abs(point.X - CenterX(area, count, style, i));
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return bestDistance <= slot / 2 ? best : -1;
    }

    private static SKColor Fade(SKColor color, float opacity) => color.WithAlpha((byte)Math.Round(color.Alpha * opacity));
}
