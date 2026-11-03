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

    /// <summary>The ellipsis character text is ended with when it is cut.</summary>
    public const string Ellipsis = "…";

    /// <summary>
    /// <paramref name="text"/> cut between user-perceived characters so that it and an ellipsis fit <paramref name="maxWidth"/>, or
    /// <paramref name="text"/> itself when it fits. An empty string when not even the ellipsis fits.
    /// </summary>
    public static string Ellipsize(SKFont font, string text, float trackingEm, float maxWidth)
    {
        if (Measure(font, text, trackingEm) <= maxWidth)
            return text;
        string[] elements = Elements(text).ToArray();
        int low = 0;
        int high = elements.Length - 1;
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (Measure(font, Cut(elements, mid), trackingEm) <= maxWidth)
                low = mid;
            else
                high = mid - 1;
        }

        string cut = Cut(elements, low);
        return Measure(font, cut, trackingEm) <= maxWidth ? cut : "";
    }

    private static string Cut(string[] elements, int count) => string.Concat(elements.Take(count)).TrimEnd() + Ellipsis;

    /// <summary>
    /// <paramref name="text"/> broken into lines no wider than <paramref name="width"/>: at line feeds, then at spaces, then
    /// (for a word wider than a line) between characters.
    /// </summary>
    public static List<string> Break(SKFont font, string text, float trackingEm, float width)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            string current = "";
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (Measure(font, candidate, trackingEm) <= width)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                    lines.Add(current);
                current = "";
                if (Measure(font, word, trackingEm) <= width)
                {
                    current = word;
                    continue;
                }

                // A word wider than a line: fill lines with as many characters as fit.
                string piece = "";
                foreach (string element in Elements(word))
                {
                    if (piece.Length > 0 && Measure(font, piece + element, trackingEm) > width)
                    {
                        lines.Add(piece);
                        piece = "";
                    }

                    piece += element;
                }

                current = piece;
            }

            lines.Add(current);
        }

        return lines;
    }

    /// <summary>Draws one line at <paramref name="baseline"/>, character by character when it has letter spacing.</summary>
    public static void DrawLine(SKCanvas canvas, string text, float x, float baseline, SKFont font, SKPaint paint, float trackingEm)
    {
        if (trackingEm == 0)
            canvas.DrawText(text, x, baseline, SKTextAlign.Left, font, paint);
        else
            DrawTracked(canvas, text, x, baseline, font, paint, trackingEm);
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
