// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ReadingHubSettingsTests : IDisposable
{
    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _registry = new();
    private readonly InMemorySecretBackend _backend = new();
    private readonly Dictionary<string, string> _environment = new();
    private readonly List<(string Level, string Message)> _log = [];
    private readonly object _logGate = new();

    public ReadingHubSettingsTests()
    {
        UrDeckLog.Listener = (level, message) =>
        {
            lock (_logGate)
                _log.Add((level, message));
        };
        Provider = new TypedFakeProvider();
        Plain = new FakeProvider(_time);
        _registry.TryRegister(Provider.Descriptor(), out _);
        _registry.TryRegister(Plain.Descriptor(), out _);
        Store = new SecretStore(_backend, name => _environment.GetValueOrDefault(name));
        Hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false, secrets: Store);
    }

    private TypedFakeProvider Provider { get; }

    private FakeProvider Plain { get; }

    private SecretStore Store { get; }

    private ReadingHub Hub { get; }

    public void Dispose()
    {
        Hub.Dispose();
        UrDeckLog.Listener = null;
    }

    private static bool Wait(Func<bool> condition) => HubTestExtensions.WaitFor(condition);

    private string[] Lines()
    {
        lock (_logGate)
            return _log.Select(l => l.Message).ToArray();
    }

    private static Dictionary<string, ProviderSettings> Settings(string providers) =>
        ConfigStore.Parse("{\"providers\":" + providers + "}").Providers!;

    [Fact]
    public void TheAddress_ReachesTheProvider_AndUnknownKeysAreKept()
    {
        _backend.Write("typed", "token", "tok", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home","retries":5,"secure":false,"other":"kept"}}"""));

        Hub.Subscribe(["typed:a"]);

        Assert.True(Wait(() => Provider.Starts.Count == 1));
        var settings = Provider.Starts[0];
        Assert.Equal("https://ha.home", settings.Url);
        Assert.Equal(5, settings.Retries);
        Assert.False(settings.Secure);
        Assert.Equal("kept", settings.ExtensionData!["other"].GetString());
        Assert.Equal("tok", settings.Token!.Reveal());
    }

    [Fact]
    public void AWrongTypedValue_IsLoggedOnce_AndTheDefaultStays()
    {
        _backend.Write("typed", "token", "tok", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home","retries":"many"}}"""));

        Hub.Subscribe(["typed:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1));
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.cabin","retries":"many"}}"""));
        Assert.True(Wait(() => Provider.Starts.Count == 2));

        Assert.Equal(3, Provider.Starts[0].Retries);
        Assert.Single(Lines(), l => l.Contains("providers.typed.retries is not a whole number"));
    }

    [Fact]
    public void ARequiredSettingMissing_NeverStartsTheProvider_AndTheReadingSaysWhatToSet()
    {
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));

        Hub.Subscribe(["typed:a"]);

        Assert.True(Wait(() => Hub.Read("typed:a").State == ReadingState.Unavailable));
        Assert.Equal("Set up Typed in Settings", Hub.Read("typed:a").Reason);
        Assert.Empty(Provider.Starts);
    }

    [Fact]
    public void AProviderWithoutAMessage_NamesItselfAndTheSetting()
    {
        var other = new TypedFakeProvider();
        _registry.TryRegister(other.Descriptor("quiet") with { MissingSettingMessage = "" }, out _);

        Hub.Subscribe(["quiet:a"]);

        Assert.True(Wait(() => Hub.Read("quiet:a").State == ReadingState.Unavailable));
        Assert.Contains("Address", Hub.Read("quiet:a").Reason);
        Assert.Contains("Typed", Hub.Read("quiet:a").Reason);
    }

    [Fact]
    public void SettingTheMissingSettings_StartsTheWaitingProvider()
    {
        Hub.Subscribe(["typed:a"]);
        Assert.True(Wait(() => Hub.Read("typed:a").State == ReadingState.Unavailable));

        _backend.Write("typed", "token", "tok", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));

        Assert.True(Wait(() => Provider.Starts.Count == 1));
    }

    [Fact]
    public void ASecretSaved_RestartsTheProviderWithIt()
    {
        _backend.Write("typed", "token", "old", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));
        Hub.Subscribe(["typed:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1));

        Store.Save("typed", "token", "new", "https://ha.home");
        Hub.SecretsChanged();

        Assert.True(Wait(() => Provider.Starts.Count == 2));
        Assert.Equal("new", Provider.Starts[1].Token!.Reveal());
        Assert.Equal(1, Provider.Stops);
    }

    [Fact]
    public void ASecretRemoved_StopsTheProviderAndTheReadingReportsTheMissingSetting()
    {
        _backend.Write("typed", "token", "old", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));
        Hub.Subscribe(["typed:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1));

        Store.Remove("typed", "token");
        Hub.SecretsChanged();

        Assert.True(Wait(() => Hub.Read("typed:a").State == ReadingState.Unavailable));
        Assert.Single(Provider.Starts);
        Assert.Equal("Set up Typed in Settings", Hub.Read("typed:a").Reason);
    }

    [Fact]
    public void OnlyTheProviderWhoseSettingsChanged_IsRestarted()
    {
        _backend.Write("typed", "token", "tok", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"},"fake":{"intervalMs":1000}}"""));
        Hub.Subscribe(["typed:a", "fake:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1 && Plain.Count("start") == 1));

        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.cabin"},"fake":{"intervalMs":1000}}"""));

        Assert.True(Wait(() => Provider.Starts.Count == 2));
        Assert.Equal("https://ha.cabin", Provider.Starts[1].Url);
        Assert.Equal(1, Plain.Count("start"));
        Assert.Equal(0, Plain.Count("stop"));
    }

    [Fact]
    public void ASaveThatChangesNothingForTheProvider_RestartsNothing()
    {
        _backend.Write("typed", "token", "tok", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));
        Hub.Subscribe(["typed:a", "fake:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1 && Plain.Count("start") == 1));

        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"},"fake":{"intervalMs":1000}}"""));
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));

        Assert.Single(Provider.Starts);
        Assert.Equal(0, Provider.Stops);
        Assert.Equal(1, Plain.Count("start"));
    }

    [Fact]
    public void AnIntervalChange_TakesEffectWithoutARestart()
    {
        _backend.Write("typed", "token", "tok", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));
        Hub.Subscribe(["typed:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1));

        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home","intervalMs":5000,"actionTimeoutMs":3000}}"""));

        Assert.Single(Provider.Starts);
        Assert.Equal(0, Provider.Stops);
        Assert.Contains(Lines(), l => l.Contains("sampled every 5000 ms"));
    }

    [Fact]
    public void ARestartedProvider_KeepsItsLastValueAsPending()
    {
        _backend.Write("typed", "token", "tok", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));
        Hub.Subscribe(["typed:a"]);
        Assert.True(Wait(() => Hub.Read("typed:a").State == ReadingState.Ok));

        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.cabin"}}"""));

        Assert.True(Wait(() => Provider.Starts.Count == 2));
        Assert.True(Wait(() => Hub.Read("typed:a").Value != null));
    }

    [Fact]
    public void EachSecret_IsLoggedWithItsSource_AndNeverItsValue()
    {
        _backend.Write("typed", "token", "stored-value-123", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));
        Hub.Subscribe(["typed:a"]);
        Assert.True(Wait(() => Provider.Starts.Count == 1));
        _environment["URDECK_SECRET_TYPED_TOKEN"] = "env-value-456";
        Hub.SecretsChanged();
        Assert.True(Wait(() => Provider.Starts.Count == 2));

        string[] lines = Lines();
        Assert.Contains("Secret typed/token: from Credential Manager", lines);
        Assert.Contains("Secret typed/token: from URDECK_SECRET_TYPED_TOKEN", lines);
        Assert.DoesNotContain(lines, l => l.Contains("stored-value-123") || l.Contains("env-value-456"));
        Assert.Equal("env-value-456", Provider.Starts[1].Token!.Reveal());
    }

    [Fact]
    public void AMissingSecret_IsLogged()
    {
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));

        Hub.Subscribe(["typed:a"]);

        Assert.True(Wait(() => Hub.Read("typed:a").State == ReadingState.Unavailable));
        Assert.Contains("Secret typed/token: missing; set it in Settings", Lines());
    }

    [Fact]
    public void AProvider_NeverReceivesAnotherProvidersSecret()
    {
        _backend.Write("other", "token", "belongs-to-other", null);
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home"}}"""));

        Hub.Subscribe(["typed:a"]);

        Assert.True(Wait(() => Hub.Read("typed:a").State == ReadingState.Unavailable));
        Assert.Empty(Provider.Starts);
    }

    [Fact]
    public void ATokenSavedForAnotherAddress_IsUsed_AndTheLogExplains()
    {
        _backend.Write("typed", "token", "tok", "https://ha.home");
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.cabin"}}"""));

        Hub.Subscribe(["typed:a"]);

        Assert.True(Wait(() => Provider.Starts.Count == 1));
        Assert.Equal("tok", Provider.Starts[0].Token!.Reveal());
        Assert.Contains(Lines(), l => l.Contains("saved for https://ha.home") && l.Contains("url is https://ha.cabin")
            && l.Contains("URDECK_SECRET_TYPED_TOKEN") && !l.Contains("tok'"));
    }

    [Fact]
    public void ASecretLeftInTheFile_IsUsedWhenNothingElseHasOne()
    {
        Hub.ApplySettings(Settings("""{"typed":{"url":"https://ha.home","token":"from-file"}}"""));

        Hub.Subscribe(["typed:a"]);

        Assert.True(Wait(() => Provider.Starts.Count == 1));
        Assert.Equal("from-file", Provider.Starts[0].Token!.Reveal());
        Assert.DoesNotContain(Lines(), l => l.Contains("from-file"));
    }

    [Fact]
    public void ASampleThatReplacesTheProvider_IgnoresItsRequiredSettings()
    {
        var descriptor = Provider.Descriptor("sampled") with { SettingsTypeOverride = typeof(ProviderSettings) };
        _registry.TryRegister(descriptor, out _);

        Assert.Empty(descriptor.Schema.Settings);
    }
}
