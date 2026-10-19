// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// A short status text in a rectangle, as <see cref="Theme.Chip"/> says: the text alone, or on a pill. The text is drawn at
/// the size of its step and shrinks, never grows, to fit; the theme's case and spacing apply as for <see cref="TextLine"/>.
/// With the theme's <c>colorBy</c> set to status and a level given, the level's colour replaces the theme's chip colours.
/// </summary>
public static class Chip
{
    // The pill's padding as a share of the text size.
    private const float PadX = 0.7f;
    private const float PadY = 0.4f;

    /// <summary>Draws the chip and returns the rectangle it occupies (the pill, or the text).</summary>
    public static SKRect Draw(
        SKCanvas canvas,
        Theme theme,
        SKRect rect,
        string text,
        GaugeLevel? level = null,
        TextStep step = TextStep.Label,
        HorizontalAlign horizontal = HorizontalAlign.Center,
        VerticalAlign vertical = VerticalAlign.Middle,
        TextEmphasis emphasis = TextEmphasis.Primary) =>
        Run(canvas, theme, rect, text, level, step, horizontal, vertical, emphasis);

    /// <summary>The rectangle <see cref="Draw"/> would occupy in <paramref name="rect"/>, without drawing anything.</summary>
    public static SKRect Measure(
        Theme theme,
        SKRect rect,
        string text,
        TextStep step = TextStep.Label,
        HorizontalAlign horizontal = HorizontalAlign.Center,
        VerticalAlign vertical = VerticalAlign.Middle) =>
        Run(null, theme, rect, text, null, step, horizontal, vertical, TextEmphasis.Primary);

    private static SKRect Run(
        SKCanvas? canvas,
        Theme theme,
        SKRect rect,
        string text,
        GaugeLevel? level,
        TextStep step,
        HorizontalAlign horizontal,
        VerticalAlign vertical,
        TextEmphasis emphasis)
    {
        if (string.IsNullOrEmpty(text) || rect.Width <= 0 || rect.Height <= 0)
            return SKRect.Create(rect.Left, rect.Top, 0, 0);

        var chip = theme.Chip;
        SKColor? status = chip.ColorByStatus && level is { } l ? LevelColor(theme, l) : null;

        if (chip.Shape == ChipShape.None)
        {
            if (canvas == null)
                return TextLine.Measure(theme, rect, text, step, horizontal, vertical);
            if ((status ?? chip.TextColor) is { } plain)
                return TextLine.DrawColored(canvas, theme, rect, text, step, plain, horizontal, vertical);
            return TextLine.Draw(canvas, theme, rect, text, step, emphasis, horizontal, vertical);
        }

        var role = step switch { TextStep.Label => TextRole.Label, TextStep.Body => TextRole.Body, _ => TextRole.Title };
        float size = theme.GetTextSize(step);
        text = TextStyle.Apply(TextStyle.CaseFor(theme.TextStyle, step), text);
        float tracking = TextStyle.TrackingFor(theme.TextStyle, step);

        using var font = TextMetrics.CreateFont(theme.GetTypeface(role), size);
        float width = TextMetrics.Measure(font, text, tracking);
        float cap = TextMetrics.CapHeight(font);
        float pillWidth = width + 2 * PadX * size;
        float pillHeight = cap + 2 * PadY * size;

        // The padding and the text shrink together until the pill fits.
        float shrink = Math.Min(1f, Math.Min(rect.Width / pillWidth, rect.Height / pillHeight));
        if (shrink < 1f)
        {
            font.Size = size * shrink;
            width = TextMetrics.Measure(font, text, tracking);
            cap = TextMetrics.CapHeight(font);
            pillWidth *= shrink;
            pillHeight *= shrink;
        }

        float left = horizontal switch
        {
            HorizontalAlign.Left => rect.Left,
            HorizontalAlign.Right => rect.Right - pillWidth,
            _ => rect.MidX - pillWidth / 2,
        };
        float top = vertical switch
        {
            VerticalAlign.Top => rect.Top,
            VerticalAlign.Bottom => rect.Bottom - pillHeight,
            _ => rect.MidY - pillHeight / 2,
        };
        var pill = SKRect.Create(left, top, pillWidth, pillHeight);
        if (canvas == null)
            return pill;

        SKColor? fill = status?.WithAlpha(0x33) ?? chip.Fill;
        SKColor? border = status ?? chip.Border;
        SKColor ink = status ?? chip.TextColor ?? theme.Text;
        float radius = pillHeight / 2;
        if (fill is { } f)
        {
            using var paint = new SKPaint { Color = f, IsAntialias = true };
            canvas.DrawRoundRect(pill, radius, radius, paint);
        }

        if (border is { } b)
        {
            float stroke = Math.Max(1f, font.Size * 0.08f);
            using var paint = new SKPaint { Color = b, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke };
            var inner = pill;
            inner.Inflate(-stroke / 2, -stroke / 2);
            canvas.DrawRoundRect(inner, radius - stroke / 2, radius - stroke / 2, paint);
        }

        using var textPaint = new SKPaint { Color = ink, IsAntialias = true };
        float x = pill.MidX - width / 2;
        float baseline = pill.MidY + cap / 2;
        if (tracking == 0)
            canvas.DrawText(text, x, baseline, SKTextAlign.Left, font, textPaint);
        else
            TextMetrics.DrawTracked(canvas, text, x, baseline, font, textPaint, tracking);
        return pill;
    }

    private static SKColor LevelColor(Theme theme, GaugeLevel level) => level switch
    {
        GaugeLevel.Critical => theme.Critical,
        GaugeLevel.Warning => theme.Warning,
        _ => theme.Good,
    };
}
