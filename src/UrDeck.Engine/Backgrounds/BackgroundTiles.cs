// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Backgrounds;

/// <summary>The pictures an editor shows for a background: its own sample, or one still frame drawn here.</summary>
public static class BackgroundTiles
{
    /// <summary>
    /// The file of the background's first still picture sample (lowest <c>order</c>), as the manifest lists it, or null when it
    /// lists none or the file is missing. The bytes are the picture as it is (PNG, JPEG or WebP).
    /// </summary>
    public static byte[]? SamplePicture(BackgroundPackage package)
    {
        var sample = package.Manifest.Samples
            .Where(s => !string.IsNullOrWhiteSpace(s.File) && !string.Equals(s.Type, "video", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Order ?? int.MaxValue)
            .FirstOrDefault();
        if (sample?.File == null)
            return null;

        try
        {
            using var stream = package.Source.Open(sample.File);
            if (stream == null)
                return null;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UrDeckLog.Warn($"Background '{package.Name}': the sample {sample.File} could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// One frame of the background (its own defaults, the theme's palette) at <paramref name="width"/> x <paramref name="height"/>
    /// pixels as a PNG, for a background that has no sample. Null when it cannot be drawn.
    /// </summary>
    public static byte[]? RenderStill(BackgroundPackage package, LoadedTheme theme, int width, int height)
    {
        try
        {
            using var background = DeckBackground.TryCreate(package, BackgroundSettings.ForThemeShader(package.Name), theme, out _, out _);
            if (background == null)
                return null;

            using var bitmap = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColor.Parse(theme.Definition.Colors!.Background));
                background.PaintAtTime(canvas, width, height, BackgroundClock.SnapshotTime);
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            return data.ToArray();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UrDeckLog.Warn($"Background '{package.Name}': a picture of it could not be drawn: {ex.Message}");
            return null;
        }
    }
}
