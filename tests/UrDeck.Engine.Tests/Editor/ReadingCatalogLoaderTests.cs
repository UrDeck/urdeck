// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Data;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests.Editor;

public sealed class ReadingCatalogLoaderTests : IDisposable
{
    private readonly FakeTime _time = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"urdeck-loader-{Guid.NewGuid():N}");
    private readonly ProviderRegistry _registry = new();
    private readonly ReadingHub _hub;
    private readonly ReadingDescriptionCache _cache;
    private readonly BrowsingProvider _browsing = new();
    private readonly FakeProvider _fixed;
    private IReadOnlyCollection<string> _used = ["browse:light.desk"];

    public ReadingCatalogLoaderTests()
    {
        _fixed = new FakeProvider(_time)
        {
            Catalog =
            [
                new ReadingDescriptor("a", ReadingKind.Number, "A", "Reading a"),
                new ReadingDescriptor("n/{n}/load", ReadingKind.Percent, "Core {n}", "Core {n} load"),
            ],
            OnSample = (_, _, _) => Task.CompletedTask,
        };
        _registry.TryRegister(_browsing.Descriptor(), out _);
        _registry.TryRegister(_fixed.Descriptor(), out _);
        _hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false);
        _cache = new ReadingDescriptionCache(Path.Combine(_folder, "cache", "readings.json"));
    }

    public void Dispose()
    {
        _hub.Dispose();
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private ReadingCatalogLoader Loader() => new(_hub, _cache, () => _used);

    [Fact]
    public async Task AProviderThatLists_GivesItsListing_AndCachesTheReadingsInUse()
    {
        var provider = _registry.Get("browse")!;

        var loaded = await Loader().LoadAsync(provider, TestContext.Current.CancellationToken);

        Assert.True(loaded.Connected);
        Assert.False(loaded.FromCache);
        Assert.Equal(["browse:light.desk", "browse:sensor.office"], loaded.Entries.Select(e => e.Id));
        Assert.Equal(["light.desk"], _cache.Read("browse").Select(d => d.Path));
    }

    [Fact]
    public async Task WhenTheListingFails_TheCachedReadingsInUseAreOffered_WithTheMessage()
    {
        var provider = _registry.Get("browse")!;
        await Loader().LoadAsync(provider, TestContext.Current.CancellationToken);
        _browsing.Error = new InvalidOperationException("set up Browse in Settings");

        var loaded = await Loader().LoadAsync(provider, TestContext.Current.CancellationToken);

        Assert.False(loaded.Connected);
        Assert.True(loaded.FromCache);
        Assert.Contains("set up Browse in Settings", loaded.Message, StringComparison.Ordinal);
        Assert.Equal(["browse:light.desk"], loaded.Entries.Select(e => e.Id));
    }

    [Fact]
    public async Task AProviderThatCannotList_GivesItsCatalog_WithPatternsAsTemplates()
    {
        var loaded = await Loader().LoadAsync(_registry.Get("fake")!, TestContext.Current.CancellationToken);

        Assert.True(loaded.Connected);
        Assert.Equal(["fake:a", "fake:n/{n}/load"], loaded.Entries.Select(e => e.Id).Order(StringComparer.Ordinal));
        Assert.Equal(["fake:n/{n}/load"], loaded.Entries.Where(e => e.IsTemplate).Select(e => e.Id));
    }

    /// <summary>A provider with an <c>{entity}</c> pattern that lists two entities, or fails.</summary>
    private sealed class BrowsingProvider : IDataProvider, IReadingBrowser
    {
        public Exception? Error { get; set; }

        public IReadOnlyList<ReadingDescriptor> Describe() => [new ReadingDescriptor("{entity}", ReadingKind.Text, "{entity}", "An entity")];

        public void Start(IReadingSink sink, ProviderSettings settings)
        {
        }

        public void SetDemand(IReadOnlyCollection<string> paths)
        {
        }

        public Task SampleAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Shutdown()
        {
        }

        public Task<IReadOnlyList<ReadingDescriptor>> BrowseAsync(CancellationToken cancellationToken) =>
            Error != null
                ? Task.FromException<IReadOnlyList<ReadingDescriptor>>(Error)
                : Task.FromResult<IReadOnlyList<ReadingDescriptor>>(
                [
                    new ReadingDescriptor("light.desk", ReadingKind.OnOff, "Desk", "light.desk") { Action = "light.desk/toggle", Category = "Lights" },
                    new ReadingDescriptor("sensor.office", ReadingKind.Number, "Office", "sensor.office") { Category = "Sensors" },
                ]);

        public ProviderDescriptor Descriptor() =>
            new("browse", "Browse", typeof(BrowsingProvider), 1000, 250, "tests") { Factory = () => this };
    }
}
