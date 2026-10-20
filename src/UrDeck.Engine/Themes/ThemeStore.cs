// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk;

namespace UrDeck.Engine.Themes;

/// <summary>Where a theme's font comes from: a family name or file name, and how to open a bundled file.</summary>
public sealed record ThemeFont(string Spec, Func<string, Stream?> Open);

/// <summary>
/// A theme with every value defined (user values merged over the default theme) plus its font source. <see cref="Folder"/>
/// is the user theme's folder, which its picture is read from; null for a built-in theme.
/// </summary>
public sealed record LoadedTheme(string Name, ThemeDefinition Definition, ThemeFont Font, string? Folder = null)
{
    /// <summary>
    /// The fonts <c>typography.fonts</c> names for the value, unit and label roles. A role that is not here uses
    /// <see cref="Font"/>.
    /// </summary>
    public IReadOnlyDictionary<TextRole, ThemeFont> RoleFonts { get; init; } = new Dictionary<TextRole, ThemeFont>();

    /// <summary>What the resolver has already warned about for this theme, so each finding is logged once per load.</summary>
    internal ThemeWarnings Warnings => LazyInitializer.EnsureInitialized(ref _warnings, () => new ThemeWarnings(Findings))!;

    private ThemeWarnings? _warnings;

    /// <summary>Where this theme's findings go: the log unless the loader says otherwise (the command line tool collects them).</summary>
    internal IThemeFindings Findings { get; init; } = new LogFindings(Name);

    /// <summary>The theme's <c>meta.name</c>, or its folder name when it has none.</summary>
    public string DisplayName => Definition.Meta?.Name ?? Name;

    /// <summary>The theme's <c>meta.mode</c> (<c>dark</c> or <c>light</c>), or null. Informational: it changes no colour.</summary>
    public string? Mode => Definition.Meta?.Mode;
}

/// <summary>
/// Finds themes: user themes in <c>themes/&lt;name&gt;/theme.json</c> first, then the built-in ones embedded in this
/// assembly. Nothing is cached, so every <see cref="Load"/> re-reads the user folder.
/// </summary>
public sealed class ThemeStore(string themesDirectory)
{
    public const string DefaultName = "default-dark";
    public const string SettingsFileName = "theme.json";

    private static readonly Assembly Self = typeof(ThemeStore).Assembly;

    public static IReadOnlyList<string> BuiltInNames { get; } = [DefaultName, "default-light", "glass", "carbon", "paper"];

    /// <summary>The folder user themes are read from.</summary>
    public string ThemesDirectory => themesDirectory;

    /// <summary>
    /// The theme of that name exactly as it is: the user's, else the built-in one, else null (no fallback to the default
    /// theme, no warning that it is missing). A user theme that cannot be read logs its warning and gives null, or the
    /// built-in theme of that name.
    /// </summary>
    internal LoadedTheme? LoadExact(string name)
    {
        var baseline = LoadBuiltIn(DefaultName)!;
        return LoadUser(name, baseline) ?? (name == DefaultName ? baseline : LoadBuiltIn(name));
    }

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

    private LoadedTheme? LoadUser(string name, LoadedTheme baseline, IThemeFindings? findings = null)
    {
        // A theme name is a folder name; anything that could point elsewhere is not a theme.
        if (name != Path.GetFileName(name) || name is "." or "..")
            return null;

        string folder = Path.GetFullPath(Path.Combine(themesDirectory, name));
        string settings = Path.Combine(folder, SettingsFileName);
        if (!File.Exists(settings))
            return null;

        var sink = findings ?? new LogFindings(name);
        ThemeDefinition user;
        HashSet<string> reported;
        try
        {
            user = ThemeLenientJson.Read(File.ReadAllText(settings), sink, UrDeckJson.Options, out reported);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            sink.Report(new ThemeFinding(ThemeCodes.FileUnusable, ThemeSeverity.Error, null, $"could not be read from {settings} and is skipped: {ex.Message}") { Form = ThemeLogForm.Space });
            return null;
        }

        user.Sanitize(sink, reported);

        // One open function per folder: the resolver reads a file once per resolve, keyed by the function and the name.
        Func<string, Stream?> open = file => OpenThemeFile(folder, file);
        ThemeFont? font = null;
        if (user.Typography?.Font is { } spec)
        {
            if (IsFontFile(spec) && !IsInside(folder, ResolveIn(folder, spec)))
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
            fonts.Value = RoleFont(sink, folder, open, TextRole.Value, fonts.Value, roleFonts);
            fonts.Unit = RoleFont(sink, folder, open, TextRole.Unit, fonts.Unit, roleFonts);
            fonts.Label = RoleFont(sink, folder, open, TextRole.Label, fonts.Label, roleFonts);
        }

        var merged = user.MergeOver(baseline.Definition);
        merged.CheckRules(sink);
        return new LoadedTheme(name, merged, font ?? baseline.Font, folder) { RoleFonts = roleFonts, Findings = sink };
    }

    /// <summary>Adds a role's font to <paramref name="roleFonts"/>, or refuses a path that leaves the folder; returns the spec kept.</summary>
    private static string? RoleFont(
        IThemeFindings sink, string folder, Func<string, Stream?> open, TextRole role, string? spec, Dictionary<TextRole, ThemeFont> roleFonts)
    {
        if (string.IsNullOrWhiteSpace(spec))
            return null;
        if (IsFontFile(spec) && !IsInside(folder, ResolveIn(folder, spec)))
        {
            sink.Report(new ThemeFinding(ThemeCodes.FontFile, ThemeSeverity.Error, $"typography.fonts.{RoleName(role)}", $"font '{spec}' for the {RoleName(role)} role leaves the theme folder and is ignored."));
            return null;
        }
        roleFonts[role] = new ThemeFont(spec, open);
        return spec;
    }

    internal static string RoleName(TextRole role) => role.ToString().ToLowerInvariant();

    /// <summary>The built-in theme of that name (one of <see cref="BuiltInNames"/>), or null. Reads no folder and logs nothing.</summary>
    public static LoadedTheme? LoadBuiltIn(string name)
    {
        using var stream = Self.GetManifestResourceStream($"UrDeck.Themes.{name}.json");
        if (stream == null)
            return null;

        var definition = JsonSerializer.Deserialize<ThemeDefinition>(stream, UrDeckJson.Options)!;
        if (name != DefaultName)
            definition = definition.MergeOver(LoadBuiltIn(DefaultName)!.Definition);

        var roleFonts = new Dictionary<TextRole, ThemeFont>();
        if (definition.Typography!.Fonts is { } fonts)
            foreach (var (role, spec) in new[] { (TextRole.Value, fonts.Value), (TextRole.Unit, fonts.Unit), (TextRole.Label, fonts.Label) })
                if (!string.IsNullOrWhiteSpace(spec))
                    roleFonts[role] = new ThemeFont(spec, OpenBuiltInFile);
        return new LoadedTheme(name, definition, new ThemeFont(definition.Typography.Font ?? "", OpenBuiltInFile)) { RoleFonts = roleFonts };
    }

    internal static ThemeFont BuiltInDefaultFont => LoadBuiltIn(DefaultName)!.Font;

    internal static Stream? OpenBuiltInFile(string fileName) => Self.GetManifestResourceStream($"UrDeck.Themes.{fileName}");

    internal static bool IsFontFile(string spec) =>
        spec.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
        || spec.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
        || spec.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase);

    private static FileStream? OpenThemeFile(string folder, string fileName)
    {
        string path = ResolveIn(folder, fileName);
        return IsInside(folder, path) && File.Exists(path) ? File.OpenRead(path) : null;
    }

    // A backslash is a separator in a theme file on every platform, so ..\x cannot slip past the check where it is not one.
    internal static string ResolveIn(string folder, string relative) =>
        Path.GetFullPath(Path.Combine(folder, relative.Replace('\\', Path.DirectorySeparatorChar)));

    internal static bool IsInside(string folder, string path) =>
        path.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
