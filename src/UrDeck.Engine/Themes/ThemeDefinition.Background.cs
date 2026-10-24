// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeBackgroundDefinition
{
    /// <summary>The animated background this theme selects when the user has not picked one.</summary>
    public ThemeBackgroundDefaultDefinition? Default { get; set; }

    /// <summary>Still background used when no animated background is selected, or when motion is off. Image, gradient or colour.</summary>
    public ThemeBackgroundStaticDefinition? Static { get; set; }

    /// <summary>A veil between the background and the cards, to hold text contrast.</summary>
    public ThemeBackgroundScrimDefinition? Scrim { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Static?.Sanitize(check, $"{path}.static");
        Scrim?.Sanitize(check, $"{path}.scrim");
    }
}

public sealed partial class ThemeBackgroundDefaultDefinition
{
    public string? Name { get; set; }

}

public sealed partial class ThemeBackgroundStaticDefinition
{
    /// <summary>File in the theme folder.</summary>
    public string? Image { get; set; }

    public string? Color { get; set; }

    /// <summary>Linear or radial gradient.</summary>
    public ThemeBackgroundStaticGradientDefinition? Gradient { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Color = check.Color($"{path}.color", Color);
        if (Gradient is { } gradientValue)
        {
            bool had = gradientValue.Colors is not null;
            gradientValue.Sanitize(check, $"{path}.gradient");
            if (gradientValue.Colors is null)
            {
                if (!had)
                    check.Missing($"{path}.gradient", "colors");
                Gradient = null;
            }
        }
    }
}

public sealed partial class ThemeBackgroundStaticGradientDefinition
{
    /// <summary>Stops.</summary>
    public List<string>? Colors { get; set; }

    public string? Kind { get; set; }

    /// <summary>Linear only.</summary>
    public double? Angle { get; set; }

    /// <summary>Radial centre x, fraction of width.</summary>
    public double? Cx { get; set; }

    /// <summary>Radial centre y, fraction of height.</summary>
    public double? Cy { get; set; }

    /// <summary>Radial radius, fraction of width.</summary>
    public double? Radius { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Colors = check.Colors($"{path}.colors", Colors, 2, 6);
        Kind = check.Enum($"{path}.kind", Kind, "linear", "radial");
        Angle = check.Angle($"{path}.angle", Angle, -360, 360);
        Cx = check.Number($"{path}.cx", Cx, -1, 2);
        Cy = check.Number($"{path}.cy", Cy, -1, 2);
        Radius = check.Number($"{path}.radius", Radius, 0.1, 3);
    }
}

public sealed partial class ThemeBackgroundScrimDefinition
{
    public string? Color { get; set; }

    public double? Alpha { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Color = check.Color($"{path}.color", Color);
        Alpha = check.Number($"{path}.alpha", Alpha, 0, 1);
    }
}
