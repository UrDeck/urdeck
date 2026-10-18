// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// What a card has outside its edge: the shadow, the glow and the part of the corner marks outside the card. One routine for
/// the host's underlay pictures (<see cref="UnderlayPicture"/>) and the snapshot's pass under the cards
/// (<see cref="PageRenderer"/>), so the window and a snapshot agree.
/// </summary>
internal static class CardUnderlay
{
    /// <summary>
    /// Draws the outside of the card <paramref name="shape"/>, whose bounds are where the card lies in the canvas's current
    /// coordinates: shadow, then glow, then the card's own shape cut out of both (a translucent card does not show its own
    /// shadow through its fill), then the marks with the card's rectangle clipped away (the card's surface draws the rest).
    /// </summary>
    public static void Draw(SKCanvas canvas, ResolvedCardStyle style, CardShape shape)
    {
        if (!style.HasOutside)
            return;
        var card = shape.Bounds;
        var reach = card;
        reach.Inflate(style.Bleed + 1, style.Bleed + 1);

        if (style.Shadow != null || style.Glow != null)
        {
            int layer = canvas.SaveLayer(reach, null);
            if (style.Shadow is { } shadow)
            {
                using var blur = shadow.Blur > 0 ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, shadow.Blur / 2) : null;
                using var paint = new SKPaint { Color = shadow.Color, IsAntialias = true, MaskFilter = blur };
                using var moved = ShadowShape(style, shape, shadow.Spread);
                moved.Offset(shadow.X, shadow.Y);
                canvas.DrawPath(moved, paint);
            }
            if (style.Glow is { } glow)
            {
                using var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, glow.Blur / 2);
                using var paint = new SKPaint { Color = glow.Color, IsAntialias = true, MaskFilter = blur };
                shape.Draw(canvas, paint);
            }
            using (var cut = new SKPaint { Color = SKColors.Black, IsAntialias = true, BlendMode = SKBlendMode.DstOut })
                shape.Draw(canvas, cut);
            canvas.RestoreToCount(layer);
        }

        if (style.Marks is { ReachesOutside: true } marks)
        {
            int save = canvas.Save();
            canvas.ClipRect(card, SKClipOperation.Difference);
            using var paint = CardChrome.MarkPaint(marks);
            CardChrome.DrawMarks(canvas, card, marks, paint);
            canvas.RestoreToCount(save);
        }
    }

    /// <summary>
    /// The card's shape grown by <paramref name="spread"/> on every side (shrunk when negative): a rounded rectangle keeps its
    /// corners concentric, a chamfer's cut moves out with its sides.
    /// </summary>
    private static SKPath ShadowShape(ResolvedCardStyle style, CardShape shape, float spread)
    {
        var bounds = shape.Bounds;
        var grown = bounds;
        grown.Inflate(spread, spread);
        if (grown.Width <= 0 || grown.Height <= 0)
            return new SKPath();
        if (shape.Path != null)
        {
            float cut = Math.Min(style.Cut, Math.Min(bounds.Width, bounds.Height) / 2);
            // A 45 degree cut offset by s along its normal starts s (2 - sqrt 2) further along each side.
            float grownCut = Math.Clamp(cut + spread * (2 - MathF.Sqrt(2)), 0, Math.Min(grown.Width, grown.Height) / 2);
            return CardShape.Chamfer(grown, grownCut, style.CutCorners);
        }

        float radius = Math.Clamp(shape.Round!.Radii[0].X + spread, 0, Math.Min(grown.Width, grown.Height) / 2);
        using var round = new SKRoundRect(grown, radius, radius);
        using var builder = new SKPathBuilder();
        builder.AddRoundRect(round);
        return builder.Detach();
    }
}
