// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

[Format2]
public sealed partial class ThemeMetaDefinition
{
    /// <summary>Display name for the editor.</summary>
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Author { get; set; }

    public string? License { get; set; }

    public string? Version { get; set; }

    /// <summary>Lets the editor group themes and pick a matching default.</summary>
    public string? Mode { get; set; }

    public List<string>? Tags { get; set; }

    /// <summary>Recommendations for the config editor.</summary>
    public ThemeMetaPairsWithDefinition? PairsWith { get; set; }

    /// <summary>Same shape as a background manifest's samples.</summary>
    public List<ThemeMetaSampleDefinition>? Samples { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Mode = check.Enum($"{path}.mode", Mode, "dark", "light");
        Tags = check.Strings($"{path}.tags", Tags, 0, int.MaxValue);
        PairsWith?.Sanitize(check, $"{path}.pairsWith");
    }
}

[Format2]
public sealed partial class ThemeMetaPairsWithDefinition
{
    /// <summary>Background names that suit this theme, best first. Names of built-in or user backgrounds.</summary>
    public List<string>? Backgrounds { get; set; }

    internal void Sanitize(ThemeValueCheck check, string path)
    {
        Backgrounds = check.Strings($"{path}.backgrounds", Backgrounds, 0, int.MaxValue);
    }
}

[Format2]
public sealed partial class ThemeMetaSampleDefinition
{
    public string? File { get; set; }

    public string? Caption { get; set; }

}
