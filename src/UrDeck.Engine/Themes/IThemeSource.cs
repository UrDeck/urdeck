// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;

namespace UrDeck.Engine.Themes;

/// <summary>
/// The files of one theme, read by relative path: a folder on disk, or a built-in theme embedded in the engine. The rule that
/// a path stays inside the theme lives in the source, so the deck and the command line tool judge a folder by the same code.
/// </summary>
public interface IThemeSource
{
    /// <summary>The theme's name: its folder's name, or the built-in theme's.</summary>
    string Name { get; }

    /// <summary>The folder on disk the files are read from, or null for a built-in theme.</summary>
    string? Folder { get; }

    /// <summary>The text of <c>theme.json</c>, or null when there is none. May throw <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>.</summary>
    string? ReadSettings();

    /// <summary>
    /// False for a path that leaves the theme: absolute, or reaching out with <c>..</c>. A backslash is a separator on every
    /// platform, so <c>..\x</c> cannot slip past on one where it is not.
    /// </summary>
    bool IsInside(string file);

    /// <summary>True when the file exists inside the theme.</summary>
    bool Exists(string file);

    /// <summary>The size of the file in bytes, or null when it does not exist or the path is refused.</summary>
    long? Length(string file);

    /// <summary>The file's content, or null when it does not exist or the path is refused. The caller disposes it.</summary>
    Stream? Open(string file);

    /// <summary>The names of the files next to <c>theme.json</c> (for the licence of a font), in ordinal order.</summary>
    IReadOnlyList<string> Files();
}

/// <summary>A theme folder on disk.</summary>
public sealed class DirectoryThemeSource : IThemeSource
{
    private readonly string _folder;

    /// <summary>The theme in <paramref name="folder"/>, named as <paramref name="name"/> or, without one, as the folder.</summary>
    public DirectoryThemeSource(string folder, string? name = null)
    {
        _folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        Name = name ?? Path.GetFileName(_folder);
    }

    public string Name { get; }

    public string Folder => _folder;

    public string? ReadSettings()
    {
        string settings = Path.Combine(_folder, ThemeStore.SettingsFileName);
        return File.Exists(settings) ? File.ReadAllText(settings) : null;
    }

    public bool IsInside(string file) => ThemeStore.IsInside(_folder, ThemeStore.ResolveIn(_folder, file));

    public bool Exists(string file) => IsInside(file) && File.Exists(ThemeStore.ResolveIn(_folder, file));

    public long? Length(string file) => Exists(file) ? new FileInfo(ThemeStore.ResolveIn(_folder, file)).Length : null;

    public Stream? Open(string file)
    {
        string path = ThemeStore.ResolveIn(_folder, file);
        return ThemeStore.IsInside(_folder, path) && File.Exists(path) ? File.OpenRead(path) : null;
    }

    public IReadOnlyList<string> Files() =>
        Directory.EnumerateFiles(_folder).Select(f => Path.GetFileName(f)!).Order(StringComparer.Ordinal).ToList();
}

/// <summary>
/// A built-in theme, read from the resources of the engine as shipped. The resources are flat (<c>UrDeck.Themes.&lt;file&gt;</c>)
/// and the fonts are shared between themes, so every file of the set is next to every theme's settings.
/// </summary>
public sealed class EmbeddedThemeSource(string name) : IThemeSource
{
    private const string Prefix = "UrDeck.Themes.";

    private static readonly Assembly Self = typeof(EmbeddedThemeSource).Assembly;

    public string Name => name;

    public string? Folder => null;

    public string? ReadSettings()
    {
        using var stream = Self.GetManifestResourceStream($"{Prefix}{name}.json");
        if (stream == null)
            return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // The resources have no folders: a name with a separator or a dot segment is outside.
    public bool IsInside(string file) =>
        !string.IsNullOrWhiteSpace(file) && file is not ("." or "..") && file.IndexOfAny(['/', '\\', ':', '\0']) < 0;

    public bool Exists(string file) => IsInside(file) && Self.GetManifestResourceInfo(Prefix + file) != null;

    public long? Length(string file)
    {
        using var stream = Open(file);
        return stream?.Length;
    }

    public Stream? Open(string file) => IsInside(file) ? Self.GetManifestResourceStream(Prefix + file) : null;

    public IReadOnlyList<string> Files() =>
        Self.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(n => n[Prefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToList();
}
