// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Components;

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDefinition
{
    private void SanitizeGauge(ThemeValueCheck check)
    {
        if (Gauge is not { } gauge)
            return;
        if (gauge.Style is { } style && !ThemeGaugeDefinition.IsStyle(style))
        {
            check.Warn(ThemeCodes.NotOneOf, "gauge.style", $"gauge.style '{style}' must be {ThemeGaugeDefinition.StyleNames}; using the default.");
            gauge.Style = null;
        }
        gauge.Sanitize(check, "gauge");
    }
}

public sealed partial class ThemeGaugeDefinition
{
    internal const string StyleNames = "'ring', 'bar', 'verticalBar', 'meter', 'tach', 'marker' or 'thickbar'";

    /// <summary>"ring", "bar" or "verticalBar", or a later style: "meter", "tach", "marker" or "thickbar".</summary>
    public string? Style { get; set; }

    internal static bool IsStyle(string style) => Parse(style) != null;

    /// <summary>The style the SDK draws, or null for a name that is none (the caller then uses the default).</summary>
    internal static GaugeStyle? Parse(string? style) => style?.Trim().ToLowerInvariant() switch
    {
        "ring" => GaugeStyle.Ring,
        "bar" => GaugeStyle.Bar,
        "verticalbar" => GaugeStyle.VerticalBar,
        "meter" => GaugeStyle.Meter,
        "tach" => GaugeStyle.Tach,
        "marker" => GaugeStyle.Marker,
        "thickbar" => GaugeStyle.ThickBar,
        _ => null,
    };

    /// <summary>Where a gauge's label sits. auto keeps the v1 placement (Prism: below).</summary>
    public string? LabelPosition { get; set; }

    /// <summary>'stacked' draws a bar with the readout above, left-aligned, and the track at the bottom (Carbon). A meter and a thick bar are stacked by definition.</summary>
    public string? Layout { get; set; }

    /// <summary>The space between a bar's, meter's or thick bar's track and its readout, in thicknesses of the track (default 0.5).</summary>
    public double? ReadoutGap { get; set; }

    /// <summary>Ring/tach/marker geometry.</summary>
    public ThemeGaugeSweepDefinition? Sweep { get; set; }

    /// <summary>Chops the fill into LED blocks.</summary>
    public ThemeGaugeSegmentsDefinition? Segments { get; set; }

    /// <summary>Outline round the track (thickbar).</summary>
    public ThemeGaugeOutlineDefinition? Outline { get; set; }

    /// <summary>Tick marks on rings/tach/meter.</summary>
    public ThemeGaugeTicksDefinition? Ticks { get; set; }

    /// <summary>Scale labels under a bar (Paper).</summary>
    public ThemeGaugeScaleDefinition? Scale { get; set; }

    public ThemeGaugeRedlineDefinition? Redline { get; set; }

    /// <summary>Fill gradient along the value: 'ramp' uses colors.ramp, or give stops.</summary>
    public ThemeGaugeGradientDefinition? Gradient { get; set; }

    /// <summary>Glow round the fill. color omitted = fill colour.</summary>
    public ThemeGlowDefinition? Glow { get; set; }

    /// <summary>Value marker (Obsidian jewel, Blueprint needle).</summary>
    public ThemeGaugeMarkerDefinition? Marker { get; set; }

    /// <summary>Centre cross on dials (Blueprint).</summary>
    public bool? Crosshair { get; set; }

    /// <summary>Overrides the default accentDim track.</summary>
    public ThemeGaugeTrackDefinition? Track { get; set; }

    /// <summary>'ramp' colours the fill by value along colors.ramp instead of the accent/warning/critical steps.</summary>
    public string? ColorBy { get; set; }

    /// <summary>Colours the readout with the same colour as the fill.</summary>
    public bool? ValueTint { get; set; }

    /// <summary>A history line under the readout; needs Stats to keep a ring buffer.</summary>
    public ThemeGaugeSparklineDefinition? Sparkline { get; set; }

    /// <summary>A status chip beside the label.</summary>
    public ThemeGaugeStatusDefinition? Status { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        LabelPosition = check.Enum($"{path}.labelPosition", LabelPosition, "auto", "inside", "below", "above");
        Layout = check.Enum($"{path}.layout", Layout, "auto", "stacked");
        ReadoutGap = check.Number($"{path}.readoutGap", ReadoutGap, 0, 4);
        Sweep?.Sanitize(check, $"{path}.sweep");
        Segments?.Sanitize(check, $"{path}.segments");
        Outline?.Sanitize(check, $"{path}.outline");
        Ticks?.Sanitize(check, $"{path}.ticks");
        Scale?.Sanitize(check, $"{path}.scale");
        Redline?.Sanitize(check, $"{path}.redline");
        Gradient?.Sanitize(check, $"{path}.gradient");
        if (Gradient is { IsEmpty: true })
            Gradient = null;
        Glow?.Sanitize(check, $"{path}.glow");
        Marker?.Sanitize(check, $"{path}.marker");
        Track?.Sanitize(check, $"{path}.track");
        ColorBy = check.Enum($"{path}.colorBy", ColorBy, "accent", "ramp");
        Sparkline?.Sanitize(check, $"{path}.sparkline");
        Status?.Sanitize(check, $"{path}.status");
    }

    internal ThemeGaugeDefinition MergeOver(ThemeGaugeDefinition? b)
    {
        var result = new ThemeGaugeDefinition { Style = Style ?? b?.Style };
        result.LabelPosition = LabelPosition;
        result.Layout = Layout;
        result.ReadoutGap = ReadoutGap;
        result.Sweep = Sweep;
        result.Segments = Segments;
        result.Outline = Outline;
        result.Ticks = Ticks;
        result.Scale = Scale;
        result.Redline = Redline;
        result.Gradient = Gradient;
        result.Glow = Glow;
        result.Marker = Marker;
        result.Crosshair = Crosshair;
        result.Track = Track;
        result.ColorBy = ColorBy;
        result.ValueTint = ValueTint;
        result.Sparkline = Sparkline;
        result.Status = Status;
        return result;
    }
}

public sealed partial class ThemeGaugeSweepDefinition
{
    /// <summary>Degrees, 0 = 3 o'clock, clockwise. v1 ring: 135.</summary>
    public double? Start { get; set; }

    /// <summary>v1 ring: 270.</summary>
    public double? Span { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Start = check.Angle($"{path}.start", Start, -360, 360);
        Span = check.Number($"{path}.span", Span, 30, 360);
    }
}

public sealed partial class ThemeGaugeSegmentsDefinition
{
    /// <summary>Segment length (bars) or arc length in cells (rings).</summary>
    public double? Length { get; set; }

    /// <summary>Space between segments.</summary>
    public double? Gap { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Length = check.Number($"{path}.length", Length, 0.005, 1);
        Gap = check.Number($"{path}.gap", Gap, 0, 1);
    }
}

public sealed partial class ThemeGaugeOutlineDefinition
{
    public double? Width { get; set; }

    public string? Color { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Width = check.Number($"{path}.width", Width, 0.002, 0.2);
        Color = check.Color($"{path}.color", Color);
    }
}

public sealed partial class ThemeGaugeTicksDefinition
{
    public int? Count { get; set; }

    public int? MajorEvery { get; set; }

    public double? Length { get; set; }

    public double? Thickness { get; set; }

    public string? Color { get; set; }

    /// <summary>Print numbers at major ticks.</summary>
    public bool? Labels { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Count = check.Integer($"{path}.count", Count, 2, 60);
        MajorEvery = check.Integer($"{path}.majorEvery", MajorEvery, 1, 20);
        Length = check.Number($"{path}.length", Length, 0.005, 0.5);
        Thickness = check.Number($"{path}.thickness", Thickness, 0.001, 0.05);
        Color = check.Color($"{path}.color", Color);
    }
}

public sealed partial class ThemeGaugeScaleDefinition
{
    /// <summary>Printed under a bar at even spacing, e.g. ["0","50","100"].</summary>
    public List<string>? Labels { get; set; }

    public double? Size { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Labels = check.Strings($"{path}.labels", Labels, 2, 11);
        Size = check.Number($"{path}.size", Size, 0.005, 0.5);
    }
}

public sealed partial class ThemeGaugeRedlineDefinition
{
    /// <summary>Fraction where the zone starts.</summary>
    public double? From { get; set; }

    public string? Color { get; set; }

    public ThemeGlowDefinition? Glow { get; set; }

    /// <summary>Zone only lights while the value is inside it (Ember).</summary>
    public bool? LightsAbove { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        From = check.Number($"{path}.from", From, 0, 1);
        Color = check.Color($"{path}.color", Color);
        Glow?.Sanitize(check, $"{path}.glow");
    }
}

public sealed partial class ThemeGaugeMarkerDefinition
{
    public string? Shape { get; set; }

    public double? Size { get; set; }

    public string? Color { get; set; }

    /// <summary>Dark ring round the marker.</summary>
    public bool? Halo { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Shape = check.Enum($"{path}.shape", Shape, "diamond", "needle");
        Size = check.Number($"{path}.size", Size, 0.005, 0.5);
        Color = check.Color($"{path}.color", Color);
    }
}

public sealed partial class ThemeGaugeTrackDefinition
{
    public string? Color { get; set; }

    /// <summary>Track width relative to stroke.thickness.</summary>
    public double? WidthRatio { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Color = check.Color($"{path}.color", Color);
        WidthRatio = check.Number($"{path}.widthRatio", WidthRatio, 0.1, 2);
    }
}

public sealed partial class ThemeGaugeSparklineDefinition
{
    public bool? Enabled { get; set; }

    public double? Height { get; set; }

    public double? HistorySeconds { get; set; }

    public double? StrokeWidth { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Height = check.Number($"{path}.height", Height, 0.05, 1);
        HistorySeconds = check.Number($"{path}.historySeconds", HistorySeconds, 10, 600);
        StrokeWidth = check.Number($"{path}.strokeWidth", StrokeWidth, 0.002, 0.1);
    }
}

public sealed partial class ThemeGaugeStatusDefinition
{
    public bool? Enabled { get; set; }

    /// <summary>[warm, hot] as 0..1. Default [0.7,0.9].</summary>
    public List<double>? Thresholds { get; set; }

    /// <summary>[ok, warm, hot].</summary>
    public List<string>? Labels { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Thresholds = check.Numbers($"{path}.thresholds", Thresholds, 2, 0, 1);
        Labels = check.Strings($"{path}.labels", Labels, 3, 3);
    }
}
