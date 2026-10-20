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
        gauge.SanitizeFormat2(check, "gauge");
    }
}

public sealed partial class ThemeGaugeDefinition
{
    internal const string StyleNames = "'ring', 'bar', 'verticalBar', 'meter', 'tach', 'marker' or 'thickbar'";

    /// <summary>"ring", "bar" or "verticalBar", or a format 2 style: "meter", "tach", "marker" or "thickbar".</summary>
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

    internal ThemeGaugeDefinition MergeOver(ThemeGaugeDefinition? b)
    {
        var result = new ThemeGaugeDefinition { Style = Style ?? b?.Style };
        MergeFormat2(b, result);
        return result;
    }
}
