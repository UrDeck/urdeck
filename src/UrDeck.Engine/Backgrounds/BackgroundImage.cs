// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Icons;

namespace UrDeck.Engine.Backgrounds;

/// <summary>An image background's file: where it is, and the picture scaled once to cover the panel.</summary>
public static class BackgroundImage
{
    /// <summary>Pictures above this many pixels are refused (a 8192x8192 file decodes to 256 MB).</summary>
    private const long MaxPixels = 8192L * 8192L;

    /// <summary>
    /// The full path of <paramref name="path"/>: environment variables expanded, then absolute as it is or relative to
    /// <paramref name="appDirectory"/>.
    /// </summary>
    public static string ResolvePath(string path, string appDirectory)
    {
        string expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        return Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(appDirectory, expanded));
    }

    /// <summary>
    /// The part of an <paramref name="image"/>-sized picture that, scaled to <paramref name="target"/>, covers it: centred,
    /// keeping the proportions, the overflow cropped.
    /// </summary>
    public static SKRect CoverRect(SKSizeI image, SKSizeI target)
    {
        if (image.Width <= 0 || image.Height <= 0 || target.Width <= 0 || target.Height <= 0)
            return SKRect.Empty;

        double scale = Math.Max((double)target.Width / image.Width, (double)target.Height / image.Height);
        float width = (float)(target.Width / scale);
        float height = (float)(target.Height / scale);
        return SKRect.Create((image.Width - width) / 2f, (image.Height - height) / 2f, width, height);
    }

    /// <summary>
    /// Decodes the file and scales it once to cover <paramref name="width"/> x <paramref name="height"/>; the decoded
    /// original is released. Null, with a warning that names the path, when the file is missing or not a usable picture.
    /// </summary>
    public static SKBitmap? Prepare(string fullPath, int width, int height)
    {
        using var original = ImageFiles.Load(fullPath, MaxPixels, applyOrientation: true);
        if (original == null)
        {
            UrDeckLog.Warn(File.Exists(fullPath)
                ? $"Background: {fullPath} is not a picture that can be shown (PNG, JPEG, BMP, WebP or GIF, up to 8192x8192)."
                : $"Background: the picture {fullPath} does not exist.");
            return null;
        }

        return Cover(original, width, height);
    }

    /// <summary><paramref name="source"/> scaled to cover a surface of the given size.</summary>
    public static SKBitmap Cover(SKBitmap source, int width, int height)
    {
        var rect = CoverRect(new SKSizeI(source.Width, source.Height), new SKSizeI(width, height));
        var result = new SKBitmap(new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        using var image = SKImage.FromBitmap(source);

        // Shrinking wants mipmaps; enlarging a smoothly cubic filter.
        bool shrinking = rect.Width > width;
        var sampling = shrinking
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : new SKSamplingOptions(SKCubicResampler.Mitchell);
        canvas.DrawImage(image, rect, SKRect.Create(width, height), sampling);
        return result;
    }
}
