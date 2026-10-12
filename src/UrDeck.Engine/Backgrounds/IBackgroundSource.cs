// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;

namespace UrDeck.Engine.Backgrounds;

/// <summary>
/// The files of one background folder, read by relative path. The rule that a path stays inside the folder lives here,
/// so a folder on disk and an embedded one are checked by the same code.
/// </summary>
public interface IBackgroundSource
{
    /// <summary>True when the file exists; false too for a path that is rooted or leaves the folder.</summary>
    bool Exists(string relative);

    /// <summary>The text of the file, or null when it does not exist or the path is refused.</summary>
    string? ReadText(string relative);

    /// <summary>The file's content, or null when it does not exist or the path is refused. The caller disposes it.</summary>
    Stream? Open(string relative);
}

/// <summary>The path rule shared by every <see cref="IBackgroundSource"/>.</summary>
public static class BackgroundPaths
{
    /// <summary>
    /// The path with forward slashes and no <c>.</c> segments, or null when it is empty, rooted, or leaves the folder
    /// (<c>..</c> after normalising).
    /// </summary>
    public static string? Normalize(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return null;

        string path = relative.Replace('\\', '/');
        if (path.StartsWith('/') || (path.Length >= 2 && path[1] == ':') || path.Contains('\0', StringComparison.Ordinal))
            return null;

        var parts = new List<string>();
        foreach (string part in path.Split('/'))
        {
            if (part.Length == 0 || part == ".")
                continue;
            if (part == "..")
            {
                if (parts.Count == 0)
                    return null;
                parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(part);
        }

        return parts.Count == 0 ? null : string.Join('/', parts);
    }
}

/// <summary>A background folder on disk. Files are opened shared, because an editor may hold one while it saves.</summary>
public sealed class DirectoryBackgroundSource(string folder) : IBackgroundSource
{
    public string Folder { get; } = folder;

    public bool Exists(string relative) => Resolve(relative) is { } path && File.Exists(path);

    public string? ReadText(string relative)
    {
        using var stream = Open(relative);
        if (stream == null)
            return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public Stream? Open(string relative)
    {
        string? path = Resolve(relative);
        if (path == null || !File.Exists(path))
            return null;

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(100);
            }
        }
    }

    private string? Resolve(string relative) =>
        BackgroundPaths.Normalize(relative) is { } path ? Path.Combine(Folder, path.Replace('/', Path.DirectorySeparatorChar)) : null;
}

/// <summary>A built-in background: the resources named <c>UrDeck.Backgrounds.&lt;name&gt;/&lt;file&gt;</c> in this assembly.</summary>
public sealed class EmbeddedBackgroundSource(string name) : IBackgroundSource
{
    private static readonly Assembly Self = typeof(EmbeddedBackgroundSource).Assembly;

    public bool Exists(string relative) => ResourceName(relative) is { } resource && Array.IndexOf(Self.GetManifestResourceNames(), resource) >= 0;

    public string? ReadText(string relative)
    {
        using var stream = Open(relative);
        if (stream == null)
            return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public Stream? Open(string relative) => ResourceName(relative) is { } resource ? Self.GetManifestResourceStream(resource) : null;

    private string? ResourceName(string relative) =>
        BackgroundPaths.Normalize(relative) is { } path ? $"UrDeck.Backgrounds.{name}/{path}" : null;
}
