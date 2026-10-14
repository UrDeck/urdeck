// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeDefinition
{
    private void SanitizePressAndDock(ThemeValueCheck check)
    {
        if (Press is { } press)
        {
            press.Scale = check.Number("press.scale", press.Scale, 0.5, 1);
            press.Opacity = check.Number("press.opacity", press.Opacity, 0.1, 1);
        }
        if (Dock is { } dock)
            dock.Height = check.Number("dock.height", dock.Height, 0.2, 1);
    }
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
