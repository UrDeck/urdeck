// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

[Format2]
public sealed partial class ThemeIconsDefinition
{
    /// <summary>Folder name inside the theme folder (icons/&lt;set&gt;/&lt;key&gt;.svg), or a built-in set name. Keys are in 04-widgets-and-icons.md. A missing key falls back to the built-in 'outline' set, then to the widget's own icon.</summary>
    public string? Set { get; set; }

    /// <summary>Override stroke width for set icons, fraction of the icon size.</summary>
    public double? Stroke { get; set; }

    /// <summary>'accent', 'accent2', 'text', 'ramp', 'none' (keep the SVG's colours) or a #colour.</summary>
    public string? Tint { get; set; }

    /// <summary>Glow behind set icons (Rigline). Colour defaults to the icon's tint.</summary>
    public ThemeGlowDefinition? Glow { get; set; }

    /// <summary>false = static icons even where the widget would use Lottie (weather). Default true.</summary>
    public bool? Animated { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Stroke = check.Number($"{path}.stroke", Stroke, 0.005, 0.2);
        Tint = check.IconTint($"{path}.tint", Tint);
        Glow?.Sanitize(check, $"{path}.glow");
    }
}
