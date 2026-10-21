// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using UrDeck.Widgets.Weather;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The weather card under a theme that names an icon set: static themed glyphs, no animation work.</summary>
public sealed class WeatherThemedIconTests : IDisposable
{
    private const int CardWidth = 1100;
    private const string Place = "Portland, OR";

    private static readonly DateTime T0 = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Local);
    private static readonly SKColor Cold = new(0x10, 0x30, 0xE0);
    private static readonly SKColor Hot = new(0xE0, 0x30, 0x10);

    /// <summary>Answers every key with a box that fills its whole square, and remembers what was asked.</summary>
    private sealed class RecordingSet : IIconSet, IDisposable
    {
        private readonly IconGlyph _box;

        public RecordingSet()
        {
            using var builder = new SKPathBuilder();
            builder.AddRect(new SKRect(0, 0, 24, 24));
            _box = new IconGlyph([new IconShape(builder.Detach(), true)]);
        }

        public List<string> Asked { get; } = [];

        public HashSet<string> Missing { get; } = [];

        public bool TryGet(string key, out IconGlyph glyph)
        {
            Asked.Add(key);
            glyph = _box;
            return !Missing.Contains(key);
        }

        public void Dispose() => _box.Shapes[0].Path.Dispose();
    }

    private sealed class FakeSource : IReadingSource
    {
        public Dictionary<string, Reading> Readings { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ReadingDescriptor> Catalog { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Reading Read(string id) => Readings.TryGetValue(id, out Reading r) ? r : Reading.Unavailable("unknown");

        public ReadingDescriptor? Describe(string id) => Catalog.TryGetValue(id, out ReadingDescriptor? d) ? d : null;

        public bool RegionUsesFahrenheit => false;

        public CultureInfo RegionCulture => CultureInfo.GetCultureInfo("en-US");
    }

    private sealed class Host(IReadingSource readings, List<string> log) : IWidgetHost
    {
        public IReadingSource Readings { get; } = readings;

        public void Log(string message) => log.Add(message);
    }

    private readonly Theme _base = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly RecordingSet _set = new();
    private readonly FakeSource _source = new();
    private readonly List<string> _log = [];
    private readonly List<WeatherWidget> _widgets = [];
    private readonly List<Theme> _themes = [];

    public void Dispose()
    {
        foreach (WeatherWidget widget in _widgets)
            widget.Dispose();
        foreach (Theme theme in _themes)
            theme.Icons.Dispose();
        _base.Dispose();
        _set.Dispose();
    }

    private static string Id(string path) => $"weather:{Place}/{path}";

    private void Describe(string path, ReadingKind kind, string label, string name, Func<ReadingDescriptor, ReadingDescriptor>? more = null)
    {
        var descriptor = new ReadingDescriptor(Place + "/" + path, kind, label, name);
        _source.Catalog[Id(path)] = more?.Invoke(descriptor) ?? descriptor;
    }

    private void Set(string path, Reading reading) => _source.Readings[Id(path)] = reading;

    private void Fill(string? condition = "rain", bool isDay = true, double temperature = 15, string[]? days = null)
    {
        days ??= ["snow", "cloudy", "clear", "partly-cloudy", "thunder", "fog", "rain"];
        Describe("place", ReadingKind.Text, "Place", "Place");
        Describe("current/temperature", ReadingKind.Temperature, "Now", "Temperature", d => d with { Min = -60, Max = 60 });
        Describe("current/apparent", ReadingKind.Temperature, "Feels like", "Feels like", d => d with { Min = -60, Max = 60 });
        Describe("current/condition", ReadingKind.Text, "Condition", "Weather condition");
        Describe("current/is-day", ReadingKind.OnOff, "Daytime", "Is daytime");
        Describe("current/humidity", ReadingKind.Percent, "Humidity", "Relative humidity", d => d with { Min = 0, Max = 100 });
        Describe("current/wind-speed", ReadingKind.Speed, "Wind", "Wind speed", d => d with { Min = 0, Max = 150 });
        Describe("current/wind-direction", ReadingKind.Number, "Direction", "Wind direction", d => d with { Unit = "°", Min = 0, Max = 360 });
        Set("place", Reading.Ok("Portland, Oregon"));
        Set("current/temperature", Reading.Ok(temperature));
        Set("current/apparent", Reading.Ok(temperature));
        if (condition != null)
            Set("current/condition", Reading.Ok(condition));
        Set("current/is-day", Reading.Ok(isDay));
        Set("current/humidity", Reading.Ok(80));
        Set("current/wind-speed", Reading.Ok(10));
        Set("current/wind-direction", Reading.Ok(90));
        for (int i = 0; i < 7; i++)
        {
            string at = $"daily/{i}/";
            Describe(at + "date", ReadingKind.Date, "Day", $"Day {i}");
            Describe(at + "condition", ReadingKind.Text, "Condition", $"Day {i} condition");
            Describe(at + "high", ReadingKind.Temperature, "High", $"Day {i} high", d => d with { Min = -60, Max = 60 });
            Describe(at + "low", ReadingKind.Temperature, "Low", $"Day {i} low", d => d with { Min = -60, Max = 60 });
            Describe(at + "sunrise", ReadingKind.Time, "Sunrise", $"Day {i} sunrise");
            Describe(at + "sunset", ReadingKind.Time, "Sunset", $"Day {i} sunset");
            Describe(at + "precip-chance", ReadingKind.Percent, "Chance", $"Day {i} chance", d => d with { Min = 0, Max = 100 });
            Describe(at + "rain", ReadingKind.Depth, "Rain", $"Day {i} rain", d => d with { DepthUnit = DepthUnit.Millimetre });
            Describe(at + "snow", ReadingKind.Depth, "Snow", $"Day {i} snow", d => d with { DepthUnit = DepthUnit.Centimetre });
            Set(at + "date", Reading.Ok(new DateTimeOffset(2026, 10, 8 + i, 0, 0, 0, TimeSpan.FromHours(-7))));
            Set(at + "condition", Reading.Ok(days[i]));
            Set(at + "high", Reading.Ok(temperature));
            Set(at + "low", Reading.Ok(temperature - 5));
            Set(at + "sunrise", Reading.Ok(new DateTimeOffset(2026, 10, 8 + i, 6, 42, 0, TimeSpan.FromHours(-7))));
            Set(at + "sunset", Reading.Ok(new DateTimeOffset(2026, 10, 8 + i, 19, 3, 0, TimeSpan.FromHours(-7))));
            Set(at + "precip-chance", Reading.Ok(20));
            Set(at + "rain", Reading.Ok(0));
            Set(at + "snow", Reading.Ok(0));
        }
    }

    private WeatherWidget Widget(int height = 2, Action<WeatherConfig>? configure = null)
    {
        var widget = new WeatherWidget();
        widget.Attach(new Host(_source, _log));
        var config = new WeatherConfig { WidgetTypeId = "urdeck.widgets.weather", Width = 4, Height = height, Location = Place };
        configure?.Invoke(config);
        widget.Configure(config);
        _widgets.Add(widget);
        return widget;
    }

    /// <summary>A theme with the recording set (named or not), built on the default theme's typefaces.</summary>
    private Theme SetTheme(IconTint tint = IconTint.Accent, bool namesSet = true, float? titleSize = null)
    {
        var theme = new Theme
        {
            Typefaces = _base.Typefaces,
            TextStyle = _base.TextStyle,
            Padding = _base.Padding,
            Gap = _base.Gap,
            LabelSize = _base.LabelSize,
            BodySize = _base.BodySize,
            TitleSize = titleSize ?? _base.TitleSize,
            Ramp = [Cold, Hot],
            Icons = new IconTheme { Set = _set, NamesSet = namesSet, Tint = tint },
        };
        _themes.Add(theme);
        return theme;
    }

    private static SKBitmap Paint(WeatherWidget widget, Theme theme, int height, DateTime? time = null)
    {
        var bitmap = new SKBitmap(CardWidth, height);
        using var canvas = new SKCanvas(bitmap);
        float pad = theme.Padding;
        widget.Render(new WidgetRenderContext(
            canvas, time ?? T0, new System.Drawing.Size(CardWidth, height), theme, new SKRect(pad, pad, CardWidth - pad, height - pad), widget.Config, CancellationToken.None));
        return bitmap;
    }

    private static int Near(SKBitmap bitmap, SKColor color, SKRectI area)
    {
        int count = 0;
        for (int y = Math.Max(0, area.Top); y < Math.Min(bitmap.Height, area.Bottom); y++)
        {
            for (int x = Math.Max(0, area.Left); x < Math.Min(bitmap.Width, area.Right); x++)
            {
                SKColor c = bitmap.GetPixel(x, y);
                if (c.Alpha > 240 && Math.Abs(c.Red - color.Red) < 4 && Math.Abs(c.Green - color.Green) < 4 && Math.Abs(c.Blue - color.Blue) < 4)
                    count++;
            }
        }

        return count;
    }

    private static readonly SKRectI MainIcon = new(0, 0, 260, 260);

    [Fact]
    public void TheMainIcon_IsTheGlyphOfTheCondition_InTheThemesTint()
    {
        Fill("rain");
        using var theme = SetTheme();
        WeatherWidget widget = Widget();

        using SKBitmap bitmap = Paint(widget, theme, 550);

        Assert.Contains("weather/rain", _set.Asked);
        Assert.True(Near(bitmap, theme.Accent, MainIcon) > 1000);
    }

    [Fact]
    public void ClearAtNight_IsTheNightVariant()
    {
        Fill("clear", isDay: false);
        using var theme = SetTheme();

        using SKBitmap bitmap = Paint(Widget(), theme, 550);

        Assert.Contains("weather/clear-night", _set.Asked);
        Assert.DoesNotContain("weather/clear-day", _set.Asked);
    }

    [Fact]
    public void TheForecast_UsesTheDaytimeVariantEvenAtNight()
    {
        Fill("rain", isDay: false);
        using var theme = SetTheme();

        using SKBitmap bitmap = Paint(Widget(4), theme, 1100);

        Assert.Contains("weather/clear-day", _set.Asked);
        Assert.Contains("weather/snow", _set.Asked);
        Assert.Contains("weather/overcast", _set.Asked);
        Assert.Contains("weather/partly-cloudy-day", _set.Asked);
        Assert.Contains("weather/thunderstorms-day", _set.Asked);
        Assert.DoesNotContain("weather/clear-night", _set.Asked);
        Assert.DoesNotContain("weather/partly-cloudy-night", _set.Asked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fahrenheit")]
    public void RampTint_IsThePositionOfTheTemperatureInCelsius_WhateverUnitTheCardShows(string? unit)
    {
        Fill("rain", temperature: 15);
        using var theme = SetTheme(IconTint.Ramp);

        // 15 degrees Celsius, which is 59 Fahrenheit, is the middle of -10..40.
        using SKBitmap bitmap = Paint(Widget(2, c => c.Unit = unit), theme, 550);

        SKColor middle = ColorMath.Along([Cold, Hot], 0.5f);
        Assert.True(Near(bitmap, middle, MainIcon) > 1000);
        Assert.Equal(0, Near(bitmap, Hot, MainIcon));
    }

    [Fact]
    public void RampTint_IsClamped_AndTheForecastUsesTheDaysHigh()
    {
        Fill("rain", temperature: 45);
        using var theme = SetTheme(IconTint.Ramp);

        using SKBitmap bitmap = Paint(Widget(4), theme, 1100);

        Assert.True(Near(bitmap, Hot, MainIcon) > 1000);
        // Every forecast column's high is 45 too.
        Assert.True(Near(bitmap, Hot, new SKRectI(0, 600, CardWidth, 1100)) > 3000);
    }

    [Fact]
    public void RampTint_WithNoTemperature_IsTheMiddleOfTheRamp()
    {
        Fill("rain");
        Set("current/temperature", Reading.Unavailable("none"));
        using var theme = SetTheme(IconTint.Ramp);

        using SKBitmap bitmap = Paint(Widget(), theme, 550);

        Assert.True(Near(bitmap, ColorMath.Along([Cold, Hot], 0.5f), MainIcon) > 1000);
    }

    [Fact]
    public void ThereIsNoAnimationWork()
    {
        Fill("rain");
        using var theme = SetTheme();
        WeatherWidget widget = Widget(4, c => { c.Motion = "full"; c.AnimateForecast = JsonSerializer.SerializeToElement(true); });

        using SKBitmap bitmap = Paint(widget, theme, 1100);

        Assert.False(widget.IsAnimating);
        Assert.Null(widget.NextAnimationAt);
        Assert.Null(widget.LoadedIconKey);
        Assert.Empty(widget.LoadedForecastIcons);
        Assert.Equal(0, widget.PosterCount);
    }

    [Fact]
    public void AnUnavailableCondition_IsTheNotAvailableGlyph()
    {
        Fill(condition: null);
        using var theme = SetTheme();

        using SKBitmap bitmap = Paint(Widget(), theme, 550);

        Assert.Contains("weather/not-available", _set.Asked);
    }

    [Fact]
    public void AGlyphTheSetLacks_IsLoggedOnce_AndTheRestOfTheCardIsDrawn()
    {
        Fill("rain");
        _set.Missing.Add("weather/rain");
        using var theme = SetTheme();
        WeatherWidget widget = Widget();

        using SKBitmap first = Paint(widget, theme, 550);
        using SKBitmap second = Paint(widget, theme, 550);

        Assert.Single(_log, l => l.Contains("weather/rain", StringComparison.Ordinal));
        Assert.Equal(0, Near(first, theme.Accent, MainIcon));
        Assert.Contains(first.Pixels, p => p.Alpha > 0);
    }

    [Fact]
    public void ATheme_WithNoSet_StillDrawsTheAnimations()
    {
        Fill("rain");
        using var plain = SetTheme(namesSet: false);
        WeatherWidget widget = Widget(2, c => c.Motion = "full");

        using SKBitmap bitmap = Paint(widget, plain, 550);

        Assert.Equal("rain", widget.LoadedIconKey);
        Assert.True(widget.IsAnimating);
        Assert.Empty(_set.Asked);
    }

    [Fact]
    public void ChangingToASetTheme_ReleasesTheAnimation_AndBackLoadsItAgain()
    {
        Fill("rain");
        using var plain = SetTheme(namesSet: false);
        using var named = SetTheme();
        WeatherWidget widget = Widget(4, c => c.Motion = "full");

        using (SKBitmap one = Paint(widget, plain, 1100))
            Assert.Equal("rain", widget.LoadedIconKey);
        using (SKBitmap two = Paint(widget, named, 1100))
        {
            Assert.Null(widget.LoadedIconKey);
            Assert.False(widget.IsAnimating);
            Assert.Equal(0, widget.PosterCount);
        }

        using SKBitmap three = Paint(widget, plain, 1100);
        Assert.Equal("rain", widget.LoadedIconKey);
        Assert.True(widget.PosterCount > 0);
    }

    [Fact]
    public void ALargeTitleSize_TightensTheForecastRows_SoTheIconsStayReadable()
    {
        Fill("rain");
        using var theme = SetTheme(titleSize: 40);

        using SKBitmap bitmap = Paint(Widget(4), theme, 1100);

        // Five columns of about 190 px: an icon of at least 0.6 of a column is over 10 000 pixels each, so over 40 000 in all.
        int filled = Near(bitmap, theme.Accent, new SKRectI(0, 550, CardWidth, 1100));
        Assert.True(filled > 40000, $"filled {filled}");
    }
}
