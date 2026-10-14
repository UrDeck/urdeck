// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDefinition
{
    private void SanitizeIndicator(ThemeValueCheck check)
    {
        if (Indicator is not { } ind)
            return;
        ind.BandHeight = check.Number("indicator.bandHeight", ind.BandHeight, 0.02, 1);
        ind.DotSize = check.Number("indicator.dotSize", ind.DotSize, 0.005, 0.5);
        ind.PillLength = check.Number("indicator.pillLength", ind.PillLength, 0.005, 1);
        ind.Spacing = check.Number("indicator.spacing", ind.Spacing, 0, 1);
        ind.Active = check.Color("indicator.active", ind.Active);
        ind.Inactive = check.Color("indicator.inactive", ind.Inactive);
        ind.Backdrop = check.Color("indicator.backdrop", ind.Backdrop);
    }
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
