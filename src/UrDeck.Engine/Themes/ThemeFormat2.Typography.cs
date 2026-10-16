// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeTypographyDefinition
{
    /// <summary>Per-role overrides; each is a family or a file in the theme folder. Missing role falls back to typography.font.</summary>
    [Format2]
    public ThemeTypographyFontsDefinition? Fonts { get; set; }

    /// <summary>Multiplies the readout's size (Pop and Obsidian run big).</summary>
    [Format2]
    public double? ValueScale { get; set; }

    /// <summary>Letter case applied at draw time.</summary>
    [Format2]
    public ThemeTypographyCaseDefinition? Case { get; set; }

    /// <summary>Letter spacing in em. Applied per glyph at draw time; the Readout's equal-width digits are kept.</summary>
    [Format2]
    public ThemeTypographyTrackingDefinition? Tracking { get; set; }

    /// <summary>Synthetic skew of the value, degrees. Prefer a real italic file when the family has one.</summary>
    [Format2]
    public double? Slant { get; set; }

    /// <summary>Fills the value text with a gradient (Rigline clock).</summary>
    [Format2]
    public ThemeTypographyValueFillDefinition? ValueFill { get; set; }

    /// <summary>Glow behind value text.</summary>
    [Format2]
    public ThemeGlowDefinition? ValueGlow { get; set; }

    /// <summary>
    /// <c>off</c> (absent) leaves a font's <c>opsz</c> axis at its default; <c>auto</c> sets it per role: the top of the axis
    /// for value and unit, the role's pixel size for label, body and title.
    /// </summary>
    [Format2]
    public string? OpticalSize { get; set; }

    private void MergeFormat2(ThemeTypographyDefinition? b, ThemeTypographyDefinition result)
    {
        result.Fonts = ThemeMerge.Group(Fonts, b?.Fonts, (own, from) => own.MergeOver(from));
        result.ValueScale = ValueScale ?? b?.ValueScale;
        result.Case = ThemeMerge.Group(Case, b?.Case, (own, from) => own.MergeOver(from));
        result.Tracking = ThemeMerge.Group(Tracking, b?.Tracking, (own, from) => own.MergeOver(from));
        result.Slant = Slant ?? b?.Slant;
        result.ValueFill = ValueFill;
        result.ValueGlow = ValueGlow;
        result.OpticalSize = OpticalSize ?? b?.OpticalSize;
    }

    internal void SanitizeFormat2(ThemeValueCheck check, string path)
    {
        ValueScale = check.Number($"{path}.valueScale", ValueScale, 0.5, 2);
        Case?.Sanitize(check, $"{path}.case");
        Tracking?.Sanitize(check, $"{path}.tracking");
        Slant = check.Number($"{path}.slant", Slant, 0, 20);
        ValueFill?.Sanitize(check, $"{path}.valueFill");
        ValueGlow?.Sanitize(check, $"{path}.valueGlow");
        OpticalSize = check.Enum($"{path}.opticalSize", OpticalSize, "off", "auto");
    }
}

[Format2]
public sealed partial class ThemeTypographyFontsDefinition
{
    public string? Value { get; set; }

    public string? Unit { get; set; }

    public string? Label { get; set; }

    internal ThemeTypographyFontsDefinition MergeOver(ThemeTypographyFontsDefinition? b) => new()
    {
        Value = Value ?? b?.Value,
        Unit = Unit ?? b?.Unit,
        Label = Label ?? b?.Label,
    };

}

[Format2]
public sealed partial class ThemeTypographyCaseDefinition
{
    public string? Label { get; set; }

    public string? Title { get; set; }

    internal ThemeTypographyCaseDefinition MergeOver(ThemeTypographyCaseDefinition? b) => new()
    {
        Label = Label ?? b?.Label,
        Title = Title ?? b?.Title,
    };

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Label = check.Enum($"{path}.label", Label, "none", "upper", "lower");
        Title = check.Enum($"{path}.title", Title, "none", "upper", "lower");
    }
}

[Format2]
public sealed partial class ThemeTypographyTrackingDefinition
{
    /// <summary>em</summary>
    public double? Value { get; set; }

    /// <summary>em</summary>
    public double? Label { get; set; }

    /// <summary>em</summary>
    public double? Title { get; set; }

    internal ThemeTypographyTrackingDefinition MergeOver(ThemeTypographyTrackingDefinition? b) => new()
    {
        Value = Value ?? b?.Value,
        Label = Label ?? b?.Label,
        Title = Title ?? b?.Title,
    };

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Value = check.Number($"{path}.value", Value, -0.1, 0.6);
        Label = check.Number($"{path}.label", Label, -0.1, 0.6);
        Title = check.Number($"{path}.title", Title, -0.1, 0.6);
    }
}

[Format2]
public sealed partial class ThemeTypographyValueFillDefinition
{
    /// <summary>Gradient stops.</summary>
    public List<string>? Colors { get; set; }

    public double? Angle { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Colors = check.Colors($"{path}.colors", Colors, 2, 6);
        Angle = check.Angle($"{path}.angle", Angle, -360, 360);
    }
}
