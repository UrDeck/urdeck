// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Backgrounds;

/// <summary>
/// A background folder, read: its name, where it came from (a folder path, or <c>built-in</c>), the manifest, the entry
/// shader's text and the source the other files are read through. <see cref="ManifestDiagnostics"/> are the findings of
/// reading the manifest, for the lint.
/// </summary>
public sealed record BackgroundPackage(
    string Name,
    string Origin,
    bool IsBuiltIn,
    BackgroundManifest Manifest,
    string ManifestText,
    string EntryPath,
    string EntryText,
    IBackgroundSource Source,
    IReadOnlyList<ShaderDiagnostic> ManifestDiagnostics)
{
    /// <summary>The text of the entry shader, for the shader's key and its compilation.</summary>
    public string Text => EntryText;
}

/// <summary>
/// The outcome of looking up a background: the package, or the reason there is none (and the lint code when the reason
/// is one of the lint's errors).
/// </summary>
public sealed record BackgroundLoad(BackgroundPackage? Package, string? Failure, string? Code);

/// <summary>
/// Finds a background by name: the folder <c>backgrounds/&lt;name&gt;/</c> next to the executable first, then the
/// built-in ones embedded in this assembly. Nothing is cached, so every <see cref="Load"/> re-reads the user folder.
/// </summary>
public sealed class ShaderStore(string backgroundsDirectory)
{
    public const string FileExtension = ".sksl";

    public static IReadOnlyList<string> BuiltInNames { get; } = ["plasma", "aurora", "gradient", "julia"];

    /// <summary>The folder user backgrounds are read from.</summary>
    public string BackgroundsDirectory => backgroundsDirectory;

    /// <summary>
    /// The named background, or null when there is none (the reason is logged unless <paramref name="warn"/> is false,
    /// which keeps the log quiet for key building). A name outside letters, digits, '-' and '_' is refused before any
    /// file is touched.
    /// </summary>
    public BackgroundPackage? Load(string name, bool warn = true)
    {
        var result = TryLoad(name);
        if (result.Package == null && warn)
            UrDeckLog.Warn($"Background '{name}': {result.Failure}");
        return result.Package;
    }

    /// <summary>
    /// The background in the folder at <paramref name="folder"/>, wherever it is: its name is the folder's last part, and a
    /// built-in of the same name is never used in its place. The reason there is none is in the result. Nothing is logged.
    /// </summary>
    public static BackgroundLoad TryRead(string folder)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        string name = Path.GetFileName(full);
        try
        {
            if (!Directory.Exists(full))
                return Fail($"the folder {full} does not exist.");
            return Read(name, full, new DirectoryBackgroundSource(full), builtIn: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail($"the folder {full} could not be read: {ex.Message}");
        }
    }

    /// <summary>The background, or the reason it cannot be used. Nothing is logged.</summary>
    public BackgroundLoad TryLoad(string name)
    {
        if (!BackgroundSettings.IsValidName(name))
            return Fail($"the name may only contain letters, digits, '-' and '_'.");

        string folder = Path.Combine(backgroundsDirectory, name);
        try
        {
            if (Directory.Exists(folder))
                return Read(name, folder, new DirectoryBackgroundSource(folder), builtIn: false);

            var embedded = new EmbeddedBackgroundSource(name);
            if (embedded.Exists(BackgroundManifest.FileName))
                return Read(name, "built-in", embedded, builtIn: true);

            string loose = Path.Combine(backgroundsDirectory, name + FileExtension);
            if (File.Exists(loose))
            {
                return Fail($"{loose} is a loose file; a background is now a folder: create {folder} with a "
                    + $"{BackgroundManifest.FileName} and move the file in as {name}{FileExtension}.");
            }

            return Fail($"no shader named '{name}' (not built in, and no folder {folder}).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail($"the folder {folder} could not be read: {ex.Message}");
        }
    }

    private static BackgroundLoad Read(string name, string origin, IBackgroundSource source, bool builtIn)
    {
        string? manifestText = source.ReadText(BackgroundManifest.FileName);
        if (manifestText == null)
        {
            return Fail($"{ShaderCodes.ManifestMissing} {BackgroundManifest.FileName} is missing from {origin}; a background needs a "
                + $"{BackgroundManifest.FileName} (\"manifest\": 1) next to {name}{FileExtension}.", ShaderCodes.ManifestMissing);
        }

        var read = BackgroundManifestReader.Read(manifestText);
        if (read.Manifest == null)
        {
            var error = read.Diagnostics[0];
            return Fail($"{error.Code} {error.Message}", error.Code);
        }

        string entry = read.Manifest.Entry ?? name + FileExtension;
        string? normalized = BackgroundPaths.Normalize(entry);
        if (normalized == null)
        {
            return Fail($"{ShaderCodes.EntryLeavesFolder} entry '{entry}' must name a file inside the folder (no absolute path, no '..').",
                ShaderCodes.EntryLeavesFolder);
        }

        string? text = source.ReadText(normalized);
        if (text == null)
            return Fail($"{ShaderCodes.EntryMissing} the entry file '{normalized}' does not exist in {origin}.", ShaderCodes.EntryMissing);

        return new BackgroundLoad(
            new BackgroundPackage(name, origin, builtIn, read.Manifest, manifestText, normalized, text, source, read.Diagnostics),
            null,
            null);
    }

    private static BackgroundLoad Fail(string reason, string? code = null) => new(null, reason, code);
}
