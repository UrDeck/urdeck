// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

public class SdkContractTests
{
    [Fact]
    public void SdkAssemblyVersion_IsFrozen()
    {
        // Plugins bind to the host's UrDeck.Sdk by assembly version; a bump makes already-built plugins load a second
        // SDK copy and silently fail. Only change this together with a deliberate, announced breaking SDK change.
        var version = typeof(Widget<>).Assembly.GetName().Version;
        Assert.Equal(new Version(0, 3, 0, 0), version);
    }

    [Fact]
    public void ADescriptorBuiltWithoutLevels_HasNone()
    {
        // Providers built before warning and critical existed construct descriptors without them.
        var descriptor = new ReadingDescriptor("cpu/load", ReadingKind.Percent, "CPU", "CPU load");

        Assert.Null(descriptor.Warning);
        Assert.Null(descriptor.Critical);
    }

    [Fact]
    public void TheSystemProvidersReadings_DeclareNoLevels()
    {
        var provider = new UrDeck.Providers.Machine.SystemProvider();

        Assert.All(provider.Describe(), d => Assert.True(d.Warning == null && d.Critical == null));
    }

    [Fact]
    public void NeedsRender_DefaultsToTrue()
    {
        // A widget that does not override it is repainted on every refresh.
        IWidget widget = new PlainWidget();

        Assert.True(widget.NeedsRender(DateTime.Now));
    }

    [Fact]
    public void IsAnimating_DefaultsToFalse()
    {
        IWidget widget = new PlainWidget();

        Assert.False(widget.IsAnimating);
    }

    [Fact]
    public void Subscriptions_DefaultToEmpty()
    {
        IWidget widget = new PlainWidget();

        Assert.Empty(widget.Subscriptions);
    }

    [Fact]
    public void Attach_DefaultsToANoOp()
    {
        // A widget that implements IWidget without overriding Attach (a default interface member) accepts a host.
        IWidget widget = new BareWidget();

        widget.Attach(new Host(new Source()));
        Assert.Empty(widget.Subscriptions);
    }

    [Fact]
    public void AnUnattachedWidget_ReadsEverythingAsUnavailable()
    {
        var widget = new PlainWidget();

        var reading = widget.ReadingOf("system:cpu/load");

        Assert.Equal(ReadingState.Unavailable, reading.State);
        Assert.NotNull(reading.Reason);
        Assert.Null(widget.DescriptionOf("system:cpu/load"));
    }

    [Fact]
    public void AnAttachedWidget_ReadsThroughTheHost()
    {
        var widget = new PlainWidget();
        widget.Attach(new Host(new Source()));

        Assert.Equal(ReadingState.Ok, widget.ReadingOf("x:y").State);
    }

    private sealed class Source : IReadingSource
    {
        public Reading Read(string id) => Reading.Ok(1);

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => false;
    }

    private sealed class Host(IReadingSource readings) : IWidgetHost
    {
        public IReadingSource Readings { get; } = readings;
    }

    private sealed class BareWidget : IWidget
    {
        public string Name => "bare";
        public string Description => "";
        public string Category => "";
        public System.Drawing.Size[] SupportedSizes => [];
        public Type ConfigType => typeof(WidgetConfig);
        public WidgetConfig Config { get; private set; } = new();

        public void Configure(WidgetConfig config) => Config = config;

        public Task UpdateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Render(WidgetRenderContext context)
        {
        }
    }

    private sealed class PlainWidget : Widget<WidgetConfig>
    {
        public Reading ReadingOf(string id) => Readings.Read(id);

        public ReadingDescriptor? DescriptionOf(string id) => Readings.Describe(id);

        public override void Render(WidgetRenderContext context)
        {
        }
    }
}
