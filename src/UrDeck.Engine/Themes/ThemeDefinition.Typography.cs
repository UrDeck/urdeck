// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDefinition
{
    private void SanitizeTypography(ThemeValueCheck check)
    {
        if (Typography is not { } t)
            return;
        t.LabelSize = check.Number("typography.labelSize", t.LabelSize, 0.005, 1);
        t.BodySize = check.Number("typography.bodySize", t.BodySize, 0.005, 1);
        t.TitleSize = check.Number("typography.titleSize", t.TitleSize, 0.005, 1);
        t.UnitRatio = check.Number("typography.unitRatio", t.UnitRatio, 0.05, 1);
        if (t.Font is { } font && string.IsNullOrWhiteSpace(font))
            t.Font = null;
        if (t.Weights is { } w)
        {
            w.Value = check.Number("typography.weights.value", w.Value, 1, 1000);
            w.Unit = check.Number("typography.weights.unit", w.Unit, 1, 1000);
            w.Label = check.Number("typography.weights.label", w.Label, 1, 1000);
            w.Body = check.Number("typography.weights.body", w.Body, 1, 1000);
            w.Title = check.Number("typography.weights.title", w.Title, 1, 1000);
        }
        t.Sanitize(check, "typography");
    }
}

public sealed class ThemeWeightsDefinition
{
    public double? Value { get; set; }
    public double? Unit { get; set; }
    public double? Label { get; set; }
    public double? Body { get; set; }
    public double? Title { get; set; }

    internal ThemeWeightsDefinition MergeOver(ThemeWeightsDefinition? b) => new()
    {
        Value = Value ?? b?.Value,
        Unit = Unit ?? b?.Unit,
        Label = Label ?? b?.Label,
        Body = Body ?? b?.Body,
        Title = Title ?? b?.Title,
    };
}

public sealed partial class ThemeTypographyDefinition
{
    /// <summary>An installed font family name, or a <c>.ttf</c>/<c>.otf</c> file name inside the theme's folder.</summary>
    public string? Font { get; set; }
    public ThemeWeightsDefinition? Weights { get; set; }
    public double? LabelSize { get; set; }
    public double? BodySize { get; set; }
    public double? TitleSize { get; set; }
    public double? UnitRatio { get; set; }

    /// <summary>Per-role overrides; each is a family or a file in the theme folder. Missing role falls back to typography.font.</summary>
    public ThemeTypographyFontsDefinition? Fonts { get; set; }

    /// <summary>Multiplies the readout's size (Pop and Obsidian run big).</summary>
    public double? ValueScale { get; set; }

    /// <summary>Letter case applied at draw time.</summary>
    public ThemeTypographyCaseDefinition? Case { get; set; }

    /// <summary>Letter spacing in em. Applied per glyph at draw time; the Readout's equal-width digits are kept.</summary>
    public ThemeTypographyTrackingDefinition? Tracking { get; set; }

    /// <summary>Synthetic skew of the value, degrees. Prefer a real italic file when the family has one.</summary>
    public double? Slant { get; set; }

    /// <summary>Fills the value text with a gradient (Rigline clock).</summary>
    public ThemeTypographyValueFillDefinition? ValueFill { get; set; }

    /// <summary>Glow behind value text.</summary>
    public ThemeGlowDefinition? ValueGlow { get; set; }

    /// <summary>
    /// <c>off</c> (absent) leaves a font's <c>opsz</c> axis at its default; <c>auto</c> sets it per role: the top of the axis
    /// for value and unit, the role's pixel size for label, body and title.
    /// </summary>
    public string? OpticalSize { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        ValueScale = check.Number($"{path}.valueScale", ValueScale, 0.5, 2);
        Case?.Sanitize(check, $"{path}.case");
        Tracking?.Sanitize(check, $"{path}.tracking");
        Slant = check.Number($"{path}.slant", Slant, 0, 20);
        ValueFill?.Sanitize(check, $"{path}.valueFill");
        ValueGlow?.Sanitize(check, $"{path}.valueGlow");
        OpticalSize = check.Enum($"{path}.opticalSize", OpticalSize, "off", "auto");
    }

    internal ThemeTypographyDefinition MergeOver(ThemeTypographyDefinition? b)
    {
        var result = new ThemeTypographyDefinition
        {
            Font = Font ?? b?.Font,
            Weights = (Weights ?? new ThemeWeightsDefinition()).MergeOver(b?.Weights),
            LabelSize = LabelSize ?? b?.LabelSize,
            BodySize = BodySize ?? b?.BodySize,
            TitleSize = TitleSize ?? b?.TitleSize,
            UnitRatio = UnitRatio ?? b?.UnitRatio,
        };
        result.Fonts = ThemeMerge.Group(Fonts, b?.Fonts, (own, from) => own.MergeOver(from));
        result.ValueScale = ValueScale ?? b?.ValueScale;
        result.Case = ThemeMerge.Group(Case, b?.Case, (own, from) => own.MergeOver(from));
        result.Tracking = ThemeMerge.Group(Tracking, b?.Tracking, (own, from) => own.MergeOver(from));
        result.Slant = Slant ?? b?.Slant;
        result.ValueFill = ValueFill;
        result.ValueGlow = ValueGlow;
        result.OpticalSize = OpticalSize ?? b?.OpticalSize;
        return result;
    }
}

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
