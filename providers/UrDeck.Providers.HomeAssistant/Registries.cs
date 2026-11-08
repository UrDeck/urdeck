// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Nodes;

namespace UrDeck.Providers.HomeAssistant;

/// <summary>
/// Home Assistant's area, device and entity registries, read once for a listing (provider-homeassistant, "Listing Entities"): which
/// area (room) an entity is in is its own assignment, else its device's. Only the WebSocket API serves them, so a listing opens
/// a short connection of its own beside the REST request for the states; the shown entities never need it.
/// </summary>
internal sealed class Registries
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private readonly Dictionary<string, string> _areaNames;
    private readonly Dictionary<string, string> _deviceAreas;
    private readonly Dictionary<string, (string? Area, string? Device)> _entities;

    private Registries(Dictionary<string, string> areaNames, Dictionary<string, string> deviceAreas, Dictionary<string, (string? Area, string? Device)> entities)
    {
        _areaNames = areaNames;
        _deviceAreas = deviceAreas;
        _entities = entities;
    }

    /// <summary>The name of the area the entity is in; null when it is in none.</summary>
    public string? AreaOf(string entityId)
    {
        if (!_entities.TryGetValue(entityId, out var entry))
            return null;
        string? area = entry.Area;
        if (area == null && entry.Device != null)
            _deviceAreas.TryGetValue(entry.Device, out area);
        return area != null && _areaNames.TryGetValue(area, out string? name) ? name : null;
    }

    /// <summary>Reads the registries over a short WebSocket session. Throws with a message saying why when it cannot.</summary>
    public static async Task<Registries> ReadAsync(IHomeAssistantClients clients, Uri address, string token, CancellationToken cancellationToken)
    {
        var socketAddress = new UriBuilder(address) { Scheme = address.Scheme == Uri.UriSchemeHttps ? "wss" : "ws" };
        socketAddress.Path = socketAddress.Path.TrimEnd('/') + "/api/websocket";
        await using var socket = clients.CreateSocket();
        using var limit = new CancellationTokenSource(Limit);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        try
        {
            await socket.ConnectAsync(socketAddress.Uri, linked.Token);
            var hello = await socket.ReceiveAsync(linked.Token);
            if (Text(hello, "type") != "auth_required")
                throw new InvalidOperationException("the address does not answer like Home Assistant");
            await socket.SendAsync(new JsonObject { ["type"] = "auth", ["access_token"] = token }.ToJsonString(), linked.Token);
            var answer = await socket.ReceiveAsync(linked.Token);
            if (Text(answer, "type") != "auth_ok")
                throw new InvalidOperationException("Home Assistant did not accept the sign-in");

            string[] commands = ["config/area_registry/list", "config/device_registry/list", "config/entity_registry/list"];
            for (int i = 0; i < commands.Length; i++)
                await socket.SendAsync(new JsonObject { ["id"] = i + 1, ["type"] = commands[i] }.ToJsonString(), linked.Token);

            var results = new JsonElement?[commands.Length];
            int missing = commands.Length;
            while (missing > 0)
            {
                var message = await socket.ReceiveAsync(linked.Token) ?? throw new InvalidOperationException("the connection was closed");
                if (Text(message, "type") != "result" || !message.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out int id)
                    || id < 1 || id > commands.Length || results[id - 1] != null)
                    continue;
                if (!message.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True
                    || !message.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException($"{commands[id - 1]} was refused");
                results[id - 1] = result;
                missing--;
            }

            return Parse(results[0]!.Value, results[1]!.Value, results[2]!.Value);
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException($"no answer within {Limit.TotalSeconds:0} s");
        }
    }

    internal static Registries Parse(JsonElement areas, JsonElement devices, JsonElement entities)
    {
        var areaNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var area in areas.EnumerateArray())
        {
            if (Text(area, "area_id") is { Length: > 0 } id && Text(area, "name") is { Length: > 0 } name)
                areaNames[id] = name;
        }

        var deviceAreas = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var device in devices.EnumerateArray())
        {
            if (Text(device, "id") is { Length: > 0 } id && Text(device, "area_id") is { Length: > 0 } area)
                deviceAreas[id] = area;
        }

        var entries = new Dictionary<string, (string?, string?)>(StringComparer.Ordinal);
        foreach (var entity in entities.EnumerateArray())
        {
            if (Text(entity, "entity_id") is { Length: > 0 } id)
                entries[id] = (Text(entity, "area_id") is { Length: > 0 } own ? own : null, Text(entity, "device_id") is { Length: > 0 } device ? device : null);
        }

        return new Registries(areaNames, deviceAreas, entries);
    }

    private static string? Text(JsonElement? element, string property) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
