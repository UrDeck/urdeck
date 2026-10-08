// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk.Data;

namespace UrDeck.Sdk.Components;

/// <summary>What a widget tells <see cref="Gauge"/> beyond the reading.</summary>
public sealed class GaugeOptions
{
    public string? Label { get; init; }

    /// <summary>The fraction to draw now, 0 to 1; null draws the track alone.</summary>
    public float? Fraction { get; init; }

    /// <summary>The fraction the gauge is heading for, when it is not the one drawn now (an ease). Defaults to <see cref="Fraction"/>.</summary>
    public float? TargetFraction { get; init; }

    public GaugeLevel Level { get; init; }

    /// <summary>Fraction of the fitted readout size to draw at, from 0.1 to 1.0; lets the gauges of one card share a size.</summary>
    public float ReadoutScale { get; init; } = 1f;
}

/// <summary>
/// A formatted reading drawn with a shape that shows how far the value is along its range. The component places the
/// readout itself, because it lies inside a ring, below a bar and over a vertical bar. It keeps no state: it draws the
/// fraction it is given, and any motion between two values is the widget's.
/// </summary>
public static class Gauge
{
    private const float RingSweep = 270f;
    private const float RingStart = 135f;
    private const float BarReadoutGap = 0.5f;
    private const float VerticalReadoutShare = 0.45f;

    /// <summary>Draws the gauge and returns the rectangle its readout occupies.</summary>
    public static SKRect Draw(SKCanvas canvas, Theme theme, SKRect rect, GaugeStyle style, ReadingText reading, GaugeOptions? options = null)
    {
        options ??= new GaugeOptions();
        if (rect.Width <= 0 || rect.Height <= 0)
            return SKRect.Create(rect.Left, rect.Top, 0, 0);

        var readoutOptions = new ReadoutOptions { Label = options.Label, Scale = options.ReadoutScale };
        if (style == GaugeStyle.Plain)
            return Readout.Draw(canvas, theme, rect, reading, readoutOptions);

        var readout = ReadoutRect(theme, rect, style);
        var fill = FillColor(theme, reading, options);
        var track = fill.WithAlpha(theme.AccentDim.Alpha);
        float? fraction = options.Fraction is { } f ? Math.Clamp(f, 0f, 1f) : null;

        switch (style)
        {
            case GaugeStyle.Ring:
                DrawRing(canvas, theme, rect, track, fill, fraction);
                break;
            case GaugeStyle.Bar:
                DrawBar(canvas, theme, rect, track, fill, fraction);
                break;
            default:
                DrawVerticalBar(canvas, theme, rect, track, fill, fraction);
                if (OverFill(rect, readout, options.TargetFraction ?? options.Fraction))
                {
                    var ink = Contrast(theme, fill);
                    return Readout.Draw(canvas, theme, readout, reading.Value, ForReading(reading, options, ink));
                }
                break;
        }

        return Readout.Draw(canvas, theme, readout, reading, readoutOptions);
    }

    /// <summary>
    /// The value size in pixels that <see cref="Draw"/> would use at a <see cref="GaugeOptions.ReadoutScale"/> of 1. A widget
    /// that wants several gauges at one text size draws each at the smallest size divided by its own.
    /// </summary>
    public static float Measure(Theme theme, SKRect rect, GaugeStyle style, ReadingText reading, GaugeOptions? options = null) =>
        Readout.FitSize(theme, ReadoutRect(theme, rect, style), reading.Value, ForReading(reading, options, null));

    private static ReadoutOptions ForReading(ReadingText reading, GaugeOptions? options, SKColor? ink) => new()
    {
        Unit = reading.Unit,
        UnitPlacement = reading.UnitPlacement,
        WidestValue = reading.WidestValue,
        Label = options?.Label,
        ValueColor = ink,
        LabelColor = ink,
        Scale = options?.ReadoutScale ?? 1f,
    };

    private static SKColor FillColor(Theme theme, ReadingText reading, GaugeOptions options)
    {
        if (!reading.IsCurrent)
            return theme.TextMuted;
        return options.Level switch
        {
            GaugeLevel.Critical => theme.Critical,
            GaugeLevel.Warning => theme.Warning,
            _ => theme.Accent,
        };
    }

    private static SKRect ReadoutRect(Theme theme, SKRect rect, GaugeStyle style)
    {
        switch (style)
        {
            case GaugeStyle.Ring:
                {
                    float diameter = Math.Min(rect.Width, rect.Height);
                    float inner = diameter / 2 - diameter * theme.StrokeRatio;
                    return SKRect.Create(rect.MidX - inner * 0.7f, rect.MidY - inner * 0.6f, inner * 1.4f, inner * 1.2f);
                }
            case GaugeStyle.Bar:
                {
                    float top = rect.Top + Thickness(theme, rect) * (1 + BarReadoutGap);
                    return new SKRect(rect.Left, Math.Min(top, rect.Bottom), rect.Right, rect.Bottom);
                }
            case GaugeStyle.VerticalBar:
                {
                    float inset = Thickness(theme, rect);
                    var inside = SKRect.Inflate(rect, -inset, -inset);
                    float height = inside.Height * VerticalReadoutShare;
                    return new SKRect(inside.Left, inside.Bottom - height, inside.Right, inside.Bottom);
                }
            default:
                return rect;
        }
    }

    private static float Thickness(Theme theme, SKRect rect) => Math.Min(rect.Width, rect.Height) * theme.StrokeRatio;

    private static void DrawRing(SKCanvas canvas, Theme theme, SKRect rect, SKColor track, SKColor fill, float? fraction)
    {
        float diameter = Math.Min(rect.Width, rect.Height);
        float stroke = diameter * theme.StrokeRatio;
        var oval = SKRect.Create(rect.MidX - diameter / 2 + stroke / 2, rect.MidY - diameter / 2 + stroke / 2, diameter - stroke, diameter - stroke);
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke, StrokeCap = theme.StrokeCap };

        paint.Color = track;
        canvas.DrawArc(oval, RingStart, RingSweep, false, paint);
        if (fraction is > 0f)
        {
            paint.Color = fill;
            canvas.DrawArc(oval, RingStart, RingSweep * fraction.Value, false, paint);
        }
    }

    private static void DrawBar(SKCanvas canvas, Theme theme, SKRect rect, SKColor track, SKColor fill, float? fraction)
    {
        float thickness = Thickness(theme, rect);
        float y = rect.Top + thickness / 2;
        float left = rect.Left + thickness / 2;
        float right = rect.Right - thickness / 2;
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = thickness, StrokeCap = theme.StrokeCap };

        paint.Color = track;
        canvas.DrawLine(left, y, right, y, paint);
        if (fraction is > 0f)
        {
            paint.Color = fill;
            canvas.DrawLine(left, y, left + (right - left) * fraction.Value, y, paint);
        }
    }

    private static void DrawVerticalBar(SKCanvas canvas, Theme theme, SKRect rect, SKColor track, SKColor fill, float? fraction)
    {
        float radius = Math.Min(theme.CardRadius, Math.Min(rect.Width, rect.Height) / 2);
        using var shape = new SKRoundRect(rect, radius);
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = track };
        canvas.DrawRoundRect(shape, paint);
        if (fraction is not > 0f)
            return;

        canvas.Save();
        canvas.ClipRoundRect(shape, SKClipOperation.Intersect, true);
        paint.Color = fill;
        canvas.DrawRect(new SKRect(rect.Left, rect.Bottom - rect.Height * fraction.Value, rect.Right, rect.Bottom), paint);
        canvas.Restore();
    }

    /// <summary>True when the fill reaches the middle of the readout, so the text lies over it.</summary>
    private static bool OverFill(SKRect rect, SKRect readout, float? fraction) =>
        fraction is { } f && rect.Bottom - rect.Height * Math.Clamp(f, 0f, 1f) <= readout.MidY;

    /// <summary>The theme's text or background colour, whichever contrasts more with <paramref name="fill"/> (WCAG).</summary>
    private static SKColor Contrast(Theme theme, SKColor fill)
    {
        // Colours are compared as opaque: a translucent background still stands for its hue.
        double lf = Luminance(fill);
        double text = Ratio(lf, Luminance(theme.Text));
        double background = Ratio(lf, Luminance(theme.Background));
        return text >= background ? theme.Text.WithAlpha(0xFF) : theme.Background.WithAlpha(0xFF);
    }

    private static double Ratio(double a, double b) => (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);

    private static double Luminance(SKColor c) =>
        0.2126 * Linear(c.Red) + 0.7152 * Linear(c.Green) + 0.0722 * Linear(c.Blue);

    private static double Linear(byte channel)
    {
        double s = channel / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
