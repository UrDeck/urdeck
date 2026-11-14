// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UrDeck.Providers.Steam;

/// <summary>
/// What the provider keeps in the data folder's <c>cache/steam/</c>: game names looked up at the store (<c>apps.json</c>, kept for
/// good) and the avatars of the friends shown (<c>avatars/&lt;sha&gt;_medium.jpg</c>, at most <see cref="MaxAvatars"/>, the
/// least recently used removed). Nothing is written until something is stored, and a file that cannot be written is skipped: a cache
/// is never a reason to fail.
/// </summary>
internal sealed partial class SteamCaches(string root)
{
    internal const int MaxAvatars = 300;

    private readonly object _gate = new();
    private Dictionary<uint, string>? _names;

    public string AvatarFolder => Path.Combine(root, "avatars");

    /// <summary>Whether <paramref name="sha"/> is the hash of an avatar: hexadecimal only, so that it can never be a path.</summary>
    public static bool IsAvatarHash(string sha) => AvatarHash().IsMatch(sha);

    /// <summary>
    /// The avatar host's form of the hash Steam reports: Steam gives the 20-byte SHA-1 as base64 (28 characters), the host names the
    /// file by its 40 hexadecimal digits. Hexadecimal text is kept; the hash of no avatar (all zeros) and anything else is "".
    /// </summary>
    public static string NormalizeAvatarHash(string reported)
    {
        string hex = reported;
        if (reported.Length == 28)
        {
            try
            {
                hex = Convert.ToHexStringLower(Convert.FromBase64String(reported));
            }
            catch (FormatException)
            {
                return "";
            }
        }

        return IsAvatarHash(hex) && hex.Any(c => c != '0') ? hex.ToLowerInvariant() : "";
    }

    /// <summary>The name stored for the app, or null.</summary>
    public string? Name(uint appId)
    {
        lock (_gate)
            return Names().GetValueOrDefault(appId);
    }

    /// <summary>Stores the app's name and writes the file.</summary>
    public void SetName(uint appId, string name)
    {
        lock (_gate)
        {
            var names = Names();
            names[appId] = name;
            try
            {
                Directory.CreateDirectory(root);
                string file = Path.Combine(root, "apps.json");
                string temp = file + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(names.ToDictionary(n => n.Key.ToString(CultureInfo.InvariantCulture), n => n.Value)));
                File.Move(temp, file, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Kept in memory for this run.
            }
        }
    }

    /// <summary>The path of the avatar when it is stored (and marks it as used now), else "".</summary>
    public string AvatarPath(string sha)
    {
        if (!IsAvatarHash(sha))
            return "";
        string path = Path.Combine(AvatarFolder, sha.ToLowerInvariant() + "_medium.jpg");
        try
        {
            if (!File.Exists(path))
                return "";
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    /// <summary>Stores an avatar and removes the least recently used ones beyond <see cref="MaxAvatars"/>; returns its path, or "".</summary>
    public string SaveAvatar(string sha, byte[] bytes)
    {
        if (!IsAvatarHash(sha))
            return "";
        try
        {
            Directory.CreateDirectory(AvatarFolder);
            string path = Path.Combine(AvatarFolder, sha.ToLowerInvariant() + "_medium.jpg");
            File.WriteAllBytes(path, bytes);
            Trim();
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    [GeneratedRegex("^[0-9a-fA-F]{8,64}$")]
    private static partial Regex AvatarHash();

    private Dictionary<uint, string> Names()
    {
        if (_names is not null)
            return _names;
        _names = [];
        try
        {
            string file = Path.Combine(root, "apps.json");
            if (File.Exists(file))
            {
                var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                foreach (var (key, value) in stored ?? [])
                {
                    if (uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out uint id) && !string.IsNullOrEmpty(value))
                        _names[id] = value;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // An unreadable file is a cache that starts again.
        }

        return _names;
    }

    private void Trim()
    {
        var files = new DirectoryInfo(AvatarFolder).GetFiles("*_medium.jpg");
        if (files.Length <= MaxAvatars)
            return;
        foreach (var file in files.OrderBy(f => f.LastWriteTimeUtc).Take(files.Length - MaxAvatars))
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Removed on a later save.
            }
        }
    }
}
