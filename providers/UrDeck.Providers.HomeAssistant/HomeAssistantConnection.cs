// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.HomeAssistant;

/// <summary>Where the connection stands; for the log and the tests.</summary>
internal enum ConnectionPhase
{
    /// <summary>No socket: nothing is shown, or the provider has not been asked for anything yet.</summary>
    Idle,

    Connecting,

    /// <summary>Signed in; states arrive for the entities shown.</summary>
    Subscribed,

    /// <summary>Waiting to try again after a lost or failed connection.</summary>
    Backoff,

    /// <summary>Home Assistant rejected the token; nothing is tried until the settings change (which starts a new provider run).</summary>
    Rejected,
}

/// <summary>
/// The WebSocket side of the provider (provider-homeassistant, "Connection Follows Demand" and "Outages And Reconnecting"): one
/// loop that opens the socket while something is shown, signs in, subscribes to exactly the shown entities with
/// <c>subscribe_entities</c> (its first event carries their full states, then diffs), re-subscribes 250 ms after the first of a
/// burst of demand changes, checks the connection with a ping every 30 s (10 s to answer), closes it after 30 s with nothing
/// shown, and after a loss reports an interruption and tries again after 1, 2, 5, 10, 30 and then every 60 s, each varied by up
/// to 20 %. Everything but <see cref="SetDemand"/>, <see cref="LastState"/> and <see cref="TemperatureUnit"/> runs on the loop.
/// </summary>
internal sealed class HomeAssistantConnection : IAsyncDisposable
{
    internal static readonly TimeSpan[] Delays =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

    internal const double Jitter = 0.2;
    internal static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(250);
    internal static readonly TimeSpan IdleClose = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan PingEvery = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan PingAnswer = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan Handshake = TimeSpan.FromSeconds(10);

    internal const string RejectedReason = "Home Assistant rejected the token; fix it in Settings";

    private readonly Uri _address;
    private readonly Uri _socketAddress;
    private readonly string _token;
    private readonly IHomeAssistantClients _clients;
    private readonly IReadingSink _sink;
    private readonly TimeProvider _time;
    private readonly Func<double> _random;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<string, string> _lastStates = new(StringComparer.Ordinal);
    private string[] _demand = [];
    private TaskCompletionSource _wake = NewSignal();
    private string? _fatal;
    private Task? _loop;
    private volatile string? _temperatureUnit;
    private volatile ConnectionPhase _phase;
    private int _waits;
    private bool _waiting;

    // One connection's state, touched only by the loop.
    private IHomeAssistantSocket? _socket;
    private CancellationToken _session;
    private int _nextId;
    private int? _subscription;
    private HashSet<string> _subscribed = new(StringComparer.Ordinal);
    private bool _initialized;
    private readonly Dictionary<string, EntityState> _entities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReadingDescriptor> _described = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unexplained = new(StringComparer.Ordinal);
    private List<string>? _listing;
    private bool _listingFailed;
    private int? _listingRequest;
    private int? _configRequest;
    private int? _pingId;
    private long _pingSent;
    private long _lastPing;

    /// <param name="address">The installation's address (<c>http</c> or <c>https</c>).</param>
    /// <param name="token">The long-lived access token; sent only to <paramref name="address"/>, never logged.</param>
    /// <param name="clients">Makes the socket of each connection.</param>
    /// <param name="sink">Where the states go.</param>
    /// <param name="time">The clock of the debounce, the ping, the idle close and the backoff.</param>
    /// <param name="random">A number from 0 to 1 for the jitter.</param>
    public HomeAssistantConnection(Uri address, string token, IHomeAssistantClients clients, IReadingSink sink, TimeProvider time, Func<double> random)
    {
        _address = address;
        _token = token;
        _clients = clients;
        _sink = sink;
        _time = time;
        _random = random;
        var socket = new UriBuilder(address) { Scheme = address.Scheme == Uri.UriSchemeHttps ? "wss" : "ws" };
        socket.Path = socket.Path.TrimEnd('/') + "/api/websocket";
        _socketAddress = socket.Uri;
    }

    public ConnectionPhase Phase => _phase;

    /// <summary>How often the loop has waited for something; with <see cref="IsWaiting"/>, tells a test the loop reacted and is parked.</summary>
    internal int Waits => Volatile.Read(ref _waits);

    /// <summary>Whether the loop is waiting (for demand, a message, a timer or the next attempt).</summary>
    internal bool IsWaiting => Volatile.Read(ref _waiting);

    /// <summary>The installation's temperature unit (<c>°C</c> or <c>°F</c>) from <c>get_config</c>; null before the first connection.</summary>
    public string? TemperatureUnit => _temperatureUnit;

    /// <summary>The state an entity was last reported in while shown; null when it is not known.</summary>
    public string? LastState(string entityId) => _lastStates.TryGetValue(entityId.ToLowerInvariant(), out string? state) ? state : null;

    public void Start() => _loop = Task.Run(() => RunAsync(_stop.Token));

    /// <summary>The shown paths; the subscription follows within <see cref="Debounce"/> (the socket opens for a first one).</summary>
    public void SetDemand(IReadOnlyCollection<string> paths)
    {
        string? fatal;
        lock (_gate)
        {
            _demand = [.. paths];
            fatal = _fatal;
            _wake.TrySetResult();

            // Woken: the loop is no longer parked on what it waited for.
            Volatile.Write(ref _waiting, false);
        }

        if (fatal != null && paths.Count > 0)
            _sink.PublishBatch(paths.Select(p => ReadingUpdate.Missing(p, fatal)).ToList());
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        if (_loop != null)
        {
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                // A socket that does not close in time is dropped with the provider.
            }
        }

        _stop.Dispose();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void Park()
    {
        Interlocked.Increment(ref _waits);
        Volatile.Write(ref _waiting, true);
    }

    private void Unpark() => Volatile.Write(ref _waiting, false);

    /// <summary>The delay before attempt <paramref name="attempt"/> (from 1): the table's, varied by up to <see cref="Jitter"/>.</summary>
    internal TimeSpan DelayFor(int attempt)
    {
        var delay = Delays[Math.Clamp(attempt - 1, 0, Delays.Length - 1)];
        double factor = 1 + ((Math.Clamp(_random(), 0, 1) * 2) - 1) * Jitter;
        return delay * factor;
    }

    private async Task RunAsync(CancellationToken stop)
    {
        int attempts = 0;
        try
        {
            while (true)
            {
                _phase = ConnectionPhase.Idle;
                await WaitForDemandAsync(stop);
                _phase = ConnectionPhase.Connecting;
                var end = await SessionAsync(attempts, stop);
                if (end.Kind == EndKind.Idle)
                {
                    attempts = 0;
                    continue;
                }

                if (end.Kind == EndKind.Rejected)
                {
                    Reject(end.Cause);
                    return;
                }

                if (end.WasConnected)
                    attempts = 0;
                attempts++;
                _phase = ConnectionPhase.Backoff;
                _sink.Interrupted(end.WasConnected
                    ? $"connection to Home Assistant lost: {end.Cause}"
                    : $"cannot connect to Home Assistant at {_address}: {end.Cause}");
                var delay = DelayFor(attempts);
                _sink.Log(string.Create(CultureInfo.InvariantCulture, $"Home Assistant: attempt {attempts} in {delay.TotalSeconds:0.#} s ({end.Cause})"));
                Park();
                await Task.Delay(delay, _time, stop);
                Unpark();
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // Stopped.
        }
        catch (Exception ex)
        {
            _sink.Log($"Home Assistant: the connection stopped after an error: {ex.GetBaseException().Message}");
            _sink.Interrupted($"the Home Assistant connection failed: {ex.GetBaseException().Message}");
        }
    }

    private async Task WaitForDemandAsync(CancellationToken stop)
    {
        while (true)
        {
            Task wake;
            lock (_gate)
            {
                if (DemandEntities(_demand).Count > 0)
                    return;
                if (_wake.Task.IsCompleted)
                    _wake = NewSignal();
                wake = _wake.Task;
            }

            Park();
            await wake.WaitAsync(stop);
            Unpark();
        }
    }

    private enum EndKind
    {
        Idle,
        Lost,
        Rejected,
    }

    private readonly record struct SessionEnd(EndKind Kind, string Cause, bool WasConnected);

    private async Task<SessionEnd> SessionAsync(int attempts, CancellationToken stop)
    {
        ResetSession();
        await using var socket = _clients.CreateSocket();
        using var session = CancellationTokenSource.CreateLinkedTokenSource(stop);
        _socket = socket;
        _session = session.Token;
        bool connected = false;
        Task<JsonElement?>? receive = null;
        try
        {
            string version;
            using (var limit = new CancellationTokenSource(Handshake, _time))
            using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(session.Token, limit.Token))
            {
                try
                {
                    await socket.ConnectAsync(_socketAddress, handshake.Token);
                    var hello = await socket.ReceiveAsync(handshake.Token);
                    if (TypeOf(hello) != "auth_required")
                        return new SessionEnd(EndKind.Lost, hello == null ? "the connection was closed" : "the address does not answer like Home Assistant", false);
                    await socket.SendAsync(new JsonObject { ["type"] = "auth", ["access_token"] = _token }.ToJsonString(), handshake.Token);
                    var answer = await socket.ReceiveAsync(handshake.Token);
                    switch (TypeOf(answer))
                    {
                        case "auth_invalid":
                            return new SessionEnd(EndKind.Rejected, Text(answer, "message") ?? "invalid token", false);
                        case "auth_ok":
                            version = Text(answer, "ha_version") ?? "?";
                            break;
                        default:
                            return new SessionEnd(EndKind.Lost, answer == null ? "the connection was closed while signing in" : "Home Assistant did not accept the sign-in", false);
                    }
                }
                catch (Exception) when (limit.IsCancellationRequested && !stop.IsCancellationRequested)
                {
                    return new SessionEnd(EndKind.Lost, $"no answer within {Handshake.TotalSeconds:0} s", false);
                }
            }

            connected = true;
            _phase = ConnectionPhase.Subscribed;
            if (attempts > 0)
                _sink.Log($"Home Assistant: reconnected after {attempts} attempt{(attempts == 1 ? "" : "s")}");
            lock (_gate)
            {
                if (_wake.Task.IsCompleted)
                    _wake = NewSignal();
            }

            _configRequest = await SendAsync(new JsonObject { ["type"] = "get_config" });
            await SubscribeAsync(version);
            _lastPing = _time.GetTimestamp();
            long? emptySince = _subscribed.Count == 0 ? _time.GetTimestamp() : null;
            bool dirty = false;
            long dirtySince = 0;
            receive = socket.ReceiveAsync(session.Token);
            while (true)
            {
                if (_pingId != null && Elapsed(_pingSent) >= PingAnswer)
                    return new SessionEnd(EndKind.Lost, $"no answer to a ping within {PingAnswer.TotalSeconds:0} s", true);
                if (_pingId == null && Elapsed(_lastPing) >= PingEvery)
                {
                    _pingSent = _time.GetTimestamp();
                    _pingId = await SendAsync(new JsonObject { ["type"] = "ping" });
                }

                if (dirty && Elapsed(dirtySince) >= Debounce)
                {
                    dirty = false;
                    await SubscribeAsync(null);
                    emptySince = _subscribed.Count == 0 ? emptySince ?? _time.GetTimestamp() : null;
                }

                if (emptySince is { } since && Elapsed(since) >= IdleClose)
                {
                    _sink.Log($"Home Assistant: nothing shown for {IdleClose.TotalSeconds:0} s; connection closed");
                    return new SessionEnd(EndKind.Idle, "", true);
                }

                var wait = _pingId != null ? PingAnswer - Elapsed(_pingSent) : PingEvery - Elapsed(_lastPing);
                if (dirty)
                    wait = Min(wait, Debounce - Elapsed(dirtySince));
                if (emptySince is { } empty)
                    wait = Min(wait, IdleClose - Elapsed(empty));
                if (wait <= TimeSpan.Zero)
                    continue;

                Task wake;
                lock (_gate)
                    wake = _wake.Task;
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
                var delay = Task.Delay(wait, _time, waitCts.Token);
                if (!receive.IsCompleted && !wake.IsCompleted)
                    Park();
                var done = await Task.WhenAny(receive, wake, delay);
                Unpark();
                await waitCts.CancelAsync();
                stop.ThrowIfCancellationRequested();
                if (done == receive)
                {
                    var message = await receive;
                    receive = null;
                    if (message == null)
                        return new SessionEnd(EndKind.Lost, "Home Assistant closed the connection", true);
                    if (await HandleAsync(message.Value) is { } lost)
                        return new SessionEnd(EndKind.Lost, lost, true);
                    receive = socket.ReceiveAsync(session.Token);
                }
                else if (done == wake)
                {
                    lock (_gate)
                    {
                        if (_wake.Task.IsCompleted)
                            _wake = NewSignal();
                    }

                    if (!dirty)
                    {
                        dirty = true;
                        dirtySince = _time.GetTimestamp();
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SessionEnd(EndKind.Lost, ex.GetBaseException().Message, connected);
        }
        finally
        {
            await session.CancelAsync();
            receive?.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            _socket = null;
        }
    }

    private void ResetSession()
    {
        _nextId = 0;
        _subscription = null;
        _subscribed = new HashSet<string>(StringComparer.Ordinal);
        _initialized = false;
        _entities.Clear();
        _described.Clear();
        _unexplained.Clear();
        _listing = null;
        _listingFailed = false;
        _listingRequest = null;
        _configRequest = null;
        _pingId = null;
    }

    private TimeSpan Elapsed(long since) => _time.GetElapsedTime(since);

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    /// <summary>Sends a command with the next id and returns the id.</summary>
    private async Task<int> SendAsync(JsonObject command)
    {
        int id = ++_nextId;
        command["id"] = id;
        await _socket!.SendAsync(command.ToJsonString(), _session);
        return id;
    }

    /// <summary>
    /// Subscribes to the shown entities when they differ from the subscribed ones (and drops what is no longer shown); with the
    /// same entities, the shown paths get their current values (a new attribute of a subscribed entity).
    /// </summary>
    private async Task SubscribeAsync(string? connectedTo)
    {
        string[] demand;
        lock (_gate)
            demand = _demand;
        var wanted = DemandEntities(demand);
        foreach (string path in _described.Keys.Where(p => !demand.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList())
            _described.Remove(path);

        if (_subscription != null && _subscribed.SetEquals(wanted))
        {
            Publish(_entities.Keys.ToList());
            return;
        }

        if (_subscription is { } old)
        {
            await SendAsync(new JsonObject { ["type"] = "unsubscribe_events", ["subscription"] = old });
            _subscription = null;
        }

        foreach (string id in _entities.Keys.Where(id => !wanted.Contains(id)).ToList())
        {
            _entities.Remove(id);
            _lastStates.TryRemove(id, out _);
        }

        _unexplained.IntersectWith(wanted);
        _subscribed = wanted;
        if (wanted.Count > 0)
        {
            var ids = new JsonArray();
            foreach (string id in wanted.Order(StringComparer.Ordinal))
                ids.Add(id);
            _subscription = await SendAsync(new JsonObject { ["type"] = "subscribe_entities", ["entity_ids"] = ids });
            _initialized = false;
        }

        string entities = wanted.Count == 1 ? "1 entity" : $"{wanted.Count} entities";
        _sink.Log(connectedTo != null
            ? $"Connected to Home Assistant {connectedTo} at {_address}: {entities} subscribed"
            : $"Home Assistant: {entities} subscribed");
    }

    /// <summary>Handles one message; returns why the connection is to be treated as lost, or null.</summary>
    private async Task<string?> HandleAsync(JsonElement message)
    {
        if (message.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in message.EnumerateArray())
            {
                if (await HandleAsync(item) is { } lost)
                    return lost;
            }

            return null;
        }

        int? id = message.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out int value) ? value : null;
        switch (TypeOf(message))
        {
            case "event" when id != null && id == _subscription && message.TryGetProperty("event", out var change):
                await ApplyAsync(change);
                break;
            case "result" when id != null:
                bool success = message.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
                if (id == _subscription && !success)
                    return $"Home Assistant refused the subscription: {ErrorOf(message)}";
                if (id == _listingRequest)
                    ListingArrived(message, success);
                else if (id == _configRequest && success && message.TryGetProperty("result", out var config))
                    _temperatureUnit = config.TryGetProperty("unit_system", out var units) ? Text(units, "temperature") : null;
                break;
            case "pong" when id != null && id == _pingId:
                _pingId = null;
                _lastPing = _time.GetTimestamp();
                break;
        }

        return null;
    }

    /// <summary>An event of the subscription: <c>a</c> full states (all of them first), <c>c</c> diffs, <c>r</c> removals.</summary>
    private async Task ApplyAsync(JsonElement change)
    {
        var touched = new List<string>();
        var gone = new List<string>();
        bool first = false;
        if (change.TryGetProperty("a", out var added) && added.ValueKind == JsonValueKind.Object)
        {
            foreach (var entity in added.EnumerateObject())
            {
                if (!_subscribed.Contains(entity.Name))
                    continue;
                _entities[entity.Name] = EntityState.FromCompressed(entity.Name, entity.Value);
                touched.Add(entity.Name);
            }

            first = !_initialized;
            _initialized = true;
        }

        if (change.TryGetProperty("c", out var changed) && changed.ValueKind == JsonValueKind.Object)
        {
            foreach (var entity in changed.EnumerateObject())
            {
                if (_entities.TryGetValue(entity.Name, out var state))
                {
                    state.Apply(entity.Value);
                    touched.Add(entity.Name);
                }
            }
        }

        if (change.TryGetProperty("r", out var removed) && removed.ValueKind == JsonValueKind.Array)
        {
            foreach (var entity in removed.EnumerateArray())
            {
                if (entity.ValueKind == JsonValueKind.String && _entities.Remove(entity.GetString()!))
                {
                    _lastStates.TryRemove(entity.GetString()!, out _);
                    gone.Add(entity.GetString()!);
                }
            }
        }

        // An entity Home Assistant does not have is silently left out of the first event.
        if (first)
            gone.AddRange(_subscribed.Where(id => !_entities.ContainsKey(id)));

        Publish(touched);
        if (gone.Count > 0)
            await ExplainAsync(gone);
    }

    /// <summary>Describes (when that changed) and publishes, in one batch, every shown path of the entities.</summary>
    private void Publish(List<string> entityIds)
    {
        if (entityIds.Count == 0)
            return;
        string[] demand;
        lock (_gate)
            demand = _demand;
        var byEntity = DemandByEntity(demand);
        var updates = new List<ReadingUpdate>();
        foreach (string entityId in entityIds.Distinct(StringComparer.Ordinal))
        {
            if (!_entities.TryGetValue(entityId, out var entity))
                continue;
            _lastStates[entityId] = entity.State;
            if (!byEntity.TryGetValue(entityId, out var paths))
                continue;
            foreach (var (path, attribute) in paths)
            {
                var description = EntityMapping.Describe(entity, attribute);
                if (!_described.TryGetValue(path, out var old) || old != description)
                {
                    _described[path] = description;
                    _sink.Describe(path, description);
                }

                if (EntityMapping.Value(entity, path, attribute, description) is { } update)
                    updates.Add(update);
            }
        }

        if (updates.Count > 0)
            _sink.PublishBatch(updates);
    }

    /// <summary>Says why the entities are missing, with suggestions from one listing per connection.</summary>
    private async Task ExplainAsync(IEnumerable<string> missing)
    {
        _unexplained.UnionWith(missing);
        if (_listing != null || _listingFailed)
            PublishMissing();
        else
            _listingRequest ??= await SendAsync(new JsonObject { ["type"] = "get_states" });
    }

    private void ListingArrived(JsonElement message, bool success)
    {
        _listingRequest = null;
        if (success && message.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
        {
            _listing = result.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("entity_id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null)
                .OfType<string>()
                .ToList();
        }
        else
        {
            _listingFailed = true;
        }

        PublishMissing();
    }

    private void PublishMissing()
    {
        string[] demand;
        lock (_gate)
            demand = _demand;
        var byEntity = DemandByEntity(demand);
        var updates = new List<ReadingUpdate>();
        foreach (string entityId in _unexplained)
        {
            if (_entities.ContainsKey(entityId) || !byEntity.TryGetValue(entityId, out var paths))
                continue;
            string reason = Suggestions.Explain(entityId, _listing);
            updates.AddRange(paths.Select(p => ReadingUpdate.Missing(p.Path, reason)));
        }

        _unexplained.Clear();
        if (updates.Count > 0)
            _sink.PublishBatch(updates);
    }

    private void Reject(string cause)
    {
        string[] demand;
        lock (_gate)
        {
            _fatal = RejectedReason;
            demand = _demand;
        }

        _phase = ConnectionPhase.Rejected;
        _sink.Log($"Home Assistant at {_address} rejected the token ({cause}); no more attempts until the settings change");
        if (demand.Length > 0)
            _sink.PublishBatch(demand.Select(p => ReadingUpdate.Missing(p, RejectedReason)).ToList());
    }

    /// <summary>The entity of a path: the part before the first <c>/</c>, in lower case as Home Assistant writes ids.</summary>
    internal static string EntityOf(string path)
    {
        int slash = path.IndexOf('/', StringComparison.Ordinal);
        return (slash < 0 ? path : path[..slash]).ToLowerInvariant();
    }

    private static HashSet<string> DemandEntities(IEnumerable<string> demand) =>
        new(demand.Select(EntityOf).Where(e => e.Length > 0), StringComparer.Ordinal);

    /// <summary>The shown paths by entity, each with its attribute (null for the entity's state).</summary>
    private static Dictionary<string, List<(string Path, string? Attribute)>> DemandByEntity(IEnumerable<string> demand)
    {
        var map = new Dictionary<string, List<(string, string?)>>(StringComparer.Ordinal);
        foreach (string path in demand)
        {
            int slash = path.IndexOf('/', StringComparison.Ordinal);
            string entity = EntityOf(path);
            if (!map.TryGetValue(entity, out var list))
                map[entity] = list = [];
            list.Add((path, slash < 0 ? null : path[(slash + 1)..]));
        }

        return map;
    }

    private static string? TypeOf(JsonElement? message) => message is { } m ? Text(m, "type") : null;

    private static string? Text(JsonElement? element, string property) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string ErrorOf(JsonElement message) =>
        message.TryGetProperty("error", out var error) ? Text(error, "message") ?? error.GetRawText() : "no reason given";
}
