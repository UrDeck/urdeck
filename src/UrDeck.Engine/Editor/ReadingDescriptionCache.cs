// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Settings;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Editor;

/// <summary>
/// The descriptions of the readings the pages use, kept in <c>cache/readings.json</c> (reading-picker spec, "Provider Not
/// Connected"): written after a provider listed its readings, read when it cannot, so the picker still names the readings in use.
/// Holds descriptions only: never a value and never a secret. Small, and rewritten whole.
/// </summary>
public sealed class ReadingDescriptionCache(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    private readonly object _gate = new();

    /// <summary>
    /// Keeps the descriptions of the used readings among <paramref name="listed"/> as the provider's, replacing what the cache held
    /// for the provider. A listing that has none of the used readings leaves nothing for the provider. Never throws: a cache that
    /// cannot be written is a cache that is not there.
    /// </summary>
    /// <param name="providerId">The provider.</param>
    /// <param name="listed">What the provider listed.</param>
    /// <param name="usedIds">The full ids (<c>provider:path</c>) that the configuration uses.</param>
    public void Store(string providerId, IEnumerable<ReadingDescriptor> listed, IReadOnlyCollection<string> usedIds)
    {
        var used = new HashSet<string>(usedIds, StringComparer.OrdinalIgnoreCase);
        var kept = listed.Where(d => used.Contains($"{providerId}:{d.Path}")).ToList();
        lock (_gate)
        {
            var all = Load();
            foreach (string key in all.Keys.Where(k => IsOf(k, providerId)).ToList())
                all.Remove(key);
            foreach (var description in kept)
                all[$"{providerId}:{description.Path}"] = description;
            Save(all);
        }
    }

    /// <summary>The cached descriptions of a provider's readings, by path; empty when there are none.</summary>
    public IReadOnlyList<ReadingDescriptor> Read(string providerId)
    {
        lock (_gate)
            return [.. Load().Where(pair => IsOf(pair.Key, providerId)).Select(pair => pair.Value).OrderBy(d => d.Path, StringComparer.Ordinal)];
    }

    private static bool IsOf(string key, string providerId) =>
        key.StartsWith(providerId + ":", StringComparison.OrdinalIgnoreCase);

    private Dictionary<string, ReadingDescriptor> Load()
    {
        try
        {
            if (!File.Exists(path))
                return new Dictionary<string, ReadingDescriptor>(StringComparer.OrdinalIgnoreCase);
            var read = JsonSerializer.Deserialize<Dictionary<string, ReadingDescriptor>>(File.ReadAllText(path), Options);
            return new Dictionary<string, ReadingDescriptor>(read ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            UrDeckLog.Warn($"The reading cache {path} cannot be read ({ex.Message}); it is ignored");
            return new Dictionary<string, ReadingDescriptor>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save(Dictionary<string, ReadingDescriptor> all)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(all.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value), Options));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UrDeckLog.Warn($"The reading cache {path} cannot be written: {ex.Message}");
        }
    }
}

/// <summary>The readings a configuration uses: the values of its widgets' reading settings.</summary>
public static class UsedReadings
{
    /// <summary>
    /// The full ids in the reading settings of every widget of every page and of the dock, found through the widgets' settings
    /// schemas (kind <c>reading</c>, also inside list items). A widget whose type has no schema contributes nothing.
    /// </summary>
    public static IReadOnlyCollection<string> Of(UrDeckConfig config, Func<string, SettingSchema?> schemaOf)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var widget in config.Pages.SelectMany(p => p.Widgets).Concat(config.Dock))
        {
            var schema = schemaOf(widget.WidgetTypeId);
            if (schema != null && JsonSerializer.SerializeToNode<UrDeck.Sdk.WidgetConfig>(widget, UrDeckJson.Options) is JsonObject values)
                Collect(schema, values, ids);
        }

        return ids;
    }

    private static void Collect(SettingSchema schema, JsonObject values, HashSet<string> ids)
    {
        foreach (var setting in schema.Settings)
        {
            if (!values.TryGetPropertyValue(setting.Key, out var node) || node == null)
                continue;
            if (setting.Kind == SettingKinds.Reading)
            {
                if (node is JsonValue value && value.TryGetValue(out string? id) && !string.IsNullOrWhiteSpace(id))
                    ids.Add(id.Trim());
            }
            else if (setting.Item != null && node is JsonArray items)
            {
                foreach (var item in items.OfType<JsonObject>())
                    Collect(setting.Item, item, ids);
            }
        }
    }
}
