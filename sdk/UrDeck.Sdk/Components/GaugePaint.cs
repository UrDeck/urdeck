// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk.Data;

namespace UrDeck.Sdk.Components;

/// <summary>What the gauge styles share: strokes, blocks, glow, the readout and small text.</summary>
internal static class GaugePaint
{
    /// <summary>The space between a track and its readout, in thicknesses of the track.</summary>
    public const float ReadoutGap = 0.5f;

    public static SKPaint Stroke(float width, SKStrokeCap cap, SKColor color) => new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = width,
        StrokeCap = cap,
        Color = color,
    };

    /// <summary>The paint of the blurred copy a glow draws under a shape; one filter per radius, kept on the theme.</summary>
    public static SKPaint Glow(Theme theme, GaugeGlow glow, SKColor fill, float width, SKStrokeCap cap)
    {
        var color = glow.Color ?? fill;
        var paint = Stroke(width, cap, color.WithAlpha((byte)Math.Round(color.Alpha * Math.Clamp(glow.Alpha, 0f, 1f))));
        float sigma = glow.Blur / 2;
        paint.MaskFilter = theme.Gauge.Cache.Get(("blur", sigma), () => SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma));
        return paint;
    }

    /// <summary>
    /// The pieces of a track cut into blocks <paramref name="length"/> long with <paramref name="gap"/> between them, from the
    /// start to <paramref name="filled"/> (of <paramref name="total"/>); the last one is clipped, so a partial block is a partial block.
    /// </summary>
    public static IEnumerable<(float Start, float Length)> Blocks(float total, float length, float gap, float filled)
    {
        float step = Math.Max(length + gap, 0.01f);
        float end = Math.Min(total, filled);
        for (float start = 0; start < end - 0.01f; start += step)
        {
            float clipped = Math.Min(length, end - start);
            if (clipped > 0.01f)
                yield return (start, clipped);
        }
    }

    public static ReadoutOptions ReadoutOptionsFor(ReadingText reading, GaugeOptions? options, SKColor? ink) => new()
    {
        Unit = reading.Unit,
        UnitPlacement = reading.UnitPlacement,
        WidestValue = reading.WidestValue,
        Label = options?.Label,
        ValueColor = ink,
        LabelColor = ink,
        Scale = options?.ReadoutScale ?? 1f,
    };

    /// <summary>Draws the readout of a gauge in <paramref name="rect"/>; the value takes the fill's colour when the theme asks for it.</summary>
    public static SKRect DrawReadout(
        GaugeContext c,
        SKRect rect,
        HorizontalAlign horizontal = HorizontalAlign.Center,
        VerticalAlign vertical = VerticalAlign.Middle,
        bool withLabel = true)
    {
        var options = new ReadoutOptions
        {
            Label = withLabel ? c.Options.Label : null,
            Scale = c.Options.ReadoutScale,
            Horizontal = horizontal,
            Vertical = vertical,
            ValueColor = c.Current && c.G.ValueTint ? c.Fill.WithAlpha(0xFF) : null,
        };
        return Readout.Draw(c.Canvas, c.Theme, rect, c.Reading, options);
    }

    /// <summary>A line of the label role at an explicit size, for tick numbers and scale labels; <paramref name="top"/> is the top of the capitals.</summary>
    public static void DrawText(SKCanvas canvas, Theme theme, string text, float x, float top, float size, SKColor color, HorizontalAlign align)
    {
        if (string.IsNullOrEmpty(text) || size <= 0)
            return;
        text = TextStyle.Apply(theme.TextStyle.LabelCase, text);
        using var font = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Label), size);
        float tracking = theme.TextStyle.LabelTracking;
        float width = TextMetrics.Measure(font, text, tracking);
        float left = align switch
        {
            HorizontalAlign.Left => x,
            HorizontalAlign.Right => x - width,
            _ => x - width / 2,
        };
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        TextMetrics.DrawTracked(canvas, text, left, top + TextMetrics.CapHeight(font), font, paint, tracking);
    }

    /// <summary>The height of the capitals of a line at <paramref name="size"/>.</summary>
    public static float TextHeight(Theme theme, float size)
    {
        using var font = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Label), size);
        return TextMetrics.CapHeight(font);
    }
}
