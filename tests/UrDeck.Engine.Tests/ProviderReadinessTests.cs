// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Settings;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>A provider with required settings (see <see cref="TypedSettings"/>) whose availability and guidance a test scripts.</summary>
internal sealed class AvailabilityFakeProvider : DataProvider<TypedSettings>
{
    public bool Available { get; set; } = true;

    public Exception? AvailabilityError { get; set; }

    public SetupGuidance? Say { get; set; }

    public int Starts { get; private set; }

    public override bool IsAvailable => AvailabilityError == null ? Available : throw AvailabilityError;

    public override SetupGuidance? Guidance => Say;

    public override IReadOnlyList<ReadingDescriptor> Describe() =>
        [new ReadingDescriptor("a", ReadingKind.Number, "A", "Reading a")];

    public override void Start(IReadingSink sink, TypedSettings settings) => Starts++;

    public override void SetDemand(IReadOnlyCollection<string> paths)
    {
    }

    public override Task SampleAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override void Shutdown()
    {
    }

    public ProviderDescriptor Descriptor() =>
        new("avail", "Avail", typeof(AvailabilityFakeProvider), 1000, 250, "tests") { Factory = () => this };
}

public sealed class ProviderReadinessTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _registry = new();
    private readonly InMemorySecretBackend _backend = new();
    private readonly ConfigStore _config;
    private readonly ReadingHub _hub;
    private readonly SecretStore _store;
    private readonly ProviderSettingsEditor _editor;
    private readonly ReadingDescriptionCache _cache;
    private readonly AvailabilityFakeProvider _provider = new();
    private IReadOnlyCollection<string> _used = [];

    public ProviderReadinessTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "urdeck-config.json"), "{}");
        _config = new ConfigStore(Path.Combine(_dir, "urdeck-config.json"));
        _registry.TryRegister(_provider.Descriptor(), out _);
        _registry.TryRegister(new FakeProvider(_time).Descriptor("plain"), out _);
        _store = new SecretStore(_backend, _ => null);
        _hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false, secrets: _store);
        _editor = new ProviderSettingsEditor(_config, _registry, _store, _hub);
        _cache = new ReadingDescriptionCache(Path.Combine(_dir, "cache", "readings.json"));
    }

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

    private static Dictionary<string, string> Form() =>
        new() { ["url"] = "https://ha.home", ["retries"] = "3", ["secure"] = "true" };

    private void Configure() =>
        Assert.True(_editor.Save("avail", Form(), new Dictionary<string, string> { ["token"] = "t0ken" }).Ok);

    [Fact]
    public void AProviderThatDeclaresNothing_IsReady()
    {
        var status = _hub.GetStatus("plain")!;

        Assert.Equal(ProviderReadiness.Ready, status.Readiness);
        Assert.Null(status.Guidance);
        Assert.Empty(status.Missing);
    }

    [Fact]
    public void ARequiredSettingWithoutAValue_NeedsSetup_AndNamesIt()
    {
        var status = _hub.GetStatus("avail")!;

        Assert.Equal(ProviderReadiness.NeedsSetup, status.Readiness);
        Assert.Equal(["Address", "Token"], status.Missing);
    }

    [Fact]
    public void AnUnavailableProvider_IsNotAvailable_WithItsGuidance_EvenWithSettingsMissing()
    {
        _provider.Available = false;
        _provider.Say = new SetupGuidance("Avail is not installed.", "https://example.com/", "Get Avail");

        var status = _hub.GetStatus("avail")!;

        Assert.Equal(ProviderReadiness.NotAvailable, status.Readiness);
        Assert.Equal("Avail is not installed.", status.Guidance!.Text);
        Assert.Equal("Get Avail", status.Guidance.LinkText);
    }

    [Fact]
    public void SavingTheMissingSettings_MakesTheProviderReady_WithoutARestart()
    {
        Assert.Equal(ProviderReadiness.NeedsSetup, _hub.GetStatus("avail")!.Readiness);

        Configure();

        var status = _hub.GetStatus("avail")!;
        Assert.Equal(ProviderReadiness.Ready, status.Readiness);
        Assert.Null(status.Guidance);
    }

    [Fact]
    public void ReadyProviders_NeverShowGuidance()
    {
        Configure();
        _provider.Say = new SetupGuidance("Ignored.");

        Assert.Null(_hub.GetStatus("avail")!.Guidance);
    }

    [Fact]
    public void ACheckThatThrows_MeansAvailable()
    {
        _provider.AvailabilityError = new InvalidOperationException("boom");
        Configure();

        Assert.Equal(ProviderReadiness.Ready, _hub.GetStatus("avail")!.Readiness);
        Assert.Equal(ProviderReadiness.Ready, _hub.GetStatus("avail")!.Readiness);
    }

    [Fact]
    public void AskingForTheStatus_StartsNothing()
    {
        Configure();

        _hub.GetStatus("avail");
        _hub.GetStatus("avail");

        Assert.Equal(0, _provider.Starts);
    }

    [Fact]
    public void AnUnknownProvider_HasNoStatus() => Assert.Null(_hub.GetStatus("nope"));

    [Fact]
    public void TheProvidersSection_ListsEveryProvider_NotReadyOnesLast()
    {
        var forms = _editor.GetForms();

        Assert.Equal(["plain", "avail"], forms.Select(f => f.ProviderId));
        Assert.Equal(ProviderReadiness.NeedsSetup, forms[1].Status!.Readiness);
    }

    [Fact]
    public async Task ThePicker_HidesAProviderThatIsNotReady_AndNoPageUses()
    {
        var loader = new ReadingCatalogLoader(_hub, _cache, () => _used);

        var loaded = await loader.LoadAsync(_registry.Get("avail")!, TestContext.Current.CancellationToken);

        Assert.True(loaded.Hidden);
        Assert.Empty(loaded.Entries);
        Assert.Equal(0, _provider.Starts);
    }

    [Fact]
    public async Task ThePicker_KeepsAProviderThatIsNotReady_WhenAPageUsesItsReading()
    {
        _used = ["avail:a"];
        var loader = new ReadingCatalogLoader(_hub, _cache, () => _used);

        var loaded = await loader.LoadAsync(_registry.Get("avail")!, TestContext.Current.CancellationToken);

        Assert.False(loaded.Hidden);
        Assert.False(loaded.Connected);
        Assert.Equal(["avail:a"], loaded.Entries.Select(e => e.Id));
        Assert.Equal(ProviderReadiness.NeedsSetup, loaded.Status!.Readiness);
        Assert.Equal(0, _provider.Starts);
    }

    [Fact]
    public async Task ThePicker_OffersTheProvider_OnceItIsReady()
    {
        Configure();
        var loader = new ReadingCatalogLoader(_hub, _cache, () => _used);

        var loaded = await loader.LoadAsync(_registry.Get("avail")!, TestContext.Current.CancellationToken);

        Assert.False(loaded.Hidden);
        Assert.True(loaded.Connected);
        Assert.Equal(["avail:a"], loaded.Entries.Select(e => e.Id));
    }
}
