// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>What the theme gives the gauge (theme-gauges): the <c>layout</c> key, the resolved values and the colours beside them.</summary>
public sealed class GaugeThemeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-gaugetheme-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public GaugeThemeTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "themes");

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    private Theme Resolve(string json, float cellPx = 275)
    {
        string dir = Path.Combine(_root, "themes", "probe");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ThemeStore.SettingsFileName), json);
        return ThemeResolver.Resolve(new ThemeStore(Path.Combine(_root, "themes")).Load("probe"), cellPx);
    }

    // gauge.layout

    [Fact]
    public void TheLayout_IsAuto_UnlessTheThemeSaysStacked()
    {
        using var none = Resolve("""{ "gauge": { "style": "bar" } }""");
        using var stacked = Resolve("""{ "gauge": { "layout": "stacked" } }""");
        using var shouted = Resolve("""{ "gauge": { "layout": "STACKED" } }""");

        Assert.Equal(GaugeLayout.Auto, none.Gauge.Layout);
        Assert.Equal(GaugeLayout.Stacked, stacked.Gauge.Layout);
        Assert.Equal(GaugeLayout.Stacked, shouted.Gauge.Layout);
        Assert.Empty(Log);
    }

    [Fact]
    public void AnUnknownLayout_IsClearedWithOneWarning()
    {
        using var theme = Resolve("""{ "gauge": { "layout": "sideways" } }""");

        Assert.Equal(GaugeLayout.Auto, theme.Gauge.Layout);
        Assert.Single(Log.Split('\n', StringSplitOptions.RemoveEmptyEntries), line => line.Contains("gauge.layout", StringComparison.Ordinal));
    }

    // The values

    [Fact]
    public void AV1Theme_GivesTheEmptyGaugeTheme()
    {
        foreach (string name in ThemeStore.BuiltInNames)
        {
            using var theme = ThemeResolver.Resolve(new ThemeStore("").Load(name), 275);

            Assert.True(theme.Gauge.IsEmpty, name);
        }
    }

    [Fact]
    public void TheNewStyles_AreGivenAsThemselves_AndAThemeThatSetsOnlyAStyleIsStillEmpty()
    {
        foreach (string style in new[] { "meter", "tach", "marker", "thickbar", "thickBar" })
        {
            using var theme = Resolve("{ \"gauge\": { \"style\": \"" + style + "\" } }");

            Assert.Equal(Enum.Parse<GaugeStyle>(style, ignoreCase: true), theme.GaugeStyle);
            Assert.True(theme.Gauge.IsEmpty);
        }

        Assert.Empty(Log);
    }

    [Fact]
    public void LengthsAreInPixels_FromTheCellSize()
    {
        const string json = """
            { "gauge": { "ticks": { "length": 0.06, "thickness": 0.01 }, "segments": { "length": 0.1, "gap": 0.02 },
              "glow": { "blur": 0.05 }, "outline": { "width": 0.02 } } }
            """;
        using var large = Resolve(json, 275);
        using var small = Resolve(json, 100);

        Assert.Equal(16.5f, large.Gauge.Ticks!.Length, 3);
        Assert.Equal(2.75f, large.Gauge.Ticks.Thickness, 3);
        Assert.Equal(27.5f, large.Gauge.Segments!.Length, 3);
        Assert.Equal(5.5f, large.Gauge.Segments.Gap, 3);
        Assert.Equal(13.75f, large.Gauge.Glow!.Blur, 3);
        Assert.Equal(5.5f, large.Gauge.Outline!.Width, 3);
        Assert.Equal(6f, small.Gauge.Ticks!.Length, 3);
    }

    [Fact]
    public void AGroupTheThemeWrote_TakesTheDefaultsOfAKeyItLeftOut()
    {
        using var theme = Resolve("""{ "gauge": { "ticks": {}, "redline": {}, "marker": {}, "glow": { "blur": 0.04 } } }""");

        Assert.Equal(11, theme.Gauge.Ticks!.Count);
        Assert.Equal(5, theme.Gauge.Ticks.MajorEvery);
        Assert.False(theme.Gauge.Ticks.Labels);
        Assert.Equal(0.85f, theme.Gauge.Redline!.From);
        Assert.False(theme.Gauge.Redline.LightsAbove);
        Assert.Equal(GaugeMarkerShape.Diamond, theme.Gauge.Marker!.Shape);
        Assert.Equal(0.6f, theme.Gauge.Glow!.Alpha);
        Assert.Null(theme.Gauge.Sweep);
        Assert.Null(theme.Gauge.Segments);
    }

    [Fact]
    public void AGradient_IsTheRampOrItsOwnColours()
    {
        using var ramp = Resolve("""{ "gauge": { "gradient": "ramp", "colorBy": "ramp", "valueTint": true } }""");
        using var own = Resolve("""{ "gauge": { "gradient": { "colors": ["#ff0000", "#0000ff"], "angle": 45 } } }""");

        Assert.True(ramp.Gauge.Gradient!.Ramp);
        Assert.True(ramp.Gauge.ColorByRamp);
        Assert.True(ramp.Gauge.ValueTint);
        Assert.False(own.Gauge.Gradient!.Ramp);
        Assert.Equal([SKColor.Parse("#ff0000"), SKColor.Parse("#0000ff")], own.Gauge.Gradient.Colors);
        Assert.Equal(45f, own.Gauge.Gradient.AngleDegrees);
    }

    [Fact]
    public void EachDraftedTheme_ResolvesItsGaugeValues()
    {
        var store = new ThemeStore(FixtureFolder);
        var expected = new Dictionary<string, GaugeStyle>
        {
            ["ember"] = GaugeStyle.Tach,
            ["blueprint"] = GaugeStyle.Marker,
            ["obsidian"] = GaugeStyle.Marker,
            ["pop"] = GaugeStyle.ThickBar,
            ["paper"] = GaugeStyle.Meter,
            ["carbon"] = GaugeStyle.Bar,
            ["rigline"] = GaugeStyle.Ring,
            ["prism"] = GaugeStyle.Ring,
        };

        foreach (var (name, style) in expected)
        {
            using var theme = ThemeResolver.Resolve(store.Load(name), 275);

            Assert.Equal(style, theme.GaugeStyle);
            Assert.False(theme.Gauge.IsEmpty, name);
        }

        using var ember = ThemeResolver.Resolve(store.Load("ember"), 275);
        Assert.Equal(0.8f, ember.Gauge.Redline!.From);
        Assert.True(ember.Gauge.Redline.LightsAbove);
        Assert.Equal(GaugeMarkerShape.Needle, ember.Gauge.Marker!.Shape);
        Assert.Equal(28, ember.Gauge.Ticks!.Count);

        using var carbon = ThemeResolver.Resolve(store.Load("carbon"), 275);
        Assert.Equal(GaugeLayout.Stacked, carbon.Gauge.Layout);
        Assert.NotNull(carbon.Gauge.Segments);

        using var prism = ThemeResolver.Resolve(store.Load("prism"), 275);
        Assert.Equal(GaugeLabelPosition.Below, prism.Gauge.LabelPosition);
        Assert.Equal(new GaugeSweep(-90, 360), prism.Gauge.Sweep);

        using var rigline = ThemeResolver.Resolve(store.Load("rigline"), 275);
        Assert.True(rigline.Gauge.ColorByRamp);
        Assert.Equal(3, rigline.Ramp.Count);

        using var paper = ThemeResolver.Resolve(store.Load("paper"), 275);
        Assert.Equal(["0", "50", "100"], paper.Gauge.Scale!.Labels);
    }

    // The colours beside the gauge values

    [Fact]
    public void WithNoSecondAccent_ItIsTheAccent_AndWithNoRamp_ItIsGoodWarningCritical()
    {
        using var theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);

        Assert.Equal(theme.Accent, theme.Accent2);
        Assert.Equal([theme.Good, theme.Warning, theme.Critical], theme.Ramp);
    }

    [Fact]
    public void TheInk_IsWhicheverOfTextAndBackgroundContrastsMoreWithTheAccent()
    {
        using var darkOnBright = Resolve("""{ "colors": { "accent": "#ffd000", "text": "#ffffff", "background": "#101020" } }""");
        using var lightOnDark = Resolve("""{ "colors": { "accent": "#202080", "text": "#ffffff", "background": "#101020" } }""");
        using var own = Resolve("""{ "colors": { "accent": "#202080", "ink": "#ff00ff" } }""");

        Assert.Equal(SKColor.Parse("#101020"), darkOnBright.Ink);
        Assert.Equal(SKColors.White, lightOnDark.Ink);
        Assert.Equal(SKColor.Parse("#ff00ff"), own.Ink);
    }

    [Fact]
    public void ATheme_ThatSetsTheColours_GivesThemToTheWidget()
    {
        using var theme = Resolve("""{ "colors": { "accent2": "#00ff00", "ramp": ["#ff0000", "#00ff00", "#0000ff"] } }""");

        Assert.Equal(SKColor.Parse("#00ff00"), theme.Accent2);
        Assert.Equal([SKColor.Parse("#ff0000"), SKColor.Parse("#00ff00"), SKColor.Parse("#0000ff")], theme.Ramp);
    }
}
