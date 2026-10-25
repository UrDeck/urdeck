// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// The layout of a row of buttons, and nothing else: it returns one rectangle per button and draws nothing, so the look is the
/// theme's glyphs and text styles. A widget calls <see cref="Layout"/> to draw each glyph and <see cref="HitTest"/> to decide
/// which button a tap hit, from the same rectangle, so what is drawn and what is tapped cannot differ.
/// <para>
/// The buttons are squares, centred as a group in the rectangle and on its vertical middle. A weight scales a button's side
/// (a larger middle button); the gap between two buttons is <see cref="GapShare"/> of the unit side. Sizes follow from the
/// rectangle only, never from a resolution or a scale.
/// </para>
/// </summary>
public static class ButtonRow
{
    /// <summary>The gap between two buttons, as a share of the unit side.</summary>
    public const float GapShare = 0.5f;

    /// <summary>The smallest side of a button, as a share of the rectangle's height, while the group fits the width.</summary>
    public const float MinTouchShare = 0.6f;

    /// <summary>
    /// The rectangles of <paramref name="count"/> buttons in <paramref name="bounds"/>, in order from left to right. A
    /// <paramref name="weights"/> entry scales that button's side (1 when missing, never below 0.1). The group is as large as
    /// the height and width allow, so no button is taller than the rectangle and none overlaps another; a rectangle too
    /// narrow for the minimum touch size shrinks the group to fit. An empty or inverted rectangle or a count of zero gives none.
    /// </summary>
    public static SKRect[] Layout(SKRect bounds, int count, ReadOnlySpan<float> weights = default)
    {
        if (count <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return [];

        float[] scale = new float[count];
        float sum = 0;
        float largest = 0;
        for (int i = 0; i < count; i++)
        {
            scale[i] = i < weights.Length ? Math.Max(weights[i], 0.1f) : 1f;
            sum += scale[i];
            largest = Math.Max(largest, scale[i]);
        }

        // The unit side: the largest button fills the height, unless the group is wider than the rectangle.
        float unit = Math.Min(bounds.Height / largest, bounds.Width / (sum + GapShare * (count - 1)));
        float[] sides = new float[count];
        float minTouch = MinTouchShare * bounds.Height;
        float total = GapShare * unit * (count - 1);
        for (int i = 0; i < count; i++)
        {
            sides[i] = Math.Min(Math.Max(unit * scale[i], minTouch), bounds.Height);
            total += sides[i];
        }

        // Raising the small buttons to the minimum can make the group too wide: then everything shrinks together.
        float gap = GapShare * unit;
        if (total > bounds.Width)
        {
            float shrink = bounds.Width / total;
            for (int i = 0; i < count; i++)
                sides[i] *= shrink;
            gap *= shrink;
            total = bounds.Width;
        }

        var rects = new SKRect[count];
        float x = bounds.MidX - total / 2;
        for (int i = 0; i < count; i++)
        {
            rects[i] = SKRect.Create(x, bounds.MidY - sides[i] / 2, sides[i], sides[i]);
            x += sides[i] + gap;
        }

        return rects;
    }

    /// <summary>
    /// The index of the button that <paramref name="point"/> hits, or -1. A button's rectangle is grown by half the gap on every
    /// side, so a finger landing in the gap hits the nearer button and never two; a point beyond the group hits none.
    /// </summary>
    public static int HitTest(SKRect bounds, int count, SKPoint point, ReadOnlySpan<float> weights = default)
    {
        SKRect[] rects = Layout(bounds, count, weights);
        if (rects.Length == 0)
            return -1;

        float halfGap = rects.Length > 1 ? (rects[1].Left - rects[0].Right) / 2 : 0;
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < rects.Length; i++)
        {
            var grown = new SKRect(rects[i].Left - halfGap, rects[i].Top - halfGap, rects[i].Right + halfGap, rects[i].Bottom + halfGap);
            if (point.X < grown.Left || point.X > grown.Right || point.Y < grown.Top || point.Y > grown.Bottom)
                continue;

            float distance = Math.Abs(point.X - rects[i].MidX);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }
}
