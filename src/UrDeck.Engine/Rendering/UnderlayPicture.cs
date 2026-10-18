// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// One card's outside (<see cref="CardUnderlay"/>) drawn once as a PNG, for the host to stretch under its cards as a
/// compositor sprite. A nine-grid picture is drawn round a card of twice the corner zone and stretched round any card at
/// least that big; an exact one is drawn round a card of one size. A picture of blurred pieces only is drawn at half size in
/// each direction and stretched back. Made and kept by <see cref="ResolvedCardStyle.Underlay"/>, released with the style.
/// </summary>
public sealed class UnderlayPicture : IDisposable
{
    private UnderlayPicture(byte[] png, int width, int height, bool nineGrid, float inset, float scale, int bleed, System.Drawing.Size card, TimeSpan buildTime)
    {
        Png = png;
        PixelWidth = width;
        PixelHeight = height;
        IsNineGrid = nineGrid;
        Inset = inset;
        Scale = scale;
        Bleed = bleed;
        Card = card;
        BuildTime = buildTime;
    }

    /// <summary>
    /// The picture, encoded: the host decodes it into a <c>LoadedImageSurface</c>. Kept small rather than as pixels, since the
    /// decoded surface is what draws (docs/perf/card-chrome.md, "As built (card-underlay)").
    /// </summary>
    public byte[] Png { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    /// <summary>True for the picture every card at least twice the corner zone shares, stretched by its <see cref="Inset"/>.</summary>
    public bool IsNineGrid { get; }

    /// <summary>A nine-grid picture's inset on every side, in the picture's pixels: the bleed plus the corner zone.</summary>
    public float Inset { get; }

    /// <summary>The picture's pixels per physical pixel: 1, or 0.5 for a picture of blurred pieces only.</summary>
    public float Scale { get; }

    /// <summary>How many physical pixels the picture reaches past the card on every side.</summary>
    public int Bleed { get; }

    /// <summary>The card the picture was drawn round, in physical pixels.</summary>
    public System.Drawing.Size Card { get; }

    /// <summary>How long the drawing and the encoding took.</summary>
    public TimeSpan BuildTime { get; }

    /// <summary>The host's decoded copy of <see cref="Png"/>, shared by every page and released with the picture.</summary>
    public IDisposable? HostSurface { get; set; }

    internal static UnderlayPicture MakeNineGrid(ResolvedCardStyle style)
    {
        int side = 2 * style.CornerZone + 2;
        return Make(style, new System.Drawing.Size(side, side), nineGrid: true);
    }

    internal static UnderlayPicture MakeExact(ResolvedCardStyle style, System.Drawing.Size card) => Make(style, card, nineGrid: false);

    private static UnderlayPicture Make(ResolvedCardStyle style, System.Drawing.Size card, bool nineGrid)
    {
        long started = Stopwatch.GetTimestamp();
        int bleed = style.Bleed;
        int width = card.Width + 2 * bleed, height = card.Height + 2 * bleed;
        float scale = style.UnderlayIsSoft ? 0.5f : 1f;
        // A nine-grid picture's sides are even, so half of it is exact; an exact picture rounds up and stretches to fit.
        int pixelWidth = (int)MathF.Ceiling(width * scale), pixelHeight = (int)MathF.Ceiling(height * scale);

        using var surface = SKSurface.Create(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale((float)pixelWidth / width, (float)pixelHeight / height);
        canvas.Translate(bleed, bleed);
        using (var shape = CardShape.For(style, card))
            CardUnderlay.Draw(canvas, style, shape);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new UnderlayPicture(data.ToArray(), pixelWidth, pixelHeight, nineGrid, (bleed + style.CornerZone) * scale, scale, bleed, card,
            Stopwatch.GetElapsedTime(started));
    }

    /// <summary>The picture decoded (tests).</summary>
    internal SKBitmap ToBitmap() => SKBitmap.Decode(Png);

    public void Dispose()
    {
        HostSurface?.Dispose();
        HostSurface = null;
    }
}
