// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SkiaSharp;

namespace UrDeck.Engine.Icons;

/// <summary>
/// The icon Windows shows for a path, a <c>shell:</c> item or an address, through <c>IShellItemImageFactory</c>: an
/// executable's or a <c>.lnk</c>'s application icon, a packaged application's icon, a folder or document icon, and the
/// default browser's icon for a web address. No network is involved.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class ShellIconLoader : IDisposable
{
    /// <summary>The size asked of the shell; the largest it keeps for most applications.</summary>
    public const int Size = 256;

    private static readonly TimeSpan IdleTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    private readonly StaWorker _worker = new(IdleTime);

    /// <summary>True while the worker thread exists.</summary>
    public bool IsWorkerRunning => _worker.IsRunning;

    /// <summary>The icon, or null when Windows has none for the name. Blocks the caller while the worker extracts it.</summary>
    public SKBitmap? Load(string parsingName)
    {
        var done = new TaskCompletionSource<SKBitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool queued = _worker.Post(() =>
        {
            try
            {
                done.SetResult(Extract(parsingName));
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        });
        if (!queued)
            return null;

        if (done.Task.Wait(Patience))
            return done.Task.Result;

        // The shell is stuck on this item (a sleeping network drive). Give up on it; free the icon if it ever arrives.
        done.Task.ContinueWith(t => t.Result?.Dispose(), TaskContinuationOptions.OnlyOnRanToCompletion);
        return null;
    }

    private static SKBitmap? Extract(string parsingName)
    {
        int hr = NativeMethods.SHCreateItemFromParsingName(parsingName, 0, in NativeMethods.ImageFactoryId, out var factory);
        if (hr < 0 || factory == null)
            return null;

        try
        {
            hr = factory.GetImage(new NativeMethods.SIZE(Size, Size), NativeMethods.IconOnly, out nint bitmap);
            if (hr < 0 || bitmap == 0)
                return null;
            try
            {
                return FromHandle(bitmap);
            }
            finally
            {
                NativeMethods.DeleteObject(bitmap);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    private static SKBitmap? FromHandle(nint bitmap)
    {
        int size = Marshal.SizeOf<NativeMethods.DIBSECTION>();
        int read = NativeMethods.GetObject(bitmap, size, out var section);

        // The shell hands out a 32 bit DIB section: its header says which way up the rows are.
        if (read == size && section.Bitmap.Bits != 0 && section.Bitmap.BitsPixel == 32)
        {
            int width = section.Bitmap.Width;
            int height = section.Bitmap.Height;
            if (width <= 0 || height <= 0)
                return null;
            byte[] pixels = new byte[width * height * 4];
            Marshal.Copy(section.Bitmap.Bits, pixels, 0, pixels.Length);
            return ShellIconPixels.ToBitmap(pixels, width, height, bottomUp: section.Header.Height > 0);
        }

        if (read < Marshal.SizeOf<NativeMethods.BITMAP>() || section.Bitmap.Width <= 0 || section.Bitmap.Height <= 0)
            return null;

        // Anything else (a device-dependent bitmap): let GDI convert it to 32 bit rows from the top.
        var header = new NativeMethods.BITMAPINFOHEADER
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
            Width = section.Bitmap.Width,
            Height = -section.Bitmap.Height,
            Planes = 1,
            BitCount = 32,
        };
        byte[] converted = new byte[section.Bitmap.Width * section.Bitmap.Height * 4];
        nint screen = NativeMethods.GetDC(0);
        try
        {
            int rows = NativeMethods.GetDIBits(screen, bitmap, 0, (uint)section.Bitmap.Height, converted, ref header, 0);
            return rows == 0 ? null : ShellIconPixels.ToBitmap(converted, section.Bitmap.Width, section.Bitmap.Height, bottomUp: false);
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(0, screen);
        }
    }

    public void Dispose() => _worker.Dispose();

    private static class NativeMethods
    {
        /// <summary>SIIGBF_ICONONLY: the icon, never a thumbnail of the file's content.</summary>
        public const int IconOnly = 0x4;

        public static readonly Guid ImageFactoryId = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE(int width, int height)
        {
            public int Width = width;
            public int Height = height;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAP
        {
            public int Type;
            public int Width;
            public int Height;
            public int WidthBytes;
            public ushort Planes;
            public ushort BitsPixel;
            public nint Bits;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DIBSECTION
        {
            public BITMAP Bitmap;
            public BITMAPINFOHEADER Header;
            public uint RedMask;
            public uint GreenMask;
            public uint BlueMask;
            public nint Section;
            public uint Offset;
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, int flags, out nint bitmap);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHCreateItemFromParsingName(
            string path,
            nint bindContext,
            in Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? item);

        [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
        public static extern int GetObject(nint handle, int size, out DIBSECTION section);

        [DllImport("gdi32.dll")]
        public static extern int GetDIBits(nint dc, nint bitmap, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFOHEADER info, uint usage);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(nint handle);

        [DllImport("user32.dll")]
        public static extern nint GetDC(nint window);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(nint window, nint dc);
    }
}

/// <summary>Turns the pixels of a shell icon into a bitmap. Pure, so the two rules it holds are tested on any machine.</summary>
internal static class ShellIconPixels
{
    /// <summary>
    /// <paramref name="bgra"/> holds <paramref name="width"/> x <paramref name="height"/> pixels of four bytes (blue,
    /// green, red, alpha), rows from the bottom when <paramref name="bottomUp"/>. The shell premultiplies most icons but
    /// not all, and says nowhere which: the pixels count as premultiplied unless one has a colour above its alpha, which
    /// premultiplied pixels cannot. An image whose alpha is zero everywhere has no alpha channel and is opaque.
    /// </summary>
    public static SKBitmap ToBitmap(byte[] bgra, int width, int height, bool bottomUp)
    {
        bool anyAlpha = false;
        bool straight = false;
        for (int i = 0; i + 3 < bgra.Length; i += 4)
        {
            byte alpha = bgra[i + 3];
            anyAlpha |= alpha != 0;
            straight |= bgra[i] > alpha || bgra[i + 1] > alpha || bgra[i + 2] > alpha;
        }

        var alphaType = !anyAlpha ? SKAlphaType.Opaque : straight ? SKAlphaType.Unpremul : SKAlphaType.Premul;
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, alphaType));
        nint target = bitmap.GetPixels();
        int stride = width * 4;
        for (int row = 0; row < height; row++)
        {
            int source = (bottomUp ? height - 1 - row : row) * stride;
            if (!anyAlpha)
            {
                for (int x = 3; x < stride; x += 4)
                    bgra[source + x] = 0xFF;
            }

            Marshal.Copy(bgra, source, target + row * bitmap.RowBytes, stride);
        }

        return bitmap;
    }
}
