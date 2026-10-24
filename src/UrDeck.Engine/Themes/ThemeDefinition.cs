// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

/// <summary>
/// The shape of a theme's settings file. Every member is nullable so that a partial user theme can be merged over the
/// default theme. Sizes are fractions of the grid cell; colours are <c>#RRGGBB</c> or <c>#AARRGGBB</c>. The groups live
/// in <c>ThemeDefinition.&lt;Group&gt;.cs</c>.
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

    /// <summary>Describes the theme; never affects drawing, and never taken from the default theme.</summary>
    public ThemeMetaDefinition? Meta { get; set; }

    /// <summary>A texture over the page background; never taken from the default theme.</summary>
    public ThemePatternDefinition? Pattern { get; set; }

    public ThemeDividerDefinition? Divider { get; set; }

    public ThemeChipDefinition? Chip { get; set; }

    public ThemeWellDefinition? Well { get; set; }

    /// <summary>The theme's own icon set; never taken from the default theme.</summary>
    public ThemeIconsDefinition? Icons { get; set; }

    /// <summary>The background the theme pairs with; never taken from the default theme.</summary>
    public ThemeBackgroundDefinition? Background { get; set; }

    public ThemeMotionDefinition? Motion { get; set; }

    /// <summary>
    /// This definition's values, with anything it leaves out taken from <paramref name="baseline"/>. A group that
    /// neither has stays absent, and the groups listed in the theme spec are never taken from the baseline at all.
    /// </summary>
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
        Meta = Meta,
        Pattern = Pattern,
        Divider = ThemeMerge.Group(Divider, baseline.Divider, (own, from) => own.MergeOver(from)),
        Chip = ThemeMerge.Group(Chip, baseline.Chip, (own, from) => own.MergeOver(from)),
        Well = ThemeMerge.Group(Well, baseline.Well, (own, from) => own.MergeOver(from)),
        Icons = Icons,
        Background = Background,
        Motion = ThemeMerge.Group(Motion, baseline.Motion, (own, from) => own.MergeOver(from)),
    };

    /// <summary>Clears every invalid value (so a merge replaces it with the default's) and logs a warning for each.</summary>
    internal void Sanitize(IThemeFindings sink, HashSet<string>? reported = null)
    {
        var check = new ThemeValueCheck(sink, reported);
        SanitizeColors(check);
        SanitizeCard(check);
        SanitizeTypography(check);
        SanitizeStroke(check);
        SanitizeIndicator(check);
        SanitizePressAndDock(check);
        SanitizeGauge(check);
        Meta?.Sanitize(check, "meta");
        Pattern?.Sanitize(check, "pattern");
        Divider?.Sanitize(check, "divider");
        Chip?.Sanitize(check, "chip");
        Well?.Sanitize(check, "well");
        Icons?.Sanitize(check, "icons");
        Background?.Sanitize(check, "background");
        Motion?.Sanitize(check, "motion");
    }
}
