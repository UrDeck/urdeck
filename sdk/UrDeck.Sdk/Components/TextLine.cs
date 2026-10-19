// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// One line of text at one of the theme's three steps. The text is drawn at the step's size, and only shrinks, never
/// grows, when it is wider than the rectangle. The theme's letter case and spacing apply to the label and title steps
/// (<see cref="Theme.TextStyle"/>), before the text is measured.
/// </summary>
public static class TextLine
{
    /// <summary>Draws the line and returns the rectangle it occupies (its width by its cap height).</summary>
    public static SKRect Draw(
        SKCanvas canvas,
        Theme theme,
        SKRect rect,
        string text,
        TextStep step = TextStep.Body,
        TextEmphasis emphasis = TextEmphasis.Primary,
        HorizontalAlign horizontal = HorizontalAlign.Center,
        VerticalAlign vertical = VerticalAlign.Middle) =>
        Run(canvas, theme, rect, text, step, emphasis, horizontal, vertical);

    /// <summary>The rectangle <see cref="Draw"/> would occupy, without drawing anything.</summary>
    public static SKRect Measure(
        Theme theme,
        SKRect rect,
        string text,
        TextStep step = TextStep.Body,
        HorizontalAlign horizontal = HorizontalAlign.Center,
        VerticalAlign vertical = VerticalAlign.Middle) =>
        Run(null, theme, rect, text, step, TextEmphasis.Primary, horizontal, vertical);

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
        Run(canvas, theme, rect, text, step, TextEmphasis.Primary, horizontal, vertical, color);

    private static SKRect Run(
        SKCanvas? canvas,
        Theme theme,
        SKRect rect,
        string text,
        TextStep step,
        TextEmphasis emphasis,
        HorizontalAlign horizontal,
        VerticalAlign vertical,
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
        if (width > rect.Width)
        {
            font.Size = size * rect.Width / width;
            width = TextMetrics.Measure(font, text, tracking);
        }

        float cap = TextMetrics.CapHeight(font);
        float left = horizontal switch
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
            if (tracking == 0)
                canvas.DrawText(text, left, top + cap, SKTextAlign.Left, font, paint);
            else
                TextMetrics.DrawTracked(canvas, text, left, top + cap, font, paint, tracking);
        }
        return SKRect.Create(left, top, width, cap);
    }
}
