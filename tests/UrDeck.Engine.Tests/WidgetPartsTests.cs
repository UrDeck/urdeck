// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using UrDeck.Widgets.Clock;
using UrDeck.Widgets.Clock.Config;
using UrDeck.Widgets.Stats;
using UrDeck.Widgets.Weather;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The theme's parts (theme-widget-parts): the values on the theme, the four components, and the widgets that use them.</summary>
public sealed class WidgetPartsTests : IDisposable
{
    private const string PartsTheme = """
        {
          "divider": { "style": "gradient", "color": "#ffd9b86c", "gradientTo": "#00d9b86c", "thickness": 0.02 },
          "chip": { "shape": "pill", "fill": "#ffffd84a", "border": "#ff111111", "textColor": "#ff111111", "colorBy": "none" },
          "well": { "fill": "#ff336699", "radius": 0.09, "border": "#ffee3344" },
          "gauge": {
            "sparkline": { "enabled": true, "height": 0.3, "historySeconds": 10, "strokeWidth": 0.014 },
            "status": { "enabled": true, "thresholds": [0.7, 0.9], "labels": ["OK", "WARM", "HOT"] }
          }
        }
        """;

    private const string StatusTheme = """
        {
          "chip": { "shape": "none", "colorBy": "status" },
          "gauge": {
            "sparkline": { "enabled": true, "height": 0.3, "historySeconds": 10, "strokeWidth": 0.014 },
            "status": { "enabled": true, "thresholds": [0.7, 0.9], "labels": ["OK", "WARM", "HOT"] }
          }
        }
        """;

    private static readonly System.Text.Json.JsonSerializerOptions Web = new(System.Text.Json.JsonSerializerDefaults.Web);

    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-parts-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;
    private readonly List<Theme> _themes = [];

    public WidgetPartsTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        foreach (var theme in _themes)
            theme.Dispose();
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private Theme Resolve(string json, float cellPx = 275)
    {
        string dir = Path.Combine(_root, "themes", "probe");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ThemeStore.SettingsFileName), json);
        var theme = ThemeResolver.Resolve(new ThemeStore(Path.Combine(_root, "themes")).Load("probe"), cellPx);
        _themes.Add(theme);
        return theme;
    }

    private Theme Builtin(string name, float cellPx = 275)
    {
        var theme = ThemeResolver.Resolve(new ThemeStore("").Load(name), cellPx);
        _themes.Add(theme);
        return theme;
    }

    private static SKBitmap Draw(int width, int height, Action<SKCanvas> draw)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        draw(canvas);
        return bitmap;
    }

    private static int Count(SKBitmap bitmap, Func<SKColor, bool> match)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (match(bitmap.GetPixel(x, y)))
                    count++;
        return count;
    }

    private static bool Same(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    // The theme's values

    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("glass")]
    public void AV1Theme_GivesPartValuesThatChangeNothing(string name)
    {
        var theme = Builtin(name);

        Assert.Equal(ChipShape.None, theme.Chip.Shape);
        Assert.False(theme.Chip.ColorByStatus);
        Assert.Null(theme.Well);
        Assert.Null(theme.Gauge.Sparkline);
        Assert.Null(theme.Gauge.Status);
        Assert.True(theme.Gauge.IsEmpty);

        // The divider is the rule the Weather card drew before the component existed.
        Assert.Equal(DividerStyle.Line, theme.Divider.Style);
        Assert.Equal(theme.TextMuted.WithAlpha(0x55), theme.Divider.Color);
        Assert.Equal(Math.Max(1f, theme.Gap * 0.15f), theme.Divider.Thickness);
        Assert.Null(theme.Divider.GradientTo);
    }

    [Fact]
    public void AThemeWithNoDividerInTheSdk_HasTheSameDefaultAsTheResolver()
    {
        using var bare = new Theme();
        var resolved = Builtin("default-dark");

        Assert.Equal(DividerStyle.Line, bare.Divider.Style);
        Assert.Equal(bare.TextMuted.WithAlpha(0x55), bare.Divider.Color);
        Assert.Equal(Math.Max(1f, bare.Gap * 0.15f), bare.Divider.Thickness);
        Assert.Equal(DividerStyle.Line, resolved.Divider.Style);
    }

    [Fact]
    public void ThePartValues_AreInPixelsOfTheSurface()
    {
        var theme = Resolve(PartsTheme, 275);

        Assert.Equal(0.09f * 275, theme.Well!.Radius, 3);
        Assert.Equal(0.02f * 275, theme.Divider.Thickness, 3);
        Assert.Equal(ChipShape.Pill, theme.Chip.Shape);
        Assert.False(theme.Chip.ColorByStatus);
        Assert.Equal(0.3f * 275, theme.Gauge.Sparkline!.Height, 3);
        Assert.Equal(0.014f * 275, theme.Gauge.Sparkline.StrokeWidth, 3);
        Assert.Equal(10, theme.Gauge.Sparkline.HistorySeconds);
        Assert.Equal(0.9f * 275, theme.Gauge.Sparkline.MinRectHeight, 3);
        Assert.Equal(["OK", "WARM", "HOT"], theme.Gauge.Status!.Labels);
        Assert.Equal(0.7f, theme.Gauge.Status.WarmAt);
        Assert.Equal(0.9f, theme.Gauge.Status.HotAt);
    }

    [Fact]
    public void AGaugeGroupThatIsNotEnabled_GivesNothing()
    {
        var theme = Resolve("""{ "gauge": { "sparkline": { "enabled": false }, "status": { "labels": ["a", "b", "c"] } } }""");

        Assert.Null(theme.Gauge.Sparkline);
        Assert.Null(theme.Gauge.Status);
    }

    [Fact]
    public void AChipGroupWithoutColorBy_ColoursByStatus()
    {
        var theme = Resolve("""{ "chip": { "shape": "none" } }""");

        Assert.True(theme.Chip.ColorByStatus);
    }

    // Chip

    [Fact]
    public void Chip_PlainTextWithNoChipValues_IsTheThemeText()
    {
        var theme = Builtin("default-dark");
        using var plain = Draw(300, 80, c => Chip.Draw(c, theme, new SKRect(0, 0, 300, 80), "WARM"));
        using var line = Draw(300, 80, c => TextLine.Draw(c, theme, new SKRect(0, 0, 300, 80), "WARM", TextStep.Label));

        Assert.True(Same(plain, line));
        Assert.True(Count(plain, p => p == theme.Text) > 0);
    }

    [Fact]
    public void Chip_PlainTextByStatus_TakesTheLevelColour()
    {
        var theme = Resolve(StatusTheme);
        using var bitmap = Draw(300, 80, c => Chip.Draw(c, theme, new SKRect(0, 0, 300, 80), "WARM", GaugeLevel.Warning));

        Assert.True(Count(bitmap, p => p == theme.Warning) > 0);
        Assert.Equal(0, Count(bitmap, p => p == theme.Text));
    }

    [Fact]
    public void Chip_PlainTextByStatus_WithNoLevelIsTheThemeText()
    {
        var theme = Resolve(StatusTheme);
        using var bitmap = Draw(300, 80, c => Chip.Draw(c, theme, new SKRect(0, 0, 300, 80), "WARM"));

        Assert.True(Count(bitmap, p => p == theme.Text) > 0);
    }

    [Fact]
    public void Chip_Pill_DrawsFillBorderAndText()
    {
        var theme = Resolve(PartsTheme);
        var rect = new SKRect(0, 0, 600, 100);
        SKRect pill = default;
        using var bitmap = Draw(600, 100, c => pill = Chip.Draw(c, theme, rect, "PARTLY CLOUDY", step: TextStep.Body, horizontal: HorizontalAlign.Left));

        Assert.True(pill.Width > 0 && pill.Width < rect.Width);
        Assert.Equal(rect.Left, pill.Left);
        Assert.Equal(new SKColor(0xff, 0xd8, 0x4a), bitmap.GetPixel((int)pill.MidX, (int)pill.Top + 3));
        Assert.True(Count(bitmap, p => p == new SKColor(0x11, 0x11, 0x11)) > 0);
        Assert.Equal(0, bitmap.GetPixel((int)pill.Right + 5, (int)pill.MidY).Alpha);
    }

    [Fact]
    public void Chip_Pill_ShrinksToItsRectangle()
    {
        var theme = Resolve(PartsTheme);
        var rect = new SKRect(0, 0, 90, 30);
        SKRect pill = default;
        using var bitmap = Draw(120, 40, c => pill = Chip.Draw(c, theme, rect, "A VERY LONG CONDITION", step: TextStep.Body));

        Assert.True(pill.Left >= rect.Left - 0.01f && pill.Right <= rect.Right + 0.01f);
        Assert.True(pill.Top >= rect.Top - 0.01f && pill.Bottom <= rect.Bottom + 0.01f);
    }

    [Fact]
    public void Chip_PillByStatus_TintsWithTheLevel()
    {
        var theme = Resolve("""{ "chip": { "shape": "pill", "fill": "#ff00ff00", "colorBy": "status" } }""");
        var rect = new SKRect(0, 0, 400, 100);
        SKRect pill = default;
        using var bitmap = Draw(400, 100, c => pill = Chip.Draw(c, theme, rect, "HOT", GaugeLevel.Critical));

        Assert.Equal(0, Count(bitmap, p => p == new SKColor(0, 0xff, 0)));
        Assert.True(Count(bitmap, p => p == theme.Critical) > 0);
    }

    [Fact]
    public void Chip_EmptyTextOrRectangle_DrawsNothing()
    {
        var theme = Resolve(PartsTheme);
        using var bitmap = Draw(100, 100, c =>
        {
            Chip.Draw(c, theme, new SKRect(0, 0, 100, 100), "");
            Chip.Draw(c, theme, new SKRect(0, 0, 0, 100), "X");
        });

        Assert.Equal(0, Count(bitmap, p => p.Alpha != 0));
    }

    // Divider

    [Fact]
    public void Divider_Default_IsTheRuleWeatherAlwaysDrew()
    {
        foreach (string name in new[] { "default-dark", "default-light", "glass" })
        {
            var theme = Builtin(name);
            var rect = new SKRect(20, 100, 380, 100);
            using var part = Draw(400, 200, c => Divider.Draw(c, theme, rect));
            using var old = Draw(400, 200, c =>
            {
                using var rule = new SKPaint { Color = theme.TextMuted.WithAlpha(0x55), IsAntialias = true, StrokeWidth = Math.Max(1f, theme.Gap * 0.15f) };
                c.DrawLine(rect.Left, rect.Top, rect.Right, rect.Top, rule);
            });

            Assert.True(Same(part, old), name);
        }
    }

    [Fact]
    public void Divider_Gradient_FadesAlongTheRule()
    {
        var theme = Resolve(PartsTheme);
        using var bitmap = Draw(400, 100, c => Divider.Draw(c, theme, new SKRect(0, 50, 400, 50)));

        byte left = bitmap.GetPixel(10, 50).Alpha;
        byte middle = bitmap.GetPixel(200, 50).Alpha;
        byte right = bitmap.GetPixel(390, 50).Alpha;
        Assert.True(left > 200);
        Assert.True(middle < left && middle > right);
        Assert.True(right < 30);
    }

    [Fact]
    public void Divider_Dimension_HasATickAtEachEndAndNothingElse()
    {
        var theme = Resolve("""{ "divider": { "style": "dimension", "color": "#ffffd34d", "thickness": 0.01 } }""");
        using var bitmap = Draw(400, 100, c => Divider.Draw(c, theme, new SKRect(20, 50, 380, 50)));

        float tick = Math.Max(theme.Divider.Thickness * 6, 4f);
        int above = 50 - (int)(tick / 2) + 1;
        Assert.True(bitmap.GetPixel(20, above).Alpha > 100);
        Assert.True(bitmap.GetPixel(380, above).Alpha > 100);
        Assert.Equal(0, bitmap.GetPixel(200, above).Alpha);
        Assert.True(bitmap.GetPixel(200, 50).Alpha > 100);
    }

    [Fact]
    public void Divider_DimensionWithALabel_BreaksTheRuleRoundIt()
    {
        var theme = Resolve("""{ "divider": { "style": "dimension", "color": "#ffffd34d", "thickness": 0.01 } }""");
        using var bitmap = Draw(400, 100, c => Divider.Draw(c, theme, new SKRect(20, 50, 380, 50), "FORECAST"));

        Assert.True(bitmap.GetPixel(40, 50).Alpha > 100);
        Assert.True(Count(bitmap, p => p == theme.TextMuted) > 0);
    }

    [Fact]
    public void Divider_None_DrawsNothing()
    {
        var theme = Resolve("""{ "divider": { "style": "none" } }""");
        using var bitmap = Draw(400, 100, c => Divider.Draw(c, theme, new SKRect(0, 50, 400, 50)));

        Assert.Equal(0, Count(bitmap, p => p.Alpha != 0));
    }

    // Well

    [Fact]
    public void Well_FillsARoundedRectangleWithABorder()
    {
        var theme = Resolve(PartsTheme);
        using var bitmap = Draw(300, 300, c => Well.Draw(c, theme, new SKRect(50, 50, 250, 250)));

        Assert.Equal(new SKColor(0x33, 0x66, 0x99), bitmap.GetPixel(150, 150));
        Assert.Equal(0, bitmap.GetPixel(50, 50).Alpha);
        Assert.Equal(new SKColor(0xee, 0x33, 0x44), bitmap.GetPixel(150, 50));
    }

    [Fact]
    public void Well_RadiusIsCappedAtHalfTheSmallerSide()
    {
        var theme = Resolve("""{ "well": { "fill": "#ffffffff", "radius": 0.5 } }""");
        using var bitmap = Draw(300, 120, c => Well.Draw(c, theme, new SKRect(10, 10, 290, 50)));

        // A 40 pixel high well with a radius of 137 pixels is a stadium: its corner is empty, its middle is full.
        Assert.Equal(0, bitmap.GetPixel(11, 11).Alpha);
        Assert.Equal(SKColors.White, bitmap.GetPixel(150, 30));
    }

    [Fact]
    public void Well_WithNoWell_DrawsNothing()
    {
        var theme = Builtin("default-dark");
        using var bitmap = Draw(100, 100, c => Well.Draw(c, theme, new SKRect(0, 0, 100, 100)));

        Assert.Equal(0, Count(bitmap, p => p.Alpha != 0));
    }

    // Sparkline

    [Fact]
    public void Sparkline_RisingValues_RiseFromLowerLeftToUpperRight()
    {
        var theme = Builtin("default-dark");
        using var bitmap = Draw(200, 100, c => Sparkline.Draw(c, theme, new SKRect(0, 0, 200, 100), [0.1f, 0.5f, 0.9f], SKColors.Red, 4f));

        Assert.True(bitmap.GetPixel(4, 88).Alpha > 0);
        Assert.True(bitmap.GetPixel(196, 12).Alpha > 0);
        Assert.Equal(0, bitmap.GetPixel(4, 12).Alpha);
        Assert.Equal(0, bitmap.GetPixel(196, 88).Alpha);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Sparkline_FewerThanTwoValues_DrawNothing(int count)
    {
        var theme = Builtin("default-dark");
        using var bitmap = Draw(200, 100, c => Sparkline.Draw(c, theme, new SKRect(0, 0, 200, 100), new float[count].Select(_ => 0.5f).ToArray()));

        Assert.Equal(0, Count(bitmap, p => p.Alpha != 0));
    }

    // The gauge's status row and sparkline

    private static GaugeOptions Options(float fraction, IReadOnlyList<float>? history = null, GaugeStatusText? status = null) => new()
    {
        Label = "CPU",
        Fraction = fraction,
        Level = GaugeLevel.Normal,
        History = history,
        Status = status,
    };

    private static ReadingText Text(string value) => new(value, null, UnitPlacement.Raised, value, true);

    [Fact]
    public void Gauge_StatusIsOnTheLabelsRow_LabelLeftChipRight()
    {
        var theme = Resolve(StatusTheme);
        var rect = new SKRect(0, 0, 500, 500);
        using var plain = Draw(500, 500, c => Gauge.Draw(c, theme, rect, GaugeStyle.Ring, Text("68"), Options(0.68f)));
        using var shown = Draw(500, 500, c => Gauge.Draw(c, theme, rect, GaugeStyle.Ring, Text("68"), Options(0.68f, null, new GaugeStatusText("WARM", GaugeLevel.Warning))));

        Assert.False(Same(plain, shown));
        int rowBottom = (int)(theme.LabelSize * 1.5f);
        int rightWarning = 0;
        int leftMuted = 0;
        for (int y = 0; y < rowBottom; y++)
        {
            for (int x = 0; x < 500; x++)
            {
                var pixel = shown.GetPixel(x, y);
                if (pixel == theme.Warning && x > 250)
                    rightWarning++;
                if (pixel == theme.TextMuted && x < 250)
                    leftMuted++;
            }
        }

        Assert.True(rightWarning > 0, "the status text is at the right of the row");
        Assert.True(leftMuted > 0, "the label is at the left of the row");
    }

    [Fact]
    public void Gauge_StatusAndHistoryAreIgnoredWhenTheThemeDoesNotAskForThem()
    {
        var theme = Builtin("default-dark");
        var rect = new SKRect(0, 0, 400, 400);
        using var plain = Draw(400, 400, c => Gauge.Draw(c, theme, rect, GaugeStyle.Ring, Text("68"), Options(0.68f)));
        using var given = Draw(400, 400, c => Gauge.Draw(c, theme, rect, GaugeStyle.Ring, Text("68"), Options(0.68f, [0.1f, 0.5f, 0.9f], new GaugeStatusText("WARM", GaugeLevel.Warning))));

        Assert.True(Same(plain, given));
    }

    [Fact]
    public void Gauge_SparklineIsDrawnUnderTheReadoutAndNotInASmallRectangle()
    {
        var theme = Resolve(StatusTheme);
        float[] history = [0.2f, 0.5f, 0.3f, 0.8f, 0.6f];
        var tall = new SKRect(0, 0, 300, 300);
        var small = new SKRect(0, 0, 300, 0.6f * 275);

        using var withLine = Draw(300, 300, c => Gauge.Draw(c, theme, tall, GaugeStyle.Bar, Text("68"), Options(0.68f, history)));
        using var withoutLine = Draw(300, 300, c => Gauge.Draw(c, theme, tall, GaugeStyle.Bar, Text("68"), Options(0.68f)));
        using var smallWith = Draw(300, 300, c => Gauge.Draw(c, theme, small, GaugeStyle.Bar, Text("68"), Options(0.68f, history)));
        using var smallWithout = Draw(300, 300, c => Gauge.Draw(c, theme, small, GaugeStyle.Bar, Text("68"), Options(0.68f)));

        Assert.False(Same(withLine, withoutLine));
        Assert.True(Same(smallWith, smallWithout));

        // The line is in the colour of the fill and sits in the lowest part of the rectangle.
        int lineBottom = 300;
        int lineTop = lineBottom - (int)theme.Gauge.Sparkline!.Height;
        int inColour = 0;
        for (int y = lineTop; y < lineBottom; y++)
            for (int x = 0; x < 300; x++)
                if (withLine.GetPixel(x, y) == theme.Accent)
                    inColour++;
        Assert.True(inColour > 0);
    }

    [Fact]
    public void Gauge_MeasureCountsTheRowAndTheSparkline()
    {
        var theme = Resolve(StatusTheme);
        var rect = new SKRect(0, 0, 300, 300);
        var reading = Text("68");
        float without = Gauge.Measure(theme, rect, GaugeStyle.Ring, reading, Options(0.68f));
        float with = Gauge.Measure(theme, rect, GaugeStyle.Ring, reading, Options(0.68f, [0.1f, 0.5f, 0.9f], new GaugeStatusText("OK", GaugeLevel.Normal)));

        Assert.True(with < without);
    }

    // Stats

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

    private const string Load = "system:cpu/load";

    private static FakeSource StatsSource(double? value)
    {
        var source = new FakeSource();
        source.Catalog[Load] = new ReadingDescriptor("cpu/load", ReadingKind.Percent, "CPU", "CPU load") { Min = 0, Max = 100 };
        if (value != null)
            source.Readings[Load] = Reading.Ok(value.Value);
        return source;
    }

    private static StatsWidget StatsCard(FakeSource source, int width, int height, StatsSlot? slot = null)
    {
        var widget = new StatsWidget();
        widget.Attach(new Host(source));
        widget.Configure(new StatsConfig
        {
            WidgetTypeId = "urdeck.widgets.stats",
            Width = width,
            Height = height,
            Slots = [slot ?? new StatsSlot { Reading = Load, Style = "gauge" }],
        });
        return widget;
    }

    private static SKBitmap Paint(IWidget widget, Theme theme, DateTime time, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        float pad = theme.Padding;
        widget.Render(new WidgetRenderContext(
            canvas, time, new System.Drawing.Size(width, height), theme, new SKRect(pad, pad, width - pad, height - pad), widget.Config, CancellationToken.None));
        return bitmap;
    }

    private static FractionHistory?[] HistoriesOf(StatsWidget widget) =>
        (FractionHistory?[])typeof(StatsWidget).GetField("_histories", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(widget)!;

    private static int KeptOf(StatsWidget widget) => HistoriesOf(widget).Sum(h => h?.Count ?? 0);

    private static void PaintSeconds(StatsWidget widget, FakeSource source, Theme theme, int seconds, int size = 550)
    {
        for (int i = 0; i < seconds; i++)
        {
            source.Readings[Load] = Reading.Ok(10 + i * 2);
            using var bitmap = Paint(widget, theme, T0.AddSeconds(i), size, size);
        }
    }

    [Fact]
    public void Stats_KeepsAHistoryOnlyWhileTheSparklineIsEnabled()
    {
        var on = Resolve(StatusTheme);
        var off = Builtin("default-dark");
        var source = StatsSource(40);
        var widget = StatsCard(source, 2, 2);

        PaintSeconds(widget, source, off, 5);
        Assert.Equal(0, KeptOf(widget));

        PaintSeconds(widget, source, on, 5);
        Assert.Equal(5, KeptOf(widget));
    }

    [Fact]
    public void Stats_KeepsOneValuePerSecondAndAtMostTheThemesSeconds()
    {
        var theme = Resolve(StatusTheme);
        var source = StatsSource(40);
        var widget = StatsCard(source, 2, 2);

        // Two paints in one second are one value; thirty seconds of them are the last ten.
        using (Paint(widget, theme, T0, 550, 550))
        using (Paint(widget, theme, T0.AddMilliseconds(400), 550, 550))
            Assert.Equal(1, KeptOf(widget));

        PaintSeconds(widget, source, theme, 30);
        var history = HistoriesOf(widget).Single(h => h != null)!;
        Assert.Equal(10, history.Count);
        Assert.Equal(10, history.Capacity);
        Assert.Equal(10, history.ToArray().Length);
        Assert.True(history.ToArray()[^1] > history.ToArray()[0]);
    }

    [Fact]
    public void Stats_KeepsNoHistoryForAPositionTooSmallForTheLine()
    {
        var theme = Resolve(StatusTheme);
        var source = StatsSource(40);
        var widget = StatsCard(source, 2, 2);

        // The card is 2x2 cells of 275 pixels but is painted into a surface only 0.6 of a cell tall.
        for (int i = 0; i < 3; i++)
        {
            source.Readings[Load] = Reading.Ok(30 + i);
            using var bitmap = Paint(widget, theme, T0.AddSeconds(i), 550, (int)(0.6 * 275) + (int)(2 * theme.Padding));
        }

        Assert.Equal(0, KeptOf(widget));
    }

    [Fact]
    public void Stats_DropsTheHistoryWhenTheReadingIsReplaced()
    {
        var theme = Resolve(StatusTheme);
        var source = StatsSource(40);
        var widget = StatsCard(source, 2, 2);
        PaintSeconds(widget, source, theme, 4);
        Assert.Equal(4, KeptOf(widget));

        widget.Configure(new StatsConfig
        {
            WidgetTypeId = "urdeck.widgets.stats",
            Width = 2,
            Height = 2,
            Slots = [new StatsSlot { Reading = Load, Style = "gauge", Label = "Total" }],
        });

        Assert.Equal(0, KeptOf(widget));
        using var bitmap = Paint(widget, theme, T0.AddSeconds(10), 550, 550);
        Assert.Equal(1, KeptOf(widget));
    }

    [Fact]
    public void Stats_AHistoryIsAtMostSixHundredFloats()
    {
        var history = new FractionHistory(600);
        for (int i = 0; i < 2000; i++)
            history.Add(T0.AddSeconds(i), 0.5f);

        Assert.Equal(600, history.Count);
        Assert.Equal(600, history.ToArray().Length);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 2000; i < 3000; i++)
            history.Add(T0.AddSeconds(i), 0.25f);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024, "adding to a full history allocates nothing");
    }

    private SKColor StatusColourAt(double load, StatsSlot? slot = null)
    {
        var theme = Resolve(StatusTheme);
        var source = StatsSource(load);
        var widget = StatsCard(source, 2, 2, slot);
        using var bitmap = Paint(widget, theme, T0, 550, 550);

        // The status text is on the row above the readout, in the colour of its level; find which level's colour is there.
        int row = (int)(theme.Padding + theme.LabelSize * 1.5f);
        foreach (var colour in new[] { theme.Good, theme.Warning, theme.Critical })
            for (int y = 0; y < row; y++)
                for (int x = 275; x < 550; x++)
                    if (bitmap.GetPixel(x, y) == colour)
                        return colour;
        return SKColors.Transparent;
    }

    [Fact]
    public void Stats_StatusFollowsTheThemesThresholds()
    {
        var theme = Resolve(StatusTheme);

        Assert.Equal(theme.Good, StatusColourAt(68));
        Assert.Equal(theme.Warning, StatusColourAt(82));
        Assert.Equal(theme.Critical, StatusColourAt(95));
    }

    [Fact]
    public void Stats_ASlotsOwnLevelsWinOverTheThresholds()
    {
        var theme = Resolve(StatusTheme);

        // 60 percent is below the theme's first threshold (OK), but past the slot's own warning value.
        var colour = StatusColourAt(60, new StatsSlot { Reading = Load, Style = "gauge", Warning = 50 });

        Assert.Equal(theme.Warning, colour);
    }

    [Fact]
    public void Stats_NoFraction_NoStatus()
    {
        var theme = Resolve(StatusTheme);
        var source = StatsSource(null);
        var widget = StatsCard(source, 2, 2);
        using var bitmap = Paint(widget, theme, T0, 550, 550);

        int row = (int)(theme.Padding + theme.LabelSize * 1.5f);
        int status = 0;
        foreach (var colour in new[] { theme.Good, theme.Warning, theme.Critical })
            for (int y = 0; y < row; y++)
                for (int x = 0; x < 550; x++)
                    if (bitmap.GetPixel(x, y) == colour)
                        status++;
        Assert.Equal(0, status);
    }

    [Fact]
    public void Stats_UnderAV1Theme_DrawsAsBefore()
    {
        var theme = Builtin("default-dark");
        var source = StatsSource(82);
        var widget = StatsCard(source, 2, 2);
        using var drawn = Paint(widget, theme, T0, 550, 550);
        using var again = Paint(StatsCard(source, 2, 2), theme, T0, 550, 550);

        Assert.True(Same(drawn, again));
        Assert.Equal(0, KeptOf(widget));
    }

    // Clock

    private static ClockWidget ClockWith(Action<ClockConfig> set, string style = "simple")
    {
        var clock = new ClockWidget();
        var config = new ClockConfig { WidgetTypeId = "urdeck.widgets.clock", Width = 4, Height = 2, Style = style };
        set(config);
        clock.Configure(config);
        return clock;
    }

    private static readonly DateTime Tuesday = new(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);

    private SKBitmap PaintClock(ClockWidget clock, DateTime time, string themeName = "default-dark")
    {
        var theme = Builtin(themeName, 275);
        return Paint(clock, theme, time, 1100, 550);
    }

    [Fact]
    public void Clock_WithNoNewOption_IsDrawnOnceAMinuteAsBefore()
    {
        var clock = ClockWith(_ => { });
        using var first = PaintClock(clock, Tuesday);

        Assert.False(clock.NeedsRender(Tuesday.AddSeconds(1)));
        Assert.False(clock.NeedsRender(Tuesday.AddSeconds(27)));
        Assert.True(clock.NeedsRender(Tuesday.AddSeconds(28)));
        using var plain = PaintClock(ClockWith(_ => { }), Tuesday);
        Assert.True(Same(first, plain));
    }

    [Fact]
    public void Clock_AnUnknownHeaderIsIgnored()
    {
        using var plain = PaintClock(ClockWith(_ => { }), Tuesday);
        using var unknown = PaintClock(ClockWith(c => c.Header = "sideways"), Tuesday);

        Assert.True(Same(plain, unknown));
    }

    [Fact]
    public void Clock_HeaderDate_PutsTheDateAtTheLeftAndTheWeekAtTheRight()
    {
        var theme = Builtin("default-dark");
        using var bitmap = PaintClock(ClockWith(c => { c.Header = "date"; c.ShowWeek = true; }), Tuesday);

        // The header row is above the time: muted label text at both ends of the first row, nothing in its middle.
        int top = (int)theme.Padding;
        int bottom = top + (int)(theme.LabelSize * 1.5f);
        int leftInk = 0, rightInk = 0, middleInk = 0;
        for (int y = top; y < bottom; y++)
        {
            for (int x = 0; x < 1100; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                    continue;
                if (x < 400)
                    leftInk++;
                else if (x > 700)
                    rightInk++;
                else
                    middleInk++;
            }
        }

        Assert.True(leftInk > 0);
        Assert.True(rightInk > 0);
        Assert.Equal(0, middleInk);
    }

    [Fact]
    public void Clock_HeaderDate_NoDateIsDrawnBelowTheTime()
    {
        using var below = PaintClock(ClockWith(_ => { }), Tuesday);
        using var header = PaintClock(ClockWith(c => c.Header = "none"), Tuesday);

        // The date below the time is what "none" leaves out, so the card has less ink and the time is drawn larger.
        int Ink(SKBitmap bitmap)
        {
            int ink = 0;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < 1100; x++)
                    if (bitmap.GetPixel(x, y).Alpha != 0)
                        ink++;
            return ink;
        }

        Assert.NotEqual(Ink(below), Ink(header));
    }

    [Fact]
    public void Clock_HeaderLabel_PutsTheSubtitleAtTheLeftAndTheDateAtTheRight()
    {
        var theme = Builtin("default-dark");
        using var bitmap = PaintClock(ClockWith(c => { c.Header = "label"; c.Subtitle = "Local time"; }), Tuesday);

        int top = (int)theme.Padding;
        int bottom = top + (int)(theme.LabelSize * 1.5f);
        int leftInk = 0, rightInk = 0;
        for (int y = top; y < bottom; y++)
        {
            for (int x = 0; x < 1100; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                    continue;
                if (x < 300)
                    leftInk++;
                else if (x > 800)
                    rightInk++;
            }
        }

        Assert.True(leftInk > 0);
        Assert.True(rightInk > 0);
    }

    [Fact]
    public void Clock_ShowSeconds_DrawsOnceASecondAndShowsThem()
    {
        var clock = ClockWith(c => c.ShowSeconds = true);
        using var at32 = PaintClock(clock, Tuesday);

        Assert.True(clock.NeedsRender(Tuesday.AddSeconds(1)));
        Assert.False(clock.NeedsRender(Tuesday.AddMilliseconds(500)));
        using var at33 = PaintClock(clock, Tuesday.AddSeconds(1));
        Assert.False(Same(at32, at33));

        using var without = PaintClock(ClockWith(_ => { }), Tuesday);
        Assert.False(Same(at32, without));
    }

    [Fact]
    public void Clock_ShowSeconds_DoesNotMoveTheTimeWhenTheSecondsChangeWidth()
    {
        var clock = ClockWith(c => c.ShowSeconds = true);
        var at41 = new DateTime(2026, 10, 13, 21, 47, 41, DateTimeKind.Local);
        using var narrow = PaintClock(clock, at41, "rigline");
        using var wide = PaintClock(clock, at41.AddSeconds(3), "rigline");

        Assert.Equal(LeftmostInk(narrow), LeftmostInk(wide));
    }

    private static int LeftmostInk(SKBitmap bitmap)
    {
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel(x, y).Alpha > 0)
                    return x;
        return -1;
    }

    [Fact]
    public void Clock_BlinkSeparator_AlternatesEverySecondAndDrawsOnceASecond()
    {
        var clock = ClockWith(c => c.BlinkSeparator = true);
        var even = new DateTime(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);
        using var on = PaintClock(clock, even);
        Assert.True(clock.NeedsRender(even.AddSeconds(1)));
        using var off = PaintClock(clock, even.AddSeconds(1));
        using var onAgain = PaintClock(clock, even.AddSeconds(2));

        Assert.False(Same(on, off));
        Assert.True(Same(on, onAgain));
    }

    [Fact]
    public void Clock_FlapStyleTakesTheHeaderAndTheOptions()
    {
        var clock = ClockWith(c => { c.Header = "date"; c.ShowSeconds = true; c.BlinkSeparator = true; }, "flap");
        using var a = PaintClock(clock, Tuesday);
        using var b = PaintClock(clock, Tuesday.AddSeconds(1));
        using var plain = PaintClock(ClockWith(_ => { }, "flap"), Tuesday);

        Assert.False(Same(a, plain));
        Assert.False(Same(a, b));
        Assert.True(clock.NeedsRender(Tuesday.AddSeconds(2)));
    }

    [Fact]
    public void Clock_TheNewOptionsSurviveLoadAndSave()
    {
        const string json = """{ "header": "date", "subtitle": "Rig online", "showWeek": true, "showSeconds": true, "blinkSeparator": true, "future": 1 }""";
        var config = System.Text.Json.JsonSerializer.Deserialize<ClockConfig>(json, Web)!;

        Assert.Equal("date", config.Header);
        Assert.Equal("Rig online", config.Subtitle);
        Assert.True(config.ShowWeek && config.ShowSeconds && config.BlinkSeparator);
        Assert.Contains("\"showWeek\":true", System.Text.Json.JsonSerializer.Serialize(config, Web).Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
    }

    // Weather

    private static WeatherWidget Weather(int height)
    {
        var weather = new WeatherWidget();
        weather.Attach(new Host(new FakeSource()));
        weather.Configure(new WeatherConfig { WidgetTypeId = "urdeck.widgets.weather", Width = 4, Height = height, Location = "Portland, OR" });
        return weather;
    }

    private static int PixelsOfColour(SKBitmap bitmap, SKColor colour) => Count(bitmap, p => p == colour);

    [Fact]
    public void Weather_ForecastDaysSitOnTheThemesWell()
    {
        var theme = Resolve(PartsTheme);
        var plain = Builtin("default-dark");
        using var weather = Weather(4);
        using var withWell = Paint(weather, theme, T0, 1100, 1100);
        using var without = Paint(weather, plain, T0, 1100, 1100);

        Assert.True(PixelsOfColour(withWell, new SKColor(0x33, 0x66, 0x99)) > 5000);
        Assert.Equal(0, PixelsOfColour(without, new SKColor(0x33, 0x66, 0x99)));
    }

    [Fact]
    public void Weather_TheRuleBetweenTheHalvesIsTheThemesDivider()
    {
        var theme = Resolve(PartsTheme);
        using var weather = Weather(4);
        using var bitmap = Paint(weather, theme, T0, 1100, 1100);

        // A gradient rule: the colour is solid at its left end and has faded at its right end.
        int solid = 0;
        int faded = 0;
        for (int y = 0; y < 1100; y++)
        {
            if (bitmap.GetPixel((int)theme.Padding + 4, y) is var l && l.Red > 0xc0 && l.Green > 0x90 && l.Blue < 0x90 && l.Blue > 0x40)
                solid++;
            if (bitmap.GetPixel(1100 - (int)theme.Padding - 4, y) is var r && r.Red > 0xc0 && r.Green > 0x90 && r.Blue < 0x90 && r.Blue > 0x40)
                faded++;
        }

        Assert.True(solid > 0);
        Assert.Equal(0, faded);
    }

    [Fact]
    public void Weather_TheConditionIsOnAPillWhenTheThemeSaysSo()
    {
        var theme = Resolve(PartsTheme);
        var source = new FakeSource();
        const string id = "weather:Portland, OR/current/condition";
        source.Catalog[id] = new ReadingDescriptor("Portland, OR/current/condition", ReadingKind.Text, "Condition", "Condition");
        source.Readings[id] = Reading.Ok("partly-cloudy");
        var weather = new WeatherWidget();
        weather.Attach(new Host(source));
        weather.Configure(new WeatherConfig { WidgetTypeId = "urdeck.widgets.weather", Width = 4, Height = 2, Location = "Portland, OR" });
        using var bitmap = Paint(weather, theme, T0, 1100, 550);

        Assert.True(PixelsOfColour(bitmap, new SKColor(0xff, 0xd8, 0x4a)) > 500);
    }

    [Fact]
    public void Weather_UnderAV1Theme_HasNoWellAndNoPill()
    {
        var theme = Builtin("default-dark");
        using var weather = Weather(4);
        using var bitmap = Paint(weather, theme, T0, 1100, 1100);

        Assert.Equal(0, PixelsOfColour(bitmap, new SKColor(0x33, 0x66, 0x99)));
        Assert.Equal(0, PixelsOfColour(bitmap, new SKColor(0xff, 0xd8, 0x4a)));
    }
}
