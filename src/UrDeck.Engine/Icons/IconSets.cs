// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Engine.Icons;

/// <summary>The product's own icon keys. The lists only grow; a key is never renamed or removed.</summary>
public static class IconKeys
{
    public static IReadOnlyList<string> Weather { get; } =
    [
        "weather/clear-day", "weather/clear-night", "weather/partly-cloudy-day", "weather/partly-cloudy-night", "weather/overcast",
        "weather/fog", "weather/drizzle", "weather/rain", "weather/sleet", "weather/snow", "weather/thunderstorms-day",
        "weather/thunderstorms-night", "weather/not-available",
    ];

    public static IReadOnlyList<string> App { get; } =
    [
        "app/terminal", "app/folder", "app/file", "app/music", "app/gamepad", "app/browser", "app/mail", "app/chat", "app/settings",
        "app/code", "app/camera", "app/image", "app/video", "app/calendar", "app/clock", "app/home", "app/power", "app/lock",
        "app/link", "app/cloud", "app/download", "app/star", "app/heart", "app/bolt", "app/chart",
    ];

    public static IReadOnlyList<string> Media { get; } = ["media/play", "media/pause", "media/previous", "media/next"];

    public static IReadOnlyList<string> All { get; } = [.. Weather, .. App, .. Media];
}

/// <summary>An icon set read: its identity and the glyphs its manifest maps. The set is shared and read-only.</summary>
public sealed class IconSet(string name, string source, string license, string? upstreamVersion, IReadOnlyDictionary<string, IconGlyph> glyphs) : IIconSet
{
    public string Name => name;

    public string Source => source;

    public string License => license;

    public string? UpstreamVersion => upstreamVersion;

    public IReadOnlyCollection<string> Keys => (IReadOnlyCollection<string>)glyphs.Keys;

    public bool TryGet(string key, out IconGlyph glyph) => glyphs.TryGetValue(key, out glyph!);
}

/// <summary>The files of one icon set, read by path relative to the set's folder.</summary>
internal interface IIconSetFiles
{
    /// <summary>What the set is called in a message: its folder relative to the theme, or the built-in name.</summary>
    string Describe { get; }

    bool IsInside(string relative);

    long? Length(string relative);

    Stream? Open(string relative);

    /// <summary>Every <c>.svg</c> file in the set at any depth, with forward slashes, in ordinal order.</summary>
    IReadOnlyList<string> SvgFiles();

    bool HasLicenseFile();
}

internal sealed partial class DirectoryIconFiles(string folder, string describe) : IIconSetFiles
{
    private readonly string _folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

    public string Describe => describe;

    public bool IsInside(string relative) => ThemeStore.IsInside(_folder, ThemeStore.ResolveIn(_folder, relative));

    public long? Length(string relative) =>
        IsInside(relative) && File.Exists(ThemeStore.ResolveIn(_folder, relative)) ? new FileInfo(ThemeStore.ResolveIn(_folder, relative)).Length : null;

    public Stream? Open(string relative)
    {
        string path = ThemeStore.ResolveIn(_folder, relative);
        return ThemeStore.IsInside(_folder, path) && File.Exists(path) ? File.OpenRead(path) : null;
    }

    public IReadOnlyList<string> SvgFiles() =>
        Directory.EnumerateFiles(_folder, "*.svg", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_folder, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

    public bool HasLicenseFile() => Directory.EnumerateFiles(_folder).Any(f => LicenceName().IsMatch(Path.GetFileName(f)));

    [GeneratedRegex(@"^(LICEN[CS]E.*|COPYING.*|.+-LICEN[CS]E.*|.+-OFL\.txt)$", RegexOptions.IgnoreCase)]
    private static partial Regex LicenceName();
}

/// <summary>A built-in set, read from the engine's resources: <c>UrDeck.Icons.&lt;set&gt;/&lt;file&gt;</c>.</summary>
internal sealed class EmbeddedIconFiles(string set) : IIconSetFiles
{
    private static readonly Assembly Self = typeof(EmbeddedIconFiles).Assembly;

    private string Prefix => $"UrDeck.Icons.{set}/";

    public string Describe => $"built-in set '{set}'";

    public bool IsInside(string relative) =>
        !string.IsNullOrWhiteSpace(relative) && !relative.StartsWith('/') && !relative.Contains('\\') && !relative.Contains(':')
        && !relative.Split('/').Contains("..");

    public long? Length(string relative)
    {
        using var stream = Open(relative);
        return stream?.Length;
    }

    public Stream? Open(string relative) => IsInside(relative) ? Self.GetManifestResourceStream(Prefix + relative) : null;

    public IReadOnlyList<string> SvgFiles() => Names().Where(n => n.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)).ToList();

    public bool HasLicenseFile() => Names().Any(n => n.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase));

    public static bool Exists(string set) => Self.GetManifestResourceNames().Any(n => n.StartsWith($"UrDeck.Icons.{set}/", StringComparison.Ordinal));

    private IEnumerable<string> Names() =>
        Self.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)).Select(n => n[Prefix.Length..]).Order(StringComparer.Ordinal);
}

/// <summary>The outcome of reading a set: the set (null when its manifest cannot be used) and every finding on the way.</summary>
internal sealed record IconSetReading(IconSet? Set, IReadOnlyList<ThemeFinding> Findings)
{
    /// <summary>Every key the manifest lists, whether or not its file could be used.</summary>
    public IReadOnlyCollection<string> Declared { get; init; } = [];
}

/// <summary>
/// Reads an icon set folder through its manifest, and nothing else: no file is found by its name alone. The deck and the
/// command line tool both read sets with this class; <c>THEME075</c> (a file the manifest does not name) is a directory scan
/// and is made only on request.
/// </summary>
internal static class IconSetReader
{
    public const string ManifestFile = "manifest.json";

    private const int MaxManifestBytes = 64 * 1024;
    private const int MaxKeys = 256;

    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static IconSetReading Read(IIconSetFiles files, bool thorough)
    {
        var found = new List<ThemeFinding>();
        string where = files.Describe;

        if (files.Length(ManifestFile) is not { } length)
        {
            found.Add(Finding(ThemeCodes.IconManifest, ThemeSeverity.Error, null, $"{where} has no {ManifestFile}; add one with version 1, name, source, license and keys, or the set is not loaded."));
            return new IconSetReading(null, found);
        }

        if (length > MaxManifestBytes)
        {
            found.Add(Finding(ThemeCodes.IconManifest, ThemeSeverity.Error, null, $"{where}/{ManifestFile} is over {MaxManifestBytes / 1024} KB; list fewer keys."));
            return new IconSetReading(null, found);
        }

        JsonDocument document;
        try
        {
            using var stream = files.Open(ManifestFile)!;
            document = JsonDocument.Parse(stream, Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            found.Add(Finding(ThemeCodes.IconManifest, ThemeSeverity.Error, null, $"{where}/{ManifestFile} cannot be read ({ex.Message.Split('\n')[0].TrimEnd('.', ' ')}); fix the JSON."));
            return new IconSetReading(null, found);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 1)
            {
                found.Add(Finding(ThemeCodes.IconManifest, ThemeSeverity.Error, "version", $"{where}/{ManifestFile} must be an object with \"version\": 1; set it."));
                return new IconSetReading(null, found);
            }

            if (!root.TryGetProperty("keys", out var keys) || keys.ValueKind != JsonValueKind.Object)
            {
                found.Add(Finding(ThemeCodes.IconManifest, ThemeSeverity.Error, "keys", $"{where}/{ManifestFile} has no \"keys\" object; map each icon key to an SVG file."));
                return new IconSetReading(null, found);
            }

            string name = Text(root, "name"), source = Text(root, "source"), license = Text(root, "license");
            string? upstream = root.TryGetProperty("upstreamVersion", out var uv) && uv.ValueKind == JsonValueKind.String ? uv.GetString() : null;

            var glyphs = new Dictionary<string, IconGlyph>(StringComparer.Ordinal);
            var parsed = new Dictionary<string, IconGlyph?>(StringComparer.Ordinal);
            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var declared = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (var entry in keys.EnumerateObject())
            {
                if (++count > MaxKeys)
                {
                    found.Add(Finding(ThemeCodes.IconManifest, ThemeSeverity.Error, "keys", $"{where}/{ManifestFile} lists more than {MaxKeys} keys; the rest are ignored."));
                    break;
                }

                string key = entry.Name;
                declared.Add(key);
                string? path = entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString() : null;
                if (thorough && !IconKeys.All.Contains(key))
                    found.Add(Finding(ThemeCodes.IconKey, ThemeSeverity.Warning, key, $"{where}/{ManifestFile} key '{key}' is not one of the product's icon keys, so no widget asks for it; check the spelling."));

                if (path == null || !path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || !files.IsInside(path))
                {
                    found.Add(Finding(ThemeCodes.IconEntry, ThemeSeverity.Error, key, $"{where}/{ManifestFile} maps '{key}' to '{path}', which is not an .svg file inside the set's folder; fix the path."));
                    continue;
                }

                named.Add(path);
                if (!parsed.TryGetValue(path, out var glyph))
                {
                    glyph = ReadFile(files, where, key, path, found);
                    parsed[path] = glyph;
                }

                if (glyph != null)
                    glyphs[key] = glyph;
            }

            if (thorough)
            {
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(license))
                    found.Add(Finding(ThemeCodes.IconLicense, ThemeSeverity.Warning, null, $"{where}/{ManifestFile} has no {(string.IsNullOrWhiteSpace(source) ? "source" : "license")}; say where the icons come from and under which licence."));
                else if (!files.HasLicenseFile())
                    found.Add(Finding(ThemeCodes.IconLicense, ThemeSeverity.Warning, null, $"{where} has no licence file; add the licence text of the icons beside the manifest."));

                foreach (string svg in files.SvgFiles().Where(f => !named.Contains(f)))
                    found.Add(Finding(ThemeCodes.IconOrphan, ThemeSeverity.Warning, svg, $"{where}/{svg} is named by no manifest entry; add an entry or delete the file."));
            }

            return new IconSetReading(new IconSet(name, source, license, upstream, glyphs), found) { Declared = declared };
        }
    }

    private static string Text(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static IconGlyph? ReadFile(IIconSetFiles files, string where, string key, string path, List<ThemeFinding> found)
    {
        try
        {
            if (files.Length(path) is not { } length)
            {
                found.Add(Finding(ThemeCodes.IconEntry, ThemeSeverity.Error, key, $"{where}/{ManifestFile} maps '{key}' to '{path}', which is not in the set's folder; add the file or fix the path."));
                return null;
            }

            if (length > IconSvgParser.MaxBytes)
            {
                found.Add(Finding(ThemeCodes.IconFile, ThemeSeverity.Error, path, $"{where}/{path} is over {IconSvgParser.MaxBytes / 1024} KB; simplify the icon."));
                return null;
            }

            using var stream = files.Open(path)!;
            var result = IconSvgParser.Parse(stream);
            if (result.Glyph == null)
                found.Add(Finding(ThemeCodes.IconFile, ThemeSeverity.Error, path, $"{where}/{path} {result.Reason}."));
            return result.Glyph;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            found.Add(Finding(ThemeCodes.IconFile, ThemeSeverity.Error, path, $"{where}/{path} could not be read: {ex.Message}"));
            return null;
        }
    }

    private static ThemeFinding Finding(string code, ThemeSeverity severity, string? key, string message) =>
        new(code, severity, key, message) { Form = ThemeLogForm.Bare };
}

/// <summary>An answer made of several sets: the first that has the key.</summary>
internal sealed class IconChain(IReadOnlyList<IIconSet> sets) : IIconSet
{
    public bool TryGet(string key, out IconGlyph glyph)
    {
        foreach (var set in sets)
            if (set.TryGet(key, out glyph))
                return true;
        glyph = null!;
        return false;
    }
}

/// <summary>What a theme's icons resolved to: the lookup (theme set, then <c>outline</c>) and whether the theme's set was found.</summary>
internal sealed record ResolvedIcons(IIconSet Set, bool NamesSet);

/// <summary>
/// Finds icon sets: <c>icons/&lt;name&gt;/</c> in a theme's folder first, then the built-in sets. A set is read once per origin:
/// the built-in ones for the life of the process, a theme's for the life of its <see cref="LoadedTheme"/>.
/// </summary>
public static partial class IconSetStore
{
    public const string BuiltInName = "outline";

    public static IReadOnlyList<string> BuiltInNames { get; } = [BuiltInName];

    private static readonly Dictionary<string, IconSet?> BuiltIns = [];

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex ValidName();

    /// <summary>True for a set name that cannot reach outside the theme's <c>icons</c> folder.</summary>
    public static bool IsValidName(string? name) => name != null && ValidName().IsMatch(name) && !name.Contains("..", StringComparison.Ordinal);

    /// <summary>The built-in set of that name, or null; a broken one is logged once.</summary>
    internal static IconSet? BuiltIn(string name)
    {
        lock (BuiltIns)
        {
            if (BuiltIns.TryGetValue(name, out var cached))
                return cached;
            IconSet? set = null;
            if (EmbeddedIconFiles.Exists(name))
            {
                var reading = IconSetReader.Read(new EmbeddedIconFiles(name), thorough: false);
                foreach (var finding in reading.Findings)
                    UrDeckLog.Warn($"Icons: {finding.Code} {finding.Message}");
                set = reading.Set;
            }

            return BuiltIns[name] = set;
        }
    }

    /// <summary>Whether a set of that name exists in the theme's folder (<c>icons/&lt;name&gt;/</c>) or among the built-in ones.</summary>
    internal static bool Exists(string name, string? themeFolder) =>
        IsValidName(name) && ((themeFolder != null && Directory.Exists(FolderOf(themeFolder, name))) || EmbeddedIconFiles.Exists(name));

    internal static string FolderOf(string themeFolder, string name) => Path.Combine(themeFolder, "icons", name);

    internal static IIconSetFiles FilesOf(string themeFolder, string name) => new DirectoryIconFiles(FolderOf(themeFolder, name), $"icons/{name}");

    internal static IIconSetFiles BuiltInFiles(string name) => new EmbeddedIconFiles(name);

    /// <summary>
    /// The lookup for a loaded theme: the set it names (when found and usable), then <c>outline</c>. Each problem is reported
    /// once to the theme's findings. Read once per loaded theme and shared by every surface size.
    /// </summary>
    internal static ResolvedIcons Resolve(LoadedTheme loaded)
    {
        var sets = new List<IIconSet>(2);
        bool names = false;
        string? requested = loaded.Definition.Icons?.Set;
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (!IsValidName(requested) || !Exists(requested, loaded.Folder))
            {
                loaded.Warnings.Once($"icons:set:{requested}", ThemeCodes.UnknownIconSet, "icons.set",
                    $"icons.set '{requested}' is not an icon set that exists; the built-in '{BuiltInName}' set draws the icons.");
            }
            else
            {
                IconSet? own = null;
                if (loaded.Folder != null && Directory.Exists(FolderOf(loaded.Folder, requested)))
                {
                    var reading = IconSetReader.Read(FilesOf(loaded.Folder, requested), thorough: false);
                    foreach (var finding in reading.Findings)
                        loaded.Warnings.Once($"icons:{finding.Code}:{finding.Key}:{finding.Message}", finding);
                    own = reading.Set;
                }
                else
                {
                    own = BuiltIn(requested);
                }

                if (own != null)
                {
                    sets.Add(own);
                    names = true;
                }
            }
        }

        if (BuiltIn(BuiltInName) is { } outline && !sets.Contains(outline))
            sets.Add(outline);
        return new ResolvedIcons(sets.Count == 1 ? sets[0] : new IconChain(sets), names);
    }
}
