// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Engine.Backgrounds;

namespace UrDeck.Cli;

/// <summary>
/// The change <c>shader render --update-manifest</c> makes to <c>manifest.json</c>: the picture's entry in <c>samples</c>, with
/// <c>settings</c> saying how it was made. <see cref="Plan"/> decides before anything is rendered whether the rewrite is safe;
/// <see cref="Apply"/> writes it once the picture exists.
/// </summary>
internal sealed class SampleUpdate
{
    private readonly string _manifestPath;
    private readonly JsonObject _root;
    private readonly string _file;
    private readonly JsonObject _settings;

    private SampleUpdate(string manifestPath, JsonObject root, string file, JsonObject settings)
    {
        _manifestPath = manifestPath;
        _root = root;
        _file = file;
        _settings = settings;
    }

    /// <summary>
    /// Reads the manifest of <paramref name="folder"/> and checks that a rewrite keeps everything. Null when there is nothing to
    /// rewrite (no file, or not a JSON object): the render then fails as it does without the option. A manifest that would lose
    /// something is refused with the entry to paste by hand.
    /// </summary>
    public static SampleUpdate? Plan(string folder, string file, JsonObject settings)
    {
        string path = Path.Combine(folder, BackgroundManifest.FileName);
        string text;
        try
        {
            if (!File.Exists(path))
                return null;
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var inspection = ManifestWriter.Inspect(text);
        if (!inspection.IsObject)
            return null;

        var problems = inspection.Losses.Select(loss => $"it has {loss}, which a rewrite would lose").ToList();
        JsonObject? root = null;
        if (problems.Count == 0)
        {
            root = (JsonObject)ManifestWriter.Parse(text)!;
            if (root["samples"] is { } samples && samples is not JsonArray)
                problems.Add("its 'samples' is not an array, and the tool does not repair a manifest");
        }

        if (problems.Count > 0)
        {
            var entry = NewEntry(file, settings.DeepClone());
            throw new ToolFailureException(
                $"{PathDisplay.Of(path)} was not updated: {string.Join("; ", problems)}.\n"
                + "Nothing was rendered. Add this to its \"samples\" by hand, or put its \"settings\" in the entry for the file if it is there:\n"
                + ManifestWriter.Serialize(entry).TrimEnd('\n'));
        }

        return new SampleUpdate(path, root!, file, settings);
    }

    /// <summary>Records the picture in the manifest. Returns true when an entry was added, false when one was updated.</summary>
    public bool Apply()
    {
        if (_root["samples"] is not JsonArray samples)
        {
            samples = [];
            _root["samples"] = samples;
        }

        var existing = samples.OfType<JsonObject>().FirstOrDefault(entry => Names(entry, _file));
        bool added = existing == null;
        if (existing != null)
            existing["settings"] = _settings;
        else
            samples.Add(NewEntry(_file, _settings));

        ManifestWriter.Write(_manifestPath, _root);
        return added;
    }

    public string ManifestPath => _manifestPath;

    private static JsonObject NewEntry(string file, JsonNode settings) => new()
    {
        ["type"] = "image",
        ["file"] = file,
        ["settings"] = settings,
    };

    private static bool Names(JsonObject entry, string file) =>
        entry["file"] is JsonValue value && value.TryGetValue(out string? text) && text is not null && string.Equals(text.Replace('\\', '/'), file, StringComparison.Ordinal);
}
