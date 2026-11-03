// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// One line of text at one of the theme's three steps. The text is drawn at the step's size, and by default only shrinks,
/// never grows, when it is wider than the rectangle; <see cref="TextOverflow"/> chooses another answer (an ellipsis, a
/// clip, a marquee). The theme's letter case and spacing apply to the label and title steps
/// (<see cref="Theme.TextStyle"/>), before the text is measured.
/// </summary>
public static class TextLine
{
    /// <summary>
    /// Draws the line and returns the rectangle it occupies (its width by its cap height). <c>overflow</c> chooses what
    /// happens to text wider than the rectangle; <c>time</c> (<see cref="WidgetRenderContext.Time"/>) and <c>motion</c>
    /// (<see cref="WidgetRenderContext.Motion"/>) matter only to a <see cref="TextOverflow.Marquee"/>, which moves only at
    /// <see cref="MotionLevel.Full"/>.
    /// </summary>
    public static SKRect Draw(
        SKCanvas canvas,
        Theme theme,
        SKRect rect,
        string text,
        TextStep step = TextStep.Body,
        TextEmphasis emphasis = TextEmphasis.Primary,
        HorizontalAlign horizontal = HorizontalAlign.Center,
        VerticalAlign vertical = VerticalAlign.Middle,
        TextOverflow overflow = TextOverflow.Shrink,
        DateTime time = default,
        MotionLevel motion = MotionLevel.Full) =>
        Run(canvas, theme, rect, text, step, emphasis, horizontal, vertical, overflow, time, motion);

    /// <summary>The rectangle <see cref="Draw"/> would occupy, without drawing anything.</summary>
    public static SKRect Measure(
        Theme theme,
        SKRect rect,
        string text,
        TextStep step = TextStep.Body,
        HorizontalAlign horizontal = HorizontalAlign.Center,
        VerticalAlign vertical = VerticalAlign.Middle,
        TextOverflow overflow = TextOverflow.Shrink) =>
        Run(null, theme, rect, text, step, TextEmphasis.Primary, horizontal, vertical, overflow, default, MotionLevel.Full);

    /// <summary>
    /// Whether a line drawn with <paramref name="overflow"/> is moving: it is a <see cref="TextOverflow.Marquee"/>, wider than
    /// <paramref name="rect"/>, at <see cref="MotionLevel.Full"/>. A widget returns it from <see cref="IWidget.IsAnimating"/>.
    /// Cheap: the width comes from the layout cache.
    /// </summary>
    public static bool IsMoving(
        Theme theme,
        SKRect rect,
        string text,
        TextStep step = TextStep.Body,
        TextOverflow overflow = TextOverflow.Marquee,
        MotionLevel motion = MotionLevel.Full) =>
        overflow == TextOverflow.Marquee
        && motion == MotionLevel.Full
        && !string.IsNullOrEmpty(text)
        && rect.Width > 0
        && TextLayoutCache.Width(theme, step, text) > rect.Width;

    /// <summary>Draws the line in <paramref name="color"/> instead of one of the theme's text colours.</summary>
    internal static SKRect DrawColored(
        SKCanvas canvas,
        Theme theme,
        SKRect rect,
        string text,
        TextStep step,
        SKColor color,
        HorizontalAlign horizontal = HorizontalAlign.Center,
        VerticalAlign vertical = VerticalAlign.Middle) =>
        Run(canvas, theme, rect, text, step, TextEmphasis.Primary, horizontal, vertical, TextOverflow.Shrink, default, MotionLevel.Full, color);

    private static SKRect Run(
        SKCanvas? canvas,
        Theme theme,
        SKRect rect,
        string text,
        TextStep step,
        TextEmphasis emphasis,
        HorizontalAlign horizontal,
        VerticalAlign vertical,
        TextOverflow overflow,
        DateTime time,
        MotionLevel motion,
        SKColor? color = null)
    {
        if (string.IsNullOrEmpty(text) || rect.Width <= 0 || rect.Height <= 0)
            return SKRect.Create(rect.Left, rect.Top, 0, 0);

        var role = step switch { TextStep.Label => TextRole.Label, TextStep.Body => TextRole.Body, _ => TextRole.Title };
        float size = theme.GetTextSize(step);
        text = TextStyle.Apply(TextStyle.CaseFor(theme.TextStyle, step), text);
        float tracking = TextStyle.TrackingFor(theme.TextStyle, step);

        using var font = TextMetrics.CreateFont(theme.GetTypeface(role), size);
        float width = TextMetrics.Measure(font, text, tracking);
        bool cutAtEdge = false;
        float scrolled = 0;
        if (width > rect.Width)
        {
            // A marquee below full motion stands still and is an ellipsis.
            if (overflow == TextOverflow.Marquee && motion != MotionLevel.Full)
                overflow = TextOverflow.Ellipsis;

            switch (overflow)
            {
                case TextOverflow.Shrink:
                    font.Size = size * rect.Width / width;
                    width = TextMetrics.Measure(font, text, tracking);
                    break;
                case TextOverflow.Ellipsis:
                    text = TextMetrics.Ellipsize(font, text, tracking, rect.Width);
                    if (text.Length == 0)
                        return SKRect.Create(rect.Left, rect.Top, 0, 0);
                    width = TextMetrics.Measure(font, text, tracking);
                    break;
                case TextOverflow.Clip:
                    cutAtEdge = true;
                    break;
                default:
                    cutAtEdge = true;
                    scrolled = Marquee.Offset(time.Ticks / (double)TimeSpan.TicksPerSecond, width - rect.Width, size);
                    break;
            }
        }

        float cap = TextMetrics.CapHeight(font);
        float left = cutAtEdge
            ? rect.Left - scrolled
            : horizontal switch
            {
                HorizontalAlign.Left => rect.Left,
                HorizontalAlign.Right => rect.Right - width,
                _ => rect.MidX - width / 2,
            };
        float top = vertical switch
        {
            VerticalAlign.Top => rect.Top,
            VerticalAlign.Bottom => rect.Bottom - cap,
            _ => rect.MidY - cap / 2,
        };

        if (canvas != null)
        {
            using var paint = new SKPaint { Color = color ?? (emphasis == TextEmphasis.Muted ? theme.TextMuted : theme.Text), IsAntialias = true };
            int save = cutAtEdge ? canvas.Save() : 0;
            if (cutAtEdge)
                canvas.ClipRect(rect);
            if (tracking == 0)
                canvas.DrawText(text, left, top + cap, SKTextAlign.Left, font, paint);
            else
                TextMetrics.DrawTracked(canvas, text, left, top + cap, font, paint, tracking);
            if (cutAtEdge)
                canvas.RestoreToCount(save);
        }

        return cutAtEdge
            ? SKRect.Create(rect.Left, top, rect.Width, cap)
            : SKRect.Create(left, top, width, cap);
    }
}
