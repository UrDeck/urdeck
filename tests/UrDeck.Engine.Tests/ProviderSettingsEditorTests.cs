// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Settings;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ProviderSettingsEditorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _registry = new();
    private readonly InMemorySecretBackend _backend = new();
    private readonly Dictionary<string, string> _environment = new();
    private readonly ConfigStore _config;
    private readonly ReadingHub _hub;
    private readonly SecretStore _store;
    private readonly ProviderSettingsEditor _editor;

    public ProviderSettingsEditorTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "urdeck-config.json"), "{}");
        _config = new ConfigStore(Path.Combine(_dir, "urdeck-config.json"));
        Provider = new TypedFakeProvider();
        _registry.TryRegister(Provider.Descriptor(), out _);
        _registry.TryRegister(new FakeProvider(_time).Descriptor("plain"), out _);
        _store = new SecretStore(_backend, name => _environment.GetValueOrDefault(name));
        _hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false, secrets: _store);
        _editor = new ProviderSettingsEditor(_config, _registry, _store, _hub);
    }

    private TypedFakeProvider Provider { get; }

    public void Dispose()
    {
        _hub.Dispose();
        _config.Dispose();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static Dictionary<string, string> Form(string url = "https://ha.home", string retries = "3", string secure = "true") =>
        new() { ["url"] = url, ["retries"] = retries, ["secure"] = secure };

    private string File_() => File.ReadAllText(Path.Combine(_dir, "urdeck-config.json"));

    [Fact]
    public void OnlyProvidersWithDeclaredSettings_GetAForm()
    {
        var form = Assert.Single(_editor.GetForms());

        Assert.Equal("typed", form.ProviderId);
        Assert.Equal(["url", "token", "retries", "secure"], form.Fields.Select(f => f.Setting.Key));
        Assert.Equal(SettingType.Secret, form.Fields[1].Setting.Type);
        Assert.True(form.OffersConnectionTest);
        Assert.Equal("3", form.Fields[2].Value);
        Assert.Equal("true", form.Fields[3].Value);
    }

    [Fact]
    public void NoDeclaredSettings_GivesNoForms()
    {
        var registry = new ProviderRegistry();
        registry.TryRegister(new FakeProvider(_time).Descriptor("plain"), out _);
        var editor = new ProviderSettingsEditor(_config, registry, _store, _hub);

        Assert.Empty(editor.GetForms());
    }

    [Fact]
    public void Saving_WritesTheAddressToTheFile_AndTheTokenToTheStore_NeverTheTokenToTheFile()
    {
        var result = _editor.Save("typed", Form(), new Dictionary<string, string> { ["token"] = "s3cret-token" });

        Assert.True(result.Ok);
        string file = File_();
        Assert.Contains("https://ha.home", file);
        Assert.DoesNotContain("s3cret-token", file);
        Assert.DoesNotContain("\"token\"", file);
        var stored = _backend.Read("typed", "token")!;
        Assert.Equal("s3cret-token", stored.Value);
        Assert.Equal("https://ha.home", stored.Context);
    }

    [Fact]
    public void Saving_StartsTheProviderWithTheNewValues()
    {
        _hub.Subscribe(["typed:a"]);
        Assert.True(HubTestExtensions.WaitFor(() => _hub.Read("typed:a").State == ReadingState.Unavailable));

        _editor.Save("typed", Form(), new Dictionary<string, string> { ["token"] = "tok" });

        Assert.True(HubTestExtensions.WaitFor(() => Provider.Starts.Count == 1));
        Assert.Equal("https://ha.home", Provider.Starts[0].Url);
        Assert.Equal("tok", Provider.Starts[0].Token!.Reveal());
    }

    [Fact]
    public void ALeftOutSecret_IsNotTouched()
    {
        _backend.Write("typed", "token", "old", "https://ha.home");

        _editor.Save("typed", Form(url: "https://ha.cabin"), new Dictionary<string, string>());

        Assert.Equal("old", _backend.Read("typed", "token")!.Value);
        Assert.Contains("ha.cabin", File_());
    }

    [Fact]
    public void AnInvalidNumber_SavesNothing()
    {
        var result = _editor.Save("typed", Form(retries: "many"), new Dictionary<string, string> { ["token"] = "tok" });

        Assert.False(result.Ok);
        Assert.Contains("Retries", result.Message);
        Assert.Null(_backend.Read("typed", "token"));
        Assert.DoesNotContain("ha.home", File_());
    }

    [Fact]
    public void AnEmptyField_RemovesItsKey()
    {
        _editor.Save("typed", Form(), new Dictionary<string, string>());
        _editor.Save("typed", Form(url: ""), new Dictionary<string, string>());

        Assert.DoesNotContain("https://ha.home", File_());
    }

    [Fact]
    public void ASecretTooLarge_FailsWithTheMessage()
    {
        var result = _editor.Save("typed", Form(), new Dictionary<string, string> { ["token"] = new string('x', 3000) });

        Assert.False(result.Ok);
        Assert.Contains("2,560", result.Message);
    }

    [Fact]
    public void RemovingASecret_DeletesItAndTheProviderReportsTheMissingSetting()
    {
        _backend.Write("typed", "token", "tok", null);
        _editor.Save("typed", Form(), new Dictionary<string, string>());
        _hub.Subscribe(["typed:a"]);
        Assert.True(HubTestExtensions.WaitFor(() => Provider.Starts.Count == 1));

        var result = _editor.RemoveSecret("typed", "token");

        Assert.True(result.Ok);
        Assert.Null(_backend.Read("typed", "token"));
        Assert.True(HubTestExtensions.WaitFor(() => _hub.Read("typed:a").State == ReadingState.Unavailable));
    }

    [Fact]
    public void TheSecretField_SaysWhereTheValueIsAndWhatOverridesIt()
    {
        var none = _editor.GetForms()[0].Fields[1].Secret!;
        Assert.False(none.HasValue);
        Assert.Equal("URDECK_SECRET_TYPED_TOKEN", none.EnvironmentName);

        _backend.Write("typed", "token", "tok", null);
        var stored = _editor.GetForms()[0].Fields[1].Secret!;
        Assert.True(stored.HasValue);
        Assert.True(stored.StoredHere);
        Assert.Equal(SecretSource.Stored, stored.Source);
        Assert.False(stored.EnvironmentOverrides);

        _environment["URDECK_SECRET_TYPED_TOKEN"] = "env";
        var overridden = _editor.GetForms()[0].Fields[1].Secret!;
        Assert.Equal(SecretSource.Environment, overridden.Source);
        Assert.True(overridden.EnvironmentOverrides);
        Assert.Equal("", _editor.GetForms()[0].Fields[1].Value);
    }

    [Fact]
    public async Task TestConnection_UsesTheFormsValues_AndSavesNothing()
    {
        TypedSettings? seen = null;
        Provider.Validate = (settings, _) =>
        {
            seen = settings;
            return Task.FromResult("Connected to 2026.10");
        };

        var result = await _editor.TestAsync("typed", Form(), new Dictionary<string, string> { ["token"] = "typed-in" }, TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.Equal("Connected to 2026.10", result.Message);
        Assert.Equal("typed-in", seen!.Token!.Reveal());
        Assert.Equal("https://ha.home", seen.Url);
        Assert.Null(_backend.Read("typed", "token"));
        Assert.DoesNotContain("ha.home", File_());
    }

    [Fact]
    public async Task TestConnection_ShowsWhyItFailed()
    {
        Provider.Validate = (_, _) => throw new InvalidOperationException("The token was rejected.");

        var result = await _editor.TestAsync("typed", Form(), new Dictionary<string, string> { ["token"] = "wrong" }, TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
        Assert.Equal("The token was rejected.", result.Message);
    }

    [Fact]
    public async Task TestConnection_WithARequiredSettingMissing_SaysWhatToSet()
    {
        var result = await _editor.TestAsync("typed", Form(), new Dictionary<string, string>(), TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
        Assert.Contains("Token", result.Message);
    }

    [Fact]
    public async Task TestConnection_IsCancelledWithTheWindow()
    {
        Provider.Validate = async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return "";
        };
        using var cts = new CancellationTokenSource();

        var running = _editor.TestAsync("typed", Form(), new Dictionary<string, string> { ["token"] = "tok" }, cts.Token);
        await cts.CancelAsync();

        var result = await running.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(result.Ok);
        Assert.Contains("cancelled", result.Message);
    }
}
