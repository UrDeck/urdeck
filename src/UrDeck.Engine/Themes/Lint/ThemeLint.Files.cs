// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.RegularExpressions;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Icons;

namespace UrDeck.Engine.Themes;

public static partial class ThemeLint
{
    /// <summary>The licence files that may sit beside a font: <c>*-OFL.txt</c>, <c>LICENSE*</c> and <c>*-LICENSE*</c>.</summary>
    [GeneratedRegex(@"^(LICENSE.*|.+-LICENSE.*|.+-OFL\.txt)$", RegexOptions.IgnoreCase)]
    private static partial Regex LicenceName();

    /// <summary>The font files the theme names, as files: they exist, are small enough and carry their licence.</summary>
    private static void CheckFonts(IThemeSource source, ThemeDefinition user, List<ThemeDiagnostic> found)
    {
        string file = ThemeStore.SettingsFileName;
        var specs = new List<(string Key, string Spec)>();
        if (!string.IsNullOrWhiteSpace(user.Typography?.Font))
            specs.Add(("typography.font", user.Typography!.Font!));
        if (user.Typography?.Fonts is { } fonts)
        {
            foreach (var (key, spec) in new[] { ("value", fonts.Value), ("unit", fonts.Unit), ("label", fonts.Label) })
                if (!string.IsNullOrWhiteSpace(spec))
                    specs.Add(($"typography.fonts.{key}", spec));
        }

        var licences = new Lazy<bool>(() => source.Files().Any(f => LicenceName().IsMatch(f)));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, spec) in specs)
        {
            if (!ThemeStore.IsFontFile(spec))
            {
                if (LooksLikeFile(spec))
                {
                    found.Add(Error(ThemeCodes.FontFile,
                        $"{key} '{spec}' is not a .ttf, .otf or .ttc font file; use one of those formats, or name an installed font family.", file, key));
                }
                else
                {
                    found.Add(Warning(ThemeCodes.FontFamily,
                        $"{key} '{spec}' is a font family, so it is used only where that font is installed; put a .ttf or .otf in the theme's folder and name the file.", file, key));
                }
                continue;
            }

            // A path that leaves the theme is already a finding of the loader.
            if (!source.IsInside(spec))
                continue;
            if (source.Length(spec) is not { } length)
            {
                string where = source.Folder == null ? "shipped with the product" : "in the theme's folder";
                found.Add(Error(ThemeCodes.FontFile, $"{key} names '{spec}', which is not {where}; add the file or fix the name.", file, key));
                continue;
            }

            if (!seen.Add(spec))
                continue;
            if (length > MaxFontBytes)
            {
                found.Add(Warning(ThemeCodes.FontFileSize,
                    $"font file '{spec}' is {length / 1024.0 / 1024.0:0.0} MB, over 3 MB; subset it to the characters the deck draws to keep the theme small.", spec, key));
            }
            if (!licences.Value)
            {
                found.Add(Warning(ThemeCodes.FontFileSize,
                    $"font file '{spec}' has no licence file beside it; add its licence (for example {Path.GetFileNameWithoutExtension(spec)}-OFL.txt) so the theme can be shared.", spec, key));
            }
        }
    }

    /// <summary>A name with an extension and no space is meant as a file; "Segoe UI" is a family.</summary>
    private static bool LooksLikeFile(string spec) =>
        !spec.Contains(' ', StringComparison.Ordinal) && Path.GetExtension(spec).Length is > 1 and <= 6;

    /// <summary>The backgrounds, pairings, still picture and icon set the theme names, against what exists.</summary>
    private static void CheckBackgrounds(IThemeSource source, ThemeDefinition definition, ThemeLintOptions options, List<ThemeDiagnostic> found)
    {
        string file = ThemeStore.SettingsFileName;
        var known = new HashSet<string>(ShaderStore.BuiltInNames, StringComparer.OrdinalIgnoreCase);
        foreach (string directory in options.BackgroundDirectories)
        {
            try
            {
                foreach (string folder in Directory.EnumerateDirectories(directory))
                    if (BackgroundSettings.IsValidName(Path.GetFileName(folder)))
                        known.Add(Path.GetFileName(folder));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A directory that cannot be listed adds no backgrounds; the pairing then says so for each name it needs.
            }
        }

        string? background = definition.Background?.Default?.Name;
        if (!string.IsNullOrWhiteSpace(background) && !known.Contains(background))
        {
            found.Add(Warning(ThemeCodes.UnknownBackground,
                $"background.default.name '{background}' is not a background that exists; use one of the built-in backgrounds, or give the folder of yours with --background-dir.",
                file, "background.default.name"));
        }

        var pairs = definition.Meta?.PairsWith?.Backgrounds ?? [];
        for (int i = 0; i < pairs.Count; i++)
        {
            if (!known.Contains(pairs[i]))
            {
                found.Add(Warning(ThemeCodes.UnknownPairing,
                    $"meta.pairsWith.backgrounds names '{pairs[i]}', which is not a background that exists; fix the name or remove it.",
                    file, $"meta.pairsWith.backgrounds[{i}]"));
            }
        }

        if (!string.IsNullOrWhiteSpace(background) && pairs.Count > 0 && !pairs.Contains(background, StringComparer.OrdinalIgnoreCase))
        {
            found.Add(Warning(ThemeCodes.DefaultNotPaired,
                $"the default background '{background}' is not in meta.pairsWith.backgrounds; add it, since the theme's own choice should be one it recommends.",
                file, "meta.pairsWith.backgrounds"));
        }

        if (definition.Background?.Static?.Image is { } image && !string.IsNullOrWhiteSpace(image))
        {
            string? problem = !source.IsInside(image)
                ? "leaves the theme's folder; keep the picture inside it"
                : source.Length(image) is not { } length
                    ? "is not in the theme's folder; add the file or fix the name"
                    : length > MaxPictureBytes
                        ? $"is {length / 1024.0 / 1024.0:0.0} MB, over 8 MB; use a smaller picture"
                        : null;
            if (problem != null)
            {
                found.Add(Warning(ThemeCodes.StillPicture, $"background.static.image '{image}' {problem}.", file, "background.static.image"));
            }
        }

        CheckIconSets(source, definition, found);
    }

    /// <summary>The set the theme names against what exists, and every set in its <c>icons/</c> folder (and the built-in one it names) as files.</summary>
    private static void CheckIconSets(IThemeSource source, ThemeDefinition definition, List<ThemeDiagnostic> found)
    {
        string file = ThemeStore.SettingsFileName;
        var checkedSets = new HashSet<string>(StringComparer.Ordinal);
        string? named = definition.Icons?.Set;
        if (!string.IsNullOrWhiteSpace(named) && !IconSetStore.Exists(named, source.Folder))
        {
            found.Add(Warning(ThemeCodes.UnknownIconSet,
                $"icons.set '{named}' is not an icon set that exists, neither in the theme's icons folder nor among the built-in sets; fix the name, or add icons/{named}/ with a manifest.json.", file, "icons.set"));
        }

        if (source.Folder != null)
        {
            string icons = Path.Combine(source.Folder, "icons");
            if (Directory.Exists(icons))
            {
                foreach (string folder in Directory.EnumerateDirectories(icons).Order(StringComparer.Ordinal))
                {
                    string name = Path.GetFileName(folder);
                    if (IconSetStore.IsValidName(name) && checkedSets.Add(name))
                        CheckIconSet(IconSetStore.FilesOf(source.Folder, name), $"icons/{name}", name, found);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(named) && IconSetStore.BuiltInNames.Contains(named) && checkedSets.Add(named))
            CheckIconSet(IconSetStore.BuiltInFiles(named), $"built-in set '{named}'", named, found);
    }

    private static void CheckIconSet(IIconSetFiles files, string where, string name, List<ThemeDiagnostic> found)
    {
        var reading = IconSetReader.Read(files, thorough: true);
        foreach (var finding in reading.Findings)
        {
            // A file finding names the file; the others name the manifest.
            string at = finding.Code is ThemeCodes.IconFile or ThemeCodes.IconOrphan && finding.Key != null
                ? $"{where}/{finding.Key}"
                : $"{where}/{IconSetReader.ManifestFile}";
            found.Add(new ThemeDiagnostic(finding.Code, finding.Severity, finding.Message, at, finding.Key));
        }

        if (reading.Set is { } set)
        {
            var missing = IconKeys.All.Where(k => !reading.Declared.Contains(k)).ToList();
            if (missing.Count > 0 && name != IconSetStore.BuiltInName)
            {
                found.Add(new ThemeDiagnostic(ThemeCodes.IconMissingKeys, ThemeSeverity.Info,
                    $"{where} has no icon for {string.Join(", ", missing)}; the built-in '{IconSetStore.BuiltInName}' set answers them.", $"{where}/{IconSetReader.ManifestFile}", null));
            }
        }
    }
}
