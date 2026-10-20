// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>Where a <see cref="GaugeStyle.Bar"/> puts its readout: <see cref="Auto"/> is the bar's own layout.</summary>
public enum GaugeLayout
{
    /// <summary>The style's own layout: the track at the top of a bar, with the readout below it.</summary>
    Auto,
    /// <summary>The readout above, left-aligned, with the track at the bottom.</summary>
    Stacked,
}

/// <summary>Where a gauge's label sits.</summary>
public enum GaugeLabelPosition
{
    /// <summary>The placement the style has.</summary>
    Auto,
    /// <summary>With the value.</summary>
    Inside,
    /// <summary>Outside the shape, under it.</summary>
    Below,
    /// <summary>Outside the shape, over it.</summary>
    Above,
}

/// <summary>The shape of a marker gauge's value mark.</summary>
public enum GaugeMarkerShape
{
    Diamond,
    Needle,
}

/// <summary>Where an arc starts and how far it runs, in degrees clockwise from three o'clock.</summary>
public sealed record GaugeSweep(float Start, float Span);

/// <summary>The fill cut into blocks; lengths in pixels.</summary>
public sealed record GaugeSegments(float Length, float Gap);

/// <summary>The track's colour (null for the dimmed fill colour) and its width relative to the stroke.</summary>
public sealed record GaugeTrack(SKColor? Color, float WidthRatio);

/// <summary>An outline round a thick bar's track; the width in pixels, a null colour for the text colour.</summary>
public sealed record GaugeOutline(float Width, SKColor? Color);

/// <summary>Tick marks; lengths and thickness in pixels. A null colour is the muted text colour.</summary>
public sealed record GaugeTicks(int Count, int MajorEvery, float Length, float Thickness, SKColor? Color, bool Labels);

/// <summary>The labels printed under a meter, and their size in pixels (0 for the label step's size).</summary>
public sealed record GaugeScaleLabels(IReadOnlyList<string> Labels, float Size);

/// <summary>A glow: blur radius in pixels, a colour (null for the fill's), strength 0 to 1.</summary>
public sealed record GaugeGlow(float Blur, SKColor? Color, float Alpha);

/// <summary>The zone from <paramref name="From"/> (a fraction) to the end of the track.</summary>
public sealed record GaugeRedline(float From, SKColor? Color, GaugeGlow? Glow, bool LightsAbove);

/// <summary>The value mark of a marker or tach gauge; the size in pixels. A null colour is the fill's.</summary>
public sealed record GaugeMarker(GaugeMarkerShape Shape, float Size, SKColor? Color, bool Halo);

/// <summary>Colours laid over the whole track, evenly spaced; with <see cref="Ramp"/> the theme's colour ramp is used.</summary>
public sealed record GaugeGradient(IReadOnlyList<SKColor> Colors, float AngleDegrees, bool Ramp);

/// <summary>
/// The gauge values of the theme, already in pixels and fractions for the surface. A theme that sets none gives an instance
/// that changes nothing (<see cref="IsEmpty"/>), and the gauge then draws exactly what it drew before the values existed.
/// </summary>
public sealed class GaugeTheme : IDisposable
{
    public GaugeLayout Layout { get; init; }

    /// <summary>The space between a bar's track and its readout in thicknesses of the track, or null for the default (<see cref="GaugePaint.ReadoutGap"/>).</summary>
    public float? ReadoutGap { get; init; }

    public GaugeLabelPosition LabelPosition { get; init; }
    public GaugeSweep? Sweep { get; init; }
    public GaugeSegments? Segments { get; init; }
    public GaugeTrack? Track { get; init; }
    public GaugeOutline? Outline { get; init; }
    public GaugeTicks? Ticks { get; init; }
    public GaugeScaleLabels? Scale { get; init; }
    public GaugeRedline? Redline { get; init; }
    public GaugeMarker? Marker { get; init; }
    public bool Crosshair { get; init; }
    public GaugeGradient? Gradient { get; init; }
    public GaugeGlow? Glow { get; init; }

    /// <summary>True when the fill's colour follows the theme's ramp along the fraction instead of the level.</summary>
    public bool ColorByRamp { get; init; }

    /// <summary>True when the value is drawn in the fill's colour.</summary>
    public bool ValueTint { get; init; }

    /// <summary>The history line under the readout, or null when the theme does not enable it. Not part of <see cref="IsEmpty"/>: it is drawn round the gauge, not by it.</summary>
    public GaugeSparkline? Sparkline { get; init; }

    /// <summary>The status chip beside the label, or null when the theme does not enable it. Not part of <see cref="IsEmpty"/>.</summary>
    public GaugeStatusStyle? Status { get; init; }

    /// <summary>Shaders and blurred copies kept while the theme lives (a theme per surface size, so the size is not in the key).</summary>
    internal GaugeCache Cache { get; } = new();

    /// <summary>True when the theme sets none of the values, so the v1 drawing applies.</summary>
    public bool IsEmpty =>
        Layout == GaugeLayout.Auto && ReadoutGap == null && LabelPosition == GaugeLabelPosition.Auto && Sweep == null && Segments == null &&
        Track == null && Outline == null && Ticks == null && Scale == null && Redline == null && Marker == null &&
        !Crosshair && Gradient == null && Glow == null && !ColorByRamp && !ValueTint;

    public void Dispose() => Cache.Dispose();
}
