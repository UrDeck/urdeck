// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Cli;

/// <summary>How a path is printed: relative to the working directory when it is inside it, else absolute, always with '/'.</summary>
internal static class PathDisplay
{
    public static string Of(string path)
    {
        string full = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), full);
        bool inside = relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                      && !Path.IsPathRooted(relative);
        return (inside ? relative : full).Replace('\\', '/');
    }

    /// <summary>The path of <paramref name="file"/> inside <paramref name="folder"/>; the folder itself when the file is empty.</summary>
    public static string In(string folder, string file) =>
        string.IsNullOrEmpty(file) ? Of(folder) : Of(Path.Combine(folder, file.Replace('/', Path.DirectorySeparatorChar)));
}
