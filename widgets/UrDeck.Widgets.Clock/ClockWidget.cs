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

    // The minute that was last painted. The widget ticks every second to catch the rollover promptly,
    // but only repaints when the displayed minute (which also covers the date) changes.
    private DateTime? _lastRenderedMinute;

    public override bool NeedsRender(DateTime now) => _lastRenderedMinute != TruncateToMinute(now);

    protected override void OnConfigured()
    {
        _lastRenderedMinute = null;
        _shownTiles = null;
        _flipping = false;
    }

    public override bool IsAnimating => _flipping && IsFlap;

    private bool IsFlap => Config.Style == "flap";

    public override void Render(WidgetRenderContext context)
    {
        _lastRenderedMinute = TruncateToMinute(context.Time);
        if (IsFlap)
        {
            RenderFlap(context);
            return;
        }

        var canvas = context.Canvas;
        var theme = context.Theme;
        var content = context.ContentRect;

        bool is12h = Config.Format == "12h";
        string timeText = is12h
            ? context.Time.ToString("h:mm", CultureInfo.InvariantCulture)
            : context.Time.ToString("HH:mm", CultureInfo.InvariantCulture);
        string dateText = context.Time.ToString("ddd MMM d, yyyy", CultureInfo.CurrentCulture);

        var options = new ReadoutOptions
        {
            Unit = is12h ? (context.Time.Hour >= 12 ? "PM" : "AM") : null,
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
        if (Config.ShowDate)
        {
            dateHeight = TextLine.Measure(theme, content, dateText, TextStep.Title).Height;
            gap = theme.TitleSize;
        }

        var timeSlot = new SKRect(content.Left, content.Top, content.Right, Math.Max(content.Top + 1, content.Bottom - dateHeight - gap));
        var timeRect = Readout.Measure(theme, timeSlot, timeText, options);
        float blockHeight = timeRect.Height + gap + dateHeight;
        float shift = (content.Height - blockHeight) / 2f;

        int save = canvas.Save();
        canvas.Translate(0, -shift);
        Readout.Draw(canvas, theme, timeSlot, timeText, options);
        if (Config.ShowDate)
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
        var content = context.ContentRect;

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
        if (Config.ShowDate)
        {
            dateHeight = TextLine.Measure(theme, content, dateText, TextStep.Title).Height;
            gap = theme.TitleSize;
        }

        var tileSlot = new SKRect(content.Left, content.Top, content.Right, Math.Max(content.Top + 1, content.Bottom - dateHeight - gap));
        var layout = FlapDisplay.Fit(theme, tileSlot, is12h, (float)Config.FontSize);
        float top = content.Top + (content.Height - (layout.TileHeight + gap + dateHeight)) / 2f;

        var color = Config.TextColor != null && SKColor.TryParse(Config.TextColor, out var custom) ? custom : theme.Text;
        var flip = new FlapDisplay.Flip(_flipping ? _flipFrom : tiles, tiles, progress);
        FlapDisplay.Draw(canvas, theme, layout, top, flip, color, context.PixelSize.Height, is12h ? (context.Time.Hour >= 12 ? "PM" : "AM") : null);

        if (Config.ShowDate)
        {
            var dateSlot = new SKRect(content.Left, top + layout.TileHeight + gap, content.Right, top + layout.TileHeight + gap + dateHeight);
            TextLine.Draw(canvas, theme, dateSlot, dateText, TextStep.Title, TextEmphasis.Muted, vertical: VerticalAlign.Top);
        }
    }

    /// <summary>The four tile characters: hour and minute digits, a space for the blank first tile of a 12-hour hour below 10.</summary>
    private static string Tiles(DateTime time, bool is12h) => is12h
        ? time.ToString("%h", CultureInfo.InvariantCulture).PadLeft(2) + time.ToString("mm", CultureInfo.InvariantCulture)
        : time.ToString("HHmm", CultureInfo.InvariantCulture);

    private static DateTime TruncateToMinute(DateTime time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Kind);
}
