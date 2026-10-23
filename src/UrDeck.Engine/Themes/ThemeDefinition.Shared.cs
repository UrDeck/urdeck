// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeGradientDefinition
{
    /// <summary>Stops, evenly spaced.</summary>
    public List<string>? Colors { get; set; }

    /// <summary>Degrees, 0 = left to right, 90 = top to bottom.</summary>
    public double? Angle { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Colors = check.Colors($"{path}.colors", Colors, 2, 6);
        Angle = check.Angle($"{path}.angle", Angle, -360, 360);
    }
}

public sealed partial class ThemeGlowDefinition
{
    /// <summary>Blur radius, fraction of a cell.</summary>
    public double? Blur { get; set; }

    public string? Color { get; set; }

    /// <summary>Strength.</summary>
    public double? Alpha { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Blur = check.Number($"{path}.blur", Blur, 0, 1);
        Color = check.Color($"{path}.color", Color);
        Alpha = check.Number($"{path}.alpha", Alpha, 0, 1);
    }
}
