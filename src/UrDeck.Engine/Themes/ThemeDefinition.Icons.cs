// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

public sealed partial class ThemeIconsDefinition
{
    /// <summary>Name of an icon set: a folder icons/&lt;set&gt;/ inside the theme folder (with a manifest.json), or a built-in set (outline). A key the set lacks is answered by the built-in 'outline' set. See Icon sets in the theme reference.</summary>
    public string? Set { get; set; }

    /// <summary>Override stroke width for set icons, fraction of the icon size.</summary>
    public double? Stroke { get; set; }

    /// <summary>'accent', 'accent2', 'text', 'ramp' (the colour of colors.ramp for the value shown, such as the temperature), 'none' (the text colour) or a #colour.</summary>
    public string? Tint { get; set; }

    /// <summary>Glow behind set icons (Rigline). Colour defaults to the icon's tint.</summary>
    public ThemeGlowDefinition? Glow { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Stroke = check.Number($"{path}.stroke", Stroke, 0.005, 0.2);
        Tint = check.IconTint($"{path}.tint", Tint);
        Glow?.Sanitize(check, $"{path}.glow");
    }
}
