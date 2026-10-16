// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;

namespace UrDeck.Sdk.Components;

internal static class TextMetrics
{
    /// <summary>Reference size text is measured at; widths and heights scale linearly from it.</summary>
    public const float ReferenceSize = 100f;

    public static SKFont CreateFont(SKTypeface typeface, float size) =>
        new(typeface, size) { Subpixel = true, Edging = SKFontEdging.Antialias };

    public static float CapHeight(SKFont font)
    {
        float cap = font.Metrics.CapHeight;
        return cap > 0 ? cap : -font.Metrics.Ascent * 0.7f;
    }

    /// <summary>
    /// The width of <paramref name="text"/> with <paramref name="trackingEm"/> of the font's size added after each character
    /// but the last. With no spacing it is <see cref="SKFont.MeasureText(string, SKPaint)"/> exactly.
    /// </summary>
    public static float Measure(SKFont font, string text, float trackingEm)
    {
        if (trackingEm == 0)
            return font.MeasureText(text);
        float width = 0;
        int count = 0;
        foreach (string element in Elements(text))
        {
            width += font.MeasureText(element);
            count++;
        }
        return width + trackingEm * font.Size * Math.Max(0, count - 1);
    }

    /// <summary>Draws <paramref name="text"/> character by character with the spacing <see cref="Measure"/> counts.</summary>
    public static void DrawTracked(SKCanvas canvas, string text, float x, float baseline, SKFont font, SKPaint paint, float trackingEm)
    {
        float spacing = trackingEm * font.Size;
        foreach (string element in Elements(text))
        {
            canvas.DrawText(element, x, baseline, SKTextAlign.Left, font, paint);
            x += font.MeasureText(element) + spacing;
        }
    }

    /// <summary>The user-perceived characters of <paramref name="text"/>, so a surrogate pair or a combining mark stays whole.</summary>
    private static IEnumerable<string> Elements(string text)
    {
        var e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
            yield return e.GetTextElement();
    }

    /// <summary>The widest advance among the digits 0-9, so every digit can take the same width.</summary>
    public static float DigitAdvance(SKFont font)
    {
        float widest = 0;
        for (char d = '0'; d <= '9'; d++)
            widest = Math.Max(widest, font.MeasureText(d.ToString()));
        return widest;
    }
}
