// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// The thick bar: a heavy track, three times as thick as a bar's, outlined when the theme asks, at the bottom of the
/// rectangle with the readout above it, left-aligned. The track is a rectangle, rounded when the theme's line ends are.
/// </summary>
internal static class ThickBarGauge
{
    private const float ThicknessRatio = 3f;

    private readonly record struct Layout(SKRect Track, float Radius, float Outline, SKRect Readout);

    public static SKRect ReadoutRect(Theme theme, SKRect rect) => Measure(theme, rect).Readout;

    public static SKRect Draw(GaugeContext c)
    {
        var layout = Measure(c.Theme, c.Rect);
        using var shape = new SKRoundRect(layout.Track, layout.Radius);
        using var track = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = c.Track };
        c.Canvas.DrawRoundRect(shape, track);

        if (c.Fraction is > 0f)
        {
            c.Canvas.Save();
            c.Canvas.ClipRoundRect(shape, SKClipOperation.Intersect, true);
            using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = c.Fill };
            if (c.GradientColors is { } colors)
                fill.Shader = GradientShader(c, layout.Track, colors);
            c.Canvas.DrawRect(new SKRect(layout.Track.Left, layout.Track.Top, layout.Track.Left + layout.Track.Width * c.Fraction.Value, layout.Track.Bottom), fill);
            c.Canvas.Restore();
        }

        if (layout.Outline > 0)
        {
            var edge = SKRect.Inflate(layout.Track, layout.Outline / 2, layout.Outline / 2);
            using var outline = new SKRoundRect(edge, layout.Radius + layout.Outline / 2);
            using var line = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = layout.Outline,
                Color = c.G.Outline?.Color ?? c.Theme.Text,
            };
            c.Canvas.DrawRoundRect(outline, line);
        }

        return GaugePaint.DrawReadout(c, layout.Readout, HorizontalAlign.Left, VerticalAlign.Bottom);
    }

    private static Layout Measure(Theme theme, SKRect rect)
    {
        float unit = BarGauge.Thickness(theme, rect);
        float outline = theme.Gauge.Outline?.Width ?? 0f;
        float height = Math.Min(unit * ThicknessRatio, Math.Max(0f, rect.Height - outline * 2));
        var track = new SKRect(rect.Left + outline, rect.Bottom - outline - height, rect.Right - outline, rect.Bottom - outline);
        float radius = theme.StrokeCap == SKStrokeCap.Round ? height / 2 : 0f;
        float readoutBottom = Math.Max(rect.Top, track.Top - outline - unit * GaugePaint.ReadoutGap);
        return new Layout(track, radius, outline, new SKRect(rect.Left, rect.Top, rect.Right, readoutBottom));
    }

    private static SKShader GradientShader(GaugeContext c, SKRect track, IReadOnlyList<SKColor> colors) =>
        c.G.Cache.Get(("thick", track, c.G.Gradient!.Ramp), () =>
            SKShader.CreateLinearGradient(new SKPoint(track.Left, track.MidY), new SKPoint(track.Right, track.MidY), [.. colors], null, SKShaderTileMode.Clamp));
}
