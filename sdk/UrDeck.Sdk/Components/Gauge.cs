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

    /// <summary>
    /// Recent fractions, oldest first, for the sparkline under the readout. Drawn only when the theme enables the sparkline,
    /// there are two or more and the rectangle is at least 0.9 of a grid cell tall.
    /// </summary>
    public IReadOnlyList<float>? History { get; init; }

    /// <summary>A status chip beside the label. Drawn only when the theme enables the status.</summary>
    public GaugeStatusText? Status { get; init; }

    /// <summary>The options for the gauge itself once the status row and the sparkline are taken off: without them, and without the label when the row carries it.</summary>
    internal GaugeOptions ForShape(bool labelOnRow) => new()
    {
        Label = labelOnRow ? null : Label,
        Fraction = Fraction,
        TargetFraction = TargetFraction,
        Level = Level,
        ReadoutScale = ReadoutScale,
    };
}

/// <summary>
/// A formatted reading drawn with a shape that shows how far the value is along its range. The component places the
/// readout itself, because it lies inside a ring, below a bar and over a vertical bar. It draws the fraction it is
/// given, and any motion between two values is the widget's; the only thing it keeps is the shaders and blur filters of
/// the theme's gauge values, on <see cref="Theme.Gauge"/>, which the theme disposes.
/// </summary>
public static class Gauge
{
    /// <summary>Draws the gauge and returns the rectangle its readout occupies.</summary>
    public static SKRect Draw(SKCanvas canvas, Theme theme, SKRect rect, GaugeStyle style, ReadingText reading, GaugeOptions? options = null)
    {
        options ??= new GaugeOptions();
        if (rect.Width <= 0 || rect.Height <= 0)
            return SKRect.Create(rect.Left, rect.Top, 0, 0);

        var parts = GaugeParts.Plan(theme, rect, options);
        if (parts.Any)
        {
            var readout = Draw(canvas, theme, parts.Inner, style, reading, parts.Options);
            parts.Draw(canvas, theme, reading, options);
            return readout;
        }

        if (style == GaugeStyle.Plain)
            return Readout.Draw(canvas, theme, rect, reading, new ReadoutOptions { Label = options.Label, Scale = options.ReadoutScale });

        if (UsesV1(theme, style))
            return LegacyGauge.Draw(canvas, theme, rect, style, reading, options);

        var context = GaugeContext.Create(canvas, theme, rect, style, reading, options);
        return style switch
        {
            GaugeStyle.Ring or GaugeStyle.Tach or GaugeStyle.Marker => ArcGauge.Draw(context),
            GaugeStyle.Bar => BarGauge.Draw(context),
            GaugeStyle.Meter => MeterGauge.Draw(context),
            GaugeStyle.ThickBar => ThickBarGauge.Draw(context),
            _ => VerticalBarGauge.Draw(context),
        };
    }

    /// <summary>
    /// The value size in pixels that <see cref="Draw"/> would use at a <see cref="GaugeOptions.ReadoutScale"/> of 1. A widget
    /// that wants several gauges at one text size draws each at the smallest size divided by its own.
    /// </summary>
    public static float Measure(Theme theme, SKRect rect, GaugeStyle style, ReadingText reading, GaugeOptions? options = null)
    {
        // The status row and the sparkline take their share first, so the size is the one the shape is drawn at.
        if (options != null)
        {
            var parts = GaugeParts.Plan(theme, rect, options);
            if (parts.Any)
                return Measure(theme, parts.Inner, style, reading, parts.Options);
        }

        if (UsesV1(theme, style))
            return LegacyGauge.Measure(theme, rect, style, reading, options);

        return Readout.FitSize(theme, ReadoutRect(theme, rect, style, options), reading.Value, GaugePaint.ReadoutOptionsFor(reading, options, null));
    }

    /// <summary>A ring, bar or vertical bar under a theme with no gauge values is drawn as it always was.</summary>
    private static bool UsesV1(Theme theme, GaugeStyle style) =>
        theme.Gauge.IsEmpty && style is GaugeStyle.Ring or GaugeStyle.Bar or GaugeStyle.VerticalBar;

    private static SKRect ReadoutRect(Theme theme, SKRect rect, GaugeStyle style, GaugeOptions? options) => style switch
    {
        GaugeStyle.Ring or GaugeStyle.Tach or GaugeStyle.Marker => ArcGauge.Measure(theme, rect, !string.IsNullOrEmpty(options?.Label), style).Readout,
        GaugeStyle.Bar => BarGauge.ReadoutRect(theme, rect),
        GaugeStyle.Meter => MeterGauge.ReadoutRect(theme, rect),
        GaugeStyle.ThickBar => ThickBarGauge.ReadoutRect(theme, rect),
        GaugeStyle.VerticalBar => VerticalBarGauge.ReadoutRect(theme, rect),
        _ => rect,
    };
}
