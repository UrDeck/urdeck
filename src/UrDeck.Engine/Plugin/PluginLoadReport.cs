// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Serialization;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Plugin;

/// <summary>How one plugin file fared in the latest load.</summary>
public enum PluginFileStatus
{
    /// <summary>It loaded and registered at least one widget type or provider.</summary>
    Loaded,

    /// <summary>Loading it threw (not a .NET assembly, a missing dependency, ...).</summary>
    Failed,

    /// <summary>It loaded but registered nothing: not a plugin, or everything in it was rejected.</summary>
    Skipped,
}

/// <summary>A widget type or provider a plugin file offered and the loader refused.</summary>
public sealed record RejectedType(string Id, string Reason);

/// <summary>What the latest load made of one plugin file.</summary>
public sealed record PluginFileReport(
    string Path,
    PluginFileStatus Status,
    string? Reason,
    IReadOnlyList<string> WidgetTypes,
    IReadOnlyList<string> ProviderIds,
    IReadOnlyList<RejectedType> Rejected)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>Why a widget's type is not available.</summary>
public enum WidgetUnavailableKind
{
    /// <summary>A plugin that provided the type before is present but failed, was skipped or no longer offers it.</summary>
    DidNotLoad,

    /// <summary>The plugin that provided the type before is not in the plugin folders any more.</summary>
    Removed,

    /// <summary>No plugin ever provided the type here.</summary>
    NotInstalled,
}

/// <summary>The state of a widget whose type is not registered: <paramref name="PluginFile"/> is the file that provided the type, when known.</summary>
public sealed record WidgetUnavailable(WidgetUnavailableKind Kind, string TypeId, string? Name, string? PluginFile, string? Reason)
{
    /// <summary>The name a placeholder uses: the remembered display name, else the type id.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? TypeId : Name;
}

/// <summary>One widget type a plugin provided.</summary>
public sealed class RememberedWidgetType
{
    [JsonPropertyName("plugin")]
    public string Plugin { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("sizes")]
    public List<string> Sizes { get; set; } = [];
}

/// <summary>
/// The widget types plugins have provided, by type id (<c>cache/widget-types.json</c>): added and refreshed after each load,
/// never removed, so a plugin that fails later is still known and "did not load" can be told from "not installed".
/// </summary>
public sealed class WidgetTypeMemory
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly Dictionary<string, RememberedWidgetType> _types = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, RememberedWidgetType> Types => _types;

    public RememberedWidgetType? Find(string typeId) => _types.GetValueOrDefault(typeId);

    /// <summary>Reads the file; a missing or damaged one is an empty memory.</summary>
    public static WidgetTypeMemory Load(string? path)
    {
        var memory = new WidgetTypeMemory();
        if (path == null || !File.Exists(path))
            return memory;
        try
        {
            var read = JsonSerializer.Deserialize<Dictionary<string, RememberedWidgetType>>(File.ReadAllText(path), Json);
            foreach (var (id, type) in read ?? [])
            {
                if (type != null)
                    memory._types[id] = type;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            UrDeckLog.Warn($"Could not read {path}: {ex.Message}");
        }

        return memory;
    }

    /// <summary>Adds or refreshes a type; true when the memory changed.</summary>
    public bool Remember(string typeId, string pluginFile, string name, IEnumerable<string> sizes)
    {
        var entry = new RememberedWidgetType { Plugin = pluginFile, Name = name, Sizes = [.. sizes] };
        if (_types.TryGetValue(typeId, out var old) && old.Plugin == entry.Plugin && old.Name == entry.Name && old.Sizes.SequenceEqual(entry.Sizes))
            return false;
        _types[typeId] = entry;
        return true;
    }

    /// <summary>Writes the memory; a failure is logged, never thrown (it is a cache).</summary>
    public void Save(string path)
    {
        try
        {
            string? dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var ordered = _types.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
            File.WriteAllText(path, JsonSerializer.Serialize(ordered, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UrDeckLog.Warn($"Could not write {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Tells why <paramref name="typeId"/>, which no plugin registered now, is missing: <c>DidNotLoad</c> when the file that
    /// provided it before is present and failed, was skipped, rejected it or no longer offers it; <c>Removed</c> when that
    /// file is gone; <c>NotInstalled</c> when the type was never known. <paramref name="files"/> is the latest load report.
    /// </summary>
    public WidgetUnavailable Classify(string typeId, IReadOnlyList<PluginFileReport> files)
    {
        var rejected = files.SelectMany(f => f.Rejected.Select(r => (File: f, Rejection: r)))
            .FirstOrDefault(x => string.Equals(x.Rejection.Id, typeId, StringComparison.OrdinalIgnoreCase));
        var known = Find(typeId);
        if (known == null)
        {
            return rejected.File != null
                ? new WidgetUnavailable(WidgetUnavailableKind.DidNotLoad, typeId, null, rejected.File.FileName, rejected.Rejection.Reason)
                : new WidgetUnavailable(WidgetUnavailableKind.NotInstalled, typeId, null, null, null);
        }

        var file = files.FirstOrDefault(f => string.Equals(f.FileName, known.Plugin, StringComparison.OrdinalIgnoreCase));
        if (file == null)
            return new WidgetUnavailable(WidgetUnavailableKind.Removed, typeId, known.Name, known.Plugin, $"It was provided by {known.Plugin}.");

        string reason = file.Status != PluginFileStatus.Loaded && file.Reason != null ? file.Reason
            : rejected.File != null ? rejected.Rejection.Reason
            : $"{file.FileName} no longer provides it.";
        return new WidgetUnavailable(WidgetUnavailableKind.DidNotLoad, typeId, known.Name, known.Plugin, reason);
    }
}
