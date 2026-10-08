// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Widgets.Stats;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The stats widget's gauges, compositions and motion (stats-gauges).</summary>
public sealed class StatsGaugeTests : IDisposable
{
    private sealed class FakeSource : IReadingSource
    {
        public Dictionary<string, Reading> Readings { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ReadingDescriptor> Catalog { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Reading Read(string id) => Readings.TryGetValue(id, out var r) ? r : Reading.Unavailable("unknown");

        public ReadingDescriptor? Describe(string id) => Catalog.TryGetValue(id, out var d) ? d : null;

        public bool RegionUsesFahrenheit => false;
    }

    private sealed class Host(IReadingSource readings) : IWidgetHost
    {
        public IReadingSource Readings { get; } = readings;
    }

    private const string Core2 = "system:cpu/core/2/load";
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly FakeSource _source = new();

    public StatsGaugeTests()
    {
        _source.Catalog[Core2] = new ReadingDescriptor("cpu/core/2/load", ReadingKind.Percent, "Core 2", "CPU core 2 load") { Min = 0, Max = 100 };
    }

    public void Dispose() => _theme.Dispose();

    private StatsWidget Sized(int width, int height, params StatsSlot[] slots)
    {
        var widget = new StatsWidget();
        widget.Attach(new Host(_source));
        widget.Configure(new StatsConfig { WidgetTypeId = "urdeck.widgets.stats", Width = width, Height = height, Slots = [.. slots] });
        return widget;
    }

    private StatsWidget One(params StatsSlot[] slots) => Sized(1, 1, slots);

    private SKBitmap PaintAt(StatsWidget widget, DateTime time, int width = 275, int height = 275)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        float pad = _theme.Padding;
        widget.Render(new WidgetRenderContext(
            canvas, time, new System.Drawing.Size(width, height), _theme, new SKRect(pad, pad, width - pad, height - pad), widget.Config, CancellationToken.None));
        return bitmap;
    }

    private static StatsSlot Slot(string reading, string? style = null, double? warning = null, double? critical = null) =>
        new() { Reading = reading, Style = style, Warning = warning, Critical = critical };

    private static int Count(SKBitmap bitmap, SKColor color)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y) == color)
                    count++;
        return count;
    }

    private static SKRectI Ink(SKBitmap bitmap, SKColor color)
    {
        var box = new SKRectI(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != color)
                    continue;
                box = new SKRectI(Math.Min(box.Left, x), Math.Min(box.Top, y), Math.Max(box.Right, x), Math.Max(box.Bottom, y));
            }
        }

        return box;
    }

    private static bool SameImage(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    private StatsSlot[] CoreSlots(int count, double value = 50)
    {
        var slots = Enumerable.Range(0, count).Select(i => Slot($"system:cpu/core/{i}/load")).ToArray();
        foreach (var slot in slots)
        {
            _source.Readings[slot.Reading] = Reading.Ok(value);
            _source.Catalog[slot.Reading] = new ReadingDescriptor(slot.Reading, ReadingKind.Percent, "C", "C");
        }

        return slots;
    }

    [Fact]
    public void Subscriptions_AreTheReadingsOfTheShownPositions()
    {
        var slots = CoreSlots(7);

        Assert.Equal(["system:cpu/core/0/load"], One(slots).Subscriptions);
        Assert.Equal(5, Sized(4, 2, slots).Subscriptions.Count);
        Assert.Equal(7, Sized(4, 4, slots).Subscriptions.Count);
        Assert.Equal(["system:cpu/load"], Sized(4, 4).Subscriptions);
    }

    [Fact]
    public void ASlotWithoutAStyle_DrawsAsBefore_AtOneByOne()
    {
        _source.Readings[Core2] = Reading.Ok(37);
        using var old = PaintAt(One(Slot(Core2)), T0);
        using var explicitPlain = PaintAt(One(Slot(Core2, "plain")), T0);
        using var unknown = PaintAt(One(Slot(Core2, "disc")), T0);

        Assert.True(SameImage(old, explicitPlain));
        Assert.True(SameImage(old, unknown));
        Assert.Equal(0, Count(old, _theme.Accent));
    }

    [Theory]
    [InlineData("ring")]
    [InlineData("bar")]
    [InlineData("verticalBar")]
    [InlineData("gauge")]
    public void AGaugeStyle_DrawsTheAccentFill_AtOneByOne(string style)
    {
        _source.Readings[Core2] = Reading.Ok(37);
        using var bitmap = PaintAt(One(Slot(Core2, style)), T0);

        Assert.True(Count(bitmap, _theme.Accent) > 0);
    }

    [Fact]
    public void AnUnknownStyleAtAGaugePosition_ShowsTheThemesDefault()
    {
        _source.Readings[Core2] = Reading.Ok(37);
        using var unknown = PaintAt(Sized(4, 2, Slot(Core2, "disc")), T0, 1100, 550);
        using var absent = PaintAt(Sized(4, 2, Slot(Core2)), T0, 1100, 550);

        Assert.True(SameImage(unknown, absent));
        Assert.True(Count(absent, _theme.Accent) > 0);
    }

    [Fact]
    public void TextReadings_AreDrawnPlain_EvenWhenAGaugeIsAsked()
    {
        const string name = "system:cpu/name";
        _source.Readings[name] = Reading.Ok("Ryzen");
        _source.Catalog[name] = new ReadingDescriptor("cpu/name", ReadingKind.Text, "CPU", "CPU name");
        using var ring = PaintAt(One(Slot(name, "ring")), T0);
        using var plain = PaintAt(One(Slot(name, "plain")), T0);

        Assert.True(SameImage(ring, plain));
    }

    [Fact]
    public void ASlotsWarningAndCritical_ChangeTheFillColour_AndTheNumberKeepsTheTextColour()
    {
        _source.Readings[Core2] = Reading.Ok(60);
        using var normal = PaintAt(One(Slot(Core2, "ring")), T0);
        using var warning = PaintAt(One(Slot(Core2, "ring", warning: 50)), T0);
        using var critical = PaintAt(One(Slot(Core2, "ring", warning: 50, critical: 60)), T0);

        Assert.True(Count(normal, _theme.Accent) > 0);
        Assert.Equal(0, Count(warning, _theme.Accent));
        Assert.True(Count(warning, _theme.Warning) > 0);
        Assert.True(Count(critical, _theme.Critical) > 0);
        Assert.True(Count(critical, _theme.Text) > 0);
    }

    [Fact]
    public void AnUnavailableReading_AsARing_IsADashOverAnEmptyTrack()
    {
        _source.Readings[Core2] = Reading.Unavailable("gone");
        using var bitmap = PaintAt(One(Slot(Core2, "ring")), T0);

        Assert.Equal(0, Count(bitmap, _theme.Accent));
        Assert.Equal(0, Count(bitmap, _theme.Text));
        Assert.True(Count(bitmap, _theme.TextMuted.WithAlpha(_theme.AccentDim.Alpha)) > 0);
    }

    [Fact]
    public void FourByFour_DrawsFourGaugesAndATextRow()
    {
        var slots = CoreSlots(7);
        using var full = PaintAt(Sized(4, 4, slots), T0, 1100, 1100);
        using var two = PaintAt(Sized(4, 4, slots[..2]), T0, 1100, 1100);

        // Slots 5 to 7 are text stats under the gauges, and slots 3 and 4 are the second row of gauges.
        Assert.True(Ink(full, _theme.Text).Bottom > Ink(two, _theme.Text).Bottom);
        Assert.True(Ink(full, _theme.Accent).Bottom > Ink(two, _theme.Accent).Bottom);
    }

    [Fact]
    public void FourByTwo_ShowsFivePositions_AndKeepsTheOtherSlots()
    {
        var slots = CoreSlots(7);
        var widget = Sized(4, 2, slots);
        using var _ = PaintAt(widget, T0, 1100, 550);

        Assert.Equal(5, widget.Subscriptions.Count);
        Assert.Equal(7, widget.Config.Slots.Count);
    }

    [Fact]
    public void TheTextRow_IsTheSameHeightAtFourByTwoAndFourByFour()
    {
        var slots = CoreSlots(7);
        using var two = PaintAt(Sized(4, 2, slots), T0, 1100, 550);
        using var four = PaintAt(Sized(4, 4, slots), T0, 1100, 1100);

        // The text row is the bottom half cell of the content; the digits there are the same size in both.
        Assert.Equal(DigitHeightInTextRow(two), DigitHeightInTextRow(four));
    }

    private int DigitHeightInTextRow(SKBitmap bitmap)
    {
        int rowTop = bitmap.Height - (int)_theme.Padding - 275 / 2 + 2;
        int top = int.MaxValue, bottom = int.MinValue;
        for (int y = rowTop; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != _theme.Text)
                    continue;
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return bottom - top;
    }

    [Fact]
    public void GaugesInOneCard_ShareOneTextSize()
    {
        _source.Readings[Core2] = Reading.Ok(50);
        _source.Catalog["t:temp"] = new ReadingDescriptor("temp", ReadingKind.Temperature, "Temp", "Temp") { Min = 0, Max = 1000 };
        _source.Readings["t:temp"] = Reading.Ok(50);
        using var same = PaintAt(Sized(4, 2, Slot(Core2), Slot(Core2)), T0, 1100, 550);
        using var mixed = PaintAt(Sized(4, 2, Slot(Core2), Slot("t:temp")), T0, 1100, 550);

        // The temperature's wider widest value pulls the percentage gauge down to the same size, so the mixed card's digits are smaller.
        Assert.True(Ink(mixed, _theme.Text).Height < Ink(same, _theme.Text).Height);
    }

    [Fact]
    public void NeedsRender_IgnoresSlotsThatAreNotShown()
    {
        _source.Readings[Core2] = Reading.Ok(37);
        _source.Readings["system:memory/load"] = Reading.Ok(10);
        var widget = One(Slot(Core2), Slot("system:memory/load"));
        using var _ = PaintAt(widget, T0);

        _source.Readings["system:memory/load"] = Reading.Ok(90);

        Assert.False(widget.NeedsRender(T0));
    }

    [Fact]
    public void NeedsRender_IsFalseWhenAGaugesRoundedValueIsUnchanged_AndTrueWhenItChanges()
    {
        _source.Readings[Core2] = Reading.Ok(37.2);
        var widget = One(Slot(Core2, "ring"));
        using var _ = PaintAt(widget, T0);

        _source.Readings[Core2] = Reading.Ok(37.4);
        Assert.False(widget.NeedsRender(T0));

        _source.Readings[Core2] = Reading.Ok(40);
        Assert.True(widget.NeedsRender(T0));
    }

    [Fact]
    public void NeedsRender_IsTrueWhenOnlyTheLevelChanges()
    {
        _source.Readings[Core2] = Reading.Ok(40);
        var widget = One(Slot(Core2, "ring", warning: 50));
        using var _ = PaintAt(widget, T0);

        // The fraction moves too, but a slot threshold at the same fraction would still be seen.
        widget.Config.Slots[0].Warning = 30;

        Assert.True(widget.NeedsRender(T0));
    }

    [Fact]
    public void TheFirstPaintIsAtRest_AndAChangedFractionAnimatesForThreeHundredMilliseconds()
    {
        _source.Readings[Core2] = Reading.Ok(20);
        var widget = One(Slot(Core2, "ring"));
        using var first = PaintAt(widget, T0);
        Assert.False(widget.IsAnimating);

        _source.Readings[Core2] = Reading.Ok(60);
        Assert.True(widget.NeedsRender(T0));
        using var start = PaintAt(widget, T0);
        Assert.True(widget.IsAnimating);

        using var middle = PaintAt(widget, T0.AddMilliseconds(100));
        Assert.True(widget.IsAnimating);
        using var end = PaintAt(widget, T0.AddMilliseconds(300));
        Assert.False(widget.IsAnimating);

        Assert.True(Count(start, _theme.Accent) < Count(middle, _theme.Accent));
        Assert.True(Count(middle, _theme.Accent) < Count(end, _theme.Accent));

        // The ease ends on the picture a card that was never at 20 draws at rest, and the number was 60 from the first frame.
        _source.Readings[Core2] = Reading.Ok(60);
        using var rest = PaintAt(One(Slot(Core2, "ring")), T0);
        Assert.True(SameImage(rest, end));
        Assert.Equal(Count(rest, _theme.Text), Count(start, _theme.Text));
    }

    [Fact]
    public void ANoChange_NeitherRepaintsNorAnimates()
    {
        _source.Readings[Core2] = Reading.Ok(20);
        var widget = One(Slot(Core2, "ring"));
        using var _ = PaintAt(widget, T0);

        _source.Readings[Core2] = Reading.Ok(20.3);

        Assert.False(widget.NeedsRender(T0.AddMinutes(1)));
        Assert.False(widget.IsAnimating);
    }

    [Fact]
    public void ANewTargetDuringAnEase_ContinuesFromTheDrawnFraction()
    {
        _source.Readings[Core2] = Reading.Ok(0);
        var widget = One(Slot(Core2, "bar"));
        using var _ = PaintAt(widget, T0);
        _source.Readings[Core2] = Reading.Ok(100);
        using var __ = PaintAt(widget, T0);
        using var halfway = PaintAt(widget, T0.AddMilliseconds(100));
        int before = Count(halfway, _theme.Accent);

        // The target changes while the fill is partway; the next frame starts from where the fill is, not at an end.
        _source.Readings[Core2] = Reading.Ok(50);
        using var retarget = PaintAt(widget, T0.AddMilliseconds(100));

        Assert.True(Math.Abs(Count(retarget, _theme.Accent) - before) < before / 20);
        Assert.True(widget.IsAnimating);
    }

    [Fact]
    public void APlainCard_NeverAnimates()
    {
        _source.Readings[Core2] = Reading.Ok(20);
        var widget = One(Slot(Core2));
        using var _ = PaintAt(widget, T0);
        _source.Readings[Core2] = Reading.Ok(60);
        Assert.True(widget.NeedsRender(T0));
        using var __ = PaintAt(widget, T0);

        Assert.False(widget.IsAnimating);
    }

    [Fact]
    public void AReadingThatBecomesUnavailable_DropsToTheEmptyTrackAtOnce()
    {
        _source.Readings[Core2] = Reading.Ok(60);
        var widget = One(Slot(Core2, "ring"));
        using var _ = PaintAt(widget, T0);
        _source.Readings[Core2] = Reading.Unavailable("gone");
        using var gone = PaintAt(widget, T0);

        Assert.Equal(0, Count(gone, _theme.Accent));
        Assert.False(widget.IsAnimating);
    }

    [Fact]
    public void AFirstValue_FillsUpFromTheEmptyTrack()
    {
        _source.Readings[Core2] = Reading.Pending();
        var widget = One(Slot(Core2, "ring"));
        using var _ = PaintAt(widget, T0);
        _source.Readings[Core2] = Reading.Ok(80);
        using var start = PaintAt(widget, T0);

        Assert.Equal(0, Count(start, _theme.Accent));
        Assert.True(widget.IsAnimating);
    }

    [Fact]
    public void SlotStyleAndLimits_SurviveAConfigRoundTrip()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        const string json = """{"typeId":"urdeck.widgets.stats","slots":[{"reading":"a:b","style":"disc","min":1,"max":9,"warning":5,"critical":8}]}""";
        var config = JsonSerializer.Deserialize<StatsConfig>(json, options)!;
        var back = JsonSerializer.Deserialize<StatsConfig>(JsonSerializer.Serialize(config, options), options)!;

        var slot = back.Slots[0];
        Assert.Equal("disc", slot.Style);
        Assert.Equal((1d, 9d, 5d, 8d), (slot.Min, slot.Max, slot.Warning, slot.Critical));
    }
}
