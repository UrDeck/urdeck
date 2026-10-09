// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk.Data;

namespace UrDeck.Sdk.Components;

/// <summary>
/// The credit of the data a widget shows, drawn the same way by every widget: each distinct attribution text once, as a
/// label-step line in the muted colour. It draws text only; the link is for a tap or an about screen.
/// </summary>
public static class Attribution
{
    /// <summary>
    /// The height the credits need at this width: one line each for the distinct texts, zero when there are none. A widget
    /// takes it off its content rectangle before laying out the rest.
    /// </summary>
    public static float Measure(Theme theme, IReadOnlyList<ReadingAttribution> attributions, float width)
    {
        if (width <= 0)
            return 0;
        int lines = CountDistinct(attributions);
        return lines == 0 ? 0 : lines * LineHeight(theme);
    }

    /// <summary>Draws the credits into <paramref name="rect"/>, one line each from the top.</summary>
    public static void Draw(
        SKCanvas canvas,
        Theme theme,
        SKRect rect,
        IReadOnlyList<ReadingAttribution> attributions,
        HorizontalAlign horizontal = HorizontalAlign.Left)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || attributions.Count == 0)
            return;

        float line = LineHeight(theme);
        float top = rect.Top;
        using var font = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Label), theme.GetTextSize(TextStep.Label));
        for (int i = 0; i < attributions.Count; i++)
        {
            if (IsRepeat(attributions, i))
                continue;

            var attribution = attributions[i];
            string text = attribution.Text;
            if (!string.IsNullOrEmpty(attribution.ShortText) && font.MeasureText(text) > rect.Width)
                text = attribution.ShortText;
            TextLine.Draw(canvas, theme, SKRect.Create(rect.Left, top, rect.Width, line), text, TextStep.Label, TextEmphasis.Muted, horizontal, VerticalAlign.Middle);
            top += line;
        }
    }

    private static float LineHeight(Theme theme)
    {
        using var font = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Label), theme.GetTextSize(TextStep.Label));
        var metrics = font.Metrics;
        float height = metrics.Descent - metrics.Ascent;
        return height > 0 ? height : theme.GetTextSize(TextStep.Label) * 1.2f;
    }

    private static int CountDistinct(IReadOnlyList<ReadingAttribution> attributions)
    {
        int count = 0;
        for (int i = 0; i < attributions.Count; i++)
        {
            if (!IsRepeat(attributions, i))
                count++;
        }

        return count;
    }

    /// <summary>Whether an earlier entry has the same text, or this entry has none.</summary>
    private static bool IsRepeat(IReadOnlyList<ReadingAttribution> attributions, int index)
    {
        if (string.IsNullOrEmpty(attributions[index].Text))
            return true;
        for (int i = 0; i < index; i++)
        {
            if (string.Equals(attributions[i].Text, attributions[index].Text, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
