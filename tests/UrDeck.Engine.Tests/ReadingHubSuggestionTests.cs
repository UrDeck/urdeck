// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>settings-metadata, "Suggestions From A Provider": the hub routes a picker query like a listing.</summary>
public sealed class ReadingHubSuggestionTests : IDisposable
{
    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _registry = new();
    private readonly string _logPath = Path.Combine(Path.GetTempPath(), $"urdeck-suggest-{Guid.NewGuid():N}.log");

    public ReadingHubSuggestionTests()
    {
        UrDeckLog.LogPath = _logPath;
        Hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false);
    }

    private ReadingHub Hub { get; }

    public void Dispose()
    {
        Hub.Dispose();
        if (File.Exists(_logPath))
            File.Delete(_logPath);
    }

    [Fact]
    public async Task A_query_starts_the_provider_and_answers_with_its_suggestions()
    {
        var source = new SuggestingProvider();
        _registry.TryRegister(source.Descriptor(), out _);

        var result = await Hub.SuggestAsync("suggest", "place", "Portl", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Message);
        var only = Assert.Single(result.Suggestions!);
        Assert.Equal("Portland", only.Label);
        Assert.Equal("Oregon", only.Detail);
        Assert.Equal("Portland, Oregon", only.Values["location"]!.GetValue<string>());
        Assert.Equal("Portl", source.LastText);
        Assert.Equal(1, source.Starts);
    }

    [Fact]
    public async Task An_unknown_provider_or_one_that_does_not_suggest_answers_with_a_message()
    {
        var plain = new PlainProvider();
        _registry.TryRegister(plain.Descriptor(), out _);

        Assert.Equal("no data provider 'nosuch' is registered", (await Hub.SuggestAsync("nosuch", "place", "x", TestContext.Current.CancellationToken)).Message);
        Assert.Equal("provider 'plain' does not answer suggestion queries", (await Hub.SuggestAsync("plain", "place", "x", TestContext.Current.CancellationToken)).Message);
    }

    [Fact]
    public async Task A_kind_the_provider_does_not_answer_gets_a_message()
    {
        var source = new SuggestingProvider();
        _registry.TryRegister(source.Descriptor(), out _);

        var result = await Hub.SuggestAsync("suggest", "colour", "x", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("provider 'suggest' does not answer 'colour' queries", result.Message);
    }

    [Fact]
    public async Task A_failing_query_answers_with_the_reason_and_never_throws()
    {
        var source = new SuggestingProvider { Error = new InvalidOperationException("the service is offline") };
        _registry.TryRegister(source.Descriptor(), out _);

        var result = await Hub.SuggestAsync("suggest", "place", "x", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("provider 'suggest' could not answer: the service is offline", result.Message);
    }

    [Fact]
    public async Task A_query_that_takes_longer_than_ten_seconds_answers_with_a_message()
    {
        var source = new SuggestingProvider { Gate = new TaskCompletionSource() };
        _registry.TryRegister(source.Descriptor(), out _);

        var pending = Hub.SuggestAsync("suggest", "place", "x", TestContext.Current.CancellationToken);
        Assert.True(HubTestExtensions.WaitFor(() => source.Asked));
        _time.Advance(TimeSpan.FromSeconds(11));
        var result = await pending;

        Assert.False(result.Succeeded);
        Assert.Equal("provider 'suggest' did not answer within 10 s", result.Message);
        source.Gate.SetResult();
    }

    private sealed class PlainProvider : IDataProvider
    {
        public IReadOnlyList<ReadingDescriptor> Describe() => [];

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

        public ProviderDescriptor Descriptor() => new("plain", "Plain", typeof(PlainProvider), 1000, 250, "tests") { Factory = () => this };
    }

    private sealed class SuggestingProvider : IDataProvider, ISuggestionSource
    {
        private int _starts;
        private volatile bool _asked;

        public int Starts => Volatile.Read(ref _starts);

        public bool Asked => _asked;

        public string? LastText { get; private set; }

        public Exception? Error { get; init; }

        public TaskCompletionSource? Gate { get; init; }

        public IReadOnlyList<string> SuggestionKinds { get; } = ["place"];

        public IReadOnlyList<ReadingDescriptor> Describe() => [];

        public void Start(IReadingSink sink, ProviderSettings settings) => Interlocked.Increment(ref _starts);

        public void SetDemand(IReadOnlyCollection<string> paths)
        {
        }

        public Task SampleAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Shutdown()
        {
        }

        public async Task<IReadOnlyList<Suggestion>> SuggestAsync(string kind, string text, CancellationToken cancellationToken)
        {
            LastText = text;
            _asked = true;
            if (Gate != null)
                await Gate.Task;
            if (Error != null)
                throw Error;
            return
            [
                new Suggestion("Portland", "Oregon", new Dictionary<string, JsonNode?> { ["location"] = "Portland, Oregon", ["latitude"] = 45.52, ["longitude"] = -122.68 }),
            ];
        }

        public ProviderDescriptor Descriptor() => new("suggest", "Suggest", typeof(SuggestingProvider), 1000, 250, "tests") { Factory = () => this };
    }
}
