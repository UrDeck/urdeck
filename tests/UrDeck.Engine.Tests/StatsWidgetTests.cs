// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Widgets.Stats;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class StatsWidgetTests : IDisposable
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

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly FakeSource _source = new();

    public StatsWidgetTests()
    {
        _source.Catalog["system:cpu/load"] = new ReadingDescriptor("cpu/load", ReadingKind.Percent, "CPU", "CPU load") { Min = 0, Max = 100 };
        _source.Catalog[Core2] = new ReadingDescriptor("cpu/core/2/load", ReadingKind.Percent, "Core 2", "CPU core 2 load") { Min = 0, Max = 100 };
    }

    public void Dispose() => _theme.Dispose();

    private StatsWidget Widget(params StatsSlot[] slots)
    {
        var widget = new StatsWidget();
        widget.Attach(new Host(_source));
        widget.Configure(new StatsConfig { WidgetTypeId = "urdeck.widgets.stats", Slots = [.. slots] });
        return widget;
    }

    private static StatsSlot Slot(string reading, string? label = null, int? decimals = null) =>
        new() { Reading = reading, Label = label, Decimals = decimals };

    private SKBitmap Paint(StatsWidget widget)
    {
        var bitmap = new SKBitmap(275, 275);
        using var canvas = new SKCanvas(bitmap);
        float pad = _theme.Padding;
        widget.Render(new WidgetRenderContext(
            canvas, DateTime.Now, new System.Drawing.Size(275, 275), _theme, new SKRect(pad, pad, 275 - pad, 275 - pad), widget.Config, CancellationToken.None));
        return bitmap;
    }

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

    [Fact]
    public void WithNoSlots_ItShowsTheTotalCpuLoad()
    {
        var widget = Widget();

        Assert.Equal(["system:cpu/load"], widget.Subscriptions);
    }

    [Fact]
    public void ASlotNamesTheReading_AndOnlyTheFirstOneIsUsed()
    {
        var widget = Widget(Slot(Core2), Slot("system:memory/load"));

        Assert.Equal([Core2], widget.Subscriptions);
    }

    [Fact]
    public void ASlotWithoutAReadingId_FallsBackToTheTotalCpuLoad() =>
        Assert.Equal(["system:cpu/load"], Widget(Slot("  ")).Subscriptions);

    [Fact]
    public void Subscriptions_FollowTheConfigurationWhenItChanges()
    {
        var widget = Widget(Slot(Core2));
        Assert.Equal([Core2], widget.Subscriptions);

        widget.Configure(new StatsConfig { Slots = [Slot("system:memory/load")] });

        Assert.Equal(["system:memory/load"], widget.Subscriptions);
    }

    [Fact]
    public void ItDeclaresNoTimer_AndTheTwoSizes()
    {
        var descriptor = Engine.Plugin.WidgetDescriptor.TryCreate(typeof(StatsWidget), out _)!;

        Assert.Equal(Engine.Plugin.RefreshStrategy.OnData, descriptor.Refresh);
        Assert.Null(descriptor.RefreshInterval);
        Assert.Equal("urdeck.widgets.stats", descriptor.Id);
        Assert.Equal([new System.Drawing.Size(1, 1), new System.Drawing.Size(2, 2)], descriptor.SupportedSizes);
    }

    [Fact]
    public void ACurrentReading_IsDrawnInTheTextColour_WithTheCatalogLabelInTheMutedOne()
    {
        _source.Readings[Core2] = Reading.Ok(37);
        using var bitmap = Paint(Widget(Slot(Core2)));

        Assert.True(Count(bitmap, _theme.Text) > 0);
        Assert.True(Count(bitmap, _theme.TextMuted) > 0);
    }

    [Fact]
    public void TheLabel_IsTheCatalogsWhenAbsent_TheSlotsWhenSet_AndHiddenWhenEmpty()
    {
        _source.Readings[Core2] = Reading.Ok(37);
        using var catalog = Paint(Widget(Slot(Core2)));
        using var custom = Paint(Widget(Slot(Core2, "Render thread")));
        using var hidden = Paint(Widget(Slot(Core2, "")));

        Assert.False(SameImage(catalog, custom));
        Assert.Equal(0, Count(hidden, _theme.TextMuted));
        // With no label no space is kept for one: the value sits in the middle of the card instead of above it.
        var shown = Ink(catalog, _theme.Text);
        var alone = Ink(hidden, _theme.Text);
        Assert.True(Math.Abs((alone.Top + alone.Bottom) / 2 - 137) < Math.Abs((shown.Top + shown.Bottom) / 2 - 137));
    }

    [Fact]
    public void AnUnavailableReading_IsADash_InTheMutedColour_WithItsLabel()
    {
        _source.Readings[Core2] = Reading.Unavailable("gone");
        using var bitmap = Paint(Widget(Slot(Core2)));

        Assert.Equal(0, Count(bitmap, _theme.Text));
        Assert.True(Count(bitmap, _theme.TextMuted) > 0);
    }

    [Fact]
    public void AReadingThatDoesNotExist_IsADashLabelledWithItsId()
    {
        // No catalog entry and unavailable: the id is the only clue what the card was meant to show.
        _source.Readings.Clear();
        _source.Catalog.Clear();
        var widget = Widget(Slot("nosuch:thing"));
        using var bitmap = Paint(widget);

        Assert.Equal(0, Count(bitmap, _theme.Text));
        Assert.True(Count(bitmap, _theme.TextMuted) > 0);
    }

    [Fact]
    public void AStaleReading_KeepsItsValueInTheMutedColour_AtTheSameSize()
    {
        _source.Readings[Core2] = Reading.Ok(37);
        using var current = Paint(Widget(Slot(Core2)));
        _source.Readings[Core2] = Reading.Stale(37);
        using var stale = Paint(Widget(Slot(Core2)));

        Assert.Equal(0, Count(stale, _theme.Text));
        // Same glyphs, so the ink the current value left in the text colour is what the stale one leaves in the muted one
        // (the label is muted in both, so compare the value area only).
        Assert.Equal(Ink(current, _theme.Text).Height, Ink(stale, _theme.TextMuted).Height - LabelHeight(current));
    }

    private int LabelHeight(SKBitmap bitmap)
    {
        // The muted ink of a current card is the label alone.
        var label = Ink(bitmap, _theme.TextMuted);
        var value = Ink(bitmap, _theme.Text);
        return label.Bottom - value.Bottom;
    }

    [Fact]
    public void TheTextSize_DoesNotChangeWithTheValue()
    {
        _source.Readings[Core2] = Reading.Ok(8);
        using var small = Paint(Widget(Slot(Core2)));
        _source.Readings[Core2] = Reading.Ok(100);
        using var full = Paint(Widget(Slot(Core2)));

        Assert.Equal(Ink(small, _theme.Text).Height, Ink(full, _theme.Text).Height);
        Assert.Equal(Ink(small, _theme.Text).Top, Ink(full, _theme.Text).Top);
    }

    [Fact]
    public void ADash_KeepsTheLayout_OfAValue()
    {
        _source.Readings[Core2] = Reading.Ok(37);
        using var value = Paint(Widget(Slot(Core2)));
        _source.Readings[Core2] = Reading.Unavailable("gone");
        using var dash = Paint(Widget(Slot(Core2)));

        // The label sits where it did.
        var labelOfValue = Ink(value, _theme.TextMuted);
        var labelOfDash = Ink(dash, _theme.TextMuted);
        Assert.Equal(labelOfValue.Bottom, labelOfDash.Bottom);
    }

    [Fact]
    public void NeedsRender_IsTrueBeforeTheFirstPaint_AndAfterAConfigurationChange()
    {
        _source.Readings[Core2] = Reading.Ok(37);
        var widget = Widget(Slot(Core2));
        Assert.True(widget.NeedsRender(DateTime.Now));

        using var _ = Paint(widget);
        Assert.False(widget.NeedsRender(DateTime.Now));

        widget.Configure(new StatsConfig { Slots = [Slot(Core2)] });
        Assert.True(widget.NeedsRender(DateTime.Now));
    }

    [Fact]
    public void NeedsRender_IsFalseWhenTheRoundedValueIsUnchanged_AndTrueWhenItChanges()
    {
        _source.Readings[Core2] = Reading.Ok(3.2);
        var widget = Widget(Slot(Core2));
        using var _ = Paint(widget);

        _source.Readings[Core2] = Reading.Ok(3.4);
        Assert.False(widget.NeedsRender(DateTime.Now));

        _source.Readings[Core2] = Reading.Ok(4.1);
        Assert.True(widget.NeedsRender(DateTime.Now));
    }

    [Fact]
    public void NeedsRender_IsTrueOnceWhenTheReadingGoesStaleWithTheSameValue()
    {
        _source.Readings[Core2] = Reading.Ok(37);
        var widget = Widget(Slot(Core2));
        using var _ = Paint(widget);

        _source.Readings[Core2] = Reading.Stale(37);
        Assert.True(widget.NeedsRender(DateTime.Now));

        using var __ = Paint(widget);
        Assert.False(widget.NeedsRender(DateTime.Now));
    }

    [Fact]
    public void NeedsRender_IsTrueWhenTheCatalogLabelArrives()
    {
        _source.Catalog.Remove(Core2);
        _source.Readings[Core2] = Reading.Pending();
        var widget = Widget(Slot(Core2));
        using var _ = Paint(widget);
        Assert.False(widget.NeedsRender(DateTime.Now));

        _source.Catalog[Core2] = new ReadingDescriptor("cpu/core/2/load", ReadingKind.Percent, "Core 2", "CPU core 2 load");

        Assert.True(widget.NeedsRender(DateTime.Now));
    }

    [Fact]
    public void ASlotsDecimals_AreHonoured()
    {
        _source.Readings[Core2] = Reading.Ok(3.26);
        var widget = Widget(Slot(Core2, decimals: 1));
        using var whole = Paint(Widget(Slot(Core2)));
        using var tenths = Paint(widget);

        Assert.False(SameImage(whole, tenths));
    }

    // Configuration

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private const string ThreeSlots =
        """{"typeId":"urdeck.widgets.stats","col":1,"row":2,"slots":[{"reading":"system:cpu/load","label":""},{"reading":"system:memory/load","extra":{"x":1}},{"reading":"system:cpu/core/3/load","unit":"celsius","decimals":2}]}""";

    [Fact]
    public void ExtraSlots_SurviveAConfigRoundTrip_WithTheirUnknownProperties()
    {
        var concrete = JsonSerializer.Deserialize<StatsConfig>(ThreeSlots, Json)!;
        string again = JsonSerializer.Serialize(concrete, Json);
        var back = JsonSerializer.Deserialize<StatsConfig>(again, Json)!;

        Assert.Equal(3, back.Slots.Count);
        Assert.Equal("", back.Slots[0].Label);
        Assert.Null(back.Slots[1].Label);
        Assert.True(back.Slots[1].ExtensionData!.ContainsKey("extra"));
        Assert.Equal("celsius", back.Slots[2].Unit);
        Assert.Equal(2, back.Slots[2].Decimals);
    }

    [Fact]
    public void TheSlotListSurvivesAPageFileSaveAndLoad()
    {
        string dir = Path.Combine(Path.GetTempPath(), "urdeck-stats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "urdeck-config.json");
            File.WriteAllText(path, $$"""{"pages":[{"name":"p","widgets":[{{ThreeSlots}}]}]}""");
            using var store = new ConfigStore(path);

            store.Save();

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var slots = doc.RootElement.GetProperty("pages")[0].GetProperty("widgets")[0].GetProperty("slots");
            Assert.Equal(3, slots.GetArrayLength());
            Assert.True(slots[1].TryGetProperty("extra", out _));

            var concrete = (StatsConfig)store.Config.Pages[0].Widgets[0].ToConcrete(typeof(StatsConfig));
            Assert.Equal(3, concrete.Slots.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
