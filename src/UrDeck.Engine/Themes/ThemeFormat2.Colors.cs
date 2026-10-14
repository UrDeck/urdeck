// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeColorsDefinition
{
    /// <summary>A second accent for gradients, markers, redlines, tab highlights. Default: accent.</summary>
    [Format2]
    public string? Accent2 { get; set; }

    /// <summary>Low-to-high load colours. Default: [good, warning, critical]. Used when gauge.colorBy is 'ramp'.</summary>
    [Format2]
    public List<string>? Ramp { get; set; }

    /// <summary>Fill colours that cards cycle through when card.tint is 'cycle' (Pop).</summary>
    [Format2]
    public List<string>? CardTints { get; set; }

    /// <summary>Text colour used on top of an accent or tint fill. Default: whichever of text/background contrasts most with the fill.</summary>
    [Format2]
    public string? Ink { get; set; }

    private void MergeFormat2(ThemeColorsDefinition? b, ThemeColorsDefinition result)
    {
        result.Accent2 = Accent2 ?? b?.Accent2;
        result.Ramp = Ramp;
        result.CardTints = CardTints;
        result.Ink = Ink ?? b?.Ink;
    }

    internal void SanitizeFormat2(ThemeValueCheck check, string path)
    {
        Accent2 = check.Color($"{path}.accent2", Accent2);
        Ramp = check.Colors($"{path}.ramp", Ramp, 2, 5);
        CardTints = check.Colors($"{path}.cardTints", CardTints, 1, 8);
        Ink = check.Color($"{path}.ink", Ink);
    }
}
