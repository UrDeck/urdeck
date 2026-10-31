// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Tests.Support;
using UrDeck.Providers.HomeAssistant;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The Home Assistant provider against a scripted Home Assistant and a fake clock (provider-homeassistant spec). The entities are
/// made up; the message shapes are those of Home Assistant 2026.10.
/// </summary>
public sealed class HomeAssistantProviderTests : IDisposable
{
    private const string Token = "fake-token-6f1c";

    private readonly FakeTime _time = new();
    private readonly FakeHomeAssistant _ha = new();
    private readonly RecordingSink _sink = new();
    private readonly List<HomeAssistantProvider> _providers = [];
    private double _random = 0.5;

    public HomeAssistantProviderTests()
    {
        _ha.Set("light.desk", "on", new JsonObject { ["friendly_name"] = "Desk lamp", ["brightness"] = 128, ["color_mode"] = "brightness" });
        _ha.Set("light.desk_strip", "off", new JsonObject { ["friendly_name"] = "Desk strip", ["icon"] = "mdi:led-strip-variant", ["brightness"] = null });
        _ha.Set("sensor.office_temperature", "72", new JsonObject
        {
            ["friendly_name"] = "Office temperature",
            ["device_class"] = "temperature",
            ["unit_of_measurement"] = "°F",
            ["state_class"] = "measurement",
        });
        _ha.Set("sensor.basement_temperature", "18.5", new JsonObject
        {
            ["friendly_name"] = "Basement temperature",
            ["device_class"] = "temperature",
            ["unit_of_measurement"] = "°C",
        });
        _ha.Set("sensor.desk_power", "431", new JsonObject { ["friendly_name"] = "Desk power", ["device_class"] = "power", ["unit_of_measurement"] = "W" });
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
            provider.Shutdown();
    }

    private static HomeAssistantSettings Settings(string? url = "http://ha.test:8123") =>
        new() { Url = url, Token = new SecretValue(Token) };

    private HomeAssistantProvider Provider()
    {
        var provider = new HomeAssistantProvider(_ha, _time, () => _random);
        _providers.Add(provider);
        return provider;
    }

    private static HomeAssistantConnection Connection(HomeAssistantProvider provider) => provider.Connection!;

    /// <summary>Waits until the loop has reacted to what the test just did and is parked again, with nothing left to read.</summary>
    private void Quiet(HomeAssistantProvider provider, int before)
    {
        var connection = Connection(provider);
        Assert.True(
            HubTestExtensions.WaitFor(() => connection.Waits > before && connection.IsWaiting && _ha.Pending == 0),
            $"the connection did not settle (phase {connection.Phase})");
    }

    private void Demand(HomeAssistantProvider provider, params string[] paths)
    {
        int before = Connection(provider).Waits;
        provider.SetDemand(paths);
        Quiet(provider, before);
    }

    private void Advance(HomeAssistantProvider provider, TimeSpan by)
    {
        int before = Connection(provider).Waits;
        _time.Advance(by);
        Quiet(provider, before);
    }

    private void Push(HomeAssistantProvider provider, Action change)
    {
        int before = Connection(provider).Waits;
        change();
        Quiet(provider, before);
    }

    private HomeAssistantProvider Shown(params string[] paths)
    {
        var provider = Provider();
        provider.Start(_sink, Settings());
        Demand(provider, paths);
        return provider;
    }

    private double Number(string path)
    {
        var update = _sink.Latest(path);
        Assert.NotNull(update);
        Assert.True(update.Value.Value.HasValue, $"{path} is unavailable: {update.Value.Reason}");
        return update.Value.Value!.Value.Number;
    }

    private string? Reason(string path) => _sink.Latest(path)?.Reason;

    // Settings

    [Fact]
    public void NotSetUp_ReadingsSayHowToSetItUp_AndNothingIsContacted()
    {
        string log = Path.Combine(Path.GetTempPath(), $"urdeck-ha-{Guid.NewGuid():N}.log");
        UrDeckLog.LogPath = log;
        var registry = new ProviderRegistry();
        var descriptor = ProviderDescriptor.TryCreate(typeof(HomeAssistantProvider), out _)! with { Factory = Provider };
        registry.TryRegister(descriptor, out _);
        using (var hub = new ReadingHub(registry, _time, regionUsesFahrenheit: false))
        {
            hub.Subscribe(["homeassistant:sensor.office_temperature"]);

            Assert.True(HubTestExtensions.WaitFor(() => hub.Read("homeassistant:sensor.office_temperature").State == ReadingState.Unavailable));
            Assert.Equal("set up Home Assistant in Settings", hub.Read("homeassistant:sensor.office_temperature").Reason);
        }

        Assert.Empty(_ha.Sockets);
        Assert.Empty(_ha.RestClients);
        File.Delete(log);
    }

    [Fact]
    public void AnAddressThatIsNotHttp_IsExplainedOnEveryReading()
    {
        var provider = Provider();
        provider.Start(_sink, Settings("homeassistant.local:8123"));
        provider.SetDemand(["light.desk"]);

        Assert.Contains("is not an http or https address", Reason("light.desk"));
        Assert.Empty(_ha.Sockets);
    }

    [Fact]
    public void TheSettings_DeclareTheAddressAndATokenSecretForIt()
    {
        var schema = ProviderDescriptor.TryCreate(typeof(HomeAssistantProvider), out string? why)!.Schema;
        Assert.Null(why);

        var url = schema.Settings.Single(s => s.Key == "url");
        var token = schema.Settings.Single(s => s.Key == "token");
        Assert.True(url.Required);
        Assert.False(url.IsSecret);
        Assert.True(token.Required);
        Assert.True(token.IsSecret);
        Assert.Equal("url", token.ContextKey);
    }

    [Fact]
    public async Task TestConnection_NamesTheVersionAndTheNumberOfEntities()
    {
        string result = await Provider().ValidateAsync(Settings(), TestContext.Current.CancellationToken);

        Assert.Equal("Connected to Home Assistant 2026.10.3 · 5 entities", result);
        Assert.Equal(new Uri("http://ha.test:8123"), _ha.RestClients.Single().Address);
        Assert.Equal(Token, _ha.RestClients.Single().Token);
    }

    [Fact]
    public async Task TestConnection_SaysWhyItFailed()
    {
        var provider = Provider();
        _ha.Rest.Respond = (_, _, _) => new RestAnswer(401, "401: Unauthorized");
        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ValidateAsync(Settings(), TestContext.Current.CancellationToken));
        Assert.Equal("Home Assistant rejected the token", rejected.Message);

        _ha.Rest.Respond = (_, _, _) => new RestAnswer(200, "<html>a router</html>");
        var notHa = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ValidateAsync(Settings(), TestContext.Current.CancellationToken));
        Assert.Contains("does not answer like Home Assistant", notHa.Message);

        _ha.Rest.Respond = (_, _, _) => throw new HttpRequestException("No such host is known.");
        var unreachable = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ValidateAsync(Settings(), TestContext.Current.CancellationToken));
        Assert.Equal("cannot reach http://ha.test:8123/: No such host is known.", unreachable.Message);
    }

    // Readings, kinds and units

    [Fact]
    public void AnAttribute_IsReadAsAPercentage()
    {
        Shown("light.desk/brightness");

        Assert.Equal(50.2, Number("light.desk/brightness"), 1);
        var described = _sink.Described("light.desk/brightness")!;
        Assert.Equal(ReadingKind.Percent, described.Kind);
        Assert.Equal("light.desk/turn_on", described.Action);
        Assert.Equal(0, described.Min);
        Assert.Equal(100, described.Max);
    }

    [Fact]
    public void ALightThatIsOff_IsAtBrightnessZero()
    {
        Shown("light.desk_strip/brightness");

        Assert.Equal(0, Number("light.desk_strip/brightness"));
    }

    [Fact]
    public void AFahrenheitTemperature_IsConvertedToCelsius()
    {
        Shown("sensor.office_temperature");

        Assert.Equal(22.22, Number("sensor.office_temperature"), 2);
        var described = _sink.Described("sensor.office_temperature")!;
        Assert.Equal(ReadingKind.Temperature, described.Kind);
        Assert.Equal("Office temperature", described.Label);
        Assert.Equal("sensor.office_temperature", described.Name);
        Assert.Null(described.Device);
        Assert.Null(described.Min);

        using var celsius = new ReadingHub(new ProviderRegistry(), _time, regionUsesFahrenheit: false);
        var formatted = ReadingFormatter.Format(Reading.Ok(Number("sensor.office_temperature")), described, new ReadingFormatOptions { Decimals = 1 }, celsius);
        Assert.Equal("22.2", formatted.Value);
    }

    [Fact]
    public void MixedUnitsInOneInstallation_AreBothCelsius()
    {
        Shown("sensor.office_temperature", "sensor.basement_temperature");

        Assert.Equal(22.22, Number("sensor.office_temperature"), 2);
        Assert.Equal(18.5, Number("sensor.basement_temperature"), 2);
    }

    [Fact]
    public void Power_IsAPlainNumberWithItsUnit()
    {
        Shown("sensor.desk_power");

        Assert.Equal(431, Number("sensor.desk_power"));
        var described = _sink.Described("sensor.desk_power")!;
        Assert.Equal(ReadingKind.Number, described.Kind);
        Assert.Equal("W", described.Unit);
        Assert.Equal("home/bolt", described.Icon);
        Assert.Null(described.Action);
    }

    [Fact]
    public void EveryKindOfTheTable_IsMapped()
    {
        _ha.Set("sensor.wind", "10", new JsonObject { ["device_class"] = "wind_speed", ["unit_of_measurement"] = "mph" });
        _ha.Set("sensor.gust", "5", new JsonObject { ["device_class"] = "speed", ["unit_of_measurement"] = "m/s" });
        _ha.Set("sensor.rain", "0.5", new JsonObject { ["device_class"] = "precipitation", ["unit_of_measurement"] = "in" });
        _ha.Set("sensor.snow", "3", new JsonObject { ["device_class"] = "precipitation", ["unit_of_measurement"] = "cm" });
        _ha.Set("sensor.phone_battery", "82", new JsonObject { ["device_class"] = "battery", ["unit_of_measurement"] = "%" });
        _ha.Set("sensor.cpu", "12", new JsonObject { ["unit_of_measurement"] = "%" });
        _ha.Set("sensor.last_backup", "2026-10-13T03:15:00+00:00", new JsonObject { ["device_class"] = "timestamp" });
        _ha.Set("binary_sensor.back_door", "on", new JsonObject { ["device_class"] = "door" });
        _ha.Set("sensor.washer", "Rinsing", []);
        _ha.Set("input_number.target", "40", new JsonObject { ["unit_of_measurement"] = "°", ["min"] = 10, ["max"] = 90 });
        Shown("sensor.wind", "sensor.gust", "sensor.rain", "sensor.snow", "sensor.phone_battery", "sensor.cpu", "sensor.last_backup",
            "binary_sensor.back_door", "sensor.washer", "input_number.target");

        Assert.Equal(16.09, Number("sensor.wind"), 2);
        Assert.Equal(ReadingKind.Speed, _sink.Described("sensor.wind")!.Kind);
        Assert.Equal(18, Number("sensor.gust"), 2);
        Assert.Equal(12.7, Number("sensor.rain"), 2);
        Assert.Equal(DepthUnit.Millimetre, _sink.Described("sensor.rain")!.DepthUnit);
        Assert.Equal(3, Number("sensor.snow"));
        Assert.Equal(DepthUnit.Centimetre, _sink.Described("sensor.snow")!.DepthUnit);
        Assert.Equal(ReadingKind.Percent, _sink.Described("sensor.phone_battery")!.Kind);
        Assert.Equal("home/battery", _sink.Described("sensor.phone_battery")!.Icon);
        Assert.Equal(ReadingKind.Percent, _sink.Described("sensor.cpu")!.Kind);
        Assert.Equal(100, _sink.Described("sensor.cpu")!.Max);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 3, 15, 0, TimeSpan.Zero), _sink.Latest("sensor.last_backup")!.Value.Value!.Value.Time);
        Assert.True(_sink.Latest("binary_sensor.back_door")!.Value.Value!.Value.IsOn);
        Assert.Equal("home/door", _sink.Described("binary_sensor.back_door")!.Icon);
        Assert.Equal("Rinsing", _sink.Latest("sensor.washer")!.Value.Value!.Value.Text);
        Assert.Equal(ReadingKind.Text, _sink.Described("sensor.washer")!.Kind);
        Assert.Equal("sensor.washer", _sink.Described("sensor.washer")!.Label);
        Assert.Equal(10, _sink.Described("input_number.target")!.Min);
        Assert.Equal(90, _sink.Described("input_number.target")!.Max);
    }

    [Fact]
    public void AStateThatDoesNotFitItsKind_IsUnavailableWithTheState()
    {
        var provider = Shown("sensor.office_temperature");

        Push(provider, () => _ha.Set("sensor.office_temperature", "abc"));

        Assert.Equal("Home Assistant reported 'abc', which is not a temperature", Reason("sensor.office_temperature"));
    }

    [Fact]
    public void AChangedUnit_DescribesTheReadingAgain()
    {
        var provider = Shown("sensor.office_temperature");

        Push(provider, () => _ha.Set("sensor.office_temperature", "21", new JsonObject
        {
            ["friendly_name"] = "Office temperature",
            ["device_class"] = "temperature",
            ["unit_of_measurement"] = "°C",
        }));
        Assert.Equal(21, Number("sensor.office_temperature"));

        Push(provider, () => _ha.Set("sensor.office_temperature", "21", new JsonObject { ["friendly_name"] = "Office", ["unit_of_measurement"] = "lx" }));
        Assert.Equal(ReadingKind.Number, _sink.Described("sensor.office_temperature")!.Kind);
        Assert.Equal("lx", _sink.Described("sensor.office_temperature")!.Unit);
        Assert.Equal("Office", _sink.Described("sensor.office_temperature")!.Label);
    }

    // The connection follows demand

    [Fact]
    public void TheFirstSubscription_SignsInAndSubscribesToExactlyTheShownEntities()
    {
        Shown("light.desk/brightness", "sensor.desk_power", "light.desk");

        var socket = _ha.Current!;
        Assert.Equal(new Uri("ws://ha.test:8123/api/websocket"), socket.Address);
        Assert.Equal(Token, (string?)socket.SentOfType("auth").Single()["access_token"]);
        var subscribe = socket.SentOfType("subscribe_entities").Single();
        Assert.Equal(["light.desk", "sensor.desk_power"], subscribe["entity_ids"]!.AsArray().Select(n => (string)n!));
        Assert.Equal(ConnectionPhase.Subscribed, Connection(_providers[0]).Phase);
    }

    [Fact]
    public void TheConnectedLine_NamesTheAddressTheVersionAndTheEntities_AndNeverTheToken()
    {
        _ha.Set("switch.coffee", "off", []);
        Shown("light.desk", "sensor.desk_power", "sensor.office_temperature", "switch.coffee");

        string connected = _sink.Logs.Single(l => l.StartsWith("Connected", StringComparison.Ordinal));
        Assert.Equal("Connected to Home Assistant 2026.10.3 at http://ha.test:8123/: 4 entities subscribed", connected);
        Assert.DoesNotContain(_sink.Logs, l => l.Contains(Token, StringComparison.Ordinal));
    }

    [Fact]
    public void ChangesArrive_AsOneBatchPerEvent()
    {
        var provider = Shown("light.desk", "light.desk/brightness");
        int batches = _sink.BatchSizes.Count;

        Push(provider, () => _ha.Set("light.desk", "off", new JsonObject { ["friendly_name"] = "Desk lamp", ["brightness"] = null }));

        Assert.Equal([2], _sink.BatchSizes.Skip(batches));
        Assert.False(_sink.Latest("light.desk")!.Value.Value!.Value.IsOn);
        Assert.Equal(0, Number("light.desk/brightness"));
    }

    [Fact]
    public void ABurstOfDemandChanges_IsOneResubscribe_HalfASecondAtMost()
    {
        var provider = Shown("light.desk");
        var socket = _ha.Current!;

        provider.SetDemand(["light.desk", "sensor.desk_power"]);
        provider.SetDemand(["sensor.desk_power"]);
        Demand(provider, "sensor.desk_power", "sensor.office_temperature");
        _time.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Single(socket.SentOfType("subscribe_entities"));

        Advance(provider, TimeSpan.FromMilliseconds(50));

        var subscribes = socket.SentOfType("subscribe_entities");
        Assert.Equal(2, subscribes.Count);
        Assert.Equal(["sensor.desk_power", "sensor.office_temperature"], subscribes[1]["entity_ids"]!.AsArray().Select(n => (string)n!));
        Assert.Single(socket.SentOfType("unsubscribe_events"));
        Assert.Equal(431, Number("sensor.desk_power"));
        Assert.Equal(1, _ha.Connects);
    }

    [Fact]
    public void ANewAttributeOfASubscribedEntity_GetsItsValueWithoutAResubscribe()
    {
        var provider = Shown("light.desk");

        Demand(provider, "light.desk", "light.desk/color_mode");
        Advance(provider, TimeSpan.FromMilliseconds(250));

        Assert.Single(_ha.Current!.SentOfType("subscribe_entities"));
        Assert.Equal("brightness", _sink.Latest("light.desk/color_mode")!.Value.Value!.Value.Text);
    }

    [Fact]
    public void ChangesToEntitiesNobodyShows_AreNeverReceived()
    {
        var provider = Shown("light.desk");
        int batches = _sink.BatchSizes.Count;

        _ha.Set("sensor.desk_power", "500");
        Push(provider, () => _ha.Set("light.desk", "off"));

        Assert.Equal(1, _sink.BatchSizes.Count - batches);
        Assert.Null(_sink.Latest("sensor.desk_power"));
    }

    [Fact]
    public void WithNothingShownFor30Seconds_TheConnectionIsClosed()
    {
        var provider = Shown("light.desk");

        Demand(provider);
        Advance(provider, TimeSpan.FromMilliseconds(250));
        Assert.False(_ha.Current!.Disposed);
        Advance(provider, TimeSpan.FromSeconds(30));

        Assert.True(_ha.Current!.Disposed);
        Assert.Equal(ConnectionPhase.Idle, Connection(provider).Phase);
        Assert.Contains("Home Assistant: nothing shown for 30 s; connection closed", _sink.Logs);

        // A reading shown again opens a new connection.
        Demand(provider, "light.desk");
        Assert.Equal(2, _ha.Connects);
    }

    [Fact]
    public void APageFlipAndBackWithin30Seconds_KeepsTheConnection()
    {
        var provider = Shown("light.desk");

        Demand(provider);
        Advance(provider, TimeSpan.FromMilliseconds(250));
        _time.Advance(TimeSpan.FromSeconds(20));
        Demand(provider, "light.desk");
        Advance(provider, TimeSpan.FromMilliseconds(250));

        Assert.Equal(1, _ha.Connects);
        Assert.False(_ha.Current!.Disposed);
        Assert.True(_sink.Latest("light.desk")!.Value.Value!.Value.IsOn);
    }

    [Fact]
    public void TheHubKeepsTheProviderFor30Seconds_SoAPageFlipDoesNotRestartIt()
    {
        var descriptor = ProviderDescriptor.TryCreate(typeof(HomeAssistantProvider), out _)!;

        Assert.Equal(30_000, descriptor.LingerMs);
        Assert.True(descriptor.OffersActions);
        Assert.True(descriptor.OffersBrowsing);
    }

    // Outages and reconnecting

    [Fact]
    public void AWiFiBlip_MakesReadingsStale_RetriesAndRecovers()
    {
        var provider = Shown("sensor.desk_power");

        Push(provider, _ha.Drop);
        Assert.Equal(ConnectionPhase.Backoff, Connection(provider).Phase);
        Assert.StartsWith("connection to Home Assistant lost: ", _sink.Interruptions.Single());
        Assert.Contains(_sink.Logs, l => l.StartsWith("Home Assistant: attempt 1 in 1 s (", StringComparison.Ordinal));

        _ha.ConnectError = new System.Net.WebSockets.WebSocketException("Unable to connect to the remote server");
        Advance(provider, TimeSpan.FromSeconds(1));
        Assert.Contains(_sink.Logs, l => l == "Home Assistant: attempt 2 in 2 s (Unable to connect to the remote server)");

        _ha.ConnectError = null;
        _ha.Set("sensor.desk_power", "377");
        Advance(provider, TimeSpan.FromSeconds(2));

        Assert.Equal(ConnectionPhase.Subscribed, Connection(provider).Phase);
        Assert.Contains("Home Assistant: reconnected after 2 attempts", _sink.Logs);
        Assert.Equal(377, Number("sensor.desk_power"));
    }

    [Fact]
    public void TheDelays_Grow1_2_5_10_30_60_ThenStay_AndStartAgainAfterASuccess()
    {
        _ha.ConnectError = new System.Net.WebSockets.WebSocketException("refused");
        var provider = Provider();
        provider.Start(_sink, Settings());
        Demand(provider, "light.desk");
        foreach (int seconds in new[] { 1, 2, 5, 10, 30, 60 })
            Advance(provider, TimeSpan.FromSeconds(seconds));

        var attempts = _sink.Logs.Where(l => l.StartsWith("Home Assistant: attempt", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            ["attempt 1 in 1 s", "attempt 2 in 2 s", "attempt 3 in 5 s", "attempt 4 in 10 s", "attempt 5 in 30 s", "attempt 6 in 60 s", "attempt 7 in 60 s"],
            attempts.Select(l => l["Home Assistant: ".Length..l.IndexOf(" (", StringComparison.Ordinal)]));
        Assert.StartsWith("cannot connect to Home Assistant at http://ha.test:8123/: refused", _sink.Interruptions[0]);

        _ha.ConnectError = null;
        Advance(provider, TimeSpan.FromSeconds(60));
        Assert.Contains("Home Assistant: reconnected after 7 attempts", _sink.Logs);

        Push(provider, _ha.Drop);
        Assert.StartsWith("Home Assistant: attempt 1 in 1 s", _sink.Logs[^1]);
    }

    [Theory]
    [InlineData(0.0, 0.8)]
    [InlineData(0.5, 1.0)]
    [InlineData(1.0, 1.2)]
    public void TheJitter_StaysWithin20Percent(double random, double factor)
    {
        _random = random;
        var provider = Provider();
        provider.Start(_sink, Settings());
        var connection = Connection(provider);

        for (int attempt = 1; attempt <= 8; attempt++)
        {
            var table = HomeAssistantConnection.Delays[Math.Min(attempt, 6) - 1];
            Assert.Equal(table.TotalMilliseconds * factor, connection.DelayFor(attempt).TotalMilliseconds, 3);
        }
    }

    [Fact]
    public void APingWithoutAnAnswerWithin10Seconds_IsALostConnection()
    {
        var provider = Shown("light.desk");
        _ha.AnswerPings = false;

        Advance(provider, TimeSpan.FromSeconds(30));
        Assert.Single(_ha.Current!.SentOfType("ping"));
        Assert.Equal(ConnectionPhase.Subscribed, Connection(provider).Phase);

        Advance(provider, TimeSpan.FromSeconds(10));
        Assert.Equal(ConnectionPhase.Backoff, Connection(provider).Phase);
        Assert.Contains(_sink.Logs, l => l == "Home Assistant: attempt 1 in 1 s (no answer to a ping within 10 s)");
    }

    [Fact]
    public void AnsweredPings_KeepTheConnection()
    {
        var provider = Shown("light.desk");

        Advance(provider, TimeSpan.FromSeconds(30));
        Advance(provider, TimeSpan.FromSeconds(30));
        Advance(provider, TimeSpan.FromSeconds(30));

        Assert.Equal(3, _ha.Current!.SentOfType("ping").Count);
        Assert.Equal(1, _ha.Connects);
        Assert.Equal(ConnectionPhase.Subscribed, Connection(provider).Phase);
    }

    [Fact]
    public void ARevokedToken_MakesReadingsUnavailable_AndNothingIsTriedAgain()
    {
        var provider = Shown("light.desk");
        _ha.RejectToken = true;
        Push(provider, _ha.Drop);

        // The loop ends at the rejection, so it never parks again: wait for the phase instead.
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(HubTestExtensions.WaitFor(() => Connection(provider).Phase == ConnectionPhase.Rejected));
        Assert.Equal(HomeAssistantConnection.RejectedReason, Reason("light.desk"));
        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(2, _ha.Sockets.Count);
        Assert.Single(_sink.Logs, l => l.Contains("rejected the token", StringComparison.Ordinal));

        // A reading shown later says the same.
        provider.SetDemand(["light.desk", "sensor.desk_power"]);
        Assert.Equal(HomeAssistantConnection.RejectedReason, Reason("sensor.desk_power"));
    }

    // Entity states and reasons

    [Fact]
    public void Unavailable_IsUnavailableInHomeAssistant()
    {
        _ha.Set("sensor.garage", "unavailable", new JsonObject { ["friendly_name"] = "Garage" });
        Shown("sensor.garage", "sensor.garage/friendly_name");

        Assert.Equal("unavailable in Home Assistant", Reason("sensor.garage"));
        Assert.Equal("unavailable in Home Assistant", Reason("sensor.garage/friendly_name"));
    }

    [Fact]
    public void Unknown_PublishesNothing_SoTheReadingStaysPendingOrKeepsItsValue()
    {
        _ha.Set("sensor.boot", "unknown", []);
        var provider = Shown("sensor.boot", "sensor.desk_power");
        Assert.Null(_sink.Latest("sensor.boot"));

        Push(provider, () => _ha.Set("sensor.desk_power", "unknown"));
        Assert.Equal(431, Number("sensor.desk_power"));
    }

    [Fact]
    public void ATypo_IsUnavailableWithASuggestion()
    {
        Shown("sensor.ofice_temperature", "light.desk");

        Assert.Equal(
            "no entity 'sensor.ofice_temperature' in Home Assistant; did you mean sensor.office_temperature?",
            Reason("sensor.ofice_temperature"));
        Assert.True(_sink.Latest("light.desk")!.Value.Value!.Value.IsOn);
    }

    [Fact]
    public void MissingEntities_AreExplainedFromOneListingPerConnection()
    {
        var provider = Shown("sensor.nothing_like_it");
        Assert.Equal("no entity 'sensor.nothing_like_it' in Home Assistant", Reason("sensor.nothing_like_it"));

        Demand(provider, "sensor.nothing_like_it", "light.dsk");
        Advance(provider, TimeSpan.FromMilliseconds(250));

        Assert.Equal("no entity 'light.dsk' in Home Assistant; did you mean light.desk?", Reason("light.dsk"));
        Assert.Single(_ha.Current!.SentOfType("get_states"));
    }

    [Fact]
    public void AnEntityRemovedWhileShown_IsUnavailable()
    {
        var provider = Shown("sensor.desk_power");

        Push(provider, () => _ha.Remove("sensor.desk_power"));

        Assert.StartsWith("no entity 'sensor.desk_power' in Home Assistant", Reason("sensor.desk_power"));
    }

    [Fact]
    public void AMissingAttribute_NamesTheAttribute()
    {
        Shown("sensor.desk_power/voltage");

        Assert.Equal("sensor.desk_power has no attribute 'voltage' in Home Assistant", Reason("sensor.desk_power/voltage"));
    }

    // Actions

    private async Task<(ActionResult Result, (string Method, string Path, string Body) Request)> Act(string path, ReadingValue? argument = null, HomeAssistantProvider? provider = null)
    {
        if (provider == null)
        {
            provider = Provider();
            provider.Start(_sink, Settings());
        }

        int before = _ha.Rest.Requests.Count;
        var result = await provider.InvokeAsync(path, argument, TestContext.Current.CancellationToken);
        return (result, _ha.Rest.Requests.Skip(before).LastOrDefault());
    }

    [Fact]
    public async Task ToggleALight_CallsLightToggle_WithoutTheSocket()
    {
        var (result, request) = await Act("light.desk/toggle");

        Assert.True(result.Succeeded);
        Assert.Equal(("POST", "api/services/light/toggle", """{"entity_id":"light.desk"}"""), request);
        Assert.Empty(_ha.Sockets);
    }

    [Theory]
    [InlineData("scene.movie_night/toggle", "api/services/scene/turn_on")]
    [InlineData("script.good_night/toggle", "api/services/script/turn_on")]
    [InlineData("button.doorbell_chime/toggle", "api/services/button/press")]
    [InlineData("input_button.reset/toggle", "api/services/input_button/press")]
    [InlineData("media_player.living_room/toggle", "api/services/media_player/media_play_pause")]
    [InlineData("cover.garage/toggle", "api/services/cover/toggle")]
    [InlineData("switch.coffee/turn_off", "api/services/switch/turn_off")]
    public async Task Toggle_IsEachDomainsNaturalTap(string path, string service)
    {
        var (result, request) = await Act(path);

        Assert.True(result.Succeeded);
        Assert.Equal(service, request.Path);
    }

    [Theory]
    [InlineData("light.desk/turn_on", 40.0, """{"entity_id":"light.desk","brightness_pct":40}""")]
    [InlineData("fan.bedroom/set_percentage", 75.0, """{"entity_id":"fan.bedroom","percentage":75}""")]
    [InlineData("cover.blinds/set_cover_position", 30.0, """{"entity_id":"cover.blinds","position":30}""")]
    [InlineData("number.target/set_value", 12.5, """{"entity_id":"number.target","value":12.5}""")]
    [InlineData("media_player.kitchen/volume_set", 50.0, """{"entity_id":"media_player.kitchen","volume_level":0.5}""")]
    [InlineData("switch.coffee/turn_on", 40.0, """{"entity_id":"switch.coffee"}""")]
    public async Task TheArgument_FillsTheServicesMainField(string path, double argument, string body)
    {
        var (_, request) = await Act(path, argument);

        Assert.Equal(body, request.Body);
    }

    [Fact]
    public async Task ASelectOption_IsSentAsText()
    {
        var (_, request) = await Act("select.mode/select_option", "Eco");

        Assert.Equal("""{"entity_id":"select.mode","option":"Eco"}""", request.Body);
    }

    [Fact]
    public async Task AClimateTemperature_IsSentInTheInstallationsUnit()
    {
        _ha.TemperatureUnit = "°F";

        var (_, request) = await Act("climate.hall/set_temperature", 22.0);

        Assert.Equal("""{"entity_id":"climate.hall","temperature":71.6}""", request.Body);
    }

    [Fact]
    public async Task ALock_TogglesByItsLastState_AndFailsWhenItIsNotKnown()
    {
        _ha.Set("lock.front_door", "locked", []);
        var (unknown, _) = await Act("lock.front_door/toggle");
        Assert.False(unknown.Succeeded);
        Assert.Contains("state is not known", unknown.Message);

        var provider = Shown("lock.front_door");
        var (result, request) = await Act("lock.front_door/toggle", null, provider);

        Assert.True(result.Succeeded);
        Assert.Equal("api/services/lock/unlock", request.Path);
    }

    [Fact]
    public async Task AnUnknownService_FailsNamingTheServiceAndTheEntity()
    {
        _ha.Rest.Respond = (method, _, _) => method == "POST" ? new RestAnswer(400, "400: Bad Request") : null;

        var (result, _) = await Act("light.desk/explode");

        Assert.False(result.Succeeded);
        Assert.Equal("Home Assistant refused light.explode on light.desk (unknown service, or the entity does not support it)", result.Message);
    }

    [Fact]
    public async Task ARejectedTokenOrNoAnswer_FailsWithTheCause()
    {
        _ha.Rest.Respond = (_, _, _) => new RestAnswer(401, "401: Unauthorized");
        Assert.Equal("token rejected", (await Act("light.desk/toggle")).Result.Message);

        _ha.Rest.Respond = (_, _, _) => throw new HttpRequestException("Connection refused (ha.test:8123)");
        Assert.Equal("Home Assistant did not answer: Connection refused (ha.test:8123)", (await Act("light.desk/toggle")).Result.Message);
    }

    [Theory]
    [InlineData("light.desk")]
    [InlineData("desk/toggle")]
    [InlineData("light.desk/a/b")]
    public async Task APathThatIsNotAnAction_Fails(string path)
    {
        var (result, request) = await Act(path);

        Assert.False(result.Succeeded);
        Assert.Contains("is not a Home Assistant action", result.Message);
        Assert.Equal(default, request);
    }

    [Fact]
    public void TheActionCatalog_ListsThePatterns()
    {
        Assert.Equal(
            ["{entity}/toggle", "{entity}/turn_on", "{entity}/turn_off", "{entity}/{service}"],
            Provider().DescribeActions().Select(a => a.Path));
        Assert.Equal(["{entity}", "{entity}/{attribute}"], Provider().Describe().Select(d => d.Path));
    }

    // Natural action and icon

    [Fact]
    public void ADescription_NamesTheNaturalActionAndTheIcon()
    {
        Shown("light.desk", "light.desk_strip", "sensor.office_temperature");

        Assert.Equal("light.desk/toggle", _sink.Described("light.desk")!.Action);
        Assert.Equal("home/light", _sink.Described("light.desk")!.Icon);
        Assert.Equal("light.desk_strip/toggle", _sink.Described("light.desk_strip")!.Action);
        Assert.Equal("home/light", _sink.Described("light.desk_strip")!.Icon);
        Assert.Null(_sink.Described("sensor.office_temperature")!.Action);
        Assert.Equal("home/thermometer", _sink.Described("sensor.office_temperature")!.Icon);
    }

    [Theory]
    [InlineData("switch.plug", "outlet", null, "home/outlet", true)]
    [InlineData("switch.heater", null, null, "home/switch", true)]
    [InlineData("switch.heater", null, "mdi:power-plug-outline", "home/outlet", true)]
    [InlineData("binary_sensor.hall", "motion", null, "home/motion", false)]
    [InlineData("binary_sensor.sink", "moisture", null, "home/leak", false)]
    [InlineData("binary_sensor.bath", "window", null, "home/window", false)]
    [InlineData("cover.garage", "garage", null, "home/garage", true)]
    [InlineData("cover.living", "blind", null, "home/blinds", true)]
    [InlineData("sensor.hall_humidity", "humidity", null, "home/droplet", false)]
    [InlineData("scene.movie", null, null, "home/scene", true)]
    [InlineData("script.bed", null, null, "home/script", true)]
    [InlineData("button.reboot", null, null, "home/button", true)]
    [InlineData("lock.front", null, null, "home/lock", true)]
    [InlineData("media_player.tv", "tv", null, "media/play", true)]
    [InlineData("sensor.lamp_hours", null, "mdi:lamp", "home/light", false)]
    [InlineData("weather.home", null, "mdi:weather-cloudy", "home/entity", false)]
    [InlineData("climate.hall", null, null, "home/entity", true)]
    public void TheIcon_ComesFromDomainAndDeviceClass_ThenTheMdiName_ThenTheDomain(string id, string? deviceClass, string? mdi, string icon, bool acts)
    {
        var attributes = new JsonObject();
        if (deviceClass != null)
            attributes["device_class"] = deviceClass;
        if (mdi != null)
            attributes["icon"] = mdi;
        var entity = EntityState.FromCompressed(id, System.Text.Json.JsonDocument.Parse(new JsonObject { ["s"] = "on", ["a"] = attributes }.ToJsonString()).RootElement);

        var described = EntityMapping.Describe(entity);

        Assert.Equal(icon, described.Icon);
        Assert.Equal(acts ? $"{id}/toggle" : null, described.Action);
    }

    // Listing

    [Fact]
    public async Task Browsing_DescribesEveryEntityFromOneRequest()
    {
        var provider = Provider();
        provider.Start(_sink, Settings());

        var listed = await provider.BrowseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, listed.Count);
        Assert.Single(_ha.Rest.Requests);
        Assert.Equal(("GET", "api/states", ""), _ha.Rest.Requests[0]);
        var light = listed.Single(d => d.Path == "light.desk");
        Assert.Equal(("Desk lamp", ReadingKind.OnOff, "light.desk/toggle", "home/light"), (light.Label, light.Kind, light.Action, light.Icon));
        var temperature = listed.Single(d => d.Path == "sensor.office_temperature");
        Assert.Equal(ReadingKind.Temperature, temperature.Kind);
    }

    [Fact]
    public async Task AListedDescription_IsTheSameAsAShownOne()
    {
        var provider = Shown("sensor.office_temperature", "light.desk_strip");

        var listed = await provider.BrowseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_sink.Described("sensor.office_temperature"), listed.Single(d => d.Path == "sensor.office_temperature"));
        Assert.Equal(_sink.Described("light.desk_strip"), listed.Single(d => d.Path == "light.desk_strip"));
    }

    // Sample data

    [Fact]
    public void TheSample_CoversEveryKind_WithActionsAndIcons()
    {
        using var stream = typeof(HomeAssistantProvider).Assembly.GetManifestResourceStream(ProviderSamples.ResourceName("homeassistant"));
        Assert.NotNull(stream);
        var sample = SampleFile.Parse(new StreamReader(stream).ReadToEnd(), out var findings);

        Assert.Empty(findings);
        Assert.NotNull(sample);
        var readings = sample.Describe();
        foreach (var kind in new[] { ReadingKind.OnOff, ReadingKind.Temperature, ReadingKind.Percent, ReadingKind.Number, ReadingKind.Time, ReadingKind.Text, ReadingKind.Speed, ReadingKind.Depth })
            Assert.Contains(readings, r => r.Kind == kind);
        Assert.Equal("light.living_room/toggle", readings.Single(r => r.Path == "light.living_room").Action);
        Assert.Equal("home/light", readings.Single(r => r.Path == "light.living_room").Icon);
        Assert.Contains(readings, r => r.Path == "{entity}");
        Assert.All(readings.Where(r => r.Action != null), r => Assert.StartsWith(r.Path.Split('/')[0] + "/", r.Action));
    }

    [Fact]
    public void InDemoMode_ALightShowsItsSampleState_AndNothingIsContacted()
    {
        var descriptor = ProviderDescriptor.TryCreate(typeof(HomeAssistantProvider), out _)! with { Factory = Provider };
        var sample = ProviderSamples.Find(descriptor)!;
        var registry = new ProviderRegistry();
        registry.TryRegister(ProviderSamples.Replace(descriptor, sample, () => new DateTime(2026, 10, 13, 21, 47, 32), new DateTime(2026, 10, 13, 21, 47, 32)), out _);
        using var hub = new ReadingHub(registry, _time, regionUsesFahrenheit: false, sampling: ReadingHubSampling.Manual);

        hub.Subscribe(["homeassistant:light.living_room", "homeassistant:sensor.anything_else"]);
        hub.SampleNow();

        Assert.True(hub.Read("homeassistant:light.living_room").Value!.Value.IsOn);
        Assert.Equal("home/light", hub.Describe("homeassistant:light.living_room")!.Icon);
        Assert.Equal(ReadingState.Ok, hub.Read("homeassistant:sensor.anything_else").State);
        Assert.Empty(_ha.Sockets);
        Assert.Empty(_ha.RestClients);
    }
}
