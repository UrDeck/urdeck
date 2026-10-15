// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Backgrounds;

/// <summary>One background as a list shows it: the manifest's values (what the manifest does not say is null or empty).</summary>
public sealed record BackgroundCatalogEntry(
    string Name,
    string? Title,
    string? Description,
    string? Author,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> PairsWithThemes,
    IReadOnlyDictionary<string, ManifestParameter> Parameters,
    IReadOnlyList<ManifestSample> Samples,
    bool IsBuiltIn);

/// <summary>
/// Lists the backgrounds that exist for a settings editor and the command line tool: the built-in ones and the folders in
/// the user's <c>backgrounds</c> folder, a user folder replacing a built-in one of the same name. It reads each folder's
/// manifest and compiles no shader. A folder that cannot be read is left out, with one logged warning.
/// </summary>
public static class BackgroundCatalog
{
    public static IReadOnlyList<BackgroundCatalogEntry> List(ShaderStore store)
    {
        var names = new List<string>(ShaderStore.BuiltInNames);
        try
        {
            if (Directory.Exists(store.BackgroundsDirectory))
            {
                foreach (string folder in Directory.EnumerateDirectories(store.BackgroundsDirectory).Order(StringComparer.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileName(folder);
                    if (BackgroundSettings.IsValidName(name) && !names.Contains(name, StringComparer.Ordinal))
                        names.Add(name);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UrDeckLog.Warn($"Background list: the folder {store.BackgroundsDirectory} could not be read: {ex.Message}");
        }

        var entries = new List<BackgroundCatalogEntry>();
        foreach (string name in names)
        {
            var load = store.TryLoad(name);
            if (load.Package is not { } package)
            {
                UrDeckLog.Warn($"Background '{name}': {load.Failure} It is not listed.");
                continue;
            }

            var manifest = package.Manifest;
            entries.Add(new BackgroundCatalogEntry(
                name,
                manifest.Title,
                manifest.Description,
                manifest.Author,
                manifest.Tags,
                manifest.PairsWith?.Themes ?? [],
                manifest.Parameters,
                manifest.Samples,
                package.IsBuiltIn));
        }

        return entries;
    }
}
