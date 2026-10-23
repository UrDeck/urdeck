// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Plugin;
using UrDeck.Sdk;
using UrDeck.Widgets.Shortcut;
using UrDeck.Widgets.Stats;
using UrDeck.Widgets.Weather;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The sizes generated pictures show (widget-sdk, "Widget Metadata Attributes") and the first-party widgets' default
/// configurations (widget-sdk, "Default Configuration Draws Something"); the pictures of the defaults are in PreviewTests.
/// </summary>
public sealed class WidgetDefaultsTests
{
    [Widget("Rows", "Repeats rows", Id = "test.rows")]
    [WidgetSize(4, 2)]
    [WidgetSize(4, 8, Preview = false)]
    [RefreshOnData]
    private sealed class RowsWidget : Widget<WidgetConfig>
    {
        public override void Render(WidgetRenderContext context)
        {
        }
    }

    [Fact]
    public void ASizeLeftOutOfPreviews_CanStillBePlaced_ButIsNotPreviewed()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(RowsWidget), out string? reason);

        Assert.True(descriptor != null, reason);
        Assert.Equal([new System.Drawing.Size(4, 2), new System.Drawing.Size(4, 8)], descriptor!.SupportedSizes);
        Assert.Equal([new System.Drawing.Size(4, 2)], descriptor.PreviewSizes);
    }

    [Fact]
    public void WithoutTheFlag_EverySizeIsPreviewed()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(StatsWidget), out _)!;

        Assert.Equal(descriptor.SupportedSizes, descriptor.PreviewSizes);
    }

    [Fact]
    public void TheStatsDefault_ShowsCpuGpuMemoryAndGpuTemperature()
    {
        var slots = new StatsWidget().DefaultConfig.Slots;

        Assert.Equal(["system:cpu/load", "system:gpu/load", "system:memory/load", "system:gpu/temperature"], slots.Select(s => s.Reading));
        Assert.Equal("RAM", slots[2].Label);
    }

    [Fact]
    public void TheWeatherDefault_NamesAPlace() =>
        Assert.Equal("Portland, OR", new WeatherWidget().DefaultConfig.Location);

    [Fact]
    public void TheShortcutDefault_IsAWebAddressWithALabel()
    {
        var config = new ShortcutWidget().DefaultConfig;

        Assert.Equal("https://urdeck.app", config.Target);
        Assert.Equal("UrDeck", config.Label);
    }

    [Fact]
    public void ADefault_IsANewObjectEachTime() =>
        Assert.NotSame(new StatsWidget().DefaultConfig.Slots, new StatsWidget().DefaultConfig.Slots);

    /// <summary>A setting absent from the file keeps the configuration type's own default, not the widget's DefaultConfig.</summary>
    [Fact]
    public void AConfigurationFileWithoutTheSettings_LoadsAsBefore()
    {
        var stats = Concrete<StatsConfig>("""{ "typeId": "urdeck.widgets.stats", "width": 4, "height": 2 }""");
        var weather = Concrete<WeatherConfig>("""{ "typeId": "urdeck.widgets.weather", "width": 4, "height": 2 }""");
        var shortcut = Concrete<ShortcutConfig>("""{ "typeId": "urdeck.widgets.shortcut" }""");

        Assert.Empty(stats.Slots);
        Assert.Null(weather.Location);
        Assert.Null(shortcut.Target);
        Assert.Null(shortcut.Label);
    }

    private static T Concrete<T>(string json)
        where T : WidgetConfig =>
        (T)JsonSerializer.Deserialize<WidgetConfig>(json, Options)!.ToConcrete(typeof(T));

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}
