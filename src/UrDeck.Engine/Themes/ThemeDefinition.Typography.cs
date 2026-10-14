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
        t.SanitizeFormat2(check, "typography");
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
        MergeFormat2(b, result);
        return result;
    }
}
