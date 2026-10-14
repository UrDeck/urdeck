// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDefinition
{
    private void SanitizeCard(ThemeValueCheck check)
    {
        if (Card is not { } card)
            return;
        card.Radius = check.Number("card.radius", card.Radius, 0, 1);
        card.BorderWidth = check.Number("card.borderWidth", card.BorderWidth, 0, 1);
        card.Gap = check.Number("card.gap", card.Gap, 0, 1);
        card.Padding = check.Number("card.padding", card.Padding, 0, 1);
        card.SanitizeFormat2(check, "card");
    }
}

/// <summary>Card shape, as fractions of the grid cell.</summary>
public sealed partial class ThemeCardDefinition
{
    public double? Radius { get; set; }
    public double? BorderWidth { get; set; }
    public double? Gap { get; set; }
    public double? Padding { get; set; }

    internal ThemeCardDefinition MergeOver(ThemeCardDefinition? b)
    {
        var result = new ThemeCardDefinition
        {
            Radius = Radius ?? b?.Radius,
            BorderWidth = BorderWidth ?? b?.BorderWidth,
            Gap = Gap ?? b?.Gap,
            Padding = Padding ?? b?.Padding,
        };
        MergeFormat2(b, result);
        return result;
    }
}
