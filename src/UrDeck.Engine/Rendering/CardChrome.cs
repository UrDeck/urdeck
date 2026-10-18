// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// The card's two passes around the widget's content: <see cref="DrawBack"/> draws the shape, fill, sheen, accent strip and
/// border and leaves the canvas clipped to the shape; <see cref="DrawFront"/> restores it and draws the highlight (made
/// within the shape) and the part of the corner marks that lies on the card.
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

    /// <summary>
    /// Ends the card after the content: restores the canvas to before <see cref="DrawBack"/>, then draws the highlight,
    /// which lies within the shape, and the corner marks, which the surface's edge (or the snapshot's clip to the card's
    /// rectangle) cuts at the card's edge.
    /// </summary>
    public static void DrawFront(SKCanvas canvas, SizedCardChrome? chrome, int save)
    {
        canvas.RestoreToCount(save);
        if (chrome is { HasHighlight: true })
            chrome.DrawHighlights(canvas);
        if (chrome is { Marks: { } marks, MarkPaint: { } paint })
            DrawMarks(canvas, chrome.Shape.Bounds, marks, paint);
    }

    /// <summary>The stroke a mark is drawn with: its colour and thickness, square ends and a mitred corner.</summary>
    internal static SKPaint MarkPaint(CardMarks marks) => new()
    {
        Color = marks.Color,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = marks.Thickness,
        StrokeCap = SKStrokeCap.Butt,
        StrokeJoin = SKStrokeJoin.Miter,
    };

    /// <summary>
    /// Draws <paramref name="marks"/> at the corners of <paramref name="card"/>, whole: the card's surface draws them and its
    /// edge cuts them; what lies outside the card is drawn with the card's rectangle clipped away. Each arm is
    /// <see cref="CardMarks.Size"/> long from the mark's outer corner, which is <see cref="CardMarks.Inset"/> in from both
    /// edges. A bracket runs along both edges, a tick along the top or bottom edge only.
    /// </summary>
    internal static void DrawMarks(SKCanvas canvas, SKRect card, CardMarks marks, SKPaint paint)
    {
        // The line's centre is half its thickness further in than its outer edge.
        float outer = marks.Inset;
        float centre = outer + marks.Thickness / 2;
        foreach (var corner in new[] { CardCorners.TopLeft, CardCorners.TopRight, CardCorners.BottomRight, CardCorners.BottomLeft })
        {
            if (!marks.Corners.HasFlag(corner))
                continue;
            bool left = corner is CardCorners.TopLeft or CardCorners.BottomLeft;
            bool top = corner is CardCorners.TopLeft or CardCorners.TopRight;
            float sx = left ? 1 : -1, sy = top ? 1 : -1;
            float edgeX = left ? card.Left : card.Right, edgeY = top ? card.Top : card.Bottom;

            using var path = new SKPathBuilder();
            path.MoveTo(edgeX + sx * (outer + marks.Size), edgeY + sy * centre);
            if (marks.Style == CornerMarkStyle.Bracket)
            {
                path.LineTo(edgeX + sx * centre, edgeY + sy * centre);
                path.LineTo(edgeX + sx * centre, edgeY + sy * (outer + marks.Size));
            }
            else
            {
                path.LineTo(edgeX + sx * outer, edgeY + sy * centre);
            }
            using var line = path.Detach();
            canvas.DrawPath(line, paint);
        }
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
