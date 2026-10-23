// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemePatternDefinition
{
    /// <summary>Default none.</summary>
    public string? Kind { get; set; }

    /// <summary>Distance between marks, fraction of a cell.</summary>
    public double? Spacing { get; set; }

    /// <summary>Every Nth line of a grid is drawn heavier (Blueprint: 5).</summary>
    public int? MajorEvery { get; set; }

    /// <summary>Line width / dot radius.</summary>
    public double? Thickness { get; set; }

    public string? Color { get; set; }

    /// <summary>Style of the heavier lines.</summary>
    public ThemePatternMajorDefinition? Major { get; set; }

    /// <summary>hatch/lines direction, degrees.</summary>
    public double? Angle { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Kind = check.Enum($"{path}.kind", Kind, "none", "dots", "grid", "hatch", "scanlines");
        Spacing = check.Number($"{path}.spacing", Spacing, 0.02, 2);
        MajorEvery = check.Integer($"{path}.majorEvery", MajorEvery, 2, 20);
        Thickness = check.Number($"{path}.thickness", Thickness, 0.001, 0.1);
        Color = check.Color($"{path}.color", Color);
        Major?.Sanitize(check, $"{path}.major");
        Angle = check.Angle($"{path}.angle", Angle, -180, 180);
    }
}

public sealed partial class ThemePatternMajorDefinition
{
    public string? Color { get; set; }

    public double? Thickness { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Color = check.Color($"{path}.color", Color);
        Thickness = check.Number($"{path}.thickness", Thickness, 0.001, 0.1);
    }
}
