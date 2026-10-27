// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;

namespace UrDeck.Providers.HomeAssistant;

/// <summary>
/// One entity as Home Assistant last reported it: its state text and its attributes. Built from the compressed state of a
/// <c>subscribe_entities</c> event (<c>{"s": ..., "a": {...}}</c>) or the full state of a listing
/// (<c>{"entity_id": ..., "state": ..., "attributes": {...}}</c>), and changed by the event's diffs.
/// </summary>
internal sealed class EntityState
{
    private EntityState(string id, string state, Dictionary<string, JsonElement> attributes)
    {
        Id = id;
        State = state;
        Attributes = attributes;
    }

    public string Id { get; }

    /// <summary>The part of the id before the dot (<c>light</c>); the whole id when it has none.</summary>
    public string Domain => DomainOf(Id);

    public string State { get; private set; }

    public Dictionary<string, JsonElement> Attributes { get; }

    public static string DomainOf(string entityId)
    {
        int dot = entityId.IndexOf('.', StringComparison.Ordinal);
        return dot > 0 ? entityId[..dot] : entityId;
    }

    /// <summary>From a compressed state: <c>s</c> the state, <c>a</c> the attributes.</summary>
    public static EntityState FromCompressed(string id, JsonElement compressed)
    {
        var attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        string state = "";
        if (compressed.ValueKind == JsonValueKind.Object)
        {
            if (compressed.TryGetProperty("s", out var s))
                state = AsText(s);
            if (compressed.TryGetProperty("a", out var a) && a.ValueKind == JsonValueKind.Object)
            {
                foreach (var attribute in a.EnumerateObject())
                    attributes[attribute.Name] = attribute.Value.Clone();
            }
        }

        return new EntityState(id, state, attributes);
    }

    /// <summary>From a full state of <c>get_states</c> or <c>GET /api/states</c>; null when it has no entity id.</summary>
    public static EntityState? FromFull(JsonElement full)
    {
        if (full.ValueKind != JsonValueKind.Object || !full.TryGetProperty("entity_id", out var id) || id.ValueKind != JsonValueKind.String)
            return null;
        var attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (full.TryGetProperty("attributes", out var a) && a.ValueKind == JsonValueKind.Object)
        {
            foreach (var attribute in a.EnumerateObject())
                attributes[attribute.Name] = attribute.Value.Clone();
        }

        return new EntityState(id.GetString()!, full.TryGetProperty("state", out var s) ? AsText(s) : "", attributes);
    }

    /// <summary>Applies a diff: <c>+</c> holds the new state and the changed attributes, <c>-</c> the names of removed ones.</summary>
    public void Apply(JsonElement diff)
    {
        if (diff.ValueKind != JsonValueKind.Object)
            return;
        if (diff.TryGetProperty("+", out var added) && added.ValueKind == JsonValueKind.Object)
        {
            if (added.TryGetProperty("s", out var s))
                State = AsText(s);
            if (added.TryGetProperty("a", out var a) && a.ValueKind == JsonValueKind.Object)
            {
                foreach (var attribute in a.EnumerateObject())
                    Attributes[attribute.Name] = attribute.Value.Clone();
            }
        }

        if (diff.TryGetProperty("-", out var removed) && removed.ValueKind == JsonValueKind.Object
            && removed.TryGetProperty("a", out var names) && names.ValueKind == JsonValueKind.Array)
        {
            foreach (var name in names.EnumerateArray())
            {
                if (name.ValueKind == JsonValueKind.String)
                    Attributes.Remove(name.GetString()!);
            }
        }
    }

    /// <summary>A text attribute; null when it is missing or not a text.</summary>
    public string? Text(string attribute) =>
        Attributes.TryGetValue(attribute, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>A numeric attribute (a number, or a text that is one); null otherwise.</summary>
    public double? Number(string attribute) =>
        Attributes.TryGetValue(attribute, out var value) ? AsNumber(value) : null;

    public static double? AsNumber(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
        _ => null,
    };

    private static string AsText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        _ => value.GetRawText(),
    };
}
