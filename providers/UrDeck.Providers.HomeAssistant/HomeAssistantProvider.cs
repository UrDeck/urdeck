// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.HomeAssistant;

/// <summary>
/// The states of a Home Assistant installation, and the services to change them (provider-homeassistant spec). A reading is an
/// entity id (<c>sensor.office_temperature</c>) or an entity id and one of its attributes (<c>light.desk/brightness</c>); states
/// arrive over Home Assistant's WebSocket for the shown entities only (see <see cref="HomeAssistantConnection"/>), so
/// <see cref="SampleAsync"/> does nothing. An action (<c>light.desk/toggle</c>) is one REST request, whether or not the socket is
/// open. The provider talks to the configured address only and sends the token nowhere else.
/// </summary>
[DataProvider("homeassistant", "Home Assistant", DefaultIntervalMs = 60000, MinIntervalMs = 250, LingerMs = 30000,
    MissingSettingMessage = "set up Home Assistant in Settings")]
public sealed class HomeAssistantProvider : DataProvider<HomeAssistantSettings>, IActionProvider, IReadingBrowser, IDisposable
{
    private readonly IHomeAssistantClients _clients;
    private readonly TimeProvider _time;
    private readonly Func<double> _random;
    private readonly object _gate = new();
    private IReadingSink? _sink;
    private HomeAssistantConnection? _connection;
    private IHomeAssistantRest? _rest;
    private string? _settingsError;
    private string? _temperatureUnit;

    public HomeAssistantProvider()
        : this(HomeAssistantClients.Default, TimeProvider.System, Random.Shared.NextDouble)
    {
    }

    internal HomeAssistantProvider(IHomeAssistantClients clients, TimeProvider time, Func<double> random)
    {
        _clients = clients;
        _time = time;
        _random = random;
    }

    internal HomeAssistantConnection? Connection => _connection;

    public override IReadOnlyList<ReadingDescriptor> Describe() =>
    [
        new("{entity}", ReadingKind.Text, "{entity}", "An entity's state (described when Home Assistant reports it)") { Icon = EntityIcons.Generic },
        new("{entity}/{attribute}", ReadingKind.Text, "{entity} {attribute}", "An attribute of an entity (described when Home Assistant reports it)"),
    ];

    public IReadOnlyList<ActionDescriptor> DescribeActions() =>
    [
        new("{entity}/toggle", "Toggle", "The entity's natural action: toggle, run a scene or script, press a button, play or pause, lock or unlock"),
        new("{entity}/turn_on", "Turn on", "Turn the entity on; a light takes its brightness in percent", TakesArgument: true),
        new("{entity}/turn_off", "Turn off", "Turn the entity off"),
        new("{entity}/{service}", "Service", "Call any service of the entity's domain; the argument fills the service's main field", TakesArgument: true),
    ];

    public override void Start(IReadingSink sink, HomeAssistantSettings settings)
    {
        _sink = sink;
        var address = settings.Address(out string? error);
        string token = settings.Token?.Reveal() ?? "";
        if (address == null || token.Length == 0)
        {
            _settingsError = error ?? "set up Home Assistant in Settings";
            sink.Log(_settingsError);
            return;
        }

        lock (_gate)
            _rest = _clients.CreateRest(address, token);
        _connection = new HomeAssistantConnection(address, token, _clients, sink, _time, _random);
        _connection.Start();
    }

    public override void SetDemand(IReadOnlyCollection<string> paths)
    {
        if (_settingsError != null)
        {
            _sink?.PublishBatch(paths.Select(p => ReadingUpdate.Missing(p, _settingsError)).ToList());
            return;
        }

        _connection?.SetDemand(paths);
    }

    public override Task SampleAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override void Shutdown()
    {
        var connection = _connection;
        _connection = null;
        if (connection != null)
        {
            try
            {
                connection.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(3));
            }
            catch (AggregateException)
            {
                // The loop reports its own failures.
            }
        }

        lock (_gate)
        {
            _rest?.Dispose();
            _rest = null;
        }
    }

    public void Dispose() => Shutdown();

    public async Task<ActionResult> InvokeAsync(string path, ReadingValue? argument, CancellationToken cancellationToken)
    {
        if (_settingsError != null)
            return ActionResult.Failed(_settingsError);
        IHomeAssistantRest? rest;
        lock (_gate)
            rest = _rest;
        if (rest == null)
            return ActionResult.Failed("Home Assistant is not set up");
        if (!ServiceCalls.TryParse(path, out string entityId, out string asked, out string? error))
            return ActionResult.Failed(error!);

        string domain = EntityState.DomainOf(entityId);
        string? service = ServiceCalls.Resolve(domain, asked, _connection?.LastState(entityId), out error);
        if (service == null)
            return ActionResult.Failed(error!);

        string? unit = null;
        if (domain == "climate" && service == "set_temperature" && argument != null)
            unit = await TemperatureUnitAsync(rest, cancellationToken);

        var call = ServiceCalls.Build(entityId, service, argument, unit);
        RestAnswer answer;
        try
        {
            answer = await rest.PostAsync(call.Path, call.Body, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return ActionResult.Failed($"Home Assistant did not answer: {ex.GetBaseException().Message}");
        }

        return answer.Status switch
        {
            >= 200 and < 300 => ActionResult.Done,
            401 or 403 => ActionResult.Failed("token rejected"),
            400 or 404 => ActionResult.Failed($"Home Assistant refused {call.Domain}.{call.Service} on {call.EntityId} (unknown service, or the entity does not support it)"),
            _ => ActionResult.Failed(string.Create(CultureInfo.InvariantCulture, $"Home Assistant answered {answer.Status} to {call.Domain}.{call.Service} on {call.EntityId}")),
        };
    }

    public async Task<IReadOnlyList<ReadingDescriptor>> BrowseAsync(CancellationToken cancellationToken)
    {
        if (_settingsError != null)
            throw new InvalidOperationException(_settingsError);
        IHomeAssistantRest? rest;
        lock (_gate)
            rest = _rest;
        if (rest == null)
            throw new InvalidOperationException("Home Assistant is not set up");
        var states = await StatesAsync(rest, cancellationToken);
        return states.Select(s => EntityMapping.Describe(s)).OrderBy(d => d.Path, StringComparer.Ordinal).ToList();
    }

    public override bool OffersConnectionTest => true;

    public override async Task<string> ValidateAsync(HomeAssistantSettings settings, CancellationToken cancellationToken)
    {
        var address = settings.Address(out string? error) ?? throw new InvalidOperationException(error);
        string token = settings.Token?.Reveal() ?? "";
        if (token.Length == 0)
            throw new InvalidOperationException("the token is missing");

        using var rest = _clients.CreateRest(address, token);
        RestAnswer config;
        try
        {
            config = await rest.GetAsync("api/config", cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new InvalidOperationException($"cannot reach {address}: {ex.GetBaseException().Message}", ex);
        }

        if (config.Status is 401 or 403)
            throw new InvalidOperationException("Home Assistant rejected the token");
        string? version = config.Status == 200 ? VersionOf(config.Body) : null;
        if (version == null)
            throw new InvalidOperationException($"{address} does not answer like Home Assistant (status {config.Status})");

        var states = await StatesAsync(rest, cancellationToken);
        return string.Create(CultureInfo.InvariantCulture, $"Connected to Home Assistant {version} · {states.Count:N0} entities");
    }

    /// <summary>Every entity, from one <c>GET /api/states</c>.</summary>
    private static async Task<List<EntityState>> StatesAsync(IHomeAssistantRest rest, CancellationToken cancellationToken)
    {
        RestAnswer answer;
        try
        {
            answer = await rest.GetAsync("api/states", cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new InvalidOperationException($"Home Assistant did not answer: {ex.GetBaseException().Message}", ex);
        }

        if (answer.Status is 401 or 403)
            throw new InvalidOperationException("Home Assistant rejected the token");
        if (answer.Status != 200)
            throw new InvalidOperationException($"Home Assistant answered {answer.Status} to the list of entities");
        try
        {
            using var document = JsonDocument.Parse(answer.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Home Assistant's list of entities is not a list");
            return document.RootElement.EnumerateArray().Select(EntityState.FromFull).OfType<EntityState>().ToList();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Home Assistant's list of entities cannot be read: {ex.Message}", ex);
        }
    }

    /// <summary>The installation's temperature unit: what the socket learned, else one <c>GET /api/config</c> for the run.</summary>
    private async Task<string?> TemperatureUnitAsync(IHomeAssistantRest rest, CancellationToken cancellationToken)
    {
        if (_connection?.TemperatureUnit is { } known)
            return known;
        if (_temperatureUnit != null)
            return _temperatureUnit;
        try
        {
            var answer = await rest.GetAsync("api/config", cancellationToken);
            if (answer.Status != 200)
                return null;
            using var document = JsonDocument.Parse(answer.Body);
            if (document.RootElement.TryGetProperty("unit_system", out var units) && units.TryGetProperty("temperature", out var unit)
                && unit.ValueKind == JsonValueKind.String)
                _temperatureUnit = unit.GetString();
            return _temperatureUnit;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException)
        {
            return null;
        }
    }

    private static string? VersionOf(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("version", out var version)
                && version.ValueKind == JsonValueKind.String
                ? version.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
