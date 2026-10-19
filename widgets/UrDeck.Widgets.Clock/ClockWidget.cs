// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Widgets.Clock.Config;

namespace UrDeck.Widgets.Clock;

[Widget("Clock", "Display current time", Id = "urdeck.widgets.clock")]
[WidgetSize(4, 2)]
[WidgetSize(4, 1)]
[WidgetSize(2, 1)]
[WidgetSize(1, 1)]
[RefreshOnTick(1, TimeUnit.Seconds)]
[Category("System")]
public class ClockWidget : Widget<ClockConfig>
{
    // The widest time the readout must fit, so its size does not change from minute to minute.
    private const string WidestTime = "88:88";

    // The date is measured by cap height; this keeps room below it for descenders (the y in "Mon Oct 5, 2026").
    private const float DescentAllowance = 0.25f;

    // The minute that was last painted. The widget ticks every second to catch the rollover promptly,
    // but only repaints when the displayed minute (which also covers the date) changes.
    private DateTime? _lastRenderedMinute;

    // Only used with showSeconds or blinkSeparator: the second that was last painted.
    private DateTime? _lastRenderedSecond;

    private bool EverySecond => Config.ShowSeconds || Config.BlinkSeparator;

    public override bool NeedsRender(DateTime now) =>
        EverySecond ? _lastRenderedSecond != TruncateToSecond(now) : _lastRenderedMinute != TruncateToMinute(now);

    protected override void OnConfigured()
    {
        _lastRenderedMinute = null;
        _lastRenderedSecond = null;
        _shownTiles = null;
        _flipping = false;
    }

    public override bool IsAnimating => _flipping && IsFlap;

    private bool IsFlap => Config.Style == "flap";

    public override void Render(WidgetRenderContext context)
    {
        _lastRenderedMinute = TruncateToMinute(context.Time);
        _lastRenderedSecond = TruncateToSecond(context.Time);
        if (IsFlap)
        {
            RenderFlap(context);
            return;
        }

        var canvas = context.Canvas;
        var theme = context.Theme;
        var content = DrawHeader(context, out bool dateBelow);

        bool is12h = Config.Format == "12h";
        string separator = SeparatorVisible(context.Time) ? ":" : " ";
        string timeText = context.Time.ToString(is12h ? "%h" : "HH", CultureInfo.InvariantCulture) + separator + context.Time.ToString("mm", CultureInfo.InvariantCulture);
        string dateText = context.Time.ToString("ddd MMM d, yyyy", CultureInfo.CurrentCulture);

        var options = new ReadoutOptions
        {
            Unit = Suffix(context.Time, is12h),
            UnitPlacement = UnitPlacement.Baseline,
            WidestValue = WidestTime,
            Vertical = VerticalAlign.Bottom,
            ValueColor = Config.TextColor != null && SKColor.TryParse(Config.TextColor, out var custom) ? custom : null,
            Scale = (float)Config.FontSize,
        };

        // Time above date, the pair centered in the content rectangle: measure the date, give the time what is left,
        // then shift the whole block up by the space the time did not need.
        float dateHeight = 0f;
        float gap = 0f;
        if (dateBelow)
        {
            dateHeight = TextLine.Measure(theme, content, dateText, TextStep.Title).Height + theme.TitleSize * DescentAllowance;
            gap = theme.TitleSize;
        }

        var timeSlot = new SKRect(content.Left, content.Top, content.Right, Math.Max(content.Top + 1, content.Bottom - dateHeight - gap));
        options = KeepSize(theme, timeSlot, timeText, options, is12h);
        var timeRect = Readout.Measure(theme, timeSlot, timeText, options);
        float blockHeight = timeRect.Height + gap + dateHeight;
        float shift = (content.Height - blockHeight) / 2f;

        int save = canvas.Save();
        canvas.Translate(0, -shift);
        Readout.Draw(canvas, theme, timeSlot, timeText, options);
        if (dateBelow)
        {
            var dateSlot = new SKRect(content.Left, content.Bottom - dateHeight, content.Right, content.Bottom);
            TextLine.Draw(canvas, theme, dateSlot, dateText, TextStep.Title, TextEmphasis.Muted, vertical: VerticalAlign.Top);
        }
        canvas.RestoreToCount(save);
    }

    // Flap style. The widget keeps the characters last shown and, while a flip runs, the ones it flips from and when it
    // began; the flip's progress is computed from the render time, so it looks the same at any frame rate.
    private const double FlipSeconds = 0.5;
    private string? _shownTiles;
    private string _flipFrom = "";
    private DateTime _flipStart;
    private bool _flipping;

    private void RenderFlap(WidgetRenderContext context)
    {
        var canvas = context.Canvas;
        var theme = context.Theme;
        var content = DrawHeader(context, out bool dateBelow);

        bool is12h = Config.Format == "12h";
        string tiles = Tiles(context.Time, is12h);
        string dateText = context.Time.ToString("ddd MMM d, yyyy", CultureInfo.CurrentCulture);

        // First paint (and the first after a configuration change) is at rest; a changed time starts a flip.
        if (_shownTiles == null)
            _shownTiles = tiles;
        else if (!_flipping && tiles != _shownTiles)
        {
            _flipFrom = _shownTiles;
            _shownTiles = tiles;
            _flipStart = context.Time;
            _flipping = true;
        }

        float? progress = null;
        if (_flipping)
        {
            double elapsed = (context.Time - _flipStart).TotalSeconds;
            if (elapsed >= 0 && elapsed < FlipSeconds)
                progress = (float)(elapsed / FlipSeconds);
            else
            {
                // Finished, or the clock jumped: draw the current time at rest.
                _flipping = false;
                _shownTiles = tiles;
            }
        }

        float dateHeight = 0f;
        float gap = 0f;
        if (dateBelow)
        {
            dateHeight = TextLine.Measure(theme, content, dateText, TextStep.Title).Height + theme.TitleSize * DescentAllowance;
            gap = theme.TitleSize;
        }

        var tileSlot = new SKRect(content.Left, content.Top, content.Right, Math.Max(content.Top + 1, content.Bottom - dateHeight - gap));
        var layout = FlapDisplay.Fit(theme, tileSlot, is12h || Config.ShowSeconds, (float)Config.FontSize, WidestSuffix(is12h));
        float top = content.Top + (content.Height - (layout.TileHeight + gap + dateHeight)) / 2f;

        var color = Config.TextColor != null && SKColor.TryParse(Config.TextColor, out var custom) ? custom : theme.Text;
        var flip = new FlapDisplay.Flip(_flipping ? _flipFrom : tiles, tiles, progress);
        FlapDisplay.Draw(canvas, theme, layout, top, flip, color, context.PixelSize.Height, Suffix(context.Time, is12h), SeparatorVisible(context.Time));

        if (dateBelow)
        {
            var dateSlot = new SKRect(content.Left, top + layout.TileHeight + gap, content.Right, top + layout.TileHeight + gap + dateHeight);
            TextLine.Draw(canvas, theme, dateSlot, dateText, TextStep.Title, TextEmphasis.Muted, vertical: VerticalAlign.Top);
        }
    }

    /// <summary>The four tile characters: hour and minute digits, a space for the blank first tile of a 12-hour hour below 10.</summary>
    private static string Tiles(DateTime time, bool is12h) => is12h
        ? time.ToString("%h", CultureInfo.InvariantCulture).PadLeft(2) + time.ToString("mm", CultureInfo.InvariantCulture)
        : time.ToString("HHmm", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- header and options

    private enum HeaderMode { Absent, Date, Label, None }

    private HeaderMode Header => Config.Header?.Trim().ToLowerInvariant() switch
    {
        "date" => HeaderMode.Date,
        "label" => HeaderMode.Label,
        "none" => HeaderMode.None,
        _ => HeaderMode.Absent,
    };

    /// <summary>
    /// Draws the header row when the configuration asks for one and returns the rectangle left for the time (and, without a
    /// header, the date below it). <paramref name="dateBelow"/> says whether the date goes below the time.
    /// </summary>
    private SKRect DrawHeader(WidgetRenderContext context, out bool dateBelow)
    {
        var content = context.ContentRect;
        var mode = Header;
        dateBelow = mode == HeaderMode.Absent && Config.ShowDate;
        if (mode is HeaderMode.Absent or HeaderMode.None)
            return content;

        var theme = context.Theme;
        string date = context.Time.ToString("ddd MMM d, yyyy", CultureInfo.CurrentCulture);
        string week = $"Wk {ISOWeek.GetWeekOfYear(context.Time)}";
        string subtitle = Config.Subtitle ?? "";
        (string left, string right) = mode == HeaderMode.Date
            ? (date, Config.ShowWeek ? week : subtitle)
            : (subtitle, Config.ShowWeek ? $"{date}  {week}" : date);

        float height = theme.LabelSize * 1.5f;
        if (height >= content.Height)
            return content;

        // Each text has half of the row at most; the left one takes what the right one leaves.
        var row = SKRect.Create(content.Left, content.Top, content.Width, height);
        float half = (row.Width - theme.Gap) / 2;
        var rightRect = string.IsNullOrEmpty(right)
            ? SKRect.Empty
            : TextLine.Measure(theme, new SKRect(row.Right - half, row.Top, row.Right, row.Bottom), right, TextStep.Label, HorizontalAlign.Right);
        float leftLimit = rightRect.Width > 0 ? row.Right - Math.Max(rightRect.Width, 0) - theme.Gap : row.Right;
        TextLine.Draw(context.Canvas, theme, new SKRect(row.Left, row.Top, Math.Max(row.Left, leftLimit), row.Bottom),
            left, TextStep.Label, TextEmphasis.Muted, HorizontalAlign.Left);
        if (rightRect.Width > 0)
            TextLine.Draw(context.Canvas, theme, new SKRect(row.Right - half, row.Top, row.Right, row.Bottom),
                right, TextStep.Label, TextEmphasis.Muted, HorizontalAlign.Right);

        return new SKRect(content.Left, row.Bottom + theme.Gap, content.Right, content.Bottom);
    }

    /// <summary>The colon shows, except on every other second with <c>blinkSeparator</c>.</summary>
    private bool SeparatorVisible(DateTime time) => !Config.BlinkSeparator || time.Second % 2 == 0;

    /// <summary>The small text after the time: the seconds, the 12-hour period, both or none.</summary>
    private string? Suffix(DateTime time, bool is12h)
    {
        string? period = is12h ? (time.Hour >= 12 ? "PM" : "AM") : null;
        if (!Config.ShowSeconds)
            return period;
        string seconds = time.ToString("ss", CultureInfo.InvariantCulture);
        return period == null ? seconds : seconds + " " + period;
    }

    /// <summary>The widest the small text gets, so the size does not change from second to second; null for the usual AM or PM.</summary>
    private string? WidestSuffix(bool is12h) => Config.ShowSeconds ? (is12h ? "88 AM" : "88") : null;

    /// <summary>With seconds the unit's width changes with the digits; the time is drawn at the size the widest unit gives.</summary>
    private ReadoutOptions KeepSize(Theme theme, SKRect slot, string timeText, ReadoutOptions options, bool is12h)
    {
        if (!Config.ShowSeconds)
            return options;

        var widest = new ReadoutOptions
        {
            Unit = WidestSuffix(is12h),
            UnitPlacement = options.UnitPlacement,
            WidestValue = options.WidestValue,
            Vertical = options.Vertical,
            Scale = options.Scale,
        };
        float wide = Readout.Measure(theme, slot, timeText, widest).Height;
        float actual = Readout.Measure(theme, slot, timeText, options).Height;
        if (actual <= 0 || wide <= 0 || wide >= actual)
            return options;

        return new ReadoutOptions
        {
            Unit = options.Unit,
            UnitPlacement = options.UnitPlacement,
            WidestValue = options.WidestValue,
            Vertical = options.Vertical,
            ValueColor = options.ValueColor,
            Scale = options.Scale * wide / actual,
        };
    }

    private static DateTime TruncateToSecond(DateTime time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second, time.Kind);

    private static DateTime TruncateToMinute(DateTime time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Kind);
}
