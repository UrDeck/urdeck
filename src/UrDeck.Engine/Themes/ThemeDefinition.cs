// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.RegularExpressions;
using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Components;

namespace UrDeck.Engine.Themes;

/// <summary>
/// The shape of a theme's settings file. Every member is nullable so that a partial user theme can be merged over the
/// default theme. Sizes are fractions of the grid cell; colours are <c>#RRGGBB</c> or <c>#AARRGGBB</c>.
/// </summary>
public sealed partial class ThemeDefinition
{
    public ThemeColorsDefinition? Colors { get; set; }
    public ThemeCardDefinition? Card { get; set; }
    public ThemeTypographyDefinition? Typography { get; set; }
    public ThemeStrokeDefinition? Stroke { get; set; }
    public ThemeGaugeDefinition? Gauge { get; set; }
    public ThemeIndicatorDefinition? Indicator { get; set; }
    public ThemePressDefinition? Press { get; set; }
    public ThemeDockDefinition? Dock { get; set; }

    /// <summary>This definition's values, with anything it leaves out taken from <paramref name="baseline"/>.</summary>
    public ThemeDefinition MergeOver(ThemeDefinition baseline) => new()
    {
        Colors = (Colors ?? new ThemeColorsDefinition()).MergeOver(baseline.Colors),
        Card = (Card ?? new ThemeCardDefinition()).MergeOver(baseline.Card),
        Typography = (Typography ?? new ThemeTypographyDefinition()).MergeOver(baseline.Typography),
        Stroke = (Stroke ?? new ThemeStrokeDefinition()).MergeOver(baseline.Stroke),
        Gauge = (Gauge ?? new ThemeGaugeDefinition()).MergeOver(baseline.Gauge),
        Indicator = (Indicator ?? new ThemeIndicatorDefinition()).MergeOver(baseline.Indicator),
        Press = (Press ?? new ThemePressDefinition()).MergeOver(baseline.Press),
        Dock = (Dock ?? new ThemeDockDefinition()).MergeOver(baseline.Dock),
    };

    /// <summary>Clears every invalid value (so a merge replaces it with the default's) and logs a warning for each.</summary>
    internal void Sanitize(string themeName)
    {
        var check = new ValueCheck(themeName);
        if (Colors is { } c)
        {
            c.Background = check.Color("colors.background", c.Background);
            c.CardFill = check.Color("colors.cardFill", c.CardFill);
            c.CardBorder = check.Color("colors.cardBorder", c.CardBorder);
            c.Text = check.Color("colors.text", c.Text);
            c.TextMuted = check.Color("colors.textMuted", c.TextMuted);
            c.Accent = check.Color("colors.accent", c.Accent);
            c.AccentDim = check.Color("colors.accentDim", c.AccentDim);
            c.Good = check.Color("colors.good", c.Good);
            c.Warning = check.Color("colors.warning", c.Warning);
            c.Critical = check.Color("colors.critical", c.Critical);
        }
        if (Card is { } card)
        {
            card.Radius = check.Number("card.radius", card.Radius, 0, 1);
            card.BorderWidth = check.Number("card.borderWidth", card.BorderWidth, 0, 1);
            card.Gap = check.Number("card.gap", card.Gap, 0, 1);
            card.Padding = check.Number("card.padding", card.Padding, 0, 1);
        }
        if (Typography is { } t)
        {
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
        }
        if (Stroke is { } s)
        {
            s.Thickness = check.Number("stroke.thickness", s.Thickness, 0.001, 1);
            if (s.Cap is { } cap && !cap.Equals("round", StringComparison.OrdinalIgnoreCase)
                && !cap.Equals("square", StringComparison.OrdinalIgnoreCase))
            {
                UrDeckLog.Warn($"Theme '{themeName}': stroke.cap '{cap}' must be 'round' or 'square'; using the default.");
                s.Cap = null;
            }
        }
        if (Indicator is { } ind)
        {
            ind.BandHeight = check.Number("indicator.bandHeight", ind.BandHeight, 0.02, 1);
            ind.DotSize = check.Number("indicator.dotSize", ind.DotSize, 0.005, 0.5);
            ind.PillLength = check.Number("indicator.pillLength", ind.PillLength, 0.005, 1);
            ind.Spacing = check.Number("indicator.spacing", ind.Spacing, 0, 1);
            ind.Active = check.Color("indicator.active", ind.Active);
            ind.Inactive = check.Color("indicator.inactive", ind.Inactive);
            ind.Backdrop = check.Color("indicator.backdrop", ind.Backdrop);
        }
        if (Press is { } press)
        {
            press.Scale = check.Number("press.scale", press.Scale, 0.5, 1);
            press.Opacity = check.Number("press.opacity", press.Opacity, 0.1, 1);
        }
        if (Dock is { } dock)
            dock.Height = check.Number("dock.height", dock.Height, 0.2, 1);
        if (Gauge is { Style: { } style } && !ThemeGaugeDefinition.IsStyle(style))
        {
            UrDeckLog.Warn($"Theme '{themeName}': gauge.style '{style}' must be 'ring', 'bar' or 'verticalBar'; using the default.");
            Gauge.Style = null;
        }
    }

    private sealed partial class ValueCheck(string themeName)
    {
        [GeneratedRegex("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
        private static partial Regex ColorPattern();

        public string? Color(string path, string? value)
        {
            if (value == null || ColorPattern().IsMatch(value))
                return value;
            UrDeckLog.Warn($"Theme '{themeName}': {path} '{value}' is not #RRGGBB or #AARRGGBB; using the default.");
            return null;
        }

        public double? Number(string path, double? value, double min, double max)
        {
            if (value == null)
                return null;
            if (double.IsFinite(value.Value) && value >= min && value <= max)
                return value;
            UrDeckLog.Warn($"Theme '{themeName}': {path} {value} is outside {min}..{max}; using the default.");
            return null;
        }
    }
}

public sealed class ThemeColorsDefinition
{
    public string? Background { get; set; }
    public string? CardFill { get; set; }
    public string? CardBorder { get; set; }
    public string? Text { get; set; }
    public string? TextMuted { get; set; }
    public string? Accent { get; set; }
    public string? AccentDim { get; set; }
    public string? Good { get; set; }
    public string? Warning { get; set; }
    public string? Critical { get; set; }

    internal ThemeColorsDefinition MergeOver(ThemeColorsDefinition? b) => new()
    {
        Background = Background ?? b?.Background,
        CardFill = CardFill ?? b?.CardFill,
        CardBorder = CardBorder ?? b?.CardBorder,
        Text = Text ?? b?.Text,
        TextMuted = TextMuted ?? b?.TextMuted,
        Accent = Accent ?? b?.Accent,
        AccentDim = AccentDim ?? b?.AccentDim,
        Good = Good ?? b?.Good,
        Warning = Warning ?? b?.Warning,
        Critical = Critical ?? b?.Critical,
    };
}

/// <summary>Card shape, as fractions of the grid cell.</summary>
public sealed class ThemeCardDefinition
{
    public double? Radius { get; set; }
    public double? BorderWidth { get; set; }
    public double? Gap { get; set; }
    public double? Padding { get; set; }

    internal ThemeCardDefinition MergeOver(ThemeCardDefinition? b) => new()
    {
        Radius = Radius ?? b?.Radius,
        BorderWidth = BorderWidth ?? b?.BorderWidth,
        Gap = Gap ?? b?.Gap,
        Padding = Padding ?? b?.Padding,
    };
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

public sealed class ThemeTypographyDefinition
{
    /// <summary>An installed font family name, or a <c>.ttf</c>/<c>.otf</c> file name inside the theme's folder.</summary>
    public string? Font { get; set; }
    public ThemeWeightsDefinition? Weights { get; set; }
    public double? LabelSize { get; set; }
    public double? BodySize { get; set; }
    public double? TitleSize { get; set; }
    public double? UnitRatio { get; set; }

    internal ThemeTypographyDefinition MergeOver(ThemeTypographyDefinition? b) => new()
    {
        Font = Font ?? b?.Font,
        Weights = (Weights ?? new ThemeWeightsDefinition()).MergeOver(b?.Weights),
        LabelSize = LabelSize ?? b?.LabelSize,
        BodySize = BodySize ?? b?.BodySize,
        TitleSize = TitleSize ?? b?.TitleSize,
        UnitRatio = UnitRatio ?? b?.UnitRatio,
    };
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

public sealed class ThemeGaugeDefinition
{
    /// <summary>"ring", "bar" or "verticalBar".</summary>
    public string? Style { get; set; }

    internal static bool IsStyle(string style) => Parse(style) != null;

    internal static GaugeStyle? Parse(string? style) => style?.Trim().ToLowerInvariant() switch
    {
        "ring" => GaugeStyle.Ring,
        "bar" => GaugeStyle.Bar,
        "verticalbar" => GaugeStyle.VerticalBar,
        _ => null,
    };

    internal ThemeGaugeDefinition MergeOver(ThemeGaugeDefinition? b) => new()
    {
        Style = Style ?? b?.Style,
    };
}

/// <summary>The page indicator's look: sizes as fractions of the grid cell, and colours.</summary>
public sealed class ThemeIndicatorDefinition
{
    /// <summary>Height of the band reserved at the bottom of the screen.</summary>
    public double? BandHeight { get; set; }
    public double? DotSize { get; set; }
    public double? PillLength { get; set; }
    /// <summary>Space between neighbouring marks.</summary>
    public double? Spacing { get; set; }
    public string? Active { get; set; }
    public string? Inactive { get; set; }
    /// <summary>The translucent pill behind a floating indicator.</summary>
    public string? Backdrop { get; set; }

    internal ThemeIndicatorDefinition MergeOver(ThemeIndicatorDefinition? b) => new()
    {
        BandHeight = BandHeight ?? b?.BandHeight,
        DotSize = DotSize ?? b?.DotSize,
        PillLength = PillLength ?? b?.PillLength,
        Spacing = Spacing ?? b?.Spacing,
        Active = Active ?? b?.Active,
        Inactive = Inactive ?? b?.Inactive,
        Backdrop = Backdrop ?? b?.Backdrop,
    };
}

/// <summary>How a card looks while it is pressed: each value is a ratio, and 1 leaves the card unchanged.</summary>
public sealed class ThemePressDefinition
{
    /// <summary>The pressed card's size, 0.5 to 1.</summary>
    public double? Scale { get; set; }
    /// <summary>The pressed card's opacity, 0.1 to 1.</summary>
    public double? Opacity { get; set; }

    internal ThemePressDefinition MergeOver(ThemePressDefinition? b) => new()
    {
        Scale = Scale ?? b?.Scale,
        Opacity = Opacity ?? b?.Opacity,
    };
}

/// <summary>The dock's size, as a fraction of the grid cell.</summary>
public sealed class ThemeDockDefinition
{
    /// <summary>Height of the band reserved for the dock, which is also the side of a dock slot; 0.2 to 1.</summary>
    public double? Height { get; set; }

    internal ThemeDockDefinition MergeOver(ThemeDockDefinition? b) => new()
    {
        Height = Height ?? b?.Height,
    };
}
