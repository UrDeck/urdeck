// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Backgrounds;

/// <summary>The text of a shader and where it came from (a file path, or <c>built-in</c>).</summary>
public sealed record ShaderText(string Name, string Text, string Origin, bool IsBuiltIn);

/// <summary>
/// Finds a background shader by name: <c>backgrounds/&lt;name&gt;.sksl</c> next to the executable first, then the
/// built-in ones embedded in this assembly. Nothing is cached, so every <see cref="Load"/> re-reads the user folder.
/// </summary>
public sealed class ShaderStore(string backgroundsDirectory)
{
    public const string FileExtension = ".sksl";

    private static readonly Assembly Self = typeof(ShaderStore).Assembly;

    public static IReadOnlyList<string> BuiltInNames { get; } = ["plasma", "aurora", "gradient", "julia"];

    /// <summary>
    /// The named shader, or null when there is none (or it cannot be read). A name outside letters, digits, '-' and
    /// '_' is refused before any file is touched. <paramref name="warn"/> false keeps the log quiet (for key building).
    /// </summary>
    public ShaderText? Load(string name, bool warn = true)
    {
        if (!BackgroundSettings.IsValidName(name))
        {
            if (warn)
                UrDeckLog.Warn($"Background: shader name '{name}' may only contain letters, digits, '-' and '_'.");
            return null;
        }

        string path = Path.Combine(backgroundsDirectory, name + FileExtension);
        try
        {
            if (File.Exists(path))
                return new ShaderText(name, ReadShared(path), path, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (warn)
                UrDeckLog.Warn($"Background: shader file {path} could not be read: {ex.Message}");
            return null;
        }

        using var stream = Self.GetManifestResourceStream($"UrDeck.Backgrounds.{name}{FileExtension}");
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return new ShaderText(name, reader.ReadToEnd(), "built-in", true);
        }

        if (warn)
            UrDeckLog.Warn($"Background: no shader named '{name}' (not built in, and no {path}).");
        return null;
    }

    // Editors hold a file briefly while saving; share it and retry a few times, as the configuration does.
    private static string ReadShared(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(100);
            }
        }
    }
}
