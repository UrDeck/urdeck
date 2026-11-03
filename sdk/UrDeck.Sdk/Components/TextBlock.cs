// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>What <see cref="TextBlock"/> drew (or would draw): the rectangle it occupies, the lines shown, and whether text was cut.</summary>
/// <param name="Bounds">The rectangle the lines occupy.</param>
/// <param name="Lines">The number of lines shown.</param>
/// <param name="Truncated">True when text remained and the last line shown ends in an ellipsis.</param>
public readonly record struct TextBlockResult(SKRect Bounds, int Lines, bool Truncated);

/// <summary>
/// Text wrapped to the width of a rectangle at one of the theme's three steps. Lines break at spaces, a word wider than a
/// line breaks between characters, and a line feed starts a new line. The text never changes size to fit: a block that does
/// not fit its line limit or its height shows fewer lines and ends the last in an ellipsis. Layout is cached per text, step
/// and width, so a repaint with the same text measures nothing.
/// </summary>
public static class TextBlock
{
    /// <summary>The distance between lines, as a multiple of the step's text size.</summary>
    public const float LineSpacing = 1.25f;

    /// <summary>Draws the block and reports what it drew. <c>maxLines</c> of 0 means as many lines as the height holds.</summary>
    public static TextBlockResult Draw(
        SKCanvas canvas,
        Theme theme,
        SKRect rect,
        string text,
        TextStep step = TextStep.Body,
        TextEmphasis emphasis = TextEmphasis.Primary,
        HorizontalAlign horizontal = HorizontalAlign.Left,
        VerticalAlign vertical = VerticalAlign.Top,
        int maxLines = 0) =>
        Run(canvas, theme, rect, text, step, emphasis, horizontal, vertical, maxLines);

    /// <summary>What <see cref="Draw"/> would do, without drawing anything.</summary>
    public static TextBlockResult Measure(
        Theme theme,
        SKRect rect,
        string text,
        TextStep step = TextStep.Body,
        HorizontalAlign horizontal = HorizontalAlign.Left,
        VerticalAlign vertical = VerticalAlign.Top,
        int maxLines = 0) =>
        Run(null, theme, rect, text, step, TextEmphasis.Primary, horizontal, vertical, maxLines);

    private static TextBlockResult Run(
        SKCanvas? canvas,
        Theme theme,
        SKRect rect,
        string text,
        TextStep step,
        TextEmphasis emphasis,
        HorizontalAlign horizontal,
        VerticalAlign vertical,
        int maxLines)
    {
        var none = new TextBlockResult(SKRect.Create(rect.Left, rect.Top, 0, 0), 0, false);
        if (string.IsNullOrEmpty(text) || rect.Width <= 0 || rect.Height <= 0)
            return none;

        float size = theme.GetTextSize(step);
        float lineHeight = size * LineSpacing;
        var all = TextLayoutCache.Lines(theme, step, text, rect.Width);
        int limit = Math.Min((int)MathF.Floor(rect.Height / lineHeight), maxLines > 0 ? maxLines : int.MaxValue);
        if (limit <= 0)
            return none with { Truncated = true };

        int shown = Math.Min(all.Count, limit);
        bool truncated = all.Count > shown;
        var lines = new List<string>(all.Take(shown));

        float tracking = TextStyle.TrackingFor(theme.TextStyle, step);
        using var font = TextMetrics.CreateFont(theme.GetTypeface(TextLayoutCache.RoleOf(step)), size);
        if (truncated)
            lines[shown - 1] = TextMetrics.Ellipsize(font, lines[shown - 1] + TextMetrics.Ellipsis, tracking, rect.Width);

        float cap = TextMetrics.CapHeight(font);
        float height = shown * lineHeight;
        float top = vertical switch
        {
            VerticalAlign.Top => rect.Top,
            VerticalAlign.Bottom => rect.Bottom - height,
            _ => rect.MidY - height / 2,
        };

        using var paint = canvas == null ? null : new SKPaint { Color = emphasis == TextEmphasis.Muted ? theme.TextMuted : theme.Text, IsAntialias = true };
        float minLeft = rect.Right;
        float widest = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            float width = TextMetrics.Measure(font, lines[i], tracking);
            float left = horizontal switch
            {
                HorizontalAlign.Left => rect.Left,
                HorizontalAlign.Right => rect.Right - width,
                _ => rect.MidX - width / 2,
            };
            minLeft = Math.Min(minLeft, left);
            widest = Math.Max(widest, width);
            if (canvas != null && lines[i].Length > 0)
                TextMetrics.DrawLine(canvas, lines[i], left, top + i * lineHeight + (lineHeight + cap) / 2, font, paint!, tracking);
        }

        return new TextBlockResult(SKRect.Create(minLeft, top, widest, height), shown, truncated);
    }
}
