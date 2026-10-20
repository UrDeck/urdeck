// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDefinition
{
    private void SanitizeStroke(ThemeValueCheck check)
    {
        if (Stroke is not { } s)
            return;
        s.Thickness = check.Number("stroke.thickness", s.Thickness, 0.001, 1);
        if (s.Cap is { } cap && !cap.Equals("round", StringComparison.OrdinalIgnoreCase)
            && !cap.Equals("square", StringComparison.OrdinalIgnoreCase))
        {
            check.Warn(ThemeCodes.NotOneOf, "stroke.cap", $"stroke.cap '{cap}' must be 'round' or 'square'; using the default.");
            s.Cap = null;
        }
    }
}

public sealed class ThemeStrokeDefinition
{
    /// <summary>Line thickness as a fraction of the drawn element's size.</summary>
    public double? Thickness { get; set; }
    /// <summary>"round" or "square".</summary>
    public string? Cap { get; set; }

    internal ThemeStrokeDefinition MergeOver(ThemeStrokeDefinition? b) => new()
    {
        Thickness = Thickness ?? b?.Thickness,
        Cap = Cap ?? b?.Cap,
    };
}
