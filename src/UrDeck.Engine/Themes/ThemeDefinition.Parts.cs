// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDividerDefinition
{
    /// <summary>dimension = a line with end ticks and an optional centred label (Blueprint).</summary>
    public string? Style { get; set; }

    public string? Color { get; set; }

    public double? Thickness { get; set; }

    public string? GradientTo { get; set; }

    internal ThemeDividerDefinition MergeOver(ThemeDividerDefinition? b) => new()
    {
        Style = Style ?? b?.Style,
        Color = Color ?? b?.Color,
        Thickness = Thickness ?? b?.Thickness,
        GradientTo = GradientTo ?? b?.GradientTo,
    };

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Style = check.Enum($"{path}.style", Style, "none", "line", "gradient", "dimension");
        Color = check.Color($"{path}.color", Color);
        Thickness = check.Number($"{path}.thickness", Thickness, 0.001, 0.05);
        GradientTo = check.Color($"{path}.gradientTo", GradientTo);
    }
}

public sealed partial class ThemeWellDefinition
{
    public string? Fill { get; set; }

    public double? Radius { get; set; }

    public string? Border { get; set; }

    internal ThemeWellDefinition MergeOver(ThemeWellDefinition? b) => new()
    {
        Fill = Fill ?? b?.Fill,
        Radius = Radius ?? b?.Radius,
        Border = Border ?? b?.Border,
    };

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Fill = check.Color($"{path}.fill", Fill);
        Radius = check.Number($"{path}.radius", Radius, 0, 0.5);
        Border = check.Color($"{path}.border", Border);
    }
}

public sealed partial class ThemeChipDefinition
{
    public string? Shape { get; set; }

    public string? Fill { get; set; }

    public string? Border { get; set; }

    public string? TextColor { get; set; }

    /// <summary>status tints the chip by good/warning/critical.</summary>
    public string? ColorBy { get; set; }

    internal ThemeChipDefinition MergeOver(ThemeChipDefinition? b) => new()
    {
        Shape = Shape ?? b?.Shape,
        Fill = Fill ?? b?.Fill,
        Border = Border ?? b?.Border,
        TextColor = TextColor ?? b?.TextColor,
        ColorBy = ColorBy ?? b?.ColorBy,
    };

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Shape = check.Enum($"{path}.shape", Shape, "none", "pill");
        Fill = check.Color($"{path}.fill", Fill);
        Border = check.Color($"{path}.border", Border);
        TextColor = check.Color($"{path}.textColor", TextColor);
        ColorBy = check.Enum($"{path}.colorBy", ColorBy, "none", "status");
    }
}
