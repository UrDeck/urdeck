// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Widgets.Weather;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The 4x4 weather card (forecast columns, snow total, forecast icons), rendered with a fake reading source.</summary>
public sealed class WeatherForecastWidgetTests : IDisposable
{
    private const int CardWidth = 1100;
    private const int CardHeight = 1100;
    private const string Place = "Portland, OR";

    private static readonly ReadingAttribution Credit = new("Weather data by Open-Meteo.com", "Open-Meteo.com", "https://open-meteo.com/");
    private static readonly DateTime T0 = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Local);

    private sealed class FakeSource : IReadingSource
    {
        public Dictionary<string, Reading> Readings { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ReadingDescriptor> Catalog { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Fahrenheit { get; set; }

        public Reading Read(string id) => Readings.TryGetValue(id, out Reading r) ? r : Reading.Unavailable("unknown");

        public ReadingDescriptor? Describe(string id) => Catalog.TryGetValue(id, out ReadingDescriptor? d) ? d : null;

        public bool RegionUsesFahrenheit => Fahrenheit;

        public CultureInfo RegionCulture => CultureInfo.GetCultureInfo("en-US");
    }

    private sealed class Host(IReadingSource readings, List<string> log) : IWidgetHost
    {
        public IReadingSource Readings { get; } = readings;

        public void Log(string message) => log.Add(message);
    }

    /// <summary>One forecast day of the fake place.</summary>
    private sealed record Day(string Condition, double High, double Low, double Chance, double Rain, double Snow);

    private static readonly Day[] DefaultDays =
    [
        new("snow", 1, -6, 90, 0, 0),
        new("snow", 0, -8, 100, 0, 5.4),
        new("snow", -2, -9, 100, 0, 10.9),
        new("cloudy", 3, -4, 40, 0, 7.1),
        new("clear", 4, -3, 5, 0, 0),
        new("rain", 6, 1, 70, 7, 0),
        new("partly-cloudy", 5, 0, 20, 0, 0),
    ];

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);
    private readonly FakeSource _source = new();
    private readonly List<string> _log = [];
    private readonly List<WeatherWidget> _widgets = [];

    public void Dispose()
    {
        foreach (WeatherWidget widget in _widgets)
            widget.Dispose();
        _theme.Dispose();
    }

    private static string Id(string path) => $"weather:{Place}/{path}";

    private void Describe(string path, ReadingKind kind, string label, string name, Func<ReadingDescriptor, ReadingDescriptor>? more = null)
    {
        var descriptor = new ReadingDescriptor(Place + "/" + path, kind, label, name) { Attribution = Credit };
        _source.Catalog[Id(path)] = more?.Invoke(descriptor) ?? descriptor;
    }

    private void Set(string path, Reading reading) => _source.Readings[Id(path)] = reading;

    /// <summary>Describes every reading of the place and gives the days their values.</summary>
    private void Fill(Day[]? days = null, string condition = "clear", bool isDay = true)
    {
        days ??= DefaultDays;
        Describe("place", ReadingKind.Text, "Place", "Place");
        Describe("current/temperature", ReadingKind.Temperature, "Now", "Temperature", d => d with { Min = -60, Max = 60 });
        Describe("current/apparent", ReadingKind.Temperature, "Feels like", "Feels like temperature", d => d with { Min = -60, Max = 60 });
        Describe("current/condition", ReadingKind.Text, "Condition", "Weather condition");
        Describe("current/is-day", ReadingKind.OnOff, "Daytime", "Is daytime");
        Describe("current/humidity", ReadingKind.Percent, "Humidity", "Relative humidity", d => d with { Min = 0, Max = 100 });
        Describe("current/wind-speed", ReadingKind.Speed, "Wind", "Wind speed", d => d with { Min = 0, Max = 150 });
        Describe("current/wind-direction", ReadingKind.Number, "Direction", "Wind direction", d => d with { Unit = "°", Min = 0, Max = 360 });
        Set("place", Reading.Ok("Portland, Oregon"));
        Set("current/temperature", Reading.Ok(-2));
        Set("current/apparent", Reading.Ok(-7));
        Set("current/condition", Reading.Ok(condition));
        Set("current/is-day", Reading.Ok(isDay));
        Set("current/humidity", Reading.Ok(87));
        Set("current/wind-speed", Reading.Ok(22.5));
        Set("current/wind-direction", Reading.Ok(315));
        for (int i = 0; i < 7; i++)
        {
            string at = $"daily/{i}/";
            Describe(at + "date", ReadingKind.Date, "Day", $"Forecast day {i} date");
            Describe(at + "condition", ReadingKind.Text, "Condition", $"Forecast day {i} condition");
            Describe(at + "high", ReadingKind.Temperature, "High", $"Forecast day {i} high", d => d with { Min = -60, Max = 60 });
            Describe(at + "low", ReadingKind.Temperature, "Low", $"Forecast day {i} low", d => d with { Min = -60, Max = 60 });
            Describe(at + "sunrise", ReadingKind.Time, "Sunrise", $"Forecast day {i} sunrise");
            Describe(at + "sunset", ReadingKind.Time, "Sunset", $"Forecast day {i} sunset");
            Describe(at + "precip-chance", ReadingKind.Percent, "Chance", $"Forecast day {i} chance", d => d with { Min = 0, Max = 100 });
            Describe(at + "rain", ReadingKind.Depth, "Rain", $"Forecast day {i} rain", d => d with { DepthUnit = DepthUnit.Millimetre });
            Describe(at + "snow", ReadingKind.Depth, "Snow", $"Forecast day {i} snow", d => d with { DepthUnit = DepthUnit.Centimetre });

            Day day = days[i];
            // 2026-10-08 is a Thursday.
            Set(at + "date", Reading.Ok(new DateTimeOffset(2026, 10, 8 + i, 0, 0, 0, TimeSpan.FromHours(-7))));
            Set(at + "condition", Reading.Ok(day.Condition));
            Set(at + "high", Reading.Ok(day.High));
            Set(at + "low", Reading.Ok(day.Low));
            Set(at + "sunrise", Reading.Ok(new DateTimeOffset(2026, 10, 8 + i, 6, 42, 0, TimeSpan.FromHours(-7))));
            Set(at + "sunset", Reading.Ok(new DateTimeOffset(2026, 10, 8 + i, 19, 3, 0, TimeSpan.FromHours(-7))));
            Set(at + "precip-chance", Reading.Ok(day.Chance));
            Set(at + "rain", Reading.Ok(day.Rain));
            Set(at + "snow", Reading.Ok(day.Snow));
        }
    }

    private WeatherWidget Widget(Action<WeatherConfig>? configure = null, int height = 4)
    {
        var widget = new WeatherWidget();
        widget.Attach(new Host(_source, _log));
        var config = new WeatherConfig { WidgetTypeId = "urdeck.widgets.weather", Width = 4, Height = height, Location = Place };
        configure?.Invoke(config);
        widget.Configure(config);
        _widgets.Add(widget);
        return widget;
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private SKBitmap Paint(WeatherWidget widget, DateTime? time = null, Theme? theme = null, int height = CardHeight)
    {
        Theme used = theme ?? _theme;
        var bitmap = new SKBitmap(CardWidth, height);
        using var canvas = new SKCanvas(bitmap);
        float pad = used.Padding;
        widget.Render(new WidgetRenderContext(
            canvas, time ?? T0, new System.Drawing.Size(CardWidth, height), used, new SKRect(pad, pad, CardWidth - pad, height - pad), widget.Config, CancellationToken.None));
        return bitmap;
    }

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

    private static bool Same(SKBitmap a, SKBitmap b) => a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    private static bool SameRegion(SKBitmap a, SKBitmap b, SKRectI area)
    {
        for (int y = area.Top; y < area.Bottom; y++)
        {
            for (int x = area.Left; x < area.Right; x++)
            {
                if (a.GetPixel(x, y) != b.GetPixel(x, y))
                    return false;
            }
        }

        return true;
    }

    private static SKRectI Area(float left, float top, float right, float bottom) =>
        new((int)Math.Floor(left), (int)Math.Floor(top), (int)Math.Ceiling(right), (int)Math.Ceiling(bottom));

    /// <summary>Where the forecast panel puts its rows at 1100 x 1100 with one credit, worked out from the theme as the widget does.</summary>
    private (float LowerTop, float ColumnsTop, float RainTop, float SnowTop, float BodyBottom) Rows(int days)
    {
        float pad = _theme.Padding;
        float gap = _theme.Gap;
        float label = _theme.LabelSize * 1.5f;
        float title = _theme.TitleSize * 1.5f;
        float bodyBottom = CardHeight - pad - label - gap;
        float half = (bodyBottom - pad - (5 * gap)) / 2;
        float lowerTop = pad + half + (5 * gap);
        float columnsTop = lowerTop + (2 * title) + gap;
        float column = (CardWidth - (2 * pad)) / days;
        float room = bodyBottom - columnsTop - (5 * title);
        float icon = Math.Max(0, Math.Min(column, room));
        float start = columnsTop + (Math.Max(0, room - icon) / 2);
        float rainTop = start + title + icon + title + title;
        return (lowerTop, start, rainTop, rainTop + title, bodyBottom);
    }

    private static Day[] Dry() => [.. DefaultDays.Select(d => d with { Rain = 0, Snow = 0 })];

    private static Day[] With(Day[] days, int index, Func<Day, Day> change)
    {
        Day[] copy = [.. days];
        copy[index] = change(copy[index]);
        return copy;
    }

    private static int DaysSubscribed(WeatherWidget widget) => widget.Subscriptions.Count(id => id.EndsWith("/date", StringComparison.Ordinal));

    // Identity, size and subscriptions

    [Fact]
    public void ItOffersBothSizes_AsOneWidget()
    {
        var descriptor = WidgetDescriptor.TryCreate(typeof(WeatherWidget), out _)!;

        Assert.Equal("urdeck.widgets.weather", descriptor.Id);
        Assert.Equal([new System.Drawing.Size(4, 2), new System.Drawing.Size(4, 4)], descriptor.SupportedSizes);
        Assert.Equal(RefreshStrategy.OnData, descriptor.Refresh);
        Assert.Null(descriptor.RefreshInterval);
    }

    [Fact]
    public void At4x4_TheCardSubscribesToTheNowReadings_AndEveryShownDay()
    {
        Fill();
        WeatherWidget widget = Widget();

        string[] ids = [.. widget.Subscriptions];
        Assert.Equal(45, ids.Length);
        Assert.Equal(ids.Length, ids.Distinct().Count());
        foreach (string path in new[] { "current/apparent", "current/humidity", "current/wind-speed", "current/wind-direction", "daily/0/sunrise", "daily/0/sunset" })
            Assert.Contains(Id(path), ids);
        foreach (string field in new[] { "date", "condition", "high", "low", "precip-chance", "rain", "snow" })
        {
            Assert.Contains(Id($"daily/4/{field}"), ids);
            Assert.DoesNotContain(Id($"daily/5/{field}"), ids);
        }

        Assert.DoesNotContain(Id("daily/1/sunrise"), ids);
    }

    [Fact]
    public void At4x2_TheCardSubscribesToTheEightReadingsOnly()
    {
        WeatherWidget widget = Widget(height: 2);

        Assert.Equal(
            ["place", "current/temperature", "current/condition", "current/is-day", "daily/0/high", "daily/0/low", "daily/0/sunrise", "daily/0/sunset"],
            widget.Subscriptions.Select(id => id[(Provider(widget) + "/").Length..]));
    }

    private static string Provider(WeatherWidget widget) => "weather:" + widget.Config.Location;

    [Fact]
    public void Days_DefaultToFive_AreClamped_AndRoundedToAWholeNumber()
    {
        Assert.Equal(5, DaysSubscribed(Widget()));
        Assert.Equal(3, DaysSubscribed(Widget(c => c.Days = Json(3))));
        Assert.Equal(7, DaysSubscribed(Widget(c => c.Days = Json(12))));
        Assert.Equal(1, DaysSubscribed(Widget(c => c.Days = Json(0))));
        Assert.Equal(1, DaysSubscribed(Widget(c => c.Days = Json(-4))));
        Assert.Equal(3, DaysSubscribed(Widget(c => c.Days = Json(2.6))));
    }

    [Fact]
    public void ADaysValueThatIsNotANumber_CountsAsNotPresent_AndIsKeptInTheFile()
    {
        const string json = """{"typeId":"urdeck.widgets.weather","width":4,"height":4,"location":"Portland, OR","days":"many","animateForecast":"yes"}""";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        WidgetConfig page = JsonSerializer.Deserialize<WidgetConfig>(json, options)!;

        var concrete = (WeatherConfig)page.ToConcrete(typeof(WeatherConfig));
        string saved = JsonSerializer.Serialize(concrete, options);

        var widget = new WeatherWidget();
        widget.Attach(new Host(_source, _log));
        widget.Configure(concrete);
        _widgets.Add(widget);
        Assert.Equal(5, DaysSubscribed(widget));
        Assert.Contains("\"days\":\"many\"", saved, StringComparison.Ordinal);
        Assert.Contains("\"animateForecast\":\"yes\"", saved, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangingDays_ChangesTheSubscriptions()
    {
        WeatherWidget widget = Widget();
        Assert.Equal(5, DaysSubscribed(widget));

        widget.Configure(new WeatherConfig { Location = Place, Width = 4, Height = 4, Days = Json(2) });

        Assert.Equal(2, DaysSubscribed(widget));
    }

    // Composition

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(7)]
    public void AFullCard_ShowsEverythingWithinTheCard_AndTheColumnsDoNotTouch(int days)
    {
        Fill();
        using SKBitmap bitmap = Paint(Widget(c => c.Days = Json(days)));
        (float lowerTop, float columnsTop, _, _, float bodyBottom) = Rows(days);
        float pad = _theme.Padding;

        SKRectI ink = Ink(bitmap, new SKRectI(0, 0, CardWidth, CardHeight));
        Assert.True(ink.Left >= pad - 1 && ink.Top >= pad - 1 && ink.Right <= CardWidth - pad + 1 && ink.Bottom <= CardHeight - pad + 1, $"ink {ink} leaves the content rectangle");
        Assert.True(HasInk(bitmap, Area(pad, pad, CardWidth - pad, lowerTop - 1)), "the 4x2 composition");
        Assert.True(HasInk(bitmap, Area(pad, lowerTop, CardWidth - pad, columnsTop - 1)), "the strip of now and the snow total");
        Assert.True(HasInk(bitmap, Area(pad, bodyBottom + 1, CardWidth - pad, CardHeight)), "the credit");

        float column = (CardWidth - (2 * pad)) / days;
        for (int i = 0; i < days; i++)
        {
            float left = pad + (i * column);
            Assert.True(HasInk(bitmap, Area(left, columnsTop, left + column, columnsTop + 40)), $"weekday of day {i}");
            Assert.True(HasInk(bitmap, Area(left, columnsTop + 40, left + column, bodyBottom - 100)), $"icon and temperatures of day {i}");
            if (i == 0)
                continue;

            // Between two columns there is a gutter no text or icon reaches into.
            Assert.False(HasInk(bitmap, Area(left - 2, columnsTop, left + 2, bodyBottom)), $"ink on the line between columns {i - 1} and {i}");
        }
    }

    [Fact]
    public void AColumnPerDay_ShowsTheWeekdayAtThePlace_TheIcon_TheTemperaturesAndTheChance()
    {
        Fill();
        IReadOnlyList<string> texts = Widget(c => c.Days = Json(3)).ForecastTexts();

        Assert.Equal(6, texts.Count);
        Assert.Equal("Thu | snow | 1°/-6° | 90% |  | ", texts[3]);
        Assert.Equal("Fri | snow | 0°/-8° | 100% |  | 5.4 cm snow", texts[4]);
        Assert.Equal("Sat | snow | -2°/-9° | 100% |  | 11 cm snow", texts[5]);
    }

    [Fact]
    public void TheWeekday_IsTheWeekdayAtThePlace_NotAtThePc()
    {
        Fill();
        // 2026-10-09 00:00 at +09:00 is still Thursday evening in a zone at -07:00.
        Set("daily/0/date", Reading.Ok(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.FromHours(9))));
        Set("daily/1/date", Reading.Ok(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.FromHours(9))));

        IReadOnlyList<string> texts = Widget().ForecastTexts();

        Assert.StartsWith("Fri |", texts[3], StringComparison.Ordinal);
        Assert.StartsWith("Sat |", texts[4], StringComparison.Ordinal);
    }

    [Fact]
    public void TheStripOfNow_ShowsFeelsLikeHumidityAndWindWithItsCompassPoint()
    {
        Fill();
        Assert.Equal("Feels like -7°   Humidity 87%   Wind 23 km/h NW", Widget().ForecastTexts()[0]);

        _source.Fahrenheit = true;
        Assert.Equal("Feels like 19°   Humidity 87%   Wind 14 mph NW", Widget().ForecastTexts()[0]);
    }

    [Fact]
    public void TheStripOfNow_ShowsDashesForWhatIsMissing()
    {
        Fill();
        Set("current/wind-speed", Reading.Unavailable("no wind"));
        Set("current/humidity", Reading.Pending());

        Assert.Equal("Feels like -7°   Humidity –   Wind –", Widget().ForecastTexts()[0]);
    }

    [Fact]
    public void UnitFahrenheit_ChoosesInchesAndMilesPerHourForTheWholeCard()
    {
        Fill();
        IReadOnlyList<string> imperial = Widget(c => c.Unit = "fahrenheit").ForecastTexts();
        _source.Fahrenheit = true;
        IReadOnlyList<string> metric = Widget(c => c.Unit = "celsius").ForecastTexts();

        Assert.Equal("Feels like 19°   Humidity 87%   Wind 14 mph NW", imperial[0]);
        Assert.Equal("9.2\"", imperial[2]);
        Assert.EndsWith("2.1\" snow", imperial[4], StringComparison.Ordinal);
        Assert.Equal("Feels like -7°   Humidity 87%   Wind 23 km/h NW", metric[0]);
        Assert.Equal("23 cm", metric[2]);
        Assert.EndsWith("5.4 cm snow", metric[4], StringComparison.Ordinal);
    }

    [Fact]
    public void ADayWithRainAndSnow_ShowsBothAmounts_AndTheOtherColumnsStayAligned()
    {
        Fill(Dry());
        using SKBitmap dry = Paint(Widget());
        Fill(With(Dry(), 2, d => d with { Rain = 7, Snow = 7.5 }));
        WeatherWidget widget = Widget();
        using SKBitmap mixed = Paint(widget);
        (_, float columnsTop, float rainTop, float snowTop, float bodyBottom) = Rows(5);
        float pad = _theme.Padding;
        float column = (CardWidth - (2 * pad)) / 5;

        Assert.EndsWith("| 7.0 mm rain | 7.5 cm snow", widget.ForecastTexts()[5], StringComparison.Ordinal);
        Assert.False(HasInk(dry, Area(pad, rainTop, CardWidth - pad, bodyBottom)), "a dry week draws no amounts");
        SKRectI rain = Ink(mixed, Area(pad, rainTop, CardWidth - pad, snowTop));
        SKRectI snow = Ink(mixed, Area(pad, snowTop, CardWidth - pad, bodyBottom));
        Assert.NotEqual(SKRectI.Empty, rain);
        Assert.NotEqual(SKRectI.Empty, snow);
        Assert.True(rain.Left >= pad + (2 * column) && rain.Right <= pad + (3 * column), "the rain stays in its column");
        Assert.True(snow.Left >= pad + (2 * column) && snow.Right <= pad + (3 * column), "the snow stays in its column");
        Assert.True(rain.Bottom < snow.Top, "rain above snow");
        Assert.True(SameRegion(dry, mixed, Area(pad, columnsTop, CardWidth - pad, rainTop)), "weekdays, icons, temperatures and chances do not move");
    }

    [Fact]
    public void ADryDay_ShowsNoAmount()
    {
        Fill(Dry());
        WeatherWidget widget = Widget();

        Assert.EndsWith("| 90% |  | ", widget.ForecastTexts()[3], StringComparison.Ordinal);
    }

    [Fact]
    public void ATraceOfSnow_IsShownAsLessThanOneTenthOfAnInch_AndZeroIsNotShown()
    {
        Fill(With(Dry(), 1, d => d with { Snow = 0.1 }));

        IReadOnlyList<string> texts = Widget(c => c.Unit = "fahrenheit").ForecastTexts();

        Assert.EndsWith("| <0.1\" snow", texts[4], StringComparison.Ordinal);
        Assert.EndsWith("|  | ", texts[3], StringComparison.Ordinal);
        Assert.Equal("<0.1\"", texts[2]);
    }

    [Fact]
    public void ADayWithoutData_ShowsDashes_AndTheOtherColumnsAreUnchanged()
    {
        Fill();
        using SKBitmap full = Paint(Widget());
        foreach (string field in new[] { "date", "condition", "high", "low", "precip-chance", "rain", "snow" })
            Set($"daily/1/{field}", Reading.Unavailable("no data"));
        WeatherWidget widget = Widget();
        using SKBitmap gap = Paint(widget);
        (_, float columnsTop, _, _, float bodyBottom) = Rows(5);
        float pad = _theme.Padding;
        float column = (CardWidth - (2 * pad)) / 5;

        Assert.Equal("– | not-available | –/– | – |  | ", widget.ForecastTexts()[4]);
        Assert.True(SameRegion(full, gap, Area(pad, columnsTop, pad + column, bodyBottom)), "column 0");
        Assert.False(SameRegion(full, gap, Area(pad + column, columnsTop, pad + (2 * column), bodyBottom)), "column 1");
        Assert.True(SameRegion(full, gap, Area(pad + (2 * column), columnsTop, CardWidth - pad, bodyBottom)), "columns 2 to 4");
    }

    // The snow total

    [Fact]
    public void TheSnowTotal_IsTheSumOfTheShownDays_InTheUsersUnits()
    {
        Fill();

        IReadOnlyList<string> imperial = Widget(c => c.Unit = "fahrenheit").ForecastTexts();
        IReadOnlyList<string> metric = Widget(c => c.Unit = "celsius").ForecastTexts();
        IReadOnlyList<string> two = Widget(c => { c.Unit = "fahrenheit"; c.Days = Json(2); }).ForecastTexts();
        IReadOnlyList<string> one = Widget(c => { c.Unit = "fahrenheit"; c.Days = Json(1); }).ForecastTexts();

        Assert.Equal("Snow, next 5 days", imperial[1]);
        Assert.Equal("9.2\"", imperial[2]);
        Assert.Equal("23 cm", metric[2]);
        Assert.Equal("Snow, next 2 days", two[1]);
        Assert.Equal("2.1\"", two[2]);
        Assert.Equal("Snow today", one[1]);
        Assert.Equal("0\"", one[2]);
    }

    [Fact]
    public void AMissingDayInTheTotal_GivesADash_ButAMissingDayBeyondItDoesNot()
    {
        Fill();
        Set("daily/3/snow", Reading.Unavailable("no snowfall"));
        Assert.Equal(ReadingFormatter.Dash, Widget(c => c.Unit = "fahrenheit").ForecastTexts()[2]);

        Fill();
        Set("daily/5/snow", Reading.Unavailable("no snowfall"));
        Assert.Equal("9.2\"", Widget(c => c.Unit = "fahrenheit").ForecastTexts()[2]);

        Fill();
        Set("daily/2/snow", Reading.Pending());
        Assert.Equal(ReadingFormatter.Dash, Widget(c => c.Unit = "fahrenheit").ForecastTexts()[2]);
    }

    [Fact]
    public void NoSnowExpected_ShowsZero()
    {
        Fill(Dry());

        Assert.Equal("0\"", Widget(c => c.Unit = "fahrenheit").ForecastTexts()[2]);
    }

    [Fact]
    public void AStaleDayInTheTotal_StillCounts_AndIsDrawnMuted()
    {
        Fill();
        using SKBitmap current = Paint(Widget());
        Set("daily/2/snow", Reading.Stale(10.9));
        WeatherWidget widget = Widget();
        using SKBitmap stale = Paint(widget);

        Assert.Equal("23 cm", widget.ForecastTexts()[2]);
        Assert.False(Same(current, stale));
    }

    // The 4x2 card

    [Fact]
    public void At4x2_DaysAndAnimateForecast_HaveNoEffect_AndStayInTheFile()
    {
        Fill();
        WeatherWidget plain = Widget(height: 2);
        using SKBitmap before = Paint(plain, height: 550);
        WeatherWidget widget = Widget(c => { c.Days = Json(3); c.AnimateForecast = Json(true); }, height: 2);
        using SKBitmap after = Paint(widget, height: 550);

        Assert.True(Same(before, after));
        Assert.Equal(plain.Subscriptions, widget.Subscriptions);
        Assert.Empty(widget.ForecastTexts());
        Assert.Equal(3, widget.Config.Days!.Value.GetInt32());
        Assert.True(widget.Config.AnimateForecast!.Value.GetBoolean());
    }

    // Forecast icons

    [Fact]
    public void ForecastIcons_AreStillByDefault_WhileTheMainIconMoves()
    {
        Fill();
        WeatherWidget widget = Widget(c => c.Motion = "full");
        using SKBitmap first = Paint(widget, T0);
        using SKBitmap later = Paint(widget, T0.AddSeconds(1.3));
        (float lowerTop, _, _, _, float bodyBottom) = Rows(5);

        Assert.True(widget.IsAnimating);
        Assert.Empty(widget.LoadedForecastIcons);
        Assert.Equal(["clear-day", "overcast", "snow"], widget.PosterKeys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.False(SameRegion(first, later, Area(0, 0, CardWidth, lowerTop - 1)), "the main icon moves");
        Assert.True(SameRegion(first, later, Area(0, lowerTop, CardWidth, bodyBottom)), "the forecast does not");
    }

    [Fact]
    public void OnePosterPerDistinctIcon_IsKept_NotOnePerDay()
    {
        Fill();
        WeatherWidget widget = Widget(c => c.Days = Json(7));
        using SKBitmap _ = Paint(widget);

        Assert.Equal(5, widget.PosterCount);
        using SKBitmap again = Paint(widget);
        Assert.Equal(5, widget.PosterCount);
    }

    [Fact]
    public void ForecastIcons_MoveWhenAsked_InStepWithTheMainIcon()
    {
        Fill();
        WeatherWidget widget = Widget(c => { c.Motion = "full"; c.AnimateForecast = Json(true); });
        using SKBitmap first = Paint(widget, T0);
        using SKBitmap later = Paint(widget, T0.AddSeconds(1.3));
        (float lowerTop, _, _, _, float bodyBottom) = Rows(5);

        Assert.True(widget.IsAnimating);
        Assert.Equal(3, widget.LoadedForecastIcons.Count);
        Assert.False(SameRegion(first, later, Area(0, lowerTop, CardWidth, bodyBottom)), "the forecast icons move");
    }

    [Fact]
    public void AnimateForecast_NeverLoadsMoreAnimationsThanItShows()
    {
        Fill();
        WeatherWidget widget = Widget(c => { c.Motion = "full"; c.AnimateForecast = Json(true); c.Days = Json(2); });
        using SKBitmap _ = Paint(widget);

        Assert.Equal(["snow"], widget.LoadedForecastIcons);

        widget.Configure(new WeatherConfig { Location = Place, Width = 4, Height = 4, Motion = "full", AnimateForecast = Json(true), Days = Json(7) });
        using SKBitmap __ = Paint(widget);
        Assert.Equal(5, widget.LoadedForecastIcons.Count);

        foreach (int day in new[] { 0, 1, 2 })
            Set($"daily/{day}/condition", Reading.Ok("clear"));
        using SKBitmap ___ = Paint(widget);
        Assert.DoesNotContain("snow", widget.LoadedForecastIcons);
        Assert.Equal(4, widget.LoadedForecastIcons.Count);
    }

    [Fact]
    public void MotionOff_WinsOverAnimateForecast()
    {
        Fill();
        WeatherWidget widget = Widget(c => { c.Motion = "off"; c.AnimateForecast = Json(true); });
        using SKBitmap _ = Paint(widget);

        Assert.False(widget.IsAnimating);
        Assert.Empty(widget.LoadedForecastIcons);
        Assert.True(widget.PosterCount > 0);
    }

    [Fact]
    public void WithAnimateForecast_TheNextLoopStartIsReportedWhileResting()
    {
        Fill();
        WeatherWidget widget = Widget(c => c.AnimateForecast = Json(true));
        using SKBitmap first = Paint(widget, T0);
        double duration = widget.LoadedIcon!.Duration.TotalSeconds;

        using SKBitmap rest = Paint(widget, T0.AddSeconds(duration + 0.05));

        Assert.False(widget.IsAnimating);
        Assert.Equal(T0.AddSeconds(60), widget.NextAnimationAt);
    }

    [Fact]
    public void TheForecastIcon_IsTheDaytimeVariant_EvenAtNight()
    {
        Fill(condition: "clear", isDay: false);
        WeatherWidget widget = Widget(c => c.Days = Json(5));
        using SKBitmap _ = Paint(widget);

        Assert.Equal("clear-night", widget.LoadedIconKey);
        Assert.Contains("clear-day", widget.PosterKeys);
        Assert.DoesNotContain("clear-night", widget.PosterKeys);
    }

    [Fact]
    public void AForecastConditionThatIsUnavailable_ShowsTheNeutralIcon()
    {
        Fill();
        Set("daily/1/condition", Reading.Unavailable("none"));
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget);

        Assert.Contains(WeatherIcons.Neutral, widget.PosterKeys);
    }

    [Fact]
    public void AMissingForecastIcon_DrawsNothing_LogsOnce_AndLeavesTheColumn()
    {
        Fill();
        WeatherWidget widget = Widget(c => c.Days = Json(5));
        widget.OpenIcon = key => key == "overcast" ? null : typeof(WeatherWidget).Assembly.GetManifestResourceStream($"Icons.{key}.json");
        using SKBitmap bitmap = Paint(widget);
        using SKBitmap again = Paint(widget);
        (_, float columnsTop, _, _, float bodyBottom) = Rows(5);
        float pad = _theme.Padding;
        float column = (CardWidth - (2 * pad)) / 5;

        Assert.Single(_log, line => line.Contains("overcast", StringComparison.Ordinal));
        Assert.True(HasInk(bitmap, Area(pad + (3 * column), columnsTop, pad + (4 * column), columnsTop + 40)), "the weekday is still there");
    }

    // Repaint

    [Fact]
    public void ADayInsideTheShownDays_AsksForARepaint_AndOneBeyondThemDoesNot()
    {
        Fill();
        WeatherWidget widget = Widget(c => c.Days = Json(5));
        using SKBitmap _ = Paint(widget);
        Assert.False(widget.NeedsRender(T0));

        Set("daily/5/snow", Reading.Ok(20));
        Set("daily/6/high", Reading.Ok(30));
        Assert.False(widget.NeedsRender(T0));

        Set("daily/2/snow", Reading.Ok(14));
        Assert.True(widget.NeedsRender(T0));
        using SKBitmap __ = Paint(widget);
        Assert.False(widget.NeedsRender(T0));

        Set("daily/3/precip-chance", Reading.Ok(55));
        Assert.True(widget.NeedsRender(T0));
    }

    [Fact]
    public void ARoundedForecastValueThatDoesNotChange_DoesNotAskForARepaint()
    {
        Fill();
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget);

        Set("daily/1/high", Reading.Ok(0.2));
        Assert.False(widget.NeedsRender(T0));
    }

    [Fact]
    public void TheTotalChanging_AsksForARepaint()
    {
        Fill();
        WeatherWidget widget = Widget();
        using SKBitmap _ = Paint(widget);

        Set("daily/4/snow", Reading.Ok(3));

        Assert.True(widget.NeedsRender(T0));
    }

    // Lifetime

    [Fact]
    public void Reconfiguring_AndDisposing_ReleaseTheForecastAnimationsAndPosters()
    {
        Fill();
        WeatherWidget widget = Widget(c => { c.Motion = "full"; c.AnimateForecast = Json(true); });
        using SKBitmap _ = Paint(widget);
        Assert.NotEmpty(widget.LoadedForecastIcons);

        widget.Configure(new WeatherConfig { Location = Place, Width = 4, Height = 4 });
        Assert.Empty(widget.LoadedForecastIcons);
        Assert.Equal(0, widget.PosterCount);

        using SKBitmap __ = Paint(widget);
        Assert.True(widget.PosterCount > 0);
        widget.Dispose();
        Assert.Equal(0, widget.PosterCount);
        Assert.Empty(widget.LoadedForecastIcons);
        Assert.Null(widget.LoadedIcon);
    }

    [Fact]
    public void ADifferentTheme_ChangesTheCard_AndStillFitsTheContent()
    {
        Fill();
        using Theme light = ThemeResolver.Resolve(new ThemeStore("").Load("default-light"), 275);
        WeatherWidget widget = Widget();
        using SKBitmap dark = Paint(widget);
        using SKBitmap bright = Paint(widget, theme: light);

        Assert.False(Same(dark, bright));
        float pad = light.Padding;
        SKRectI ink = Ink(bright, new SKRectI(0, 0, CardWidth, CardHeight));
        Assert.True(ink.Left >= pad - 1 && ink.Top >= pad - 1 && ink.Right <= CardWidth - pad + 1 && ink.Bottom <= CardHeight - pad + 1, $"ink {ink} leaves the content rectangle");
    }

    [Fact]
    public void TheCredit_IsDrawnOnceForTheWholeCard()
    {
        Fill();
        WeatherWidget widget = Widget();
        using SKBitmap bitmap = Paint(widget);
        float pad = _theme.Padding;
        float label = _theme.LabelSize * 1.5f;
        (_, _, _, _, float bodyBottom) = Rows(5);

        SKRectI strip = Ink(bitmap, Area(0, bodyBottom + 1, CardWidth, CardHeight));
        Assert.NotEqual(SKRectI.Empty, strip);
        Assert.True(strip.Height <= label, "one line of credit");
        Assert.True(strip.Bottom <= CardHeight - pad + 1);
    }
}
