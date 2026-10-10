// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Engine.Tests.Support;

/// <summary>Pictures made on the spot, so the repository holds no binary fixtures.</summary>
internal static class TestImages
{
    public static readonly SKColor Ink = new(0x20, 0xA0, 0x60);

    /// <summary>A PNG of one colour.</summary>
    public static byte[] Png(int width, int height, SKColor? color = null)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(color ?? Ink);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A PNG of noise: it does not compress, so half of its bytes hold about half of its rows.</summary>
    public static byte[] NoisePng(int width, int height)
    {
        var random = new Random(7);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bitmap.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>An ICO file holding one PNG picture, which is how modern icons are stored.</summary>
    public static byte[] Ico(int size)
    {
        byte[] png = Png(size, size);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // an icon
        writer.Write((ushort)1); // one picture
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0); // no palette
        writer.Write((byte)0);
        writer.Write((ushort)1); // planes
        writer.Write((ushort)32); // bits per pixel
        writer.Write((uint)png.Length);
        writer.Write((uint)22); // the picture follows the 6 byte header and the 16 byte entry
        writer.Write(png);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Waits, in real time, until the condition holds; false when it does not within the limit.</summary>
    public static bool WaitFor(Func<bool> condition, int timeoutMs = 10000)
    {
        long until = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > until)
                return false;
            Thread.Sleep(5);
        }

        return true;
    }
}
