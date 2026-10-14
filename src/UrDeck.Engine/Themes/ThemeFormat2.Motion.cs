// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

[Format2]
public sealed partial class ThemeMotionDefinition
{
    /// <summary>The ceiling for theme-driven animation (borderAnimate, and what a later change adds). The effective level is the lower of this and the user/OS setting. Does not touch the shader background, which has its own control.</summary>
    public string? Level { get; set; }

    internal ThemeMotionDefinition MergeOver(ThemeMotionDefinition? b) => new()
    {
        Level = Level ?? b?.Level,
    };

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Level = check.Enum($"{path}.level", Level, "full", "reduced", "off");
    }
}
