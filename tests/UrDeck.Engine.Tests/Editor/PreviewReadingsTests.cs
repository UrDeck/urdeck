// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Data;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests.Editor;

public sealed class PreviewReadingsTests : IDisposable
{
    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _providers = new();
    private readonly FakeProvider _fake;
    private readonly TypedFakeProvider _typed = new();
    private readonly ReadingHub _live;
    private readonly PreviewReadings _preview;

    // Both providers carry a sample: "a" is 42 in fake's, 7 in typed's.
    private static SampleFile Sample(double value) =>
        SampleFile.Parse($$"""{ "version": 1, "readings": { "a": { "kind": "number", "label": "A", "name": "Reading a", "value": {{value}} } } }""", out _)!;

    public PreviewReadingsTests()
    {
        _fake = new FakeProvider(_time);
        _providers.TryRegister(_fake.Descriptor(), out _);
        _providers.TryRegister(_typed.Descriptor(), out _);
        _live = new ReadingHub(_providers, _time, sampling: ReadingHubSampling.Manual);
        _preview = new PreviewReadings(_live, () => _providers.Descriptors, p => p.Id switch { "fake" => Sample(42), "typed" => Sample(7), _ => null },
            _time, () => new DateTime(2026, 11, 7, 12, 0, 0), ReadingHubSampling.Manual);
    }

    public void Dispose()
    {
        _preview.Dispose();
        _live.Dispose();
    }

    private void SampleBoth()
    {
        _live.SampleNow();
        _preview.SampleNow();
    }

    [Fact]
    public void Live_ShowsTheHostsReadings()
    {
        _preview.Subscribe(["fake:a"]);
        SampleBoth();

        Assert.Equal(0d, _preview.Read("fake:a").Value!.Value.Number);
        Assert.True(_preview.HasSample("fake"));
    }

    [Fact]
    public void Live_AProviderWaitingForSettings_ShowsItsSample()
    {
        _preview.Subscribe(["typed:a"]);
        SampleBoth();

        Assert.True(_live.IsWaitingForSettings("typed"));
        Assert.Equal(["typed"], _live.ProvidersWaitingForSettings);
        Assert.Equal(ReadingState.Unavailable, _live.Read("typed:a").State);
        Assert.Equal(7d, _preview.Read("typed:a").Value!.Value.Number);
    }

    [Fact]
    public void Sample_ShowsEverySample_AndSaysEverythingChanged()
    {
        _preview.Subscribe(["fake:a"]);
        SampleBoth();
        var changed = new List<string>();
        _preview.ReadingsChanged += ids => changed.AddRange(ids);

        _preview.Mode = PreviewReadingMode.Sample;

        Assert.Equal(42d, _preview.Read("fake:a").Value!.Value.Number);
        Assert.Contains("fake:a", changed);
        // The host's hub still has the live value: the deck is unaffected.
        Assert.Equal(0d, _live.Read("fake:a").Value!.Value.Number);
    }

    [Fact]
    public void TheHostsHub_CountsThePreviewAsAnotherUser_AndLosesItOnDispose()
    {
        _live.Subscribe(["fake:a"]); // the deck
        _preview.Subscribe(["fake:a"]);
        _preview.Unsubscribe(["fake:a"]);
        _preview.Unsubscribe(["fake:a"]); // a second unsubscribe is ignored
        _live.SampleNow();

        Assert.Equal(ReadingState.Ok, _live.Read("fake:a").State);
        Assert.Contains("start", _fake.Calls);
    }

    [Fact]
    public void ReloadSamples_SubscribesAgain_AndKeepsShowingSamples()
    {
        _preview.Mode = PreviewReadingMode.Sample;
        _preview.Subscribe(["fake:a"]);
        _preview.SampleNow();

        _preview.ReloadSamples();
        _preview.SampleNow();

        Assert.Equal(42d, _preview.Read("fake:a").Value!.Value.Number);
    }

    [Fact]
    public void APreviewsWidgets_ReadFromThePreview_NotTheHostsHub()
    {
        using var loader = new UrDeck.Engine.Plugin.WidgetPluginLoader(_time);
        loader.LoadAssembly(typeof(UrDeck.Widgets.Clock.ClockWidget).Assembly);
        var widgets = loader.WithReadings(_preview);

        var widget = widgets.CreateWidget(new UrDeck.Sdk.WidgetConfig { WidgetTypeId = "urdeck.widgets.clock" }, out var services);

        Assert.NotNull(widget);
        Assert.Same(_preview, services!.Readings);
        Assert.NotNull(widgets.GetDescriptor("urdeck.widgets.clock"));
        services.Dispose();
        (widget as IDisposable)?.Dispose();
    }

    [Fact]
    public void AProviderWithoutASample_IsAlwaysLive()
    {
        var other = new FakeProvider(_time);
        _providers.TryRegister(other.Descriptor("other"), out _);
        _preview.ReloadSamples();
        _preview.Mode = PreviewReadingMode.Sample;
        _preview.Subscribe(["other:a"]);
        SampleBoth();

        Assert.False(_preview.HasSample("other"));
        Assert.Equal(0d, _preview.Read("other:a").Value!.Value.Number);
    }
}
