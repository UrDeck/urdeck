// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Engine.Icons;

/// <summary>Decodes raster images, from a file chosen by its extension or from bytes that came over the network.</summary>
internal static class ImageFiles
{
    /// <summary>A file is treated as a picture by its extension; arbitrary files are never sniffed.</summary>
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".ico"];

    /// <summary>Larger pictures are refused: an icon never needs them and decoding one costs a lot of memory.</summary>
    private const int MaxPixels = 4096 * 4096;

    public static bool IsImage(string path) =>
        Extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The picture in the file, or null when it is missing, unreadable or not an image. A photo's stored orientation is
    /// applied when <paramref name="applyOrientation"/> is set, and <paramref name="maxPixels"/> raises the size limit
    /// (a wallpaper is far larger than an icon).
    /// </summary>
    public static SKBitmap? Load(string path, long maxPixels = MaxPixels, bool applyOrientation = false)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            using var codec = SKCodec.Create(path);
            return codec == null ? null : Decode(codec, maxPixels, applyOrientation);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The picture in the bytes, or null. Only a complete image counts: a cut-off download is not one.</summary>
    public static SKBitmap? Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
            return null;
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        return codec == null ? null : Decode(codec);
    }

    private static SKBitmap? Decode(SKCodec codec, long maxPixels = MaxPixels, bool applyOrientation = false)
    {
        var size = codec.Info;
        if (size.Width <= 0 || size.Height <= 0 || (long)size.Width * size.Height > maxPixels)
            return null;

        var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
        {
            bitmap.Dispose();
            return null;
        }

        return applyOrientation ? Oriented(bitmap, codec.EncodedOrigin) : bitmap;
    }

    /// <summary><paramref name="bitmap"/> upright for its stored <paramref name="origin"/>; the original is disposed when a copy is made.</summary>
    private static SKBitmap Oriented(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        int w = bitmap.Width;
        int h = bitmap.Height;

        // Maps a pixel of the stored picture to its place in the upright one.
        (SKMatrix Matrix, bool Swap)? transform = origin switch
        {
            SKEncodedOrigin.TopRight => (new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1), false),
            SKEncodedOrigin.BottomRight => (new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), false),
            SKEncodedOrigin.BottomLeft => (new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1), false),
            SKEncodedOrigin.LeftTop => (new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1), true),
            SKEncodedOrigin.RightTop => (new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1), true),
            SKEncodedOrigin.RightBottom => (new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), true),
            SKEncodedOrigin.LeftBottom => (new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1), true),
            _ => null,
        };
        if (transform is not { } t)
            return bitmap;

        var upright = new SKBitmap(new SKImageInfo(t.Swap ? h : w, t.Swap ? w : h, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(upright))
        {
            canvas.SetMatrix(t.Matrix);
            using var image = SKImage.FromBitmap(bitmap);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        }

        bitmap.Dispose();
        return upright;
    }
}

internal static class IconImages
{
    /// <summary>
    /// <paramref name="bitmap"/> itself when its longer side is at most <paramref name="maxSide"/>, else a smaller copy
    /// (and the original is disposed). The caller owns the result.
    /// </summary>
    public static SKBitmap Fit(SKBitmap bitmap, int maxSide)
    {
        int longer = Math.Max(bitmap.Width, bitmap.Height);
        if (longer <= maxSide)
            return bitmap;

        float scale = (float)maxSide / longer;
        int width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        int height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

        // Drawn rather than resized in place: the canvas takes straight and premultiplied sources alike.
        var smaller = new SKBitmap(new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(smaller))
        using (var image = SKImage.FromBitmap(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(image, SKRect.Create(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        bitmap.Dispose();
        return smaller;
    }
}
