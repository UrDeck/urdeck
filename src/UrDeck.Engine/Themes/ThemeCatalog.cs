// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Themes;

/// <summary>The four colours a picker draws a theme's swatch from.</summary>
public sealed record ThemeSwatch(string Background, string CardFill, string Accent, string Text);

/// <summary>A sample picture a theme lists in its <c>meta</c>.</summary>
public sealed record ThemeCatalogSample(string? File, string? Caption);

/// <summary>
/// One theme as a list shows it: the <c>meta</c> values, the swatch and the name of the theme's default background.
/// <see cref="Mode"/> is <c>dark</c>, <c>light</c> or null.
/// </summary>
public sealed record ThemeCatalogEntry(
    string Name,
    string DisplayName,
    string? Description,
    string? Author,
    string? Mode,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> PairsWithBackgrounds,
    IReadOnlyList<ThemeCatalogSample> Samples,
    ThemeSwatch Swatch,
    string? DefaultBackground,
    bool IsBuiltIn);

/// <summary>
/// Lists the themes that exist for a settings editor and the command line tool: the built-in ones and the user's, a user
/// theme replacing a built-in one of the same name. It reads each theme's settings and nothing else: no font is loaded and
/// no shader compiled. A theme that cannot be read is left out (its warning is logged) without affecting the others.
/// </summary>
public static class ThemeCatalog
{
    public static IReadOnlyList<ThemeCatalogEntry> List(ThemeStore store)
    {
        var names = new List<string>(ThemeStore.BuiltInNames);
        try
        {
            if (Directory.Exists(store.ThemesDirectory))
            {
                foreach (string folder in Directory.EnumerateDirectories(store.ThemesDirectory).Order(StringComparer.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileName(folder);
                    if (File.Exists(Path.Combine(folder, ThemeStore.SettingsFileName)) && !names.Contains(name, StringComparer.Ordinal))
                        names.Add(name);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UrDeckLog.Warn($"Theme list: the folder {store.ThemesDirectory} could not be read: {ex.Message}");
        }

        var entries = new List<ThemeCatalogEntry>();
        foreach (string name in names)
        {
            if (store.LoadExact(name) is not { } theme)
                continue;

            try
            {
                entries.Add(Describe(theme, ThemeStore.BuiltInNames.Contains(name) && theme.Folder == null));
            }
            catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
            {
                UrDeckLog.Warn($"Theme '{name}' could not be listed and is skipped: {ex.Message}");
            }
        }

        return entries;
    }

    private static ThemeCatalogEntry Describe(LoadedTheme theme, bool builtIn)
    {
        var meta = theme.Definition.Meta;
        var colors = theme.Definition.Colors!;
        return new ThemeCatalogEntry(
            theme.Name,
            theme.DisplayName,
            meta?.Description,
            meta?.Author,
            meta?.Mode,
            meta?.Tags ?? [],
            meta?.PairsWith?.Backgrounds ?? [],
            (meta?.Samples ?? []).Select(s => new ThemeCatalogSample(s.File, s.Caption)).ToList(),
            new ThemeSwatch(colors.Background!, colors.CardFill!, colors.Accent!, colors.Text!),
            string.IsNullOrWhiteSpace(theme.Definition.Background?.Default?.Name) ? null : theme.Definition.Background!.Default!.Name,
            builtIn);
    }
}
