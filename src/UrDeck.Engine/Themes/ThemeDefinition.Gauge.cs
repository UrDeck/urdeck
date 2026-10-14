// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Components;

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDefinition
{
    private void SanitizeGauge(ThemeValueCheck check, string themeName)
    {
        if (Gauge is not { } gauge)
            return;
        if (gauge.Style is { } style && !ThemeGaugeDefinition.IsStyle(style))
        {
            UrDeckLog.Warn($"Theme '{themeName}': gauge.style '{style}' must be {ThemeGaugeDefinition.StyleNames}; using the default.");
            gauge.Style = null;
        }
        gauge.SanitizeFormat2(check, "gauge");
    }
}

public sealed partial class ThemeGaugeDefinition
{
    internal const string StyleNames = "'ring', 'bar', 'verticalBar', 'meter', 'tach', 'marker' or 'thickbar'";

    private static readonly string[] Format2Styles = ["meter", "tach", "marker", "thickbar"];

    /// <summary>"ring", "bar" or "verticalBar", or a format 2 style: "meter", "tach", "marker" or "thickbar".</summary>
    public string? Style { get; set; }

    internal static bool IsStyle(string style) =>
        Parse(style) != null || Format2Styles.Contains(style.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>The style the SDK draws. A format 2 style is not one yet, so it gives null and the caller uses a ring.</summary>
    internal static GaugeStyle? Parse(string? style) => style?.Trim().ToLowerInvariant() switch
    {
        "ring" => GaugeStyle.Ring,
        "bar" => GaugeStyle.Bar,
        "verticalbar" => GaugeStyle.VerticalBar,
        _ => null,
    };

    internal ThemeGaugeDefinition MergeOver(ThemeGaugeDefinition? b)
    {
        var result = new ThemeGaugeDefinition { Style = Style ?? b?.Style };
        MergeFormat2(b, result);
        return result;
    }
}
