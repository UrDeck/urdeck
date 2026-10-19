// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// The horizontal bar and what is built on it: the bar with the theme's stacked layout and the meter. The track is a line
/// with the theme's cap, cut into blocks when the theme asks.
/// </summary>
internal static class BarGauge
{
    /// <summary>A minor tick under a meter is this many thicknesses from the track.</summary>
    private const float TickGapRatio = 0.5f;

    private const float MajorTickScale = 1.5f;

    /// <summary>The space between a meter's ticks or track and its labels, in sizes of the label.</summary>
    private const float ScaleGapRatio = 0.5f;

    internal readonly record struct BarLayout(float Left, float Right, float CenterY, float Thickness, float TrackThickness, SKRect Readout, bool Stacked);

    public static SKRect ReadoutRect(Theme theme, SKRect rect) => Measure(theme, rect, stacked: theme.Gauge.Layout == GaugeLayout.Stacked, meter: false).Readout;

    public static SKRect Draw(GaugeContext c) => DrawBar(c, stacked: c.G.Layout == GaugeLayout.Stacked, meter: false);

    internal static float Thickness(Theme theme, SKRect rect) => Math.Min(rect.Width, rect.Height) * theme.StrokeRatio;

    /// <summary>
    /// The geometry of a bar. Unstacked is the bar's own layout (track at the top, readout below); stacked puts the readout
    /// above and the track at the bottom, and a meter also leaves room under the track for ticks and the scale.
    /// </summary>
    internal static BarLayout Measure(Theme theme, SKRect rect, bool stacked, bool meter)
    {
        var g = theme.Gauge;
        float thickness = Thickness(theme, rect);
        float trackThickness = thickness * (g.Track?.WidthRatio ?? 1f);
        float widest = Math.Max(thickness, trackThickness);
        bool segmented = g.Segments != null;
        float inset = segmented ? 0f : thickness / 2;
        float left = rect.Left + inset;
        float right = rect.Right - inset;

        if (!stacked)
        {
            float y = rect.Top + thickness / 2;
            float top = rect.Top + thickness * (1 + (g.ReadoutGap ?? GaugePaint.ReadoutGap));
            return new BarLayout(left, right, y, thickness, trackThickness, new SKRect(rect.Left, Math.Min(top, rect.Bottom), rect.Right, rect.Bottom), false);
        }

        float gap = g.ReadoutGap ?? GaugePaint.ReadoutGap;
        float below = meter ? BelowTrack(theme, thickness) : 0f;
        float centre = rect.Bottom - below - widest / 2;
        float readoutBottom = Math.Max(rect.Top, centre - widest / 2 - thickness * gap);
        return new BarLayout(left, right, centre, thickness, trackThickness, new SKRect(rect.Left, rect.Top, rect.Right, readoutBottom), true);
    }

    /// <summary>How much room a meter keeps under its track for the ticks and the printed scale.</summary>
    private static float BelowTrack(Theme theme, float thickness)
    {
        var g = theme.Gauge;
        float room = 0f;
        if (g.Ticks is { } ticks)
            room += thickness * TickGapRatio + ticks.Length * MajorTickScale;
        if (g.Scale != null || g.Ticks is { Labels: true })
        {
            float size = ScaleSize(theme);
            room += size * ScaleGapRatio + GaugePaint.TextHeight(theme, size);
        }
        return room;
    }

    private static float ScaleSize(Theme theme) => theme.Gauge.Scale is { Size: > 0 } scale ? scale.Size : theme.LabelSize;

    internal static SKRect DrawBar(GaugeContext c, bool stacked, bool meter)
    {
        var layout = Measure(c.Theme, c.Rect, stacked, meter);
        DrawTrack(c, layout);
        if (c.Fraction is > 0f)
            DrawFill(c, layout);
        if (meter)
            DrawScale(c, layout);

        return layout.Stacked
            ? GaugePaint.DrawReadout(c, layout.Readout, HorizontalAlign.Left, VerticalAlign.Bottom)
            : GaugePaint.DrawReadout(c, layout.Readout);
    }

    private static SKStrokeCap CapOf(GaugeContext c) => c.G.Segments != null ? SKStrokeCap.Butt : c.Theme.StrokeCap;

    private static void DrawTrack(GaugeContext c, BarLayout layout)
    {
        using var paint = GaugePaint.Stroke(layout.TrackThickness, CapOf(c), c.Track);
        StrokeLine(c, layout, paint, layout.Right - layout.Left);
    }

    /// <summary>Strokes the bar from its left for <paramref name="length"/>, cut into blocks when the theme asks.</summary>
    private static void StrokeLine(GaugeContext c, BarLayout layout, SKPaint paint, float length)
    {
        float full = layout.Right - layout.Left;
        if (c.G.Segments is not { } segments)
        {
            c.Canvas.DrawLine(layout.Left, layout.CenterY, layout.Left + length, layout.CenterY, paint);
            return;
        }

        foreach (var (start, size) in GaugePaint.Blocks(full, segments.Length, segments.Gap, length))
            c.Canvas.DrawLine(layout.Left + start, layout.CenterY, layout.Left + start + size, layout.CenterY, paint);
    }

    private static void DrawFill(GaugeContext c, BarLayout layout)
    {
        float length = (layout.Right - layout.Left) * c.Fraction!.Value;
        if (c.Current && c.G.Glow is { Blur: > 0 } glow)
        {
            using var halo = GaugePaint.Glow(c.Theme, glow, c.Fill, layout.Thickness, CapOf(c));
            StrokeLine(c, layout, halo, length);
        }

        using var paint = GaugePaint.Stroke(layout.Thickness, CapOf(c), c.Fill);
        if (c.GradientColors is { } colors)
            paint.Shader = GradientShader(c, layout, colors);
        StrokeLine(c, layout, paint, length);
    }

    /// <summary>The gradient laid over the whole track, so the fill's end shows the colour at its fraction.</summary>
    private static SKShader GradientShader(GaugeContext c, BarLayout layout, IReadOnlyList<SKColor> colors) =>
        c.G.Cache.Get(("bar", layout.Left, layout.Right, layout.CenterY, c.G.Gradient!.Ramp), () =>
            SKShader.CreateLinearGradient(new SKPoint(layout.Left, layout.CenterY), new SKPoint(layout.Right, layout.CenterY), [.. colors], null, SKShaderTileMode.Clamp));

    /// <summary>The meter's ticks and printed scale, under the track.</summary>
    private static void DrawScale(GaugeContext c, BarLayout layout)
    {
        var g = c.G;
        float widest = Math.Max(layout.Thickness, layout.TrackThickness);
        float y = layout.CenterY + widest / 2 + layout.Thickness * TickGapRatio;
        float width = layout.Right - layout.Left;
        float size = ScaleSize(c.Theme);

        if (g.Ticks is { } ticks)
        {
            var color = ticks.Color ?? c.Theme.TextMuted;
            using var paint = GaugePaint.Stroke(ticks.Thickness, SKStrokeCap.Butt, color);
            for (int i = 0; i < ticks.Count; i++)
            {
                float along = i / (float)(ticks.Count - 1);
                bool major = i % ticks.MajorEvery == 0;
                float x = layout.Left + width * along;
                c.Canvas.DrawLine(x, y, x, y + ticks.Length * (major ? MajorTickScale : 1f), paint);
                if (ticks.Labels && major)
                    GaugePaint.DrawText(
                        c.Canvas, c.Theme, ((int)Math.Round(100 * along)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        x, y + ticks.Length * MajorTickScale + size * ScaleGapRatio, size, color, EdgeAlign(i, ticks.Count));
            }
            y += ticks.Length * MajorTickScale;
        }

        if (g.Scale is not { } scale)
            return;
        float top = y + size * ScaleGapRatio;
        for (int i = 0; i < scale.Labels.Count; i++)
        {
            float x = c.Rect.Left + c.Rect.Width * (i / (float)(scale.Labels.Count - 1));
            GaugePaint.DrawText(c.Canvas, c.Theme, scale.Labels[i], x, top, size, c.Theme.TextMuted, EdgeAlign(i, scale.Labels.Count));
        }
    }

    /// <summary>The first label of a row starts at its point, the last ends there, the others are centred on it.</summary>
    private static HorizontalAlign EdgeAlign(int index, int count) =>
        index == 0 ? HorizontalAlign.Left : index == count - 1 ? HorizontalAlign.Right : HorizontalAlign.Center;
}
