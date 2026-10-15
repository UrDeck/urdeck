// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Backgrounds;

/// <summary>Why a background is recommended for a theme, best reason first.</summary>
public enum RecommendationReason
{
    /// <summary>The theme's own default background.</summary>
    ThemeDefault,

    /// <summary>The theme and the background name each other.</summary>
    Mutual,

    /// <summary>Only the theme names the background.</summary>
    ThemeListed,

    /// <summary>Only the background names the theme.</summary>
    BackgroundListed,

    /// <summary>Neither side has pairing data about the other, and they share a tag.</summary>
    TagMatch,
}

public sealed record Recommendation(string Background, RecommendationReason Reason);

/// <summary>A background listed with a theme, and whether the background names the theme too.</summary>
public sealed record PairedBackground(string Name, bool Mutual);

/// <summary>A theme listed with a background, and whether the theme names the background too.</summary>
public sealed record PairedTheme(string Name, bool Mutual);

/// <summary>
/// Joins what themes say about backgrounds (<c>meta.pairsWith.backgrounds</c>) with what backgrounds say about themes
/// (<c>pairsWith.themes</c>). A pair is listed when either side names the other and mutual when both do; the data never
/// has to be symmetric. Names are compared exactly. The index reads only the entries it is given: it loads no font,
/// compiles no shader and logs nothing, and a name that does not exist is skipped without a word.
/// </summary>
public sealed class PairingIndex
{
    private readonly Dictionary<string, ThemeCatalogEntry> _themes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BackgroundCatalogEntry> _backgrounds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _namedByAnyTheme = new(StringComparer.Ordinal);

    public PairingIndex(IEnumerable<ThemeCatalogEntry> themes, IEnumerable<BackgroundCatalogEntry> backgrounds)
    {
        foreach (var theme in themes)
        {
            _themes[theme.Name] = theme;
            foreach (string background in theme.PairsWithBackgrounds)
                _namedByAnyTheme.Add(background);
        }

        foreach (var background in backgrounds)
            _backgrounds[background.Name] = background;
    }

    public bool IsListed(string theme, string background) => NamedByTheme(theme, background) || NamedByBackground(theme, background);

    public bool IsMutual(string theme, string background) => NamedByTheme(theme, background) && NamedByBackground(theme, background);

    /// <summary>The backgrounds listed with a theme: the theme's own, in its order, then the ones that only name the theme.</summary>
    public IReadOnlyList<PairedBackground> BackgroundsFor(string theme)
    {
        var result = new List<PairedBackground>();
        if (_themes.TryGetValue(theme, out var entry))
        {
            foreach (string name in entry.PairsWithBackgrounds.Distinct(StringComparer.Ordinal))
                if (_backgrounds.ContainsKey(name))
                    result.Add(new PairedBackground(name, NamedByBackground(theme, name)));
        }

        foreach (var background in _backgrounds.Values)
            if (NamedByBackground(theme, background.Name) && !NamedByTheme(theme, background.Name))
                result.Add(new PairedBackground(background.Name, false));
        return result;
    }

    /// <summary>The themes listed with a background: the background's own, in its order, then the ones that only name the background.</summary>
    public IReadOnlyList<PairedTheme> ThemesFor(string background)
    {
        var result = new List<PairedTheme>();
        if (_backgrounds.TryGetValue(background, out var entry))
        {
            foreach (string name in entry.PairsWithThemes.Distinct(StringComparer.Ordinal))
                if (_themes.ContainsKey(name))
                    result.Add(new PairedTheme(name, NamedByTheme(name, background)));
        }

        foreach (var theme in _themes.Values)
            if (NamedByTheme(theme.Name, background) && !NamedByBackground(theme.Name, background))
                result.Add(new PairedTheme(theme.Name, false));
        return result;
    }

    /// <summary>
    /// At most <paramref name="limit"/> of <paramref name="backgrounds"/> for <paramref name="theme"/>, each with the first
    /// rung it meets: the theme's default, mutual pairs (the theme's order), pairs only the theme lists (its order), pairs
    /// only the background lists (the order given), then a shared tag for a background with no pairing data at all. A theme
    /// with no data gets an empty list. The same input always gives the same list.
    /// </summary>
    public IReadOnlyList<Recommendation> Recommend(ThemeCatalogEntry theme, IReadOnlyList<BackgroundCatalogEntry> backgrounds, int limit)
    {
        var result = new List<Recommendation>();
        if (limit <= 0)
            return result;

        // First of each name, in the order given.
        var given = new Dictionary<string, BackgroundCatalogEntry>(StringComparer.Ordinal);
        var ordered = new List<BackgroundCatalogEntry>();
        foreach (var background in backgrounds)
            if (given.TryAdd(background.Name, background))
                ordered.Add(background);

        var taken = new HashSet<string>(StringComparer.Ordinal);
        bool Add(string name, RecommendationReason reason)
        {
            if (taken.Add(name))
                result.Add(new Recommendation(name, reason));
            return result.Count >= limit;
        }

        if (theme.DefaultBackground is { } defaultName && given.ContainsKey(defaultName) && Add(defaultName, RecommendationReason.ThemeDefault))
            return result;

        foreach (string name in theme.PairsWithBackgrounds)
            if (given.TryGetValue(name, out var background) && ListsTheme(background, theme.Name) && Add(name, RecommendationReason.Mutual))
                return result;

        foreach (string name in theme.PairsWithBackgrounds)
            if (given.TryGetValue(name, out var background) && !ListsTheme(background, theme.Name) && Add(name, RecommendationReason.ThemeListed))
                return result;

        foreach (var background in ordered)
            if (ListsTheme(background, theme.Name) && !theme.PairsWithBackgrounds.Contains(background.Name, StringComparer.Ordinal)
                && Add(background.Name, RecommendationReason.BackgroundListed))
                return result;

        foreach (var background in ordered)
        {
            bool hasData = background.PairsWithThemes.Count > 0 || _namedByAnyTheme.Contains(background.Name) || theme.PairsWithBackgrounds.Contains(background.Name);
            if (!hasData && background.Tags.Intersect(theme.Tags, StringComparer.Ordinal).Any() && Add(background.Name, RecommendationReason.TagMatch))
                return result;
        }

        return result;
    }

    private static bool ListsTheme(BackgroundCatalogEntry background, string theme) => background.PairsWithThemes.Contains(theme, StringComparer.Ordinal);

    private bool NamedByTheme(string theme, string background) =>
        _themes.TryGetValue(theme, out var entry) && entry.PairsWithBackgrounds.Contains(background, StringComparer.Ordinal);

    private bool NamedByBackground(string theme, string background) =>
        _backgrounds.TryGetValue(background, out var entry) && ListsTheme(entry, theme);
}
