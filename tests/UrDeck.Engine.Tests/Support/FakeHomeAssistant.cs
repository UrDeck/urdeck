// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using UrDeck.Providers.HomeAssistant;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Tests.Support;

/// <summary>
/// A pretend Home Assistant behind the provider's two seams. It answers the WebSocket commands the provider sends in the shapes
/// captured from a real installation (auth, <c>subscribe_entities</c> with its first <c>a</c> event and later <c>c</c> and
/// <c>r</c> events, <c>get_states</c>, <c>get_config</c>, <c>ping</c>) and the REST requests, over fictional entities.
/// </summary>
internal sealed class FakeHomeAssistant : IHomeAssistantClients
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (string State, JsonObject Attributes)> _entities = new(StringComparer.Ordinal);
    private readonly List<FakeHomeAssistantSocket> _sockets = [];

    public string Version { get; set; } = "2026.10.3";

    public string TemperatureUnit { get; set; } = "°C";

    public bool RejectToken { get; set; }

    public bool AnswerPings { get; set; } = true;

    /// <summary>Thrown by a connect: the address cannot be reached.</summary>
    public Exception? ConnectError { get; set; }

    public FakeRest Rest { get; } = new();

    public List<(Uri Address, string Token)> RestClients { get; } = [];

    public IReadOnlyList<FakeHomeAssistantSocket> Sockets
    {
        get
        {
            lock (_gate)
                return _sockets.ToArray();
        }
    }

    public FakeHomeAssistantSocket? Current => Sockets is { Count: > 0 } sockets ? sockets[^1] : null;

    /// <summary>Connections that passed the connect step.</summary>
    public int Connects => Sockets.Count(s => s.Connected);

    /// <summary>Messages written to an open socket and not yet read by the provider.</summary>
    public int Pending => Sockets.Sum(s => s.Pending);

    public IHomeAssistantSocket CreateSocket()
    {
        var socket = new FakeHomeAssistantSocket(this);
        lock (_gate)
            _sockets.Add(socket);
        return socket;
    }

    public IHomeAssistantRest CreateRest(Uri address, string token)
    {
        lock (_gate)
            RestClients.Add((address, token));
        Rest.Owner = this;
        return Rest;
    }

    /// <summary>Adds or changes an entity; an open subscription that includes it gets a diff.</summary>
    public void Set(string entityId, string state, JsonObject? attributes = null)
    {
        JsonObject? diff;
        lock (_gate)
        {
            bool known = _entities.TryGetValue(entityId, out var old);
            var next = attributes ?? (known ? (JsonObject)old.Attributes.DeepClone() : []);
            _entities[entityId] = (state, next);
            diff = known ? new JsonObject { ["+"] = new JsonObject { ["s"] = state, ["a"] = next.DeepClone(), ["lc"] = 1760000000.5 } } : null;
        }

        foreach (var socket in Sockets.Where(s => s.Watches(entityId)))
        {
            socket.Push(diff != null
                ? new JsonObject { ["c"] = new JsonObject { [entityId] = diff } }
                : new JsonObject { ["a"] = new JsonObject { [entityId] = Compressed(entityId) } });
        }
    }

    public void Remove(string entityId)
    {
        lock (_gate)
            _entities.Remove(entityId);
        foreach (var socket in Sockets.Where(s => s.Watches(entityId)))
            socket.Push(new JsonObject { ["r"] = new JsonArray(entityId) });
    }

    /// <summary>Breaks the open connection, as a Wi-Fi blip does.</summary>
    public void Drop() => Current?.Break();

    internal JsonObject Compressed(string entityId)
    {
        lock (_gate)
        {
            var (state, attributes) = _entities[entityId];
            return new JsonObject { ["s"] = state, ["a"] = attributes.DeepClone(), ["c"] = "01JFAKECONTEXT", ["lc"] = 1760000000.25 };
        }
    }

    internal bool Has(string entityId)
    {
        lock (_gate)
            return _entities.ContainsKey(entityId);
    }

    internal JsonArray FullStates()
    {
        lock (_gate)
        {
            var list = new JsonArray();
            foreach (var (id, (state, attributes)) in _entities.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                list.Add(new JsonObject
                {
                    ["entity_id"] = id,
                    ["state"] = state,
                    ["attributes"] = attributes.DeepClone(),
                    ["last_changed"] = "2026-10-13T19:47:32.000000+00:00",
                    ["context"] = new JsonObject { ["id"] = "01JFAKECONTEXT" },
                });
            }

            return list;
        }
    }

    internal JsonObject Config() => new()
    {
        ["version"] = Version,
        ["unit_system"] = new JsonObject { ["temperature"] = TemperatureUnit, ["length"] = "km" },
        ["location_name"] = "Home",
    };
}

/// <summary>One WebSocket connection to <see cref="FakeHomeAssistant"/>; it records what the provider sent.</summary>
internal sealed class FakeHomeAssistantSocket(FakeHomeAssistant server) : IHomeAssistantSocket
{
    private readonly Channel<Func<JsonElement?>> _incoming = Channel.CreateUnbounded<Func<JsonElement?>>();
    private readonly object _gate = new();
    private readonly List<JsonObject> _sent = [];
    private HashSet<string> _subscribed = new(StringComparer.Ordinal);
    private int? _subscription;
    private int _pending;

    public Uri? Address { get; private set; }

    public bool Connected { get; private set; }

    public bool Disposed { get; private set; }

    public int Pending => Volatile.Read(ref _pending);

    public IReadOnlyList<JsonObject> Sent
    {
        get
        {
            lock (_gate)
                return _sent.ToArray();
        }
    }

    public IReadOnlyList<JsonObject> SentOfType(string type) => Sent.Where(m => (string?)m["type"] == type).ToList();

    public IReadOnlyCollection<string> Subscribed
    {
        get
        {
            lock (_gate)
                return _subscribed.ToArray();
        }
    }

    public bool Watches(string entityId)
    {
        lock (_gate)
            return !Disposed && _subscription != null && _subscribed.Contains(entityId);
    }

    public Task ConnectAsync(Uri address, CancellationToken cancellationToken)
    {
        Address = address;
        if (server.ConnectError is { } error)
            return Task.FromException(error);
        Connected = true;
        Enqueue(new JsonObject { ["type"] = "auth_required", ["ha_version"] = server.Version });
        return Task.CompletedTask;
    }

    public Task SendAsync(string json, CancellationToken cancellationToken)
    {
        var message = JsonNode.Parse(json)!.AsObject();
        lock (_gate)
            _sent.Add(message);
        int id = (int?)message["id"] ?? 0;
        switch ((string?)message["type"])
        {
            case "auth":
                Enqueue(server.RejectToken
                    ? new JsonObject { ["type"] = "auth_invalid", ["message"] = "Invalid access token or password" }
                    : new JsonObject { ["type"] = "auth_ok", ["ha_version"] = server.Version });
                break;
            case "subscribe_entities":
                var ids = message["entity_ids"]!.AsArray().Select(n => (string)n!).ToHashSet(StringComparer.Ordinal);
                lock (_gate)
                {
                    _subscription = id;
                    _subscribed = ids;
                }

                Enqueue(Result(id, null));
                var first = new JsonObject();
                foreach (string entity in ids.Where(server.Has))
                    first[entity] = server.Compressed(entity);
                Enqueue(new JsonObject { ["id"] = id, ["type"] = "event", ["event"] = new JsonObject { ["a"] = first } });
                break;
            case "unsubscribe_events":
                lock (_gate)
                {
                    if (_subscription == (int?)message["subscription"])
                    {
                        _subscription = null;
                        _subscribed = new HashSet<string>(StringComparer.Ordinal);
                    }
                }

                Enqueue(Result(id, null));
                break;
            case "get_states":
                Enqueue(Result(id, server.FullStates()));
                break;
            case "get_config":
                Enqueue(Result(id, server.Config()));
                break;
            case "ping":
                if (server.AnswerPings)
                    Enqueue(new JsonObject { ["id"] = id, ["type"] = "pong" });
                break;
        }

        return Task.CompletedTask;
    }

    public async Task<JsonElement?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var next = await _incoming.Reader.ReadAsync(cancellationToken);
        Interlocked.Decrement(ref _pending);
        return next();
    }

    /// <summary>An event of the current subscription.</summary>
    public void Push(JsonObject change)
    {
        int? subscription;
        lock (_gate)
            subscription = _subscription;
        if (subscription != null)
            Enqueue(new JsonObject { ["id"] = subscription, ["type"] = "event", ["event"] = change });
    }

    public void Break() => Write(() => throw new WebSocketException("The remote party closed the WebSocket connection without completing the close handshake."));

    public ValueTask DisposeAsync()
    {
        lock (_gate)
            Disposed = true;
        _incoming.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private static JsonObject Result(int id, JsonNode? result) =>
        new() { ["id"] = id, ["type"] = "result", ["success"] = true, ["result"] = result };

    private void Enqueue(JsonObject message)
    {
        var element = JsonDocument.Parse(message.ToJsonString()).RootElement.Clone();
        Write(() => element);
    }

    private void Write(Func<JsonElement?> item)
    {
        Interlocked.Increment(ref _pending);
        if (!_incoming.Writer.TryWrite(item))
            Interlocked.Decrement(ref _pending);
    }
}

/// <summary>Home Assistant's REST side: answers what <see cref="Respond"/> says, else the entities and the configuration.</summary>
internal sealed class FakeRest : IHomeAssistantRest
{
    private readonly object _gate = new();
    private readonly List<(string Method, string Path, string Body)> _requests = [];

    internal FakeHomeAssistant? Owner { get; set; }

    /// <summary>Overrides the answer to a request; null falls through to the default.</summary>
    public Func<string, string, string, RestAnswer?>? Respond { get; set; }

    public IReadOnlyList<(string Method, string Path, string Body)> Requests
    {
        get
        {
            lock (_gate)
                return _requests.ToArray();
        }
    }

    public Task<RestAnswer> GetAsync(string path, CancellationToken cancellationToken) => Answer("GET", path, "");

    public Task<RestAnswer> PostAsync(string path, string json, CancellationToken cancellationToken) => Answer("POST", path, json);

    public void Dispose()
    {
    }

    private Task<RestAnswer> Answer(string method, string path, string body)
    {
        lock (_gate)
            _requests.Add((method, path, body));
        if (Respond?.Invoke(method, path, body) is { } answer)
            return Task.FromResult(answer);
        return Task.FromResult((method, path) switch
        {
            ("GET", "api/states") => new RestAnswer(200, Owner!.FullStates().ToJsonString()),
            ("GET", "api/config") => new RestAnswer(200, Owner!.Config().ToJsonString()),
            ("POST", _) => new RestAnswer(200, "[]"),
            _ => new RestAnswer(404, "404: Not Found"),
        });
    }
}

/// <summary>A sink that keeps what a provider told it, for assertions.</summary>
internal sealed class RecordingSink : IReadingSink
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ReadingUpdate> _latest = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReadingDescriptor> _described = new(StringComparer.Ordinal);
    private readonly List<string> _logs = [];
    private readonly List<string> _interruptions = [];
    private readonly List<int> _batchSizes = [];

    public void Publish(string path, ReadingValue value) => PublishBatch([ReadingUpdate.Of(path, value)]);

    public void Unavailable(string path, string reason) => PublishBatch([ReadingUpdate.Missing(path, reason)]);

    public void PublishBatch(IReadOnlyList<ReadingUpdate> updates)
    {
        lock (_gate)
        {
            _batchSizes.Add(updates.Count);
            foreach (var update in updates)
                _latest[update.Path] = update;
        }
    }

    public void Log(string message)
    {
        lock (_gate)
            _logs.Add(message);
    }

    public void Interrupted(string reason)
    {
        lock (_gate)
            _interruptions.Add(reason);
    }

    public void Describe(string path, ReadingDescriptor descriptor)
    {
        lock (_gate)
            _described[path] = descriptor;
    }

    public ReadingUpdate? Latest(string path)
    {
        lock (_gate)
            return _latest.TryGetValue(path, out var update) ? update : null;
    }

    public ReadingDescriptor? Described(string path)
    {
        lock (_gate)
            return _described.TryGetValue(path, out var descriptor) ? descriptor : null;
    }

    public IReadOnlyList<string> Logs
    {
        get
        {
            lock (_gate)
                return _logs.ToArray();
        }
    }

    public IReadOnlyList<string> Interruptions
    {
        get
        {
            lock (_gate)
                return _interruptions.ToArray();
        }
    }

    public IReadOnlyList<int> BatchSizes
    {
        get
        {
            lock (_gate)
                return _batchSizes.ToArray();
        }
    }
}
