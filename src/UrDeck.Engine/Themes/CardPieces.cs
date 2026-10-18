// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Engine.Themes;

/// <summary>The edge of a card an accent strip runs along.</summary>
internal enum CardSide
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>The shape of a corner mark: an L along both edges, or one line along the top or bottom edge.</summary>
internal enum CornerMarkStyle
{
    Bracket,
    Tick,
}

/// <summary><c>card.highlight</c> in pixels: a line just inside the top and/or bottom edge, drawn over the content.</summary>
internal sealed record CardHighlight(SKColor? Top, SKColor? Bottom, float Thickness);

/// <summary><c>card.sheen</c>: a radial light; the centre in fractions of the card's width and height, the radius in its width.</summary>
internal sealed record CardSheen(SKColor Color, float X, float Y, float Size);

/// <summary>
/// <c>card.accentStrip</c> in pixels: a line along <see cref="Side"/>, <see cref="Length"/> of the side centred, in the colour
/// of the card's index round <see cref="Colors"/>. <see cref="GlowBlur"/> 0 is no glow; <see cref="GlowColor"/> null is the
/// strip's own colour.
/// </summary>
internal sealed record CardStrip(
    CardSide Side,
    float Thickness,
    float Length,
    SKColor[] Colors,
    bool Fade,
    float GlowBlur,
    SKColor? GlowColor,
    float GlowAlpha)
{
    /// <summary>The slot in <see cref="Colors"/> of a card at <paramref name="index"/>; the first for no index.</summary>
    public int Slot(int index) => index >= 0 ? index % Colors.Length : 0;

    /// <summary>
    /// The strip's rectangle on a card of <paramref name="width"/> by <paramref name="height"/>: against its side, centred
    /// along it.
    /// </summary>
    public SKRect Rect(float width, float height)
    {
        bool across = Side is CardSide.Top or CardSide.Bottom;
        float length = (across ? width : height) * Length;
        return Side switch
        {
            CardSide.Top => SKRect.Create((width - length) / 2, 0, length, Thickness),
            CardSide.Bottom => SKRect.Create((width - length) / 2, height - Thickness, length, Thickness),
            CardSide.Left => SKRect.Create(0, (height - length) / 2, Thickness, length),
            _ => SKRect.Create(width - Thickness, (height - length) / 2, Thickness, length),
        };
    }
}

/// <summary>
/// <c>card.cornerMarks</c> in pixels. <see cref="Inset"/> is from the card's edge to the mark's outer edge, inward when
/// positive.
/// </summary>
internal sealed record CardMarks(CornerMarkStyle Style, CardCorners Corners, float Size, float Thickness, float Inset, SKColor Color)
{
    /// <summary>True when some of a mark lies on the card (the card's surface draws that part).</summary>
    public bool ReachesTheCard => Inset + Thickness > 0;
}
