// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Sdk;

namespace UrDeck.Engine.Themes;

/// <summary>
/// Reads a theme from a source: the one path a theme folder takes, for the deck and for the command line tool, so they
/// cannot disagree about it. Reads the settings leniently, checks every value, refuses font paths that leave the theme,
/// merges over the baseline and applies the rules between values; every finding goes to the sink.
/// </summary>
public static class ThemeLoader
{
    /// <summary>
    /// The theme in <paramref name="source"/> merged over <paramref name="baseline"/>, or null when it has no
    /// <c>theme.json</c> or the file cannot be read (the latter is a <see cref="ThemeCodes.FileUnusable"/> finding).
    /// </summary>
    public static LoadedTheme? Load(IThemeSource source, LoadedTheme baseline, IThemeFindings sink)
    {
        string name = source.Name;
        ThemeDefinition user;
        HashSet<string> reported;
        try
        {
            if (source.ReadSettings() is not { } text)
                return null;
            user = ThemeLenientJson.Read(text, sink, UrDeckJson.Options, out reported);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            string where = source.Folder == null ? ThemeStore.SettingsFileName : Path.Combine(source.Folder, ThemeStore.SettingsFileName);
            sink.Report(new ThemeFinding(ThemeCodes.FileUnusable, ThemeSeverity.Error, null, $"could not be read from {where} and is skipped: {ex.Message}")
            {
                Form = ThemeLogForm.Space,
            });
            return null;
        }

        user.Sanitize(sink, reported);

        // One open function per load: the resolver reads a file once per resolve, keyed by the function and the name.
        Func<string, Stream?> open = source.Open;
        ThemeFont? font = null;
        if (user.Typography?.Font is { } spec)
        {
            if (ThemeStore.IsFontFile(spec) && !source.IsInside(spec))
            {
                sink.Report(new ThemeFinding(ThemeCodes.FontFile, ThemeSeverity.Error, "typography.font", $"font '{spec}' leaves the theme folder and is ignored."));
                user.Typography.Font = null;
            }
            else
            {
                font = new ThemeFont(spec, open);
            }
        }

        var roleFonts = new Dictionary<TextRole, ThemeFont>();
        if (user.Typography?.Fonts is { } fonts)
        {
            fonts.Value = RoleFont(sink, source, open, TextRole.Value, fonts.Value, roleFonts);
            fonts.Unit = RoleFont(sink, source, open, TextRole.Unit, fonts.Unit, roleFonts);
            fonts.Label = RoleFont(sink, source, open, TextRole.Label, fonts.Label, roleFonts);
        }

        var merged = user.MergeOver(baseline.Definition);
        merged.CheckRules(sink);
        return new LoadedTheme(name, merged, font ?? baseline.Font, source.Folder) { RoleFonts = roleFonts, Findings = sink };
    }

    /// <summary>Adds a role's font to <paramref name="roleFonts"/>, or refuses a path that leaves the theme; returns the spec kept.</summary>
    private static string? RoleFont(
        IThemeFindings sink, IThemeSource source, Func<string, Stream?> open, TextRole role, string? spec, Dictionary<TextRole, ThemeFont> roleFonts)
    {
        if (string.IsNullOrWhiteSpace(spec))
            return null;
        if (ThemeStore.IsFontFile(spec) && !source.IsInside(spec))
        {
            string roleName = ThemeStore.RoleName(role);
            sink.Report(new ThemeFinding(ThemeCodes.FontFile, ThemeSeverity.Error, $"typography.fonts.{roleName}",
                $"font '{spec}' for the {roleName} role leaves the theme folder and is ignored."));
            return null;
        }
        roleFonts[role] = new ThemeFont(spec, open);
        return spec;
    }
}
