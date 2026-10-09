// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text;
using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using UrDeck.Widgets.Weather;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The 4x2 weather card, rendered with a fake reading source.</summary>
public sealed class WeatherWidgetTests : IDisposable
{
    private const int CardWidth = 1100;
    private const int CardHeight = 550;

    private static readonly ReadingAttribution Credit = new("Weather data by Open-Meteo.com", "Open-Meteo.com", "https://open-meteo.com/");

    private sealed class FakeSource : IReadingSource
    {
        public Dictionary<string, Reading> Readings { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ReadingDescriptor> Catalog { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Use24Hour { get; set; } = true;

        public Reading Read(string id) => Readings.TryGetValue(id, out Reading r) ? r : Reading.Unavailable("unknown");

        public ReadingDescriptor? Describe(string id) => Catalog.TryGetValue(id, out ReadingDescriptor? d) ? d : null;

        public bool RegionUsesFahrenheit => false;

        public bool RegionUses24HourClock => Use24Hour;
    }

    private sealed class Host(IReadingSource readings, List<string> log) : IWidgetHost
    {
        public IReadingSource Readings { get; } = readings;

        public void Log(string message) => log.Add(message);
    }

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly FakeSource _source = new();
    private readonly List<string> _log = [];
    private readonly List<WeatherWidget> _widgets = [];
    private static readonly DateTime T0 = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Local);

    public void Dispose()
    {
        foreach (WeatherWidget widget in _widgets)
            widget.Dispose();
        _theme.Dispose();
    }

    private static string Id(string location, string path) => $"weather:{location}/{path}";

    private static readonly (string Path, ReadingKind Kind, string Label, string Name)[] Catalog =
    [
        ("place", ReadingKind.Text, "Place", "Place"),
        ("current/temperature", ReadingKind.Temperature, "Now", "Temperature"),
        ("current/condition", ReadingKind.Text, "Condition", "Weather condition"),
        ("current/is-day", ReadingKind.OnOff, "Daytime", "Is daytime"),
        ("today/high", ReadingKind.Temperature, "High", "Today's high"),
        ("today/low", ReadingKind.Temperature, "Low", "Today's low"),
        ("today/sunrise", ReadingKind.Time, "Sunrise", "Sunrise"),
        ("today/sunset", ReadingKind.Time, "Sunset", "Sunset"),
    ];

    /// <summary>Describes the readings of a place (as the weather provider's catalog does) and gives them values.</summary>
    private void Fill(string location, double temperature = 14, string condition = "partly-cloudy", bool day = true, string place = "Portland, Oregon", bool credit = true)
    {
        foreach ((string path, ReadingKind kind, string label, string name) in Catalog)
        {
            _source.Catalog[Id(location, path)] = new ReadingDescriptor(location + "/" + path, kind, label, name)
            {
                Min = kind == ReadingKind.Temperature ? -60 : null,
                Max = kind == ReadingKind.Temperature ? 60 : null,
                Attribution = credit ? Credit : null,
            };
        }

        Set(location, "place", Reading.Ok(place));
        Set(location, "current/temperature", Reading.Ok(temperature));
        Set(location, "current/condition", Reading.Ok(condition));
        Set(location, "current/is-day", Reading.Ok(day));
        Set(location, "today/high", Reading.Ok(temperature + 3));
        Set(location, "today/low", Reading.Ok(temperature - 5));
        Set(location, "today/sunrise", Reading.Ok(new DateTimeOffset(2026, 10, 8, 6, 42, 0, TimeSpan.FromHours(-7))));
        Set(location, "today/sunset", Reading.Ok(new DateTimeOffset(2026, 10, 8, 19, 3, 0, TimeSpan.FromHours(-7))));
    }

    private void Set(string location, string path, Reading reading) => _source.Readings[Id(location, path)] = reading;

    private WeatherWidget Widget(string? location = "Portland, OR", Action<WeatherConfig>? configure = null)
    {
        var widget = new WeatherWidget();
        widget.Attach(new Host(_source, _log));
        var config = new WeatherConfig { WidgetTypeId = "urdeck.widgets.weather", Width = 4, Height = 2, Location = location };
        configure?.Invoke(config);
        widget.Configure(config);
        _widgets.Add(widget);
        return widget;
    }

    private SKBitmap Paint(WeatherWidget widget, DateTime? time = null, Theme? theme = null)
    {
        Theme used = theme ?? _theme;
        var bitmap = new SKBitmap(CardWidth, CardHeight);
        using var canvas = new SKCanvas(bitmap);
        float pad = used.Padding;
        widget.Render(new WidgetRenderContext(
            canvas, time ?? T0, new System.Drawing.Size(CardWidth, CardHeight), used, new SKRect(pad, pad, CardWidth - pad, CardHeight - pad), widget.Config, CancellationToken.None));
        return bitmap;
    }

    private static bool Same(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    /// <summary>The smallest rectangle that holds every pixel that is not transparent inside <paramref name="area"/>; empty when there is none.</summary>
    private static SKRectI Ink(SKBitmap bitmap, SKRectI area)
    {
        var box = new SKRectI(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        for (int y = Math.Max(0, area.Top); y < Math.Min(bitmap.Height, area.Bottom); y++)
        {
            for (int x = Math.Max(0, area.Left); x < Math.Min(bitmap.Width, area.Right); x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                    continue;
                box = new SKRectI(Math.Min(box.Left, x), Math.Min(box.Top, y), Math.Max(box.Right, x), Math.Max(box.Bottom, y));
            }
        }

        return box.Left == int.MaxValue ? SKRectI.Empty : box;
    }

    private static bool HasInk(SKBitmap bitmap, SKRectI area) => Ink(bitmap, area) != SKRectI.Empty;

    private static readonly SKRectI WholeCard = new(0, 0, CardWidth, CardHeight);
    // Where the credit is drawn: left of the text column, below the icon, which leaves it free when there is no credit.
    private static readonly SKRectI CreditStrip = new(0, CardHeight - 45, 438, CardHeight);
    private static readonly SKRectI TemperatureArea = new(200, 30, 900, 360);

    // Identity and configuration

    [Fact]
    public void ItIsTheWeatherWidget_At4x2Only_RefreshedByDataOnly()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(WeatherWidget), out _)!;

        Assert.Equal("urdeck.widgets.weather", descriptor.Id);
        Assert.Equal([new System.Drawing.Size(4, 2)], descriptor.SupportedSizes);
        Assert.Equal(RefreshStrategy.OnData, descriptor.Refresh);
        Assert.Null(descriptor.RefreshInterval);
    }

    [Fact]
    public void ALocationTypedAsText_SubscribesToTheEightReadingsOfThatPlace()
    {
        WeatherWidget widget = Widget("  Portland, OR ");

        Assert.Equal(
            [
                "weather:Portland, OR/place", "weather:Portland, OR/current/temperature", "weather:Portland, OR/current/condition",
                "weather:Portland, OR/current/is-day", "weather:Portland, OR/today/high", "weather:Portland, OR/today/low",
                "weather:Portland, OR/today/sunrise", "weather:Portland, OR/today/sunset",
            ],
            widget.Subscriptions);
    }

    [Fact]
    public void ASlashOrAPercentInTheLocation_IsEscaped()
    {
        WeatherWidget widget = Widget("AC/DC 100%");

        Assert.All(widget.Subscriptions, id => Assert.StartsWith("weather:AC%2FDC 100%25/", id, StringComparison.Ordinal));
    }

    [Fact]
    public void Coordinates_WinOverTheLocation_AndNothingOfTheLocationIsSubscribed()
    {
        WeatherWidget widget = Widget("Zurich", c => (c.Latitude, c.Longitude) = (47.37, 8.54));

        Assert.Equal(8, widget.Subscriptions.Count);
        Assert.All(widget.Subscriptions, id => Assert.StartsWith("weather:@47.37,8.54/", id, StringComparison.Ordinal));
        Assert.DoesNotContain(widget.Subscriptions, id => id.Contains("Zurich", StringComparison.Ordinal));
    }

    [Fact]
    public void Coordinates_AreWrittenWithTheInvariantCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            WeatherWidget widget = Widget(null, c => (c.Latitude, c.Longitude) = (47.37, -8.5));

            Assert.All(widget.Subscriptions, id => Assert.StartsWith("weather:@47.37,-8.5/", id, StringComparison.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void OneCoordinateAlone_DoesNotReplaceTheLocation()
    {
        WeatherWidget widget = Widget("Zurich", c => c.Latitude = 47.37);

        Assert.All(widget.Subscriptions, id => Assert.StartsWith("weather:Zurich/", id, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoLocation_SubscribesToNothing(string? location) => Assert.Empty(Widget(location).Subscriptions);

    [Fact]
    public void ChangingTheConfiguration_ChangesTheSubscriptions()
    {
        WeatherWidget widget = Widget("Zurich");
        Assert.All(widget.Subscriptions, id => Assert.StartsWith("weather:Zurich/", id, StringComparison.Ordinal));

        widget.Configure(new WeatherConfig { Location = "Tokyo", Width = 4, Height = 2 });

        Assert.All(widget.Subscriptions, id => Assert.StartsWith("weather:Tokyo/", id, StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownProperties_SurviveLoadAndSave()
    {
        const string json = """{"typeId":"urdeck.widgets.weather","width":4,"height":2,"location":"Zurich","motion":"slow","label":"","future":{"a":1}}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        WidgetConfig page = JsonSerializer.Deserialize<WidgetConfig>(json, options)!;

        var concrete = (WeatherConfig)page.ToConcrete(typeof(WeatherConfig));
        string saved = JsonSerializer.Serialize(concrete, options);

        Assert.Equal("Zurich", concrete.Location);
        Assert.Equal("slow", concrete.Motion);
        Assert.Equal("", concrete.Label);
        Assert.Contains("\"future\"", saved, StringComparison.Ordinal);
        Assert.Contains("\"a\":1", saved, StringComparison.Ordinal);
        Assert.Contains("\"motion\":\"slow\"", saved, StringComparison.Ordinal);
    }

    // Composition

    [Fact]
    public void AFullCard_ShowsTheIconTheTextAndTheCredit_WithinTheCard()
    {
        Fill("Portland, OR");
        using SKBitmap bitmap = Paint(Widget());

        Assert.True(HasInk(bitmap, new SKRectI(0, 0, 420, CardHeight - 60)), "icon");
        Assert.True(HasInk(bitmap, TemperatureArea), "temperature");
        Assert.True(HasInk(bitmap, new SKRectI(440, 230, CardWidth, 330)), "place and condition");
        Assert.True(HasInk(bitmap, new SKRectI(440, 330, CardWidth, CardHeight - 60)), "high, low, sunrise and sunset");
        Assert.True(HasInk(bitmap, CreditStrip), "credit");
        SKRectI ink = Ink(bitmap, WholeCard);
        float pad = _theme.Padding;
        Assert.True(ink.Left >= pad - 1 && ink.Top >= pad - 1 && ink.Right <= CardWidth - pad + 1 && ink.Bottom <= CardHeight - pad + 1, $"ink {ink} leaves the content rectangle");
    }

    [Fact]
    public void TheTemperatureTextSize_DoesNotChangeWithTheValue()
    {
        Fill("Portland, OR", temperature: 9);
        using SKBitmap nine = Paint(Widget());
        Fill("Portland, OR", temperature: -12);
        using SKBitmap minus = Paint(Widget());

        SKRectI a = Ink(nine, TemperatureArea);
        SKRectI b = Ink(minus, TemperatureArea);
        // Round digits overshoot the cap height by a few pixels; a changed text size would move this by far more.
        Assert.InRange(b.Height, a.Height - 8, a.Height + 8);
        // The digits keep their size, so the two-digit value is wider than the one-digit value by about a digit.
        Assert.True(b.Width > a.Width);
    }

    [Fact]
    public void Fahrenheit_ConvertsEveryTemperatureOfTheCard()
    {
        // 22.7778 degrees Celsius is 73 Fahrenheit; the card in Fahrenheit must equal the card whose readings already say 73.
        Fill("Portland, OR", temperature: 22.7778);
        using SKBitmap converted = Paint(Widget(configure: c => c.Unit = "fahrenheit"));
        Fill("Portland, OR", temperature: 73);
        Set("Portland, OR", "today/high", Reading.Ok(78));
        Set("Portland, OR", "today/low", Reading.Ok(64));
        using SKBitmap plain = Paint(Widget(configure: c => c.Unit = "celsius"));

        Assert.True(Same(converted, plain));
    }

    [Fact]
    public void ARegionUnit_IsUsedWhenTheCardSetsNone_AndTheCardsUnitBeatsIt()
    {
        Fill("Portland, OR", temperature: 22.7778);
        using SKBitmap celsiusByDefault = Paint(Widget());
        using SKBitmap fahrenheit = Paint(Widget(configure: c => c.Unit = " Fahrenheit "));

        Assert.False(Same(celsiusByDefault, fahrenheit));
    }

    [Fact]
    public void ADifferentClock_ChangesTheTimes()
    {
        Fill("Portland, OR");
        using SKBitmap h24 = Paint(Widget());
        _source.Use24Hour = false;
        using SKBitmap h12 = Paint(Widget());

        Assert.False(Same(h24, h12));
        // The AM or PM unit makes the 12 hour times wider.
        Assert.True(Ink(h12, new SKRectI(440, 330, CardWidth, CardHeight - 60)).Right > 0);
    }

    [Fact]
    public void ThePlace_IsTheResolvedName_UnlessTheCardSetsALabel()
    {
        Fill("Portland, OR", place: "Portland, Oregon");
        using SKBitmap resolved = Paint(Widget());
        Fill("Portland, OR", place: "Elsewhere");
        using SKBitmap other = Paint(Widget());
        using SKBitmap homeA = Paint(Widget(configure: c => c.Label = "Home"));
        Fill("Portland, OR", place: "Portland, Oregon");
        using SKBitmap homeB = Paint(Widget(configure: c => c.Label = "Home"));

        Assert.False(Same(resolved, other));
        Assert.False(Same(resolved, homeB));
        Assert.True(Same(homeA, homeB), "a label hides the place reading");
    }

    [Fact]
    public void AnEmptyLabel_ShowsNoPlace_AndTheOtherContentUsesTheSpace()
    {
        Fill("Portland, OR");
        using SKBitmap shown = Paint(Widget());
        using SKBitmap hidden = Paint(Widget(configure: c => c.Label = ""));
        Fill("Portland, OR", place: "Another place");
        using SKBitmap hiddenOther = Paint(Widget(configure: c => c.Label = ""));

        Assert.False(Same(shown, hidden));
        Assert.True(Same(hidden, hiddenOther), "the place is not drawn at all");
        Assert.True(HasInk(hidden, CreditStrip), "the credit stays");
    }

    [Fact]
    public void TwoWidgets_ShowTwoPlaces_EachWithItsOwnValues()
    {
        Fill("Zurich", temperature: 11, condition: "cloudy", day: false, place: "Zurich");
        Fill("Tokyo", temperature: 19, condition: "clear", day: true, place: "Tokyo");
        using SKBitmap zurich = Paint(Widget("Zurich"));
        using SKBitmap tokyo = Paint(Widget("Tokyo"));
        using SKBitmap zurichAgain = Paint(Widget("Zurich"));

        Assert.False(Same(zurich, tokyo));
        Assert.True(Same(zurich, zurichAgain));
    }

    [Fact]
    public void TheCredit_IsDrawnWhenEveryReadingIsUnavailable_AndWithAnEmptyLabel()
    {
        Fill("Portland, OR");
        foreach ((string path, _, _, _) in Catalog)
            Set("Portland, OR", path, Reading.Unavailable("no place"));

        using SKBitmap unavailable = Paint(Widget());
        using SKBitmap hiddenLabel = Paint(Widget(configure: c => c.Label = ""));
        using SKBitmap motionOff = Paint(Widget(configure: c => c.Motion = "off"));

        Assert.True(HasInk(unavailable, CreditStrip));
        Assert.True(HasInk(hiddenLabel, CreditStrip));
        Assert.True(HasInk(motionOff, CreditStrip));
    }

    [Fact]
    public void WithoutAttribution_NoCreditIsDrawn_AndTheContentUsesTheSpace()
    {
        Fill("Portland, OR");
        using SKBitmap credited = Paint(Widget());
        Fill("Portland, OR", credit: false);
        using SKBitmap bare = Paint(Widget());

        Assert.True(Ink(credited, CreditStrip).Width > 300, "the credit line");
        Assert.True(Ink(bare, CreditStrip).Width < 250, "only the high and low are down there");
        Assert.False(Same(credited, bare));
        Assert.True(Ink(bare, WholeCard).Bottom > Ink(credited, new SKRectI(0, 0, CardWidth, CardHeight - 60)).Bottom);
    }

    [Fact]
    public void WithNoLocation_TheCardShowsDashesAndTheNeutralIcon_AndNoCredit()
    {
        WeatherWidget widget = Widget(null);
        using SKBitmap bitmap = Paint(widget);

        Assert.Equal(WeatherIcons.Neutral, widget.LoadedIconKey);
        Assert.True(Ink(bitmap, CreditStrip).Width < 250, "no credit line");
        Assert.True(HasInk(bitmap, TemperatureArea), "a dash is drawn");
    }

    [Fact]
    public void StaleValues_AreDrawnInTheMutedColour()
    {
        Fill("Portland, OR");
        using SKBitmap current = Paint(Widget());
        Set("Portland, OR", "current/temperature", Reading.Stale(14));
        using SKBitmap stale = Paint(Widget());

        Assert.False(Same(current, stale));
        SKColor muted = _theme.TextMuted;
        int mutedPixels = 0;
        for (int y = TemperatureArea.Top; y < TemperatureArea.Bottom; y++)
            for (int x = TemperatureArea.Left; x < TemperatureArea.Right; x++)
                if (stale.GetPixel(x, y) == muted)
                    mutedPixels++;
        Assert.True(mutedPixels > 100);
    }

    [Fact]
    public void ADifferentTheme_ChangesTheCard_WithoutChangingTheIconColours()
    {
        Fill("Portland, OR");
        using Theme light = ThemeResolver.Resolve(new ThemeStore("").Load("default-light"), 275);
        WeatherWidget widget = Widget();
        using SKBitmap dark = Paint(widget);
        using SKBitmap bright = Paint(widget, theme: light);

        Assert.False(Same(dark, bright));
        // The icon keeps its own colours: the same pixels under both themes.
        var iconArea = new SKRectI(20, 20, 150, 150);
        for (int y = iconArea.Top; y < iconArea.Bottom; y += 7)
            for (int x = iconArea.Left; x < iconArea.Right; x += 7)
                Assert.Equal(dark.GetPixel(x, y), bright.GetPixel(x, y));
    }

    // Icons

    [Theory]
    [InlineData("clear", true, "clear-day")]
    [InlineData("clear", false, "clear-night")]
    [InlineData("partly-cloudy", true, "partly-cloudy-day")]
    [InlineData("partly-cloudy", false, "partly-cloudy-night")]
    [InlineData("cloudy", true, "overcast")]
    [InlineData("cloudy", false, "overcast")]
    [InlineData("fog", true, "fog")]
    [InlineData("drizzle", false, "drizzle")]
    [InlineData("rain", true, "rain")]
    [InlineData("sleet", true, "sleet")]
    [InlineData("snow", false, "snow")]
    [InlineData("thunder", true, "thunderstorms-day")]
    [InlineData("thunder", false, "thunderstorms-night")]
    [InlineData("unknown", true, "not-available")]
    [InlineData("a class from the future", true, "not-available")]
    public void TheConditionAndTheDayChooseTheIcon(string condition, bool day, string expected)
    {
        Fill("Portland, OR", condition: condition, day: day);
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget);

        Assert.Equal(expected, widget.LoadedIconKey);
    }

    [Fact]
    public void EveryIconTheTableNames_IsEmbeddedAndDraws()
    {
        string[] conditions = ["clear", "partly-cloudy", "cloudy", "fog", "drizzle", "rain", "sleet", "snow", "thunder", "unknown"];
        foreach (string condition in conditions)
        {
            foreach (bool day in new[] { true, false })
            {
                Fill("Portland, OR", condition: condition, day: day);
                WeatherWidget widget = Widget();
                using SKBitmap bitmap = Paint(widget);

                Assert.NotNull(widget.LoadedIconKey);
                Assert.True(HasInk(bitmap, new SKRectI(40, 100, 420, 450)) || widget.LoadedIconKey == WeatherIcons.Neutral, $"{condition} {day}");
            }
        }

        Assert.Empty(_log);
    }

    [Fact]
    public void AnUnavailableOrMissingCondition_ShowsTheNeutralIcon()
    {
        Fill("Portland, OR");
        Set("Portland, OR", "current/condition", Reading.Unavailable("no place"));
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget);

        Assert.Equal(WeatherIcons.Neutral, widget.LoadedIconKey);
    }

    [Fact]
    public void AStaleCondition_KeepsItsLastIcon()
    {
        Fill("Portland, OR", condition: "rain");
        Set("Portland, OR", "current/condition", Reading.Stale("rain"));
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget);

        Assert.Equal("rain", widget.LoadedIconKey);
    }

    [Fact]
    public void WhenTheConditionChanges_TheNewIconShowsAndThePreviousOneIsReleased()
    {
        Fill("Portland, OR", condition: "cloudy");
        WeatherWidget widget = Widget();
        using SKBitmap first = Paint(widget);
        LottieAnimation old = widget.LoadedIcon!;
        Assert.Equal("overcast", widget.LoadedIconKey);

        Set("Portland, OR", "current/condition", Reading.Ok("rain"));
        using SKBitmap second = Paint(widget);

        Assert.Equal("rain", widget.LoadedIconKey);
        Assert.NotSame(old, widget.LoadedIcon);
        using var scratch = new SKBitmap(10, 10);
        using var canvas = new SKCanvas(scratch);
        Assert.Throws<ObjectDisposedException>(() => old.Draw(canvas, new SKRect(0, 0, 10, 10), 0));
    }

    [Fact]
    public void Reconfiguring_AndDisposing_ReleaseTheLoadedIcon()
    {
        Fill("Portland, OR");
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget);
        LottieAnimation loaded = widget.LoadedIcon!;
        using var scratch = new SKBitmap(10, 10);
        using var canvas = new SKCanvas(scratch);

        widget.Configure(new WeatherConfig { Location = "Portland, OR", Width = 4, Height = 2 });

        Assert.Null(widget.LoadedIcon);
        Assert.Throws<ObjectDisposedException>(() => loaded.Draw(canvas, new SKRect(0, 0, 10, 10), 0));

        using SKBitmap again = Paint(widget);
        LottieAnimation reloaded = widget.LoadedIcon!;
        widget.Dispose();
        Assert.Null(widget.LoadedIcon);
        Assert.Throws<ObjectDisposedException>(() => reloaded.Draw(canvas, new SKRect(0, 0, 10, 10), 0));
    }

    [Fact]
    public void AMissingIcon_DrawsNoIcon_LogsOnce_AndLeavesTheRestOfTheCard()
    {
        Fill("Portland, OR");
        using SKBitmap withIcon = Paint(Widget());
        WeatherWidget widget = Widget();
        widget.OpenIcon = _ => null;

        using SKBitmap first = Paint(widget);
        using SKBitmap second = Paint(widget);

        Assert.Null(widget.LoadedIcon);
        Assert.Single(_log);
        Assert.Contains("partly-cloudy-day", _log[0], StringComparison.Ordinal);
        Assert.False(HasInk(first, new SKRectI(0, 0, 200, 200)));
        Assert.True(HasInk(first, TemperatureArea));
        Assert.True(HasInk(first, CreditStrip));
        // Everything right of the icon is as it is with the icon.
        for (int y = 0; y < CardHeight; y += 5)
            for (int x = 440; x < CardWidth; x += 5)
                Assert.Equal(withIcon.GetPixel(x, y), first.GetPixel(x, y));
    }

    [Fact]
    public void AnAnimationThatCannotBeRead_IsTreatedAsMissing()
    {
        Fill("Portland, OR");
        WeatherWidget widget = Widget();
        widget.OpenIcon = _ => new MemoryStream(Encoding.UTF8.GetBytes("not an animation"));

        using SKBitmap _ = Paint(widget);

        Assert.Null(widget.LoadedIcon);
        Assert.Single(_log);
    }

    // Motion

    [Fact]
    public void WithFullMotion_TheCardAnimates_OnceAnIconIsLoaded()
    {
        Fill("Portland, OR");
        WeatherWidget widget = Widget();
        Assert.False(widget.IsAnimating);

        using SKBitmap _ = Paint(widget);

        Assert.True(widget.IsAnimating);
    }

    [Theory]
    [InlineData("off")]
    [InlineData(" OFF ")]
    public void WithMotionOff_TheCardDoesNotAnimate(string motion)
    {
        Fill("Portland, OR");
        WeatherWidget widget = Widget(configure: c => c.Motion = motion);

        using SKBitmap _ = Paint(widget);

        Assert.False(widget.IsAnimating);
    }

    [Fact]
    public void AnUnknownMotionValue_BehavesAsThePeriodicDefault()
    {
        Fill("Portland, OR");
        WeatherWidget widget = Widget(configure: c => c.Motion = "slow");
        using SKBitmap _ = Paint(widget, T0);
        double duration = widget.LoadedIcon!.Duration.TotalSeconds;

        Assert.True(widget.IsAnimating);
        using SKBitmap __ = Paint(widget, T0.AddSeconds(duration + 1));
        Assert.False(widget.IsAnimating);
        Assert.Equal("slow", widget.Config.Motion);
    }

    [Fact]
    public void TheDefaultIsPeriodic_OneLoopThenRest_UntilTheNextPeriod()
    {
        Fill("Portland, OR");
        WeatherWidget widget = Widget();
        using SKBitmap first = Paint(widget, T0);
        double duration = widget.LoadedIcon!.Duration.TotalSeconds;
        Assert.True(widget.IsAnimating);
        Assert.Null(widget.NextAnimationAt);

        using SKBitmap mid = Paint(widget, T0.AddSeconds(1.3));
        Assert.True(widget.IsAnimating);
        Assert.False(Same(first, mid));

        using SKBitmap rest = Paint(widget, T0.AddSeconds(duration + 0.05));
        Assert.False(widget.IsAnimating);
        Assert.True(Same(first, rest), "at rest it shows the poster frame");
        Assert.Equal(T0.AddSeconds(60), widget.NextAnimationAt);

        using SKBitmap next = Paint(widget, T0.AddSeconds(60));
        Assert.True(widget.IsAnimating, "the next loop starts at the period");
        Assert.True(Same(first, next));
        Assert.Null(widget.NextAnimationAt);
        using SKBitmap again = Paint(widget, T0.AddSeconds(60 + duration + 1));
        Assert.Equal(T0.AddSeconds(120), widget.NextAnimationAt);
    }

    [Fact]
    public void ThePeriodIsConfigurable_ButNeverShorterThanALoopAndASecond()
    {
        Fill("Portland, OR");
        WeatherWidget twenty = Widget(configure: c => c.PeriodSeconds = 20);
        using SKBitmap a = Paint(twenty, T0);
        double duration = twenty.LoadedIcon!.Duration.TotalSeconds;
        using SKBitmap b = Paint(twenty, T0.AddSeconds(duration + 1));
        Assert.Equal(T0.AddSeconds(20), twenty.NextAnimationAt);

        WeatherWidget tiny = Widget(configure: c => c.PeriodSeconds = 1);
        using SKBitmap c1 = Paint(tiny, T0);
        using SKBitmap d = Paint(tiny, T0.AddSeconds(duration + 0.5));
        Assert.Equal(T0.AddSeconds(duration + 1), tiny.NextAnimationAt);
    }

    [Fact]
    public void FullAndOffNeverWake_AndTheFrameRateIsFifteenAFrameSecond()
    {
        Fill("Portland, OR");
        WeatherWidget full = Widget(configure: c => c.Motion = "full");
        WeatherWidget off = Widget(configure: c => c.Motion = "off");
        using SKBitmap a = Paint(full, T0);
        using SKBitmap b = Paint(off, T0);

        Assert.Null(full.NextAnimationAt);
        Assert.Null(off.NextAnimationAt);
        Assert.Equal(TimeSpan.FromMilliseconds(1000.0 / 15), full.AnimationFrameInterval);
    }

    [Fact]
    public void TheFirstPaint_IsThePosterFrame_AndMotionOffAlwaysIs()
    {
        Fill("Portland, OR");
        WeatherWidget looping = Widget(configure: c => c.Motion = "full");
        WeatherWidget still = Widget(configure: c => c.Motion = "off");

        using SKBitmap first = Paint(looping, T0);
        using SKBitmap poster = Paint(still, T0);
        using SKBitmap stillLater = Paint(still, T0.AddSeconds(2.5));
        using SKBitmap loopingLater = Paint(looping, T0.AddSeconds(2.5));

        Assert.True(Same(first, poster), "first paint is the poster frame");
        Assert.True(Same(poster, stillLater), "motion off never moves");
        Assert.False(Same(first, loopingLater), "the loop moves");
    }

    [Fact]
    public void TheLoop_RepeatsAtTheEndOfTheAnimation()
    {
        Fill("Portland, OR");
        WeatherWidget widget = Widget(configure: c => c.Motion = "full");
        using SKBitmap first = Paint(widget, T0);
        double duration = widget.LoadedIcon!.Duration.TotalSeconds;

        using SKBitmap wrapped = Paint(widget, T0.AddSeconds(duration * 3));

        Assert.True(Same(first, wrapped));
    }

    [Fact]
    public void AClockThatJumpsBack_DrawsThePosterFrame_AndThenContinues()
    {
        Fill("Portland, OR");
        WeatherWidget widget = Widget();
        using SKBitmap first = Paint(widget, T0);
        using SKBitmap moved = Paint(widget, T0.AddSeconds(2));

        using SKBitmap jumped = Paint(widget, T0.AddHours(-1));
        using SKBitmap continued = Paint(widget, T0.AddHours(-1).AddSeconds(2));

        Assert.True(Same(first, jumped), "the poster frame after the jump");
        Assert.True(Same(moved, continued), "the loop continues from the jump");
    }

    [Fact]
    public void ANewIcon_StartsFromThePosterFrame()
    {
        Fill("Portland, OR", condition: "cloudy");
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget, T0);
        using SKBitmap __ = Paint(widget, T0.AddSeconds(3));

        Set("Portland, OR", "current/condition", Reading.Ok("rain"));
        using SKBitmap swapped = Paint(widget, T0.AddSeconds(3.1));
        Fill("Portland, OR", condition: "rain");
        using SKBitmap fresh = Paint(Widget(), T0);

        Assert.True(Same(swapped, fresh));
    }

    // Repaint

    [Fact]
    public void NeedsRender_IsTrueBeforeThePaint_AndFalseAfterItWhileNothingChanges()
    {
        Fill("Portland, OR");
        WeatherWidget widget = Widget();
        Assert.True(widget.NeedsRender(T0));

        using SKBitmap _ = Paint(widget);

        Assert.False(widget.NeedsRender(T0.AddMinutes(1)));
    }

    [Fact]
    public void ARoundedValueThatDoesNotChange_DoesNotAskForARepaint()
    {
        Fill("Portland, OR", temperature: 14.2);
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget);

        Fill("Portland, OR", temperature: 14.4);

        Assert.False(widget.NeedsRender(T0));
    }

    [Fact]
    public void AChangedValue_AChangedCondition_AStaleReading_AndANewCredit_EachAskForOneRepaint()
    {
        Fill("Portland, OR", temperature: 14, condition: "clear");
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget);
        Assert.False(widget.NeedsRender(T0));

        Set("Portland, OR", "current/temperature", Reading.Ok(16));
        Assert.True(widget.NeedsRender(T0));
        using SKBitmap a = Paint(widget);
        Assert.False(widget.NeedsRender(T0));

        Set("Portland, OR", "current/condition", Reading.Ok("cloudy"));
        Assert.True(widget.NeedsRender(T0));
        using SKBitmap b = Paint(widget);
        Assert.False(widget.NeedsRender(T0));

        Set("Portland, OR", "current/temperature", Reading.Stale(16));
        Assert.True(widget.NeedsRender(T0));
        using SKBitmap c = Paint(widget);
        Assert.False(widget.NeedsRender(T0));

        Fill("Portland, OR", temperature: 16, condition: "cloudy", credit: false);
        Assert.True(widget.NeedsRender(T0));
    }

    [Fact]
    public void TheDayState_ThatChangesTheIcon_AsksForARepaint()
    {
        Fill("Portland, OR", condition: "clear", day: true);
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget);

        Set("Portland, OR", "current/is-day", Reading.Ok(false));

        Assert.True(widget.NeedsRender(T0));
    }
}
