// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.Versioning;
using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public class SecretStoreTests
{
    private readonly InMemorySecretBackend _backend = new();
    private readonly Dictionary<string, string> _environment = new();

    private SecretStore Store(bool persistent = true) =>
        new(persistent ? _backend : null, name => _environment.GetValueOrDefault(name));

    [Theory]
    [InlineData("homeassistant", "token", "URDECK_SECRET_HOMEASSISTANT_TOKEN")]
    [InlineData("home-assistant", "api.token", "URDECK_SECRET_HOME_ASSISTANT_API_TOKEN")]
    [InlineData("my.plugin", "pass-word", "URDECK_SECRET_MY_PLUGIN_PASS_WORD")]
    public void TheEnvironmentName_IsUpperCaseWithDotsAndDashesAsUnderscores(string provider, string setting, string expected) =>
        Assert.Equal(expected, SecretStore.EnvironmentName(provider, setting));

    [Fact]
    public void TheEnvironment_WinsOverTheStoredValue()
    {
        _backend.Write("homeassistant", "token", "stored", "https://ha.home");
        _environment["URDECK_SECRET_HOMEASSISTANT_TOKEN"] = "from-env";

        var found = Store().Find("homeassistant", "token")!;

        Assert.Equal("from-env", found.Value);
        Assert.Equal(SecretSource.Environment, found.Source);
        Assert.Equal("URDECK_SECRET_HOMEASSISTANT_TOKEN", found.SourceName);
        Assert.Equal("stored", Store().FindStored("homeassistant", "token")!.Value);
    }

    [Fact]
    public void WithoutTheEnvironment_TheStoredValueAndItsContextAreFound()
    {
        _backend.Write("homeassistant", "token", "stored", "https://ha.home");

        var found = Store().Find("homeassistant", "token")!;

        Assert.Equal("stored", found.Value);
        Assert.Equal("https://ha.home", found.Context);
        Assert.Equal(SecretSource.Stored, found.Source);
    }

    [Fact]
    public void NothingSet_FindsNothing_AndAnEmptyVariableCountsAsUnset()
    {
        _environment["URDECK_SECRET_HOMEASSISTANT_TOKEN"] = "";

        Assert.Null(Store().Find("homeassistant", "token"));
    }

    [Fact]
    public void WhereThereIsNoStore_OnlyTheEnvironmentIsRead_AndSavingSaysSo()
    {
        _backend.Write("homeassistant", "token", "stored", null);
        var store = Store(persistent: false);

        Assert.False(store.CanStore);
        Assert.Null(store.Find("homeassistant", "token"));
        var ex = Assert.Throws<SecretStoreException>(() => store.Save("homeassistant", "token", "x", null));
        Assert.Contains("URDECK_SECRET_HOMEASSISTANT_TOKEN", ex.Message);
    }

    [Fact]
    public void ASecretLargerThanCredentialManagerAccepts_IsRefusedWithAMessage()
    {
        var ex = Assert.Throws<SecretStoreException>(() => Store().Save("homeassistant", "token", new string('x', 2561), null));

        Assert.Contains("2,560", ex.Message);
        Assert.Null(_backend.Read("homeassistant", "token"));
        Store().Save("homeassistant", "token", new string('x', 2560), null);
        Assert.NotNull(_backend.Read("homeassistant", "token"));
    }

    [Fact]
    public void SaveAndRemove_RoundTrip()
    {
        var store = Store();

        store.Save("homeassistant", "token", "abc", "https://ha.home");
        Assert.Equal("abc", store.Find("homeassistant", "token")!.Value);
        Assert.True(store.Remove("homeassistant", "token"));
        Assert.False(store.Remove("homeassistant", "token"));
        Assert.Null(store.Find("homeassistant", "token"));
    }

    [Fact]
    public void AnUnreadableStore_IsReportedOnceAndTheEnvironmentStillWorks()
    {
        var lines = new List<string>();
        UrDeckLog.Listener = (_, message) => lines.Add(message);
        try
        {
            var store = new SecretStore(new BrokenBackend(), name => name == "URDECK_SECRET_A_B" ? "env" : null);

            Assert.Null(store.Find("x", "y"));
            Assert.Null(store.Find("x", "z"));
            Assert.Equal("env", store.Find("a", "b")!.Value);
        }
        finally
        {
            UrDeckLog.Listener = null;
        }

        Assert.Single(lines, l => l.Contains("Credential Manager cannot be read"));
    }

    private static UrDeckConfig Config(string providers) => ConfigStore.Parse("{\"providers\":" + providers + "}");

    private static IEnumerable<ProviderDescriptor> Providers() => [new TypedFakeProvider().Descriptor()];

    [Fact]
    public void ATypedInToken_IsMovedToTheStoreAndRemovedFromTheFile_WithItsContext()
    {
        var config = Config("""{"typed":{"url":"https://ha.home","token":"abc"}}""");
        var lines = new List<string>();
        UrDeckLog.Listener = (_, message) => lines.Add(message);
        bool changed;
        try
        {
            changed = SecretMover.MoveFromConfig(config, Providers(), Store(), configIsNewerThanHost: false);
        }
        finally
        {
            UrDeckLog.Listener = null;
        }

        Assert.True(changed);
        Assert.Equal("abc", _backend.Read("typed", "token")!.Value);
        Assert.Equal("https://ha.home", _backend.Read("typed", "token")!.Context);
        Assert.False(config.Providers!["typed"].ExtensionData!.ContainsKey("token"));
        Assert.True(config.Providers["typed"].ExtensionData!.ContainsKey("url"));
        Assert.Contains("Secret typed/token: moved from urdeck-config.json to Credential Manager", lines);
        Assert.DoesNotContain(lines, l => l.Contains("abc"));
        Assert.DoesNotContain("abc", JsonSerializer.Serialize(config, UrDeckJson.Options));
    }

    [Fact]
    public void ATokenInANewerFile_IsNotMoved()
    {
        var config = Config("""{"typed":{"token":"abc"}}""");

        bool changed = SecretMover.MoveFromConfig(config, Providers(), Store(), configIsNewerThanHost: true);

        Assert.False(changed);
        Assert.Null(_backend.Read("typed", "token"));
        Assert.True(config.Providers!["typed"].ExtensionData!.ContainsKey("token"));
    }

    [Fact]
    public void ATokenWhereNothingCanStoreIt_StaysInTheFile()
    {
        var config = Config("""{"typed":{"token":"abc"}}""");

        bool changed = SecretMover.MoveFromConfig(config, Providers(), Store(persistent: false), configIsNewerThanHost: false);

        Assert.False(changed);
        Assert.True(config.Providers!["typed"].ExtensionData!.ContainsKey("token"));
    }

    [Fact]
    public void AValueThatIsNotText_OrAKeyThatIsNotASecret_IsLeftAlone()
    {
        var config = Config("""{"typed":{"token":5,"retries":"x"},"unknown":{"token":"abc"}}""");

        bool changed = SecretMover.MoveFromConfig(config, Providers(), Store(), configIsNewerThanHost: false);

        Assert.False(changed);
        Assert.Null(_backend.Read("unknown", "token"));
    }

    [Fact]
    public void WhenTheEnvironmentAlsoHasOne_TheFileValueIsStillMoved()
    {
        _environment["URDECK_SECRET_TYPED_TOKEN"] = "env";
        var config = Config("""{"typed":{"token":"abc"}}""");

        bool changed = SecretMover.MoveFromConfig(config, Providers(), Store(), configIsNewerThanHost: false);

        Assert.True(changed);
        Assert.Equal("abc", _backend.Read("typed", "token")!.Value);
        Assert.Equal("env", Store().Find("typed", "token")!.Value);
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    public void CredentialManager_RoundTripsAndEnumeratesUnderATestPrefix()
    {
        var backend = new CredentialManagerBackend("UrDeckTest");
        try
        {
            backend.Write("homeassistant", "token", "päss-wörd-✓", "https://ha.home");
            backend.Write("other.plugin", "key", "second", null);

            var read = backend.Read("homeassistant", "token")!;
            Assert.Equal("päss-wörd-✓", read.Value);
            Assert.Equal("https://ha.home", read.Context);
            Assert.Null(backend.Read("other.plugin", "key")!.Context);
            Assert.Null(backend.Read("homeassistant", "missing"));
            Assert.Equivalent(new[] { ("homeassistant", "token"), ("other.plugin", "key") }, backend.List());

            backend.Write("homeassistant", "token", "replaced", null);
            Assert.Equal("replaced", backend.Read("homeassistant", "token")!.Value);

            Assert.True(backend.Delete("homeassistant", "token"));
            Assert.False(backend.Delete("homeassistant", "token"));
            Assert.Equal(1, backend.DeleteAll());
            Assert.Empty(backend.List());
        }
        finally
        {
            backend.DeleteAll();
        }
    }

    private sealed class BrokenBackend : ISecretBackend
    {
        public StoredSecret? Read(string providerId, string setting) => throw new InvalidOperationException("locked");

        public void Write(string providerId, string setting, string value, string? context) => throw new InvalidOperationException("locked");

        public bool Delete(string providerId, string setting) => throw new InvalidOperationException("locked");

        public int DeleteAll() => 0;

        public IReadOnlyList<(string ProviderId, string Setting)> List() => [];
    }
}
