// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk.Data;

namespace UrDeck.Sdk.Components;

/// <summary>What one draw of a gauge works from: the colours and the fraction are settled once, here.</summary>
internal sealed class GaugeContext
{
    public required SKCanvas Canvas { get; init; }
    public required Theme Theme { get; init; }
    public required SKRect Rect { get; init; }
    public required GaugeStyle Style { get; init; }
    public required ReadingText Reading { get; init; }
    public required GaugeOptions Options { get; init; }

    /// <summary>The fraction drawn, clamped; null for the track alone.</summary>
    public float? Fraction { get; init; }

    /// <summary>The colour of the fill: the level's, the ramp's or the gradient's at the fraction, or the muted text colour for a stale reading.</summary>
    public SKColor Fill { get; init; }

    public SKColor Track { get; init; }

    public GaugeTheme G => Theme.Gauge;

    public bool Current => Reading.IsCurrent;

    public bool HasLabel => !string.IsNullOrEmpty(Options.Label);

    public static GaugeContext Create(SKCanvas canvas, Theme theme, SKRect rect, GaugeStyle style, ReadingText reading, GaugeOptions options)
    {
        float? fraction = options.Fraction is { } f ? Math.Clamp(f, 0f, 1f) : null;
        var fill = FillColor(theme, reading, options, fraction);
        var track = reading.IsCurrent && theme.Gauge.Track?.Color is { } own ? own : fill.WithAlpha(theme.AccentDim.Alpha);
        return new GaugeContext
        {
            Canvas = canvas,
            Theme = theme,
            Rect = rect,
            Style = style,
            Reading = reading,
            Options = options,
            Fraction = fraction,
            Fill = fill,
            Track = track,
        };
    }

    /// <summary>The colours a gradient runs through: the theme's ramp for <c>"ramp"</c>, otherwise its own; null when there is none or the reading is stale.</summary>
    public IReadOnlyList<SKColor>? GradientColors =>
        Current && G.Gradient is { } gradient ? (gradient.Ramp ? Theme.Ramp : gradient.Colors) : null;

    internal static SKColor FillColor(Theme theme, ReadingText reading, GaugeOptions options, float? fraction)
    {
        if (!reading.IsCurrent)
            return theme.TextMuted;
        var g = theme.Gauge;
        if (g.ColorByRamp)
            return ColorMath.Along(theme.Ramp, fraction ?? 0f);
        if (g.Gradient is { } gradient)
            return ColorMath.Along(gradient.Ramp ? theme.Ramp : gradient.Colors, fraction ?? 0f);
        return options.Level switch
        {
            GaugeLevel.Critical => theme.Critical,
            GaugeLevel.Warning => theme.Warning,
            _ => theme.Accent,
        };
    }
}
