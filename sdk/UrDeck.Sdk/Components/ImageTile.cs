// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Text;
using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// An image fitted into a rectangle, with an optional label below it and a placeholder while there is no image. The
/// image keeps its own colours and transparency. The component keeps no state, does not own the image and draws no
/// pressed state: the host shows a press on the whole card.
/// </summary>
public static class ImageTile
{
    /// <summary>An image is never drawn at more than this many times its own pixel size.</summary>
    private const float MaxEnlargement = 2f;

    /// <summary>The space between the image and the label, as a fraction of the label's line height.</summary>
    private const float LabelGap = 0.25f;

    /// <summary>The placeholder letter's cap height as a fraction of the placeholder square.</summary>
    private const float LetterShare = 0.4f;

    /// <summary>
    /// Draws the tile and returns the rectangle the image, or the placeholder, occupies.
    /// </summary>
    /// <param name="canvas">Where to draw.</param>
    /// <param name="theme">The active theme.</param>
    /// <param name="rect">The space for the image and the label together.</param>
    /// <param name="image">The image, or null to draw the placeholder. It is not disposed.</param>
    /// <param name="label">A text drawn below the image; null or empty reserves no space.</param>
    /// <param name="placeholder">
    /// The text whose first letter or digit the placeholder shows; null or a text without one draws the square empty.
    /// </param>
    public static SKRect Draw(SKCanvas canvas, Theme theme, SKRect rect, SKImage? image, string? label = null, string? placeholder = null)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return SKRect.Create(rect.Left, rect.Top, 0, 0);

        var area = rect;
        if (!string.IsNullOrEmpty(label))
        {
            float line = LineHeight(theme);
            float top = rect.Bottom - line;
            // A rectangle too low for both shows the image alone.
            if (top - line * LabelGap > rect.Top)
            {
                TextLine.Draw(canvas, theme, new SKRect(rect.Left, top, rect.Right, rect.Bottom), label, TextStep.Label, TextEmphasis.Muted);
                area = new SKRect(rect.Left, rect.Top, rect.Right, top - line * LabelGap);
            }
        }

        return image != null && image.Width > 0 && image.Height > 0
            ? DrawImage(canvas, area, image)
            : DrawPlaceholder(canvas, theme, area, placeholder);
    }

    private static SKRect DrawImage(SKCanvas canvas, SKRect area, SKImage image)
    {
        float scale = Math.Min(MaxEnlargement, Math.Min(area.Width / image.Width, area.Height / image.Height));
        float width = image.Width * scale;
        float height = image.Height * scale;
        var target = SKRect.Create(area.MidX - width / 2, area.MidY - height / 2, width, height);

        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(image, target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
        return target;
    }

    private static SKRect DrawPlaceholder(SKCanvas canvas, Theme theme, SKRect area, string? text)
    {
        float side = Math.Min(area.Width, area.Height);
        var square = SKRect.Create(area.MidX - side / 2, area.MidY - side / 2, side, side);
        float radius = Math.Min(theme.CardRadius, side / 2);

        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = theme.AccentDim };
        canvas.DrawRoundRect(square, radius, radius, paint);

        string? letter = FirstLetter(text);
        if (letter == null)
            return square;

        using var font = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Value), TextMetrics.ReferenceSize);
        float cap = TextMetrics.CapHeight(font);
        if (cap <= 0)
            return square;
        font.Size = TextMetrics.ReferenceSize * side * LetterShare / cap;

        float width = font.MeasureText(letter);
        paint.Color = theme.Text;
        canvas.DrawText(letter, square.MidX - width / 2, square.MidY + side * LetterShare / 2, SKTextAlign.Left, font, paint);
        return square;
    }

    /// <summary>The first letter or digit of the text, upper-cased; null when it has none.</summary>
    private static string? FirstLetter(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
                return Rune.ToUpperInvariant(rune).ToString();
        }

        return null;
    }

    private static float LineHeight(Theme theme)
    {
        using var font = TextMetrics.CreateFont(theme.GetTypeface(TextRole.Label), theme.GetTextSize(TextStep.Label));
        var metrics = font.Metrics;
        float height = metrics.Descent - metrics.Ascent;
        return height > 0 ? height : theme.GetTextSize(TextStep.Label) * 1.2f;
    }
}
