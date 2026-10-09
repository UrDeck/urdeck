// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;

namespace UrDeck.Widgets.Weather;

[Widget("Weather", "Shows the weather of a place", Id = "urdeck.widgets.weather")]
[WidgetSize(4, 2)]
[RefreshOnData]
[Category("Weather")]
public sealed class WeatherWidget : Widget<WeatherConfig>, IDisposable
{
    private const string Provider = "weather:";
    private const double DefaultPeriodSeconds = 60;

    /// <summary>The icon's side as a share of the card's content height.</summary>
    private const float IconShare = 0.30f;

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
        string Credit);

    private string[]? _subscriptions;
    private Shown? _drawn;
    private LottieAnimation? _icon;
    private string? _iconKey;
    private DateTime? _iconStart;
    private DateTime _lastPaint;
    private bool _animating;
    private readonly HashSet<string> _logged = new(StringComparer.Ordinal);

    /// <summary>Opens the animation of an icon by name; tests replace it to simulate a missing or unreadable icon.</summary>
    internal Func<string, Stream?> OpenIcon { get; set; } = key => typeof(WeatherWidget).Assembly.GetManifestResourceStream($"Icons.{key}.json");

    /// <summary>The name of the animation currently loaded (null when none is).</summary>
    internal string? LoadedIconKey => _icon == null ? null : _iconKey;

    /// <summary>The animation currently loaded.</summary>
    internal LottieAnimation? LoadedIcon => _icon;

    public override IReadOnlyCollection<string> Subscriptions => _subscriptions ??= Ids();

    /// <summary>True while an icon is loaded and <c>motion</c> lets it loop, so the host repaints it at its frame rate.</summary>
    public override bool IsAnimating => _icon != null && _animating;

    /// <summary>Slow, soft icons look the same at 15 frames a second and cost half of 30.</summary>
    public override TimeSpan AnimationFrameInterval => TimeSpan.FromMilliseconds(1000.0 / 15);

    /// <summary>In the periodic mode, when the loop starts next; null otherwise.</summary>
    public override DateTime? NextAnimationAt
    {
        get
        {
            if (_icon == null || _animating || Motion != MotionMode.Periodic || _iconStart is not { } start)
                return null;
            double period = Period(_icon.Duration.TotalSeconds);
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

    protected override void OnConfigured()
    {
        _subscriptions = null;
        _drawn = null;
        _animating = false;
        ReleaseIcon();
    }

    public void Dispose() => ReleaseIcon();

    public override bool NeedsRender(DateTime now) => !Equals(_drawn, Current());

    public override void Render(WidgetRenderContext context)
    {
        Theme theme = context.Theme;
        SKCanvas canvas = context.Canvas;
        Shown shown = Current();
        _drawn = shown;
        SelectIcon(shown.IconKey);

        // The credit strip is measured first and taken off the bottom, so the content never runs into it.
        SKRect content = context.ContentRect;
        IReadOnlyList<ReadingAttribution> credits = Credits();
        float strip = Attribution.Measure(theme, credits, content.Width);
        SKRect body = strip > 0 ? new SKRect(content.Left, content.Top, content.Right, content.Bottom - strip - theme.Gap) : content;
        if (strip > 0)
            Attribution.Draw(canvas, theme, SKRect.Create(content.Left, content.Bottom - strip, content.Width, strip), credits);
        if (body.Width <= 0 || body.Height <= 0)
            return;

        float iconSize = body.Height * IconShare;
        DrawIcon(canvas, SKRect.Create(body.Left, body.Top, iconSize, iconSize), context.Time);
        DrawCard(canvas, theme, body, iconSize, shown);
    }

    /// <summary>
    /// A quiet card: the icon small in the top-left corner, the temperature large in the middle, the condition with
    /// today's high and low bottom-left, and the place with sunrise and sunset bottom-right.
    /// </summary>
    private void DrawCard(SKCanvas canvas, Theme theme, SKRect body, float iconSize, Shown shown)
    {
        float titleLine = theme.GetTextSize(TextStep.Title) * 1.5f;
        float bodyLine = theme.GetTextSize(TextStep.Body) * 1.5f;
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
        TextLine.Draw(canvas, theme, SKRect.Create(left.Left, left.Top, left.Width, titleLine), shown.Condition, TextStep.Title,
            shown.ConditionCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Left, VerticalAlign.Middle);
        string range = $"{shown.HighLabel[..1]}: {Temp(shown.High)}   {shown.LowLabel[..1]}: {Temp(shown.Low)}";
        TextLine.Draw(canvas, theme, SKRect.Create(left.Left, left.Top + titleLine, left.Width, bodyLine), range, TextStep.Body,
            shown.High.IsCurrent && shown.Low.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Left, VerticalAlign.Middle);

        float rightLeft = body.Left + half;
        float rightWidth = body.Width - half;
        if (Config.Label != "")
        {
            string place = Config.Label ?? shown.Place.Value;
            TextLine.Draw(canvas, theme, SKRect.Create(rightLeft, top, rightWidth, titleLine), place, TextStep.Body,
                Config.Label != null || shown.Place.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted, HorizontalAlign.Right, VerticalAlign.Middle);
        }

        // Sunset is placed from the right edge, sunrise before it; each has a small glyph to its left.
        var row = SKRect.Create(rightLeft, top + titleLine, rightWidth, bodyLine);
        float edge = DrawSunTime(canvas, theme, row, row.Right, shown.Sunset, up: false);
        DrawSunTime(canvas, theme, row, edge - theme.Gap * 2, shown.Sunrise, up: true);
    }

    /// <summary>Draws a glyph and a time ending at <paramref name="right"/>; returns where the glyph begins.</summary>
    private static float DrawSunTime(SKCanvas canvas, Theme theme, SKRect row, float right, ReadingText time, bool up)
    {
        string text = time.Unit == null ? time.Value : $"{time.Value} {time.Unit}";
        var area = new SKRect(row.Left, row.Top, right, row.Bottom);
        SKRect drawn = TextLine.Draw(canvas, theme, area, text, TextStep.Body, time.IsCurrent ? TextEmphasis.Primary : TextEmphasis.Muted,
            HorizontalAlign.Right, VerticalAlign.Middle);

        float size = theme.GetTextSize(TextStep.Body);
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

    // The icon

    private void SelectIcon(string key)
    {
        if (key == _iconKey)
            return;

        ReleaseIcon();
        _iconKey = key;
        using Stream? stream = OpenIcon(key);
        if (stream != null && LottieAnimation.TryCreate(stream, out LottieAnimation? animation))
        {
            _icon = animation;
            return;
        }

        if (_logged.Add(key))
            Log($"Weather icon '{key}' is missing or cannot be read; the card is drawn without it");
    }

    private void ReleaseIcon()
    {
        _icon?.Dispose();
        _icon = null;
        _iconKey = null;
        _iconStart = null;
    }

    private void DrawIcon(SKCanvas canvas, SKRect rect, DateTime now)
    {
        _animating = false;
        _lastPaint = now;
        if (_icon == null)
            return;

        double duration = _icon.Duration.TotalSeconds;
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

        _icon.Draw(canvas, rect, seconds);
    }

    /// <summary>The seconds from one start of the loop to the next in the periodic mode: never less than a loop and a second.</summary>
    private double Period(double loopSeconds) => Math.Max(Config.PeriodSeconds ?? DefaultPeriodSeconds, loopSeconds + 1);

    // What to show

    private string[] Ids()
    {
        string? segment = Segment();
        if (segment == null)
            return [];

        string[] paths = ["place", "current/temperature", "current/condition", "current/is-day", "today/high", "today/low", "today/sunrise", "today/sunset"];
        return [.. paths.Select(path => $"{Provider}{segment}/{path}")];
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

        ReadingText Text(string path)
        {
            string id = Id(path);
            return segment == null
                ? ReadingFormatter.Format(Reading.Unavailable("no location"), Readings.Describe(id), null, Readings)
                : ReadingFormatter.Format(Readings.Read(id), Readings.Describe(id), new ReadingFormatOptions { DisplayUnit = unit }, Readings);
        }

        string Label(string path, string fallback) => segment == null ? fallback : Readings.Describe(Id(path))?.Label ?? fallback;

        Reading conditionReading = segment == null ? Reading.Unavailable("no location") : Readings.Read(Id("current/condition"));
        Reading dayReading = segment == null ? Reading.Unavailable("no location") : Readings.Read(Id("current/is-day"));
        string? conditionClass = conditionReading.Value is { Type: ReadingValueType.Text } value ? value.Text : null;
        bool isDay = dayReading.Value is not { Type: ReadingValueType.OnOff } day || day.IsOn;
        string words = conditionClass == null ? ReadingFormatter.Dash : WeatherIcons.Words(conditionClass);

        return new Shown(
            Text("current/temperature"),
            Text("place"),
            words,
            conditionClass != null && conditionReading.State == ReadingState.Ok,
            Label("today/high", "High"),
            Text("today/high"),
            Label("today/low", "Low"),
            Text("today/low"),
            Label("today/sunrise", "Sunrise"),
            Text("today/sunrise"),
            Label("today/sunset", "Sunset"),
            Text("today/sunset"),
            WeatherIcons.Key(conditionClass, isDay),
            string.Join('\n', Credits().Select(c => c.Text)));
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
