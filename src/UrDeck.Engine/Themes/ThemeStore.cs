// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Themes;

/// <summary>Where a theme's font comes from: a family name or file name, and how to open a bundled file.</summary>
public sealed record ThemeFont(string Spec, Func<string, Stream?> Open);

/// <summary>A theme with every value defined (user values merged over the default theme) plus its font source.</summary>
public sealed record LoadedTheme(string Name, ThemeDefinition Definition, ThemeFont Font);

/// <summary>
/// Finds themes: user themes in <c>themes/&lt;name&gt;/theme.json</c> first, then the built-in ones embedded in this
/// assembly. Nothing is cached, so every <see cref="Load"/> re-reads the user folder.
/// </summary>
public sealed class ThemeStore(string themesDirectory)
{
    public const string DefaultName = "default-dark";
    public const string SettingsFileName = "theme.json";

    private static readonly Assembly Self = typeof(ThemeStore).Assembly;

    public static IReadOnlyList<string> BuiltInNames { get; } = [DefaultName, "default-light", "glass"];

    /// <summary>
    /// Returns the named theme. Never throws: an unknown or unusable theme gives the default theme, and a user theme's
    /// invalid values give the default theme's values, each with a logged warning.
    /// </summary>
    public LoadedTheme Load(string? name)
    {
        var baseline = LoadBuiltIn(DefaultName)!;
        if (string.IsNullOrWhiteSpace(name))
            return baseline;

        if (LoadUser(name, baseline) is { } user)
            return user;
        if (name == DefaultName)
            return baseline;
        if (LoadBuiltIn(name) is { } builtIn)
            return builtIn;

        UrDeckLog.Warn($"Theme '{name}' was not found; using '{DefaultName}'.");
        return baseline;
    }

    private LoadedTheme? LoadUser(string name, LoadedTheme baseline)
    {
        // A theme name is a folder name; anything that could point elsewhere is not a theme.
        if (name != Path.GetFileName(name) || name is "." or "..")
            return null;

        string folder = Path.GetFullPath(Path.Combine(themesDirectory, name));
        string settings = Path.Combine(folder, SettingsFileName);
        if (!File.Exists(settings))
            return null;

        ThemeDefinition user;
        try
        {
            user = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(settings), UrDeckJson.Options)
                   ?? throw new JsonException("the file is empty");
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            UrDeckLog.Warn($"Theme '{name}' could not be read from {settings} and is skipped: {ex.Message}");
            return null;
        }

        user.Sanitize(name);

        ThemeFont? font = null;
        if (user.Typography?.Font is { } spec)
        {
            if (IsFontFile(spec) && !IsInside(folder, Path.GetFullPath(Path.Combine(folder, spec))))
            {
                UrDeckLog.Warn($"Theme '{name}': font '{spec}' leaves the theme folder and is ignored.");
                user.Typography.Font = null;
            }
            else
            {
                font = new ThemeFont(spec, file => OpenThemeFile(folder, file));
            }
        }

        return new LoadedTheme(name, user.MergeOver(baseline.Definition), font ?? baseline.Font);
    }

    private static LoadedTheme? LoadBuiltIn(string name)
    {
        using var stream = Self.GetManifestResourceStream($"UrDeck.Themes.{name}.json");
        if (stream == null)
            return null;

        var definition = JsonSerializer.Deserialize<ThemeDefinition>(stream, UrDeckJson.Options)!;
        if (name != DefaultName)
            definition = definition.MergeOver(LoadBuiltIn(DefaultName)!.Definition);
        return new LoadedTheme(name, definition, new ThemeFont(definition.Typography!.Font ?? "", OpenBuiltInFile));
    }

    internal static ThemeFont BuiltInDefaultFont => LoadBuiltIn(DefaultName)!.Font;

    internal static Stream? OpenBuiltInFile(string fileName) => Self.GetManifestResourceStream($"UrDeck.Themes.{fileName}");

    internal static bool IsFontFile(string spec) =>
        spec.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
        || spec.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
        || spec.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase);

    private static FileStream? OpenThemeFile(string folder, string fileName)
    {
        string path = Path.GetFullPath(Path.Combine(folder, fileName));
        return IsInside(folder, path) && File.Exists(path) ? File.OpenRead(path) : null;
    }

    private static bool IsInside(string folder, string path) =>
        path.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
