// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// A picture the host composes a moving border from, drawn once as a PNG: the conic gradient the compositor turns
/// (<see cref="Kind"/> <see cref="BorderPictureKind.ConicSource"/>), or the ring the gradient is masked to
/// (<see cref="BorderPictureKind.RingMask"/>), stretched round a card as a nine-grid. Made and kept by
/// <see cref="ResolvedCardStyle.BorderConicSource"/> and <see cref="ResolvedCardStyle.BorderRing"/>, released with the style.
/// </summary>
public sealed class BorderPicture : IDisposable
{
    internal const int ConicSide = 512;

    // The ring picture reaches this far past the corner and the border, so the nine-grid's straight runs hold only the
    // border's straight part.
    private const int RingMargin = 1;

    private BorderPicture(BorderPictureKind kind, byte[] png, int width, int height, float inset)
    {
        Kind = kind;
        Png = png;
        PixelWidth = width;
        PixelHeight = height;
        Inset = inset;
    }

    public BorderPictureKind Kind { get; }

    /// <summary>The picture, encoded: the host decodes it into a <c>LoadedImageSurface</c>.</summary>
    public byte[] Png { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    /// <summary>
    /// A ring's nine-grid inset on every side, in the picture's pixels (the corner's reach, the width and a margin); 0 for the
    /// conic source and for a ring drawn at its card's exact size, which is stretched whole.
    /// </summary>
    public float Inset { get; }

    /// <summary>True for a ring the host stretches round any card as a nine-grid; false when it is drawn for one card size.</summary>
    public bool IsNineGrid => Inset > 0;

    /// <summary>The host's decoded copy of <see cref="Png"/>, shared by every page and released with the picture.</summary>
    public IDisposable? HostSurface { get; set; }

    /// <summary>
    /// The conic gradient of <paramref name="style"/>'s border on a square: the same stops and starting angle as the still
    /// border (a sweep from the right, about the middle), so a rotation of 0 is the still border.
    /// </summary>
    internal static BorderPicture MakeConicSource(ResolvedCardStyle style)
    {
        using var bitmap = DrawConicSource(style);
        return new BorderPicture(BorderPictureKind.ConicSource, Encode(bitmap), ConicSide, ConicSide, 0);
    }

    internal static SKBitmap DrawConicSource(ResolvedCardStyle style)
    {
        var bitmap = new SKBitmap(ConicSide, ConicSide, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        using var shader = SKShader.CreateSweepGradient(new SKPoint(ConicSide / 2f, ConicSide / 2f), style.BorderGradient!);
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(SKRect.Create(ConicSide, ConicSide), paint);
        return bitmap;
    }

    /// <summary>The nine-grid inset of a ring whose card corners reach <paramref name="reach"/> pixels.</summary>
    internal static int InsetFor(ResolvedCardStyle style, float reach) => (int)MathF.Ceiling(reach + style.BorderWidth + RingMargin);

    /// <summary>
    /// The border's ring in white, drawn the way a still border is (the shape stroked twice the width under its own clip):
    /// on a card just big enough to hold its corners for a nine-grid, or on the exact card when that is smaller than two insets.
    /// </summary>
    internal static BorderPicture MakeRing(ResolvedCardStyle style, float reach, System.Drawing.Size? exactCard)
    {
        int inset = InsetFor(style, reach);
        var size = exactCard ?? new System.Drawing.Size((2 * inset) + 2, (2 * inset) + 2);
        using var bitmap = DrawRing(style, reach, size);
        return new BorderPicture(BorderPictureKind.RingMask, Encode(bitmap), size.Width, size.Height, exactCard == null ? inset : 0);
    }

    /// <summary>The ring as pixels, for <see cref="MakeRing"/> and the tests.</summary>
    internal static SKBitmap DrawRing(ResolvedCardStyle style, float reach, System.Drawing.Size size)
    {
        var bitmap = new SKBitmap(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var shape = CardShape.ForReach(style, reach, size);
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = style.BorderWidth * 2,
        };
        shape.Clip(canvas);
        shape.Draw(canvas, paint);
        return bitmap;
    }

    /// <summary>The picture decoded (tests).</summary>
    internal SKBitmap ToBitmap() => SKBitmap.Decode(Png);

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public void Dispose()
    {
        HostSurface?.Dispose();
        HostSurface = null;
    }
}

/// <summary>What a <see cref="BorderPicture"/> is for.</summary>
public enum BorderPictureKind
{
    ConicSource,
    RingMask,
}

/// <summary>
/// The paint under a border's ring when it is not the conic picture: one colour, or a linear gradient between two points
/// in the card's physical pixels, placed as the still border places it.
/// </summary>
public sealed record BorderBrushSpec(SKColor[] Colors, SKPoint Start, SKPoint End)
{
    /// <summary>True for a single colour (no gradient); <see cref="Start"/> and <see cref="End"/> are then the origin.</summary>
    public bool IsSolid => Colors.Length == 1;
}
