// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json.Nodes;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.HomeAssistant;

/// <summary>A service call: <c>POST /api/services/&lt;domain&gt;/&lt;service&gt;</c> with its JSON body.</summary>
internal sealed record ServiceCall(string Domain, string Service, string EntityId, string Body)
{
    public string Path => $"api/services/{Domain}/{Service}";

    public override string ToString() => $"{Domain}.{Service} on {EntityId}";
}

/// <summary>
/// An action path (<c>light.desk/toggle</c>) as a Home Assistant service call (provider-homeassistant, "Actions"): the domain
/// from the entity id, <c>toggle</c> as each domain's natural tap, and the one argument in the service's main field.
/// </summary>
internal static class ServiceCalls
{
    /// <summary>Splits <c>&lt;entity_id&gt;/&lt;service&gt;</c>; false with a message when the path is not one.</summary>
    public static bool TryParse(string path, out string entityId, out string service, out string? error)
    {
        entityId = "";
        service = "";
        error = null;
        string[] parts = path.Split('/');
        if (parts.Length != 2 || EntityState.DomainOf(parts[0]) == parts[0] || parts[0].EndsWith('.') || parts[1].Length == 0)
        {
            error = $"'{path}' is not a Home Assistant action (expected <entity_id>/<service>, for example light.desk/toggle)";
            return false;
        }

        entityId = parts[0];
        service = parts[1];
        return true;
    }

    /// <summary>
    /// The service <c>toggle</c> stands for in the entity's domain; any other service as it is. A lock toggles by its last
    /// known state (<paramref name="lastState"/>): null with <paramref name="error"/> when that is not known.
    /// </summary>
    public static string? Resolve(string domain, string service, string? lastState, out string? error)
    {
        error = null;
        if (service != "toggle")
            return service;
        switch (domain)
        {
            case "scene" or "script":
                return "turn_on";
            case "button" or "input_button":
                return "press";
            case "media_player":
                return "media_play_pause";
            case "lock":
                switch (lastState)
                {
                    case "locked":
                        return "unlock";
                    case "unlocked":
                        return "lock";
                    default:
                        error = "the lock's state is not known (show it on a page, or use lock or unlock)";
                        return null;
                }

            default:
                return "toggle";
        }
    }

    /// <summary>
    /// The call with its body: <c>entity_id</c> and, for the services with a main field, the argument in it. The argument of
    /// <c>climate.set_temperature</c> is degrees Celsius, sent in <paramref name="temperatureUnit"/> (the installation's).
    /// </summary>
    public static ServiceCall Build(string entityId, string service, ReadingValue? argument, string? temperatureUnit)
    {
        string domain = EntityState.DomainOf(entityId);
        var body = new JsonObject { ["entity_id"] = entityId };
        if (argument is { } value)
        {
            switch ($"{domain}/{service}")
            {
                case "light/turn_on":
                    if (Number(value) is { } brightness)
                        body["brightness_pct"] = Math.Clamp(brightness, 0, 100);
                    break;
                case "fan/set_percentage":
                    if (Number(value) is { } percentage)
                        body["percentage"] = Math.Clamp(percentage, 0, 100);
                    break;
                case "cover/set_cover_position":
                    if (Number(value) is { } position)
                        body["position"] = Math.Clamp(position, 0, 100);
                    break;
                case "number/set_value" or "input_number/set_value":
                    if (Number(value) is { } number)
                        body["value"] = number;
                    break;
                case "select/select_option" or "input_select/select_option":
                    body["option"] = value.Type == ReadingValueType.Text
                        ? value.Text
                        : Number(value)?.ToString(CultureInfo.InvariantCulture) ?? "";
                    break;
                case "media_player/volume_set":
                    if (Number(value) is { } volume)
                        body["volume_level"] = Math.Clamp(volume, 0, 100) / 100.0;
                    break;
                case "climate/set_temperature":
                    if (Number(value) is { } celsius)
                        body["temperature"] = temperatureUnit == "°F" ? Math.Round(celsius * 9 / 5 + 32, 1) : Math.Round(celsius, 1);
                    break;
            }
        }

        return new ServiceCall(domain, service, entityId, body.ToJsonString());
    }

    private static double? Number(ReadingValue value) => value.Type switch
    {
        ReadingValueType.Number => value.Number,
        ReadingValueType.OnOff => value.IsOn ? 100 : 0,
        ReadingValueType.Text when double.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
        _ => null,
    };
}

/// <summary>The entity ids nearest a missing one, for "did you mean …?".</summary>
internal static class Suggestions
{
    public const int Count = 3;

    /// <summary>
    /// Up to three ids at the smallest edit distance from <paramref name="missing"/> (all of them equally close), when that is
    /// close enough to be a typo.
    /// </summary>
    public static IReadOnlyList<string> Nearest(string missing, IEnumerable<string> known)
    {
        int limit = Math.Max(2, missing.Length / 3);
        var candidates = known
            .Select(id => (Id: id, Distance: Distance(missing, id)))
            .Where(c => c.Distance <= limit && c.Distance > 0)
            .ToList();
        if (candidates.Count == 0)
            return [];
        int nearest = candidates.Min(c => c.Distance);
        return candidates
            .Where(c => c.Distance == nearest)
            .Select(c => c.Id)
            .Order(StringComparer.Ordinal)
            .Take(Count)
            .ToList();
    }

    /// <summary>"no entity '…' in Home Assistant", with "did you mean …?" when something is close.</summary>
    public static string Explain(string missing, IEnumerable<string>? known)
    {
        string reason = $"no entity '{missing}' in Home Assistant";
        if (known == null)
            return reason;
        var nearest = Nearest(missing, known);
        return nearest.Count switch
        {
            0 => reason,
            1 => $"{reason}; did you mean {nearest[0]}?",
            _ => $"{reason}; did you mean {string.Join(", ", nearest.Take(nearest.Count - 1))} or {nearest[^1]}?",
        };
    }

    /// <summary>The Levenshtein distance, ignoring case.</summary>
    internal static int Distance(string a, string b)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
