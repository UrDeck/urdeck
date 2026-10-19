// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;

namespace UrDeck.Widgets.Weather;

[Widget("Weather", "Shows the weather of a place", Id = "urdeck.widgets.weather")]
[WidgetSize(4, 2)]
[WidgetSize(4, 4)]
[RefreshOnData]
[Category("Weather")]
public sealed class WeatherWidget : Widget<WeatherConfig>, IDisposable
{
    private const string Provider = "weather:";
    private const double DefaultPeriodSeconds = 60;
    private const int DefaultDays = 5;
    private const int MaxDays = 7;

    /// <summary>The icon's side as a share of the card's content height.</summary>
    private const float IconShare = 0.30f;

    /// <summary>A forecast icon's side as a share of its column's width, at most.</summary>
    private const float ForecastIconShare = 1.0f;

    /// <summary>What a paint puts on the card; two equal values draw the same pixels (apart from the moving icon).</summary>
    private sealed record Shown(
        ReadingText Temperature,
        ReadingText Place,
        string Condition,
        bool ConditionCurrent,
        string HighLabel,
        ReadingText High,
        string LowLabel,
        ReadingText Low,
        string SunriseLabel,
        ReadingText Sunrise,
        string SunsetLabel,
        ReadingText Sunset,
        string IconKey,
        string Credit,
        ForecastShown? Forecast);

    /// <summary>What the 4x4 card draws below the 4x2 composition.</summary>
    private sealed class ForecastShown : IEquatable<ForecastShown>
    {
        public required string Now { get; init; }

        public required bool NowCurrent { get; init; }

        public required string SnowLabel { get; init; }

        public required ReadingText SnowTotal { get; init; }

        public required DayShown[] Days { get; init; }

        public bool Equals(ForecastShown? other) =>
            other != null && Now == other.Now && NowCurrent == other.NowCurrent && SnowLabel == other.SnowLabel
            && SnowTotal.Equals(other.SnowTotal) && Days.AsSpan().SequenceEqual(other.Days);

        public override bool Equals(object? obj) => Equals(obj as ForecastShown);

        public override int GetHashCode() => HashCode.Combine(Now, SnowLabel, SnowTotal, Days.Length);
    }

    /// <summary>One forecast column. A struct with value equality, because arrays compare by reference.</summary>
    private readonly record struct DayShown(
        ReadingText Weekday,
        string IconKey,
        ReadingText High,
        ReadingText Low,
        ReadingText Chance,
        ReadingText Rain,
        bool HasRain,
        ReadingText Snow,
        bool HasSnow);

    private string[]? _subscriptions;
    private Shown? _drawn;
    private LottieAnimation? _icon;
    private string? _iconKey;
    private DateTime? _iconStart;
    private DateTime _lastPaint;
    private bool _animating;
    private double _loopSeconds;
    private readonly HashSet<string> _logged = new(StringComparer.Ordinal);

    // Forecast icons: stills are images of the poster frame, keyed by icon name and size (null when the icon is missing);
    // animations are loaded only while animateForecast asks for them.
    private readonly Dictionary<(string Key, int Size), SKImage?> _posters = [];
    private readonly Dictionary<string, LottieAnimation> _forecastIcons = new(StringComparer.Ordinal);

    /// <summary>Opens the animation of an icon by name; tests replace it to simulate a missing or unreadable icon.</summary>
    internal Func<string, Stream?> OpenIcon { get; set; } = key => typeof(WeatherWidget).Assembly.GetManifestResourceStream($"Icons.{key}.json");

    /// <summary>The name of the animation currently loaded (null when none is).</summary>
    internal string? LoadedIconKey => _icon == null ? null : _iconKey;

    /// <summary>The animation currently loaded.</summary>
    internal LottieAnimation? LoadedIcon => _icon;

    /// <summary>The names of the forecast animations currently loaded.</summary>
    internal IReadOnlyCollection<string> LoadedForecastIcons => _forecastIcons.Keys;

    /// <summary>The number of forecast poster images held.</summary>
    internal int PosterCount => _posters.Count;

    /// <summary>The names of the icons the forecast poster images show.</summary>
    internal IReadOnlyCollection<string> PosterKeys => [.. _posters.Keys.Select(poster => poster.Key).Distinct()];

    /// <summary>
    /// The texts of the forecast panel as drawn: the strip of now, the snow label, the snow total, then one line per
    /// day (<c>weekday | icon | high/low | chance | rain | snow</c>, an amount that is not drawn is empty). Empty at 4x2.
    /// </summary>
    internal IReadOnlyList<string> ForecastTexts()
    {
        ForecastShown? forecast = Current().Forecast;
        if (forecast == null)
            return [];

        var lines = new List<string> { forecast.Now, forecast.SnowLabel, WithUnit(forecast.SnowTotal) };
        lines.AddRange(forecast.Days.Select(day =>
            $"{day.Weekday.Value} | {day.IconKey} | {HighLow(day)} | {WithUnit(day.Chance)} | {RainText(day)} | {SnowText(day)}"));
        return lines;
    }

    public override IReadOnlyCollection<string> Subscriptions => _subscriptions ??= Ids();

    private bool HasAnimation => _icon != null || _forecastIcons.Count > 0;

    /// <summary>True while an icon is loaded and <c>motion</c> lets it loop, so the host repaints it at its frame rate.</summary>
    public override bool IsAnimating => HasAnimation && _animating;

    /// <summary>Slow, soft icons look the same at 15 frames a second and cost half of 30.</summary>
    public override TimeSpan AnimationFrameInterval => TimeSpan.FromMilliseconds(1000.0 / 15);

    /// <summary>In the periodic mode, when the loop starts next; null otherwise.</summary>
    public override DateTime? NextAnimationAt
    {
        get
        {
            if (!HasAnimation || _animating || Motion != MotionMode.Periodic || _iconStart is not { } start)
                return null;
            double period = Period(_loopSeconds);
            double cycles = Math.Floor((_lastPaint - start).TotalSeconds / period) + 1;
            return start.AddSeconds(cycles * period);
        }
    }

    private enum MotionMode { Periodic, Full, Off }

    /// <summary>Periodic unless the card says <c>full</c> or <c>off</c>; an unknown word counts as the default.</summary>
    private MotionMode Motion => Config.Motion?.Trim().ToLowerInvariant() switch
    {
        "off" => MotionMode.Off,
        "full" => MotionMode.Full,
        _ => MotionMode.Periodic,
    };

    /// <summary>The 4x4 layout: any card at least four cells high.</summary>
    private bool Four => Config.Height >= 4;

    /// <summary>The forecast days shown at 4x4: the setting as a whole number from 1 to 7, 5 when absent or not a number.</summary>
    private int Days
    {
        get
        {
            if (Config.Days is { ValueKind: JsonValueKind.Number } days && days.TryGetDouble(out double value) && !double.IsNaN(value))
                return (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 1, MaxDays);
            return DefaultDays;
        }
    }

    /// <summary>True when the card asks for moving forecast icons and <c>motion</c> does not forbid all motion.</summary>
    private bool AnimateForecast => Config.AnimateForecast is { ValueKind: JsonValueKind.True } && Motion != MotionMode.Off;

    protected override void OnConfigured()
    {
        _subscriptions = null;
        _drawn = null;
        _animating = false;
        ReleaseIcon();
        ReleaseForecastIcons();
    }

    public void Dispose()
    {
        ReleaseIcon();
        ReleaseForecastIcons();
    }

    public override bool NeedsRender(DateTime now) => !Equals(_drawn, Current());

    public override void Render(WidgetRenderContext context)
    {
        Theme theme = context.Theme;
        SKCanvas canvas = context.Canvas;
        Shown shown = Current();
        _drawn = shown;
        SelectIcon(shown.IconKey);
        SyncForecastIcons(shown.Forecast);

        // The credit strip is measured first and taken off the bottom, so the content never runs into it.
        SKRect content = context.ContentRect;
        IReadOnlyList<ReadingAttribution> credits = Credits();
        float strip = Attribution.Measure(theme, credits, content.Width);
        SKRect body = strip > 0 ? new SKRect(content.Left, content.Top, content.Right, content.Bottom - strip - theme.Gap) : content;
        if (strip > 0)
            Attribution.Draw(canvas, theme, SKRect.Create(content.Left, content.Bottom - strip, content.Width, strip), credits);
        if (body.Width <= 0 || body.Height <= 0)
            return;

        // At 4x4 the upper half is the 4x2 card, drawn by the same code; the forecast takes the lower half.
        SKRect top = body;
        SKRect lower = SKRect.Empty;
        if (shown.Forecast != null)
        {
            // A rule between now and the forecast, with room either side of it.
            float room = theme.Gap * 2.5f;
            float half = (body.Height - (2 * room)) / 2;
            top = new SKRect(body.Left, body.Top, body.Right, body.Top + half);
            lower = new SKRect(body.Left, top.Bottom + (2 * room), body.Right, body.Bottom);
            Divider.Draw(canvas, theme, SKRect.Create(body.Left, top.Bottom + room, body.Width, 0));
        }

        double seconds = Advance(context.Time);
        float iconSize = top.Height * IconShare;
        DrawIcon(canvas, SKRect.Create(top.Left, top.Top, iconSize, iconSize), seconds);
        DrawCard(canvas, theme, top, iconSize, shown, shown.Forecast != null ? TextStep.Title : TextStep.Body);
        if (shown.Forecast != null)
            DrawForecast(canvas, theme, lower, shown.Forecast, seconds);
    }

    /// <summary>
    /// A quiet card: the icon small in the top-left corner, the temperature large in the middle, the condition with
    /// today's high and low bottom-left, and the place with sunrise and sunset bottom-right.
    /// </summary>
    private void DrawCard(SKCanvas canvas, Theme theme, SKRect body, float iconSize, Shown shown, TextStep small)
    {
        float titleLine = theme.GetTextSize(TextStep.Title) * 1.5f;
        float bodyLine = theme.GetTextSize(small) * 1.5f;
        float bottom = titleLine + bodyLine;

        // The temperature is centred between the icon's width on both sides, above the bottom rows.
        float room = body.Bottom - bottom - theme.Gap - body.Top;
        float height = Math.Min(room, body.Height * 0.55f);
        float centre = body.Top + room / 2;
        var middle = new SKRect(body.Left + iconSize, centre - height / 2, body.Right - iconSize, centre + height / 2);
        Readout.Draw(canvas, theme, middle, shown.Temperature, new ReadoutOptions { Horizontal = HorizontalAlign.Center });

        float half = body.Width / 2;
        float top = body.Bottom - bottom;
        var left = SKRect.Create(body.Left, top, half, bottom);
        Chip.Draw(canvas, theme, SKRect.Create(left.Left, left.Top, left.Width, titleLine), shown.Condition, null, TextStep.Title,
            HorizontalAlign.Left, VerticalAlign.Middle, shown.ConditionCurrent ? TextEmphasis.Primary : TextEmphasis.Muted);
        string range = $"{shown.HighLabel[..1]}: {Temp(shown.High)}   {shown.LowLabel[..1]}: {Temp(shown.Low)}";
        TextLine.Draw(canvas, theme, SKRect.Create(left.Left, left.Top + titleLine, left.Width, bodyLine), range, small,
            shown.High.IsCurrent && shown.Low.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Left, VerticalAlign.Middle);

        float rightLeft = body.Left + half;
        float rightWidth = body.Width - half;
        if (Config.Label != "")
        {
            string place = Config.Label ?? shown.Place.Value;
            TextLine.Draw(canvas, theme, SKRect.Create(rightLeft, top, rightWidth, titleLine), place, small,
                Config.Label != null || shown.Place.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Right, VerticalAlign.Middle);
        }

        // Sunset is placed from the right edge, sunrise before it; each has a small glyph to its left.
        var row = SKRect.Create(rightLeft, top + titleLine, rightWidth, bodyLine);
        float edge = DrawSunTime(canvas, theme, row, row.Right, shown.Sunset, small, up: false);
        DrawSunTime(canvas, theme, row, edge - theme.Gap * 2, shown.Sunrise, small, up: true);
    }

    /// <summary>Draws a glyph and a time ending at <paramref name="right"/>; returns where the glyph begins.</summary>
    private static float DrawSunTime(SKCanvas canvas, Theme theme, SKRect row, float right, ReadingText time, TextStep step, bool up)
    {
        string text = time.Unit == null ? time.Value : $"{time.Value} {time.Unit}";
        var area = new SKRect(row.Left, row.Top, right, row.Bottom);
        SKRect drawn = TextLine.Draw(canvas, theme, area, text, step, time.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted,
            HorizontalAlign.Right, VerticalAlign.Middle);

        float size = theme.GetTextSize(step);
        float cx = drawn.Left - theme.Gap - size / 2;
        float baseline = row.MidY + size * 0.3f;
        using var paint = new SKPaint { Color = theme.TextMuted, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1f, size * 0.09f), StrokeCap = SKStrokeCap.Round };
        // The horizon, the half sun on it, and an arrow above it that points up for sunrise and down for sunset.
        canvas.DrawLine(cx - size * 0.55f, baseline, cx + size * 0.55f, baseline, paint);
        canvas.DrawArc(new SKRect(cx - size * 0.28f, baseline - size * 0.28f, cx + size * 0.28f, baseline + size * 0.28f), 180, 180, false, paint);
        float tip = baseline - size * 0.75f;
        float tail = baseline - size * 0.4f;
        float from = up ? tail : tip;
        float to = up ? tip : tail;
        canvas.DrawLine(cx, from, cx, to, paint);
        float wing = size * 0.14f;
        canvas.DrawLine(cx, to, cx - wing, to + (up ? wing : -wing), paint);
        canvas.DrawLine(cx, to, cx + wing, to + (up ? wing : -wing), paint);
        return cx - size * 0.55f;
    }

    private static string Temp(ReadingText value) => value.Value == ReadingFormatter.Dash ? value.Value : $"{value.Value}{value.Unit}";

    /// <summary>A value with its unit: a raised unit follows the value closely, a unit on the baseline after a space.</summary>
    private static string WithUnit(ReadingText value) => value.Unit == null || value.Value == ReadingFormatter.Dash
        ? value.Value
        : value.UnitPlacement == UnitPlacement.Raised ? value.Value + value.Unit : value.Value + " " + value.Unit;

    // The forecast

    /// <summary>
    /// The lower half of the 4x4 card: the readings of now, the snow total, and one column per day. Every height is a
    /// multiple of the theme's text sizes and every width a share of the card's, so nothing depends on the resolution.
    /// </summary>
    private void DrawForecast(SKCanvas canvas, Theme theme, SKRect area, ForecastShown forecast, double seconds)
    {
        float titleLine = theme.GetTextSize(TextStep.Title) * 1.5f;
        float y = area.Top;

        TextLine.Draw(canvas, theme, SKRect.Create(area.Left, y, area.Width, titleLine), forecast.Now, TextStep.Title,
            forecast.NowCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Left, VerticalAlign.Middle);
        y += titleLine;

        // The snow total: its value on the right, the label that says how many days it covers on the left.
        var totalRow = SKRect.Create(area.Left, y, area.Width, titleLine);
        SKRect value = TextLine.Draw(canvas, theme, totalRow, WithUnit(forecast.SnowTotal), TextStep.Title,
            forecast.SnowTotal.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Right, VerticalAlign.Middle);
        float labelRight = value.Width > 0 ? value.Left - theme.Gap : area.Right;
        TextLine.Draw(canvas, theme, new SKRect(area.Left, y, Math.Max(area.Left, labelRight), y + titleLine), forecast.SnowLabel, TextStep.Title,
            TextEmphasis.Muted, HorizontalAlign.Left, VerticalAlign.Middle);
        y += titleLine + theme.Gap;

        int days = forecast.Days.Length;
        float column = area.Width / days;
        float rows = 5 * titleLine;
        float room = area.Bottom - y - rows;
        float icon = Math.Max(0, Math.Min(column * ForecastIconShare, room));

        // What is left over is split above and below the columns, so the block sits in the middle of the space.
        y += Math.Max(0, room - icon) / 2;
        bool animate = _forecastIcons.Count > 0;

        // Text keeps a gap from its neighbour's, so that a long amount in a narrow column shrinks rather than touches it.
        float inset = theme.Gap / 2;
        for (int i = 0; i < days; i++)
        {
            DayShown day = forecast.Days[i];
            float left = area.Left + (i * column);
            float x = y;

            // A well, when the theme has one, behind the whole column; the column itself is laid out as without it.
            Well.Draw(canvas, theme, SKRect.Create(left + (inset / 2), y - inset, column - inset, (5 * titleLine) + icon + (2 * inset)));

            TextLine.Draw(canvas, theme, SKRect.Create(left + inset, x, column - (2 * inset), titleLine), day.Weekday.Value, TextStep.Title,
                day.Weekday.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Center, VerticalAlign.Middle);
            x += titleLine;

            var iconRect = SKRect.Create(left + ((column - icon) / 2), x, icon, icon);
            DrawForecastIcon(canvas, iconRect, day.IconKey, animate, seconds);
            x += icon;

            TextLine.Draw(canvas, theme, SKRect.Create(left + inset, x, column - (2 * inset), titleLine), HighLow(day), TextStep.Title,
                day.High.IsCurrent && day.Low.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Center, VerticalAlign.Middle);
            x += titleLine;

            TextLine.Draw(canvas, theme, SKRect.Create(left + inset, x, column - (2 * inset), titleLine), WithUnit(day.Chance), TextStep.Title,
                day.Chance.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Center, VerticalAlign.Middle);
            x += titleLine;

            // The space of an amount that is not drawn stays empty, so the rows line up across the columns.
            if (day.HasRain)
            {
                TextLine.Draw(canvas, theme, SKRect.Create(left + inset, x, column - (2 * inset), titleLine), RainText(day), TextStep.Title,
                    day.Rain.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Center, VerticalAlign.Middle);
            }

            x += titleLine;
            if (day.HasSnow)
            {
                TextLine.Draw(canvas, theme, SKRect.Create(left + inset, x, column - (2 * inset), titleLine), SnowText(day), TextStep.Title,
                    day.Snow.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Center, VerticalAlign.Middle);
            }
        }
    }

    private static string HighLow(DayShown day) => $"{Temp(day.High)}/{Temp(day.Low)}";

    /// <summary>The rain amount of a column, empty when none is drawn.</summary>
    private static string RainText(DayShown day) => day.HasRain ? WithUnit(day.Rain) + " rain" : "";

    /// <summary>The snow amount of a column, empty when none is drawn.</summary>
    private static string SnowText(DayShown day) => day.HasSnow ? WithUnit(day.Snow) + " snow" : "";

    // The icons

    /// <summary>Loads the animation of an icon; null, logged once, when it is missing or cannot be read.</summary>
    private LottieAnimation? Open(string key)
    {
        using Stream? stream = OpenIcon(key);
        if (stream != null && LottieAnimation.TryCreate(stream, out LottieAnimation? animation))
            return animation;

        if (_logged.Add(key))
            Log($"Weather icon '{key}' is missing or cannot be read; the card is drawn without it");
        return null;
    }

    private void SelectIcon(string key)
    {
        if (key == _iconKey)
            return;

        ReleaseIcon();
        _iconKey = key;
        _icon = Open(key);
    }

    private void ReleaseIcon()
    {
        _icon?.Dispose();
        _icon = null;
        _iconKey = null;
        _iconStart = null;
    }

    /// <summary>
    /// Keeps exactly the forecast animations the card shows moving: one per distinct icon of the days shown while
    /// <c>animateForecast</c> is on, none otherwise (the stills are bitmaps).
    /// </summary>
    private void SyncForecastIcons(ForecastShown? forecast)
    {
        if (forecast == null || !AnimateForecast)
        {
            ReleaseForecastIcons(keepPosters: true);
            return;
        }

        var wanted = new HashSet<string>(forecast.Days.Select(day => day.IconKey), StringComparer.Ordinal);
        foreach (string key in _forecastIcons.Keys.Where(key => !wanted.Contains(key)).ToList())
        {
            _forecastIcons[key].Dispose();
            _forecastIcons.Remove(key);
        }

        foreach (string key in wanted)
        {
            if (!_forecastIcons.ContainsKey(key) && Open(key) is { } animation)
                _forecastIcons[key] = animation;
        }
    }

    private void ReleaseForecastIcons(bool keepPosters = false)
    {
        foreach (LottieAnimation animation in _forecastIcons.Values)
            animation.Dispose();
        _forecastIcons.Clear();
        if (keepPosters)
            return;

        foreach (SKImage? poster in _posters.Values)
            poster?.Dispose();
        _posters.Clear();
    }

    /// <summary>
    /// The poster frame of an icon drawn once into an image of the size it is drawn at; the animation is released at
    /// once. Null when the icon is missing (and logged once).
    /// </summary>
    private SKImage? Poster(string key, int size)
    {
        if (_posters.TryGetValue((key, size), out SKImage? cached))
            return cached;

        SKImage? poster = null;
        using (LottieAnimation? animation = Open(key))
        {
            if (animation != null)
            {
                using var bitmap = new SKBitmap(size, size);
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    animation.Draw(canvas, SKRect.Create(size, size), WeatherIcons.PosterFraction * animation.Duration.TotalSeconds);
                }

                poster = SKImage.FromBitmap(bitmap);
            }
        }

        _posters[(key, size)] = poster;
        return poster;
    }

    private void DrawForecastIcon(SKCanvas canvas, SKRect rect, string key, bool animate, double seconds)
    {
        if (rect.Width < 1 || rect.Height < 1)
            return;

        if (animate && _forecastIcons.TryGetValue(key, out LottieAnimation? animation))
        {
            animation.Draw(canvas, rect, seconds);
            return;
        }

        int size = (int)Math.Ceiling(Math.Min(rect.Width, rect.Height));
        if (Poster(key, size) is { } poster)
            canvas.DrawImage(poster, rect.Left, rect.Top, new SKSamplingOptions(SKFilterMode.Linear), null);
    }

    /// <summary>
    /// Works out how far into the loop the icons are at <paramref name="now"/> and whether they are moving. The loop is
    /// timed from the first paint; the main icon's length is the loop (the first forecast icon's when there is none).
    /// </summary>
    private double Advance(DateTime now)
    {
        _animating = false;
        _lastPaint = now;
        _loopSeconds = _icon?.Duration.TotalSeconds ?? _forecastIcons.Values.FirstOrDefault()?.Duration.TotalSeconds ?? 0;
        if (!HasAnimation)
            return 0;

        double duration = _loopSeconds;
        double poster = WeatherIcons.PosterFraction * duration;
        double seconds = poster;
        if (Motion != MotionMode.Off)
        {
            // The loop is timed from the icon's first paint; a clock that jumped back is the poster frame, and the loop
            // continues from there.
            if (_iconStart is not { } start || now < start)
            {
                _iconStart = now;
                _animating = true;
            }
            else
            {
                double elapsed = (now - start).TotalSeconds;
                if (Motion == MotionMode.Full)
                {
                    seconds = (poster + elapsed) % duration;
                    _animating = true;
                }
                else
                {
                    // Periodic: one loop at the start of every period, then rest on the poster frame until the next.
                    double position = elapsed % Period(duration);
                    if (position < duration)
                    {
                        seconds = (poster + position) % duration;
                        _animating = true;
                    }
                }
            }
        }

        return seconds;
    }

    private void DrawIcon(SKCanvas canvas, SKRect rect, double seconds) => _icon?.Draw(canvas, rect, seconds);

    /// <summary>The seconds from one start of the loop to the next in the periodic mode: never less than a loop and a second.</summary>
    private double Period(double loopSeconds) => Math.Max(Config.PeriodSeconds ?? DefaultPeriodSeconds, loopSeconds + 1);

    // What to show

    private string[] Ids()
    {
        string? segment = Segment();
        if (segment == null)
            return [];

        var paths = new List<string>
        {
            "place", "current/temperature", "current/condition", "current/is-day",
            "daily/0/high", "daily/0/low", "daily/0/sunrise", "daily/0/sunset",
        };
        if (Four)
        {
            paths.AddRange(["current/apparent", "current/humidity", "current/wind-speed", "current/wind-direction"]);
            for (int day = 0; day < Days; day++)
            {
                foreach (string field in new[] { "date", "condition", "high", "low", "precip-chance", "rain", "snow" })
                    paths.Add($"daily/{day}/{field}");
            }
        }

        return [.. paths.Distinct().Select(path => $"{Provider}{segment}/{path}")];
    }

    /// <summary>The location part of the reading ids: the coordinates when both are set, else the typed place; null with neither.</summary>
    private string? Segment()
    {
        WeatherConfig config = Config;
        if (config is { Latitude: { } latitude, Longitude: { } longitude })
            return string.Create(CultureInfo.InvariantCulture, $"@{latitude},{longitude}");

        string? location = config.Location?.Trim();
        return string.IsNullOrEmpty(location)
            ? null
            : location.Replace("%", "%25", StringComparison.Ordinal).Replace("/", "%2F", StringComparison.Ordinal);
    }

    private Shown Current()
    {
        string? segment = Segment();
        string Id(string path) => $"{Provider}{segment}/{path}";
        DisplayUnit? unit = ParseUnit(Config.Unit);

        Reading ReadingOf(string path) => segment == null ? Reading.Unavailable("no location") : Readings.Read(Id(path));

        ReadingText Text(string path, DateStyle? dateStyle = null)
        {
            string id = Id(path);
            return segment == null
                ? ReadingFormatter.Format(Reading.Unavailable("no location"), Readings.Describe(id), null, Readings)
                : ReadingFormatter.Format(Readings.Read(id), Readings.Describe(id), new ReadingFormatOptions { DisplayUnit = unit, DateStyle = dateStyle }, Readings);
        }

        string Label(string path, string fallback) => segment == null ? fallback : Readings.Describe(Id(path))?.Label ?? fallback;

        Reading conditionReading = ReadingOf("current/condition");
        Reading dayReading = ReadingOf("current/is-day");
        string? conditionClass = conditionReading.Value is { Type: ReadingValueType.Text } value ? value.Text : null;
        bool isDay = dayReading.Value is not { Type: ReadingValueType.OnOff } day || day.IsOn;
        string words = conditionClass == null ? ReadingFormatter.Dash : WeatherIcons.Words(conditionClass);

        return new Shown(
            Text("current/temperature"),
            Text("place"),
            words,
            conditionClass != null && conditionReading.State == ReadingState.Ok,
            Label("daily/0/high", "High"),
            Text("daily/0/high"),
            Label("daily/0/low", "Low"),
            Text("daily/0/low"),
            Label("daily/0/sunrise", "Sunrise"),
            Text("daily/0/sunrise"),
            Label("daily/0/sunset", "Sunset"),
            Text("daily/0/sunset"),
            WeatherIcons.Key(conditionClass, isDay),
            string.Join('\n', Credits().Select(c => c.Text)),
            Four ? CurrentForecast(segment, Text, Label, ReadingOf, unit) : null);
    }

    private ForecastShown CurrentForecast(
        string? segment,
        Func<string, DateStyle?, ReadingText> text,
        Func<string, string, string> label,
        Func<string, Reading> read,
        DisplayUnit? unit)
    {
        // The strip of now: feels like, humidity, and the wind with the point it blows from.
        ReadingText feels = text("current/apparent", null);
        ReadingText humidity = text("current/humidity", null);
        ReadingText speed = text("current/wind-speed", null);
        Reading direction = read("current/wind-direction");
        string wind = WithUnit(speed);
        if (direction.Value is { Type: ReadingValueType.Number } bearing && speed.Value != ReadingFormatter.Dash)
            wind += " " + ReadingFormatter.CompassPoint(bearing.Number);
        string now = $"{label("current/apparent", "Feels like")} {Temp(feels)}   {label("current/humidity", "Humidity")} {WithUnit(humidity)}   {label("current/wind-speed", "Wind")} {wind}";
        bool nowCurrent = feels.IsCurrent && humidity.IsCurrent && speed.IsCurrent;

        int count = Days;
        var days = new DayShown[count];
        double total = 0;
        bool missing = false;
        bool stale = false;
        for (int i = 0; i < count; i++)
        {
            string at = $"daily/{i}/";
            Reading condition = read(at + "condition");
            string? conditionClass = condition.Value is { Type: ReadingValueType.Text } c ? c.Text : null;
            Reading rain = read(at + "rain");
            Reading snow = read(at + "snow");
            ReadingText rainText = text(at + "rain", null);
            ReadingText snowText = text(at + "snow", null);
            days[i] = new DayShown(
                text(at + "date", DateStyle.Weekday),
                WeatherIcons.Key(conditionClass, isDay: true),
                text(at + "high", null),
                text(at + "low", null),
                text(at + "precip-chance", null),
                rainText,
                rain.Value is { Type: ReadingValueType.Number, Number: > 0 },
                snowText,
                snow.Value is { Type: ReadingValueType.Number, Number: > 0 });

            if (snow.Value is { Type: ReadingValueType.Number } amount)
            {
                total += amount.Number;
                stale |= snow.State != ReadingState.Ok;
            }
            else
            {
                missing = true;
            }
        }

        // The total goes through the formatter like any depth, so that the trace and rounding rules hold for it too.
        string snowId = $"{Provider}{segment}/daily/0/snow";
        Reading totalReading = missing || segment == null
            ? Reading.Unavailable("a day of the forecast has no snowfall")
            : stale ? Reading.Stale(total) : Reading.Ok(total);
        ReadingText snowTotal = ReadingFormatter.Format(totalReading, Readings.Describe(snowId), new ReadingFormatOptions { DisplayUnit = unit }, Readings);

        return new ForecastShown
        {
            Now = now,
            NowCurrent = nowCurrent,
            SnowLabel = count == 1 ? "Snow today" : $"Snow, next {count.ToString(CultureInfo.InvariantCulture)} days",
            SnowTotal = snowTotal,
            Days = days,
        };
    }

    /// <summary>The credits of the readings the card shows, taken from their descriptions.</summary>
    private List<ReadingAttribution> Credits()
    {
        var credits = new List<ReadingAttribution>();
        foreach (string id in Subscriptions)
        {
            ReadingAttribution? credit = Readings.Describe(id)?.Attribution;
            if (credit != null)
                credits.Add(credit);
        }

        return credits;
    }

    private static DisplayUnit? ParseUnit(string? unit) => unit?.Trim().ToLowerInvariant() switch
    {
        "celsius" => DisplayUnit.Celsius,
        "fahrenheit" => DisplayUnit.Fahrenheit,
        _ => null,
    };
}
