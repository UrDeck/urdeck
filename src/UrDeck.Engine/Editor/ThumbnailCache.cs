// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Editor;

/// <summary>
/// Pictures the editor draws once and keeps (a theme's picture of the user's page, a background's still): PNG files named
/// <c>&lt;name&gt;-&lt;key&gt;.png</c> in one folder, where the key is a hash of everything the picture depends on. A key that
/// changed means a new file, and the old files of the name are deleted. Nothing here throws: a cache that cannot be read or
/// written is an empty one.
/// </summary>
public sealed class ThumbnailCache(string folder)
{
    /// <summary>The folder under the data folder where the theme and background pictures are kept.</summary>
    public static string FolderIn(string dataFolder) => Path.Combine(dataFolder, "cache", "theme-previews");

    /// <summary>A short hash of the parts, equal when they all are.</summary>
    public static string Key(params string?[] parts)
    {
        var builder = new StringBuilder();
        foreach (string? part in parts)
            builder.Append(part ?? "").Append('\u001f');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..16].ToLowerInvariant();
    }

    /// <summary>The file for a name and key; the name is reduced to letters, digits, <c>-</c> and <c>_</c>.</summary>
    public string PathFor(string name, string key) => Path.Combine(folder, Safe(name) + "-" + key + ".png");

    /// <summary>The picture's file when the cache has it for this key; null otherwise.</summary>
    public string? Find(string name, string key)
    {
        string path = PathFor(name, key);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Keeps the PNG for the name and key and removes the other pictures of the name. Returns the file, or null if it could not be written.</summary>
    public string? Store(string name, string key, byte[] png)
    {
        string path = PathFor(name, key);
        try
        {
            Directory.CreateDirectory(folder);
            string temp = path + ".tmp";
            File.WriteAllBytes(temp, png);
            File.Move(temp, path, overwrite: true);
            string prefix = Safe(name) + "-";
            foreach (string other in Directory.EnumerateFiles(folder, prefix + "*.png"))
            {
                // "carbon-" must not take "carbon-paper-<key>" for its own: the rest of the name is exactly a key.
                string rest = Path.GetFileNameWithoutExtension(other)[prefix.Length..];
                if (rest.Length == key.Length && !string.Equals(other, path, StringComparison.OrdinalIgnoreCase))
                    File.Delete(other);
            }

            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UrDeckLog.Warn($"Picture cache: {path} could not be written: {ex.Message}");
            return null;
        }
    }

    private static string Safe(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (char c in name)
            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_');
        return builder.Length == 0 ? "_" : builder.ToString();
    }
}

/// <summary>The user's configuration drawn in another theme: what the editor's theme pictures show.</summary>
public static class ThemePreviewConfig
{
    /// <summary>
    /// A copy of <paramref name="root"/> (the configuration document) with <c>theme</c> set to <paramref name="theme"/> and the
    /// motion level reduced, so the background and the borders are drawn as one still frame. Everything else is the user's.
    /// </summary>
    public static UrDeckConfig For(JsonObject root, string theme)
    {
        var copy = (JsonObject)root.DeepClone();
        copy["theme"] = theme;
        copy["motion"] = "reduced";
        if (copy["background"] is JsonObject background)
            copy["background"] = Identity(background);
        return ConfigStore.Parse(copy.ToJsonString());
    }

    /// <summary>What a theme's picture depends on besides the theme itself: the page, the background setting, the dock and the pager.</summary>
    public static string ContentKey(JsonObject root, string? pageId)
    {
        var pages = root["pages"] as JsonArray;
        JsonNode? page = pages?.FirstOrDefault(p => p is JsonObject o && o["id"] is JsonValue id && id.TryGetValue(out string? text) && text == pageId) ?? pages?.FirstOrDefault();
        return ThumbnailCache.Key(
            page?.ToJsonString(),
            root["dock"]?.ToJsonString(),
            root["pager"]?.ToJsonString(),
            (root["background"] as JsonObject is { } background ? Identity(background) : root["background"])?.ToJsonString());
    }

    /// <summary>
    /// Which background it is, without how it is tuned: <c>type</c>, <c>name</c> and <c>path</c>. A theme's picture shows the shader
    /// at its own values, so dragging a parameter, the dimming or the speed does not make all of the pictures again.
    /// </summary>
    private static JsonObject Identity(JsonObject background)
    {
        var identity = new JsonObject();
        foreach (var (key, value) in background)
        {
            if (value != null && (key.Equals("type", StringComparison.OrdinalIgnoreCase) || key.Equals("name", StringComparison.OrdinalIgnoreCase)
                || key.Equals("path", StringComparison.OrdinalIgnoreCase)))
                identity[key] = value.DeepClone();
        }

        return identity;
    }
}
