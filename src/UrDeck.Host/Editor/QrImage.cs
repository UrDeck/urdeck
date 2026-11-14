// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;

namespace UrDeck.Host.Editor;

/// <summary>
/// A sign-in challenge drawn as a QR code on this PC (the text is a credential and goes nowhere else). Dark modules on a light
/// plate whatever the theme, because phone scanners often cannot read an inverted code; the plate has a quiet zone of four modules.
/// </summary>
internal static class QrImage
{
    private const int QuietModules = 4;

    /// <summary>The picture of <paramref name="text"/>, each module <paramref name="scale"/> pixels wide.</summary>
    public static WriteableBitmap Render(string text, int scale)
    {
        bool[,] modules = Modules(text);
        int side = (modules.GetLength(0) + 2 * QuietModules) * scale;
        byte[] pixels = Pixels(modules, scale, QuietModules);
        var bitmap = new WriteableBitmap(side, side);
        using (var stream = bitmap.PixelBuffer.AsStream())
            stream.Write(pixels, 0, pixels.Length);
        bitmap.Invalidate();
        return bitmap;
    }

    /// <summary>The dark modules of the code, without any quiet zone the encoder may have added.</summary>
    internal static bool[,] Modules(string text)
    {
        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix;
        int size = matrix.Count;
        int min = size;
        int max = -1;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (!matrix[y][x])
                    continue;
                min = Math.Min(min, Math.Min(x, y));
                max = Math.Max(max, Math.Max(x, y));
            }
        }

        int side = max - min + 1;
        bool[,] modules = new bool[side, side];
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
                modules[y, x] = matrix[y + min][x + min];
        }

        return modules;
    }

    /// <summary>BGRA pixels of the modules with a quiet zone of <paramref name="quiet"/> modules around them.</summary>
    internal static byte[] Pixels(bool[,] modules, int scale, int quiet)
    {
        int count = modules.GetLength(0);
        int side = (count + 2 * quiet) * scale;
        byte[] pixels = new byte[side * side * 4];
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                int mx = (x / scale) - quiet;
                int my = (y / scale) - quiet;
                bool dark = mx >= 0 && my >= 0 && mx < count && my < count && modules[my, mx];
                byte value = dark ? (byte)0x14 : (byte)0xFF;
                int at = ((y * side) + x) * 4;
                pixels[at] = value;
                pixels[at + 1] = value;
                pixels[at + 2] = value;
                pixels[at + 3] = 0xFF;
            }
        }

        return pixels;
    }
}
