// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using UrDeck.Widgets.Stats;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The stats card draws the credit a reading's description asks for (see widget-stats, "Stats Attribution").</summary>
public sealed class StatsAttributionTests : IDisposable
{
    private static readonly ReadingAttribution Credit = new("Weather data by Open-Meteo.com", "Open-Meteo.com", "https://open-meteo.com/");

    private sealed class FakeSource : IReadingSource
    {
        public Dictionary<string, Reading> Readings { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ReadingDescriptor> Catalog { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Reading Read(string id) => Readings.TryGetValue(id, out Reading r) ? r : Reading.Unavailable("unknown");

        public ReadingDescriptor? Describe(string id) => Catalog.TryGetValue(id, out ReadingDescriptor? d) ? d : null;

        public bool RegionUsesFahrenheit => false;
    }

    private sealed class Host(IReadingSource readings) : IWidgetHost
    {
        public IReadingSource Readings { get; } = readings;
    }

    private const string Zurich = "weather:Zurich/current/temperature";

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly FakeSource _source = new();

    public StatsAttributionTests()
    {
        _source.Catalog["system:cpu/load"] = new ReadingDescriptor("cpu/load", ReadingKind.Percent, "CPU", "CPU load") { Min = 0, Max = 100 };
        _source.Readings["system:cpu/load"] = Reading.Ok(37);
        AddWeather(Zurich, "Now", 14, Credit);
        AddWeather("weather:Zurich/today/high", "High", 17, Credit);
        AddWeather("weather:Tokyo/current/temperature", "Tokyo", 21, new ReadingAttribution("Data by Somebody Else"));
    }

    public void Dispose() => _theme.Dispose();

    private void AddWeather(string id, string label, double value, ReadingAttribution? credit)
    {
        _source.Catalog[id] = new ReadingDescriptor(id["weather:".Length..], ReadingKind.Temperature, label, label) { Min = -60, Max = 60, Attribution = credit };
        _source.Readings[id] = Reading.Ok(value);
    }

    private void SetCredit(string id, ReadingAttribution? credit) => _source.Catalog[id] = _source.Catalog[id] with { Attribution = credit };

    private StatsWidget Widget(int width, int height, params string[] readings)
    {
        var widget = new StatsWidget();
        widget.Attach(new Host(_source));
        widget.Configure(new StatsConfig
        {
            WidgetTypeId = "urdeck.widgets.stats",
            Width = width,
            Height = height,
            Slots = [.. readings.Select(r => new StatsSlot { Reading = r })],
        });
        return widget;
    }

    private SKBitmap Paint(StatsWidget widget)
    {
        const int cell = 275;
        int width = widget.Config.Width * cell;
        int height = widget.Config.Height * cell;
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        float pad = _theme.Padding;
        widget.Render(new WidgetRenderContext(
            canvas, DateTime.Now, new System.Drawing.Size(width, height), _theme, new SKRect(pad, pad, width - pad, height - pad), widget.Config, CancellationToken.None));
        return bitmap;
    }

    /// <summary>The smallest rectangle that holds every pixel that is not transparent inside <paramref name="area"/>.</summary>
    private static SKRectI Ink(SKBitmap bitmap, SKRectI area)
    {
        var box = new SKRectI(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        for (int y = area.Top; y < area.Bottom; y++)
        {
            for (int x = area.Left; x < area.Right; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                    continue;
                box = new SKRectI(Math.Min(box.Left, x), Math.Min(box.Top, y), Math.Max(box.Right, x), Math.Max(box.Bottom, y));
            }
        }

        return box.Left == int.MaxValue ? SKRectI.Empty : box;
    }

    private static bool Same(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    private float Strip(float cardWidth, params ReadingAttribution[] credits) => Attribution.Measure(_theme, credits, cardWidth - _theme.Padding * 2);

    [Fact]
    public void AWeatherReadingAt1x1_ShowsTheCreditAtTheBottomOfTheContent()
    {
        StatsWidget widget = Widget(1, 1, Zurich);
        using SKBitmap bitmap = Paint(widget);

        float strip = Strip(275, Credit);
        float bottom = 275 - _theme.Padding;
        SKRectI ink = Ink(bitmap, new SKRectI(0, (int)(bottom - strip), 275, (int)bottom + 1));
        Assert.NotEqual(SKRectI.Empty, ink);
        Assert.True(ink.Left >= _theme.Padding - 1 && ink.Right <= bottom + 1, "the credit fits the content width");
    }

    [Fact]
    public void TheReading_StaysAboveTheCredit_AndUsesTheSpaceWhenThereIsNone()
    {
        using SKBitmap withCredit = Paint(Widget(1, 1, Zurich));
        SetCredit(Zurich, null);
        using SKBitmap without = Paint(Widget(1, 1, Zurich));

        float strip = Strip(275, Credit);
        int creditTop = (int)(275 - _theme.Padding - strip);
        SKRectI reading = Ink(withCredit, new SKRectI(0, 0, 275, creditTop));
        SKRectI alone = Ink(without, new SKRectI(0, 0, 275, 275));
        Assert.NotEqual(SKRectI.Empty, reading);
        Assert.True(reading.Bottom < creditTop, "the reading ends above the credit");
        Assert.True(alone.Bottom > reading.Bottom, "with no credit the reading sits lower, in the space the strip would take");
    }

    [Fact]
    public void TwoWeatherReadings_ShowTheCreditOnce()
    {
        StatsWidget widget = Widget(4, 2, Zurich, "weather:Zurich/today/high");
        using SKBitmap bitmap = Paint(widget);

        float one = Strip(1100, Credit);
        Assert.Equal(one, Strip(1100, Credit, Credit));
        float bottom = 550 - _theme.Padding;
        SKRectI ink = Ink(bitmap, new SKRectI(0, (int)(bottom - one), 1100, (int)bottom + 1));
        Assert.NotEqual(SKRectI.Empty, ink);
        Assert.True(ink.Height <= one, "one line of credit");
    }

    [Fact]
    public void ReadingsWithDifferentCredits_ShowBothOfThem()
    {
        StatsWidget widget = Widget(4, 2, Zurich, "weather:Tokyo/current/temperature");
        using SKBitmap bitmap = Paint(widget);

        float one = Strip(1100, Credit);
        float two = Strip(1100, Credit, new ReadingAttribution("Data by Somebody Else"));
        Assert.True(two > one);
        float bottom = 550 - _theme.Padding;
        SKRectI ink = Ink(bitmap, new SKRectI(0, (int)(bottom - two), 1100, (int)bottom + 1));
        Assert.True(ink.Height > one, "two lines of credit");
    }

    [Fact]
    public void ASystemCard_ReservesNoStrip_AndDrawsNothingAtTheBottom()
    {
        StatsWidget widget = Widget(1, 1, "system:cpu/load");
        using SKBitmap bitmap = Paint(widget);

        float bottom = 275 - _theme.Padding;
        Assert.Equal(SKRectI.Empty, Ink(bitmap, new SKRectI(0, (int)bottom - 3, 275, 275)));
        Assert.NotEqual(SKRectI.Empty, Ink(bitmap, new SKRectI(0, 0, 275, 275)));
    }

    [Fact]
    public void ACreditThatAppearsLater_AsksForARepaint()
    {
        SetCredit(Zurich, null);
        StatsWidget widget = Widget(1, 1, Zurich);
        using SKBitmap before = Paint(widget);
        Assert.False(widget.NeedsRender(DateTime.Now));

        SetCredit(Zurich, Credit);

        Assert.True(widget.NeedsRender(DateTime.Now));
        using SKBitmap after = Paint(widget);
        Assert.False(Same(before, after));
    }
}
