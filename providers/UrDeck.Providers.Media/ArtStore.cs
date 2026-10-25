// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

namespace UrDeck.Providers.Media;

/// <summary>
/// The album art on disk, so that a widget can show it through the icon service. The files live in a folder of this run under
/// the user's temporary folder; leftovers of an earlier run are removed when the store starts and the folder when it stops.
/// A file is named for its content, so the same picture is written once and a path never changes meaning. At most the current
/// file and the previous one are kept.
/// </summary>
internal sealed class ArtStore : IDisposable
{
    private const string RunPrefix = "run-";

    private readonly string _root;
    private readonly string _folder;
    private readonly List<string> _files = [];

    /// <param name="root">The folder that holds the runs' folders; created on the first write.</param>
    public ArtStore(string root)
    {
        _root = root;
        _folder = Path.Combine(root, RunPrefix + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>This run's folder.</summary>
    public string Folder => _folder;

    /// <summary>The usual root: a folder named for the product under the user's temporary folder.</summary>
    public static string DefaultRoot => Path.Combine(Path.GetTempPath(), "UrDeck", "media-art");

    /// <summary>Removes the folders of runs that are over (or of an earlier run with this process id). Never throws.</summary>
    public void ClearLeftovers()
    {
        try
        {
            if (!Directory.Exists(_root))
                return;
            foreach (string directory in Directory.EnumerateDirectories(_root, RunPrefix + "*"))
            {
                if (!IsLive(directory))
                    TryDelete(directory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A cache; whatever cannot be removed now is removed by a later start.
        }
    }

    /// <summary>
    /// Writes <paramref name="image"/> unless the same picture is already there, and returns the file's path; null when the
    /// file cannot be written. Files older than the previous one are removed.
    /// </summary>
    public string? Write(byte[] image)
    {
        string path = Path.Combine(_folder, Convert.ToHexString(SHA256.HashData(image), 0, 12).ToLowerInvariant() + Extension(image));
        try
        {
            int known = _files.FindIndex(file => string.Equals(file, path, StringComparison.Ordinal));
            if (known >= 0)
            {
                _files.RemoveAt(known);
                if (File.Exists(path))
                {
                    _files.Add(path);
                    return path;
                }
            }

            Directory.CreateDirectory(_folder);
            File.WriteAllBytes(path, image);
            _files.Add(path);
            while (_files.Count > 2)
            {
                TryDelete(_files[0]);
                _files.RemoveAt(0);
            }

            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Removes this run's folder.</summary>
    public void Dispose()
    {
        _files.Clear();
        TryDelete(_folder);
    }

    /// <summary>The extension the icon service recognises, from the picture's first bytes; a picture it cannot name is a PNG.</summary>
    internal static string Extension(ReadOnlySpan<byte> image)
    {
        if (image.Length >= 3 && image[0] == 0xFF && image[1] == 0xD8 && image[2] == 0xFF)
            return ".jpg";
        if (image.Length >= 4 && image[0] == (byte)'G' && image[1] == (byte)'I' && image[2] == (byte)'F' && image[3] == (byte)'8')
            return ".gif";
        if (image.Length >= 2 && image[0] == (byte)'B' && image[1] == (byte)'M')
            return ".bmp";
        if (image.Length >= 12 && image[0] == (byte)'R' && image[1] == (byte)'I' && image[2] == (byte)'F' && image[3] == (byte)'F'
            && image[8] == (byte)'W' && image[9] == (byte)'E' && image[10] == (byte)'B' && image[11] == (byte)'P')
            return ".webp";
        return ".png";
    }

    private static bool IsLive(string directory)
    {
        string name = Path.GetFileName(directory);
        if (!int.TryParse(name.AsSpan(RunPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int pid))
            return false;
        if (pid == Environment.ProcessId)
            return false;
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still in use (the icon service decodes it) or gone already; the folder goes at the end of the run.
        }
    }
}
