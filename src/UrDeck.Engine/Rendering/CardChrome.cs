// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Sdk;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// The card's two passes around the widget's content: <see cref="DrawBack"/> draws the shape, fill and border and leaves
/// the canvas clipped to the shape; <see cref="DrawFront"/> restores it (the pieces drawn over the content come later).
/// A plain style (<see cref="Themes.ResolvedCardStyle.IsPlain"/>) takes the drawing of before card chrome, unchanged.
/// </summary>
internal static class CardChrome
{
    /// <summary>
    /// Draws the card's back and clips the canvas to its shape. <paramref name="chrome"/> null is the plain card, drawn from
    /// <paramref name="theme"/>. Returns the save count to hand to <see cref="DrawFront"/>.
    /// </summary>
    public static int DrawBack(SKCanvas canvas, System.Drawing.Size size, Theme theme, SizedCardChrome? chrome)
    {
        if (chrome == null)
        {
            var card = PlainShape(size, theme);
            DrawPlainCard(canvas, card, theme);
            int plainSave = canvas.Save();
            canvas.ClipRoundRect(card, antialias: true);
            return plainSave;
        }

        int save = canvas.Save();
        if (chrome.BackImage is { } back)
        {
            // The image's edge is already antialiased; the clip is for the content.
            canvas.DrawImage(back, 0, 0, SKSamplingOptions.Default);
            chrome.Shape.Clip(canvas);
        }
        else
        {
            chrome.Shape.Clip(canvas);
            chrome.DrawBackPieces(canvas);
        }
        return save;
    }

    /// <summary>Ends the card after the content: restores the canvas to before <see cref="DrawBack"/>.</summary>
    public static void DrawFront(SKCanvas canvas, SizedCardChrome? chrome, int save)
    {
        _ = chrome;
        canvas.RestoreToCount(save);
    }

    /// <summary>Clips to the card's shape, for wiping what a failed widget drew.</summary>
    public static void ClipToShape(SKCanvas canvas, System.Drawing.Size size, Theme theme, SizedCardChrome? chrome)
    {
        if (chrome == null)
            canvas.ClipRoundRect(PlainShape(size, theme), antialias: true);
        else
            chrome.Shape.Clip(canvas);
    }

    /// <summary>The plain card's rounded rectangle; the corner radius is limited to half the smaller side.</summary>
    internal static SKRoundRect PlainShape(System.Drawing.Size size, Theme theme)
    {
        float radius = Math.Min(theme.CardRadius, Math.Min(size.Width, size.Height) / 2f);
        return new SKRoundRect(SKRect.Create(size.Width, size.Height), radius, radius);
    }

    private static void DrawPlainCard(SKCanvas canvas, SKRoundRect card, Theme theme)
    {
        using var fill = new SKPaint { Color = theme.CardFill, IsAntialias = true };
        canvas.DrawRoundRect(card, fill);

        if (theme.CardBorderWidth <= 0 || theme.CardBorder.Alpha == 0)
            return;

        // The border is drawn inside the card's edge so it never extends past the surface.
        float half = theme.CardBorderWidth / 2f;
        var inner = new SKRoundRect(card.Rect, card.Radii[0].X, card.Radii[0].Y);
        inner.Deflate(half, half);
        using var border = new SKPaint
        {
            Color = theme.CardBorder,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = theme.CardBorderWidth,
        };
        canvas.DrawRoundRect(inner, border);
    }
}
