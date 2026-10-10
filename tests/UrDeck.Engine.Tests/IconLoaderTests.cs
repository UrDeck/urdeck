// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.Versioning;
using SkiaSharp;
using UrDeck.Engine.Icons;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public class ShellIconPixelsTests
{
    // Blue, green, red, alpha.
    private static byte[] Pixels(params (byte B, byte G, byte R, byte A)[] pixels) =>
        pixels.SelectMany(p => new[] { p.B, p.G, p.R, p.A }).ToArray();

    [Fact]
    public void BottomUpRows_AreTurnedTheRightWayUp()
    {
        // Two rows of one pixel: the first row in memory is the bottom one.
        byte[] bgra = Pixels((0, 0, 255, 255), (255, 0, 0, 255));

        using var flipped = ShellIconPixels.ToBitmap(bgra, 1, 2, bottomUp: true);
        using var kept = ShellIconPixels.ToBitmap(Pixels((0, 0, 255, 255), (255, 0, 0, 255)), 1, 2, bottomUp: false);

        Assert.Equal(new SKColor(0, 0, 255), flipped.GetPixel(0, 0));
        Assert.Equal(new SKColor(255, 0, 0), flipped.GetPixel(0, 1));
        Assert.Equal(new SKColor(255, 0, 0), kept.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 0, 255), kept.GetPixel(0, 1));
    }

    [Fact]
    public void PixelsWhoseColoursNeverExceedTheirAlpha_ArePremultiplied()
    {
        using var bitmap = ShellIconPixels.ToBitmap(Pixels((0, 0, 0, 0), (64, 64, 64, 128), (255, 255, 255, 255)), 3, 1, bottomUp: false);

        Assert.Equal(SKAlphaType.Premul, bitmap.AlphaType);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
        // Half-transparent mid grey, stored premultiplied: read back as its true colour.
        Assert.Equal(128, bitmap.GetPixel(1, 0).Alpha);
        Assert.InRange(bitmap.GetPixel(1, 0).Red, 126, 129);
    }

    [Fact]
    public void APixelWithAColourAboveItsAlpha_MakesTheImageStraight()
    {
        using var bitmap = ShellIconPixels.ToBitmap(Pixels((0, 0, 0, 0), (200, 200, 200, 128)), 2, 1, bottomUp: false);

        Assert.Equal(SKAlphaType.Unpremul, bitmap.AlphaType);
        Assert.Equal(new SKColor(200, 200, 200, 128), bitmap.GetPixel(1, 0));
    }

    [Fact]
    public void AnImageWithNoAlphaAtAll_IsOpaque()
    {
        using var bitmap = ShellIconPixels.ToBitmap(Pixels((10, 20, 30, 0), (0, 0, 0, 0)), 2, 1, bottomUp: false);

        Assert.Equal(new SKColor(30, 20, 10, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 0, 0, 255), bitmap.GetPixel(1, 0));
    }
}

[SupportedOSPlatform("windows")]
public class ShellIconLoaderTests
{
    private static string WindowsFile(string name) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), name);

    private static int CountPixels(SKBitmap bitmap, Func<SKColor, bool> match)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (match(bitmap.GetPixel(x, y)))
                    count++;
        return count;
    }

    [Fact]
    public void AnExecutableInTheWindowsFolder_YieldsAnIconWithTransparentPixels()
    {
        using var loader = new ShellIconLoader();

        using var icon = loader.Load(WindowsFile("explorer.exe"));

        Assert.NotNull(icon);
        Assert.Equal(ShellIconLoader.Size, icon!.Width);
        Assert.Equal(ShellIconLoader.Size, icon.Height);
        Assert.True(CountPixels(icon, c => c.Alpha == 0) > 0, "the icon has no transparent pixels");
        Assert.True(CountPixels(icon, c => c.Alpha == 255) > 0, "the icon has no opaque pixels");
    }

    [Fact]
    public void AFolder_HasAnIcon_AndAPathThatDoesNotExistHasNone()
    {
        using var loader = new ShellIconLoader();

        using var folder = loader.Load(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        using var missing = loader.Load(@"C:\urdeck-no-such-folder\nothing.exe");

        Assert.NotNull(folder);
        Assert.Null(missing);
    }

    [Fact]
    public void TheWorkerThread_StartsWithTheFirstIcon_AndNotBefore()
    {
        using var loader = new ShellIconLoader();
        Assert.False(loader.IsWorkerRunning);

        loader.Load(WindowsFile("explorer.exe"))?.Dispose();

        Assert.True(loader.IsWorkerRunning);
    }
}

public class StaWorkerTests
{
    [Fact]
    public void TheThread_IsCreatedByTheFirstWork_RunsInASingleThreadedApartment_AndEndsWhenIdle()
    {
        using var worker = new StaWorker(TimeSpan.FromMilliseconds(50));
        Assert.False(worker.IsRunning);

        ApartmentState? apartment = null;
        int? first = null;
        int? second = null;
        using var done = new ManualResetEventSlim();
        worker.Post(() =>
        {
            apartment = Thread.CurrentThread.GetApartmentState();
            first = Environment.CurrentManagedThreadId;
        });
        worker.Post(() =>
        {
            second = Environment.CurrentManagedThreadId;
            done.Set();
        });

        Assert.True(done.Wait(5000));
        Assert.Equal(ApartmentState.STA, apartment);
        Assert.Equal(first, second);
        Assert.True(TestImages.WaitFor(() => !worker.IsRunning), "the worker thread did not end after it went idle");
    }

    [Fact]
    public void WorkAfterTheThreadEnded_StartsANewOne_AndAFailingWorkDoesNotStopTheWorker()
    {
        using var worker = new StaWorker(TimeSpan.FromMilliseconds(30));
        using var done = new ManualResetEventSlim();
        worker.Post(() => throw new InvalidOperationException("one bad icon"));
        Assert.True(TestImages.WaitFor(() => !worker.IsRunning));

        Assert.True(worker.Post(done.Set));

        Assert.True(done.Wait(5000));
    }

    [Fact]
    public void ADisposedWorker_TakesNoWork()
    {
        var worker = new StaWorker(TimeSpan.FromSeconds(30));
        using var done = new ManualResetEventSlim();
        worker.Post(done.Set);
        Assert.True(done.Wait(5000));

        worker.Dispose();

        Assert.False(worker.Post(() => { }));
        Assert.True(TestImages.WaitFor(() => !worker.IsRunning), "disposing did not end the idle thread");
    }
}

public sealed class ImageFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-image-tests-" + Guid.NewGuid().ToString("N"));

    public ImageFilesTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string name, byte[] bytes)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("A.PNG", true)]
    [InlineData("a.jpg", true)]
    [InlineData("a.jpeg", true)]
    [InlineData("a.webp", true)]
    [InlineData("a.bmp", true)]
    [InlineData("a.gif", true)]
    [InlineData("a.ico", true)]
    [InlineData("a.svg", false)]
    [InlineData("a.exe", false)]
    [InlineData("a.png.lnk", false)]
    [InlineData("png", false)]
    public void AFileIsAPicture_ByItsExtension(string name, bool expected) =>
        Assert.Equal(expected, ImageFiles.IsImage(Path.Combine(@"C:\icons", name)));

    [Fact]
    public void APngFile_IsDecoded()
    {
        using var bitmap = ImageFiles.Load(Write("icon.png", TestImages.Png(96, 64)));

        Assert.NotNull(bitmap);
        Assert.Equal((96, 64), (bitmap!.Width, bitmap.Height));
        Assert.Equal(TestImages.Ink, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void AnIcoFile_IsDecoded()
    {
        using var bitmap = ImageFiles.Load(Write("icon.ico", TestImages.Ico(64)));

        Assert.NotNull(bitmap);
        Assert.Equal((64, 64), (bitmap!.Width, bitmap.Height));
        Assert.Equal(TestImages.Ink, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void ATextFileRenamedToPng_IsNotAPicture() =>
        Assert.Null(ImageFiles.Load(Write("notes.png", "This is not a picture."u8.ToArray())));

    [Fact]
    public void AMissingFile_IsNotAPicture() =>
        Assert.Null(ImageFiles.Load(Path.Combine(_root, "nowhere.png")));

    [Fact]
    public void ACutOffPicture_IsNotAPicture()
    {
        byte[] whole = TestImages.NoisePng(200, 200);
        byte[] half = whole[..(whole.Length / 2)];

        Assert.NotNull(ImageFiles.Decode(whole));
        Assert.Null(ImageFiles.Decode(half));
        Assert.Null(ImageFiles.Decode([]));
    }

    [Fact]
    public void Fit_KeepsASmallPicture_AndShrinksALargeOneToTheLimit()
    {
        var small = ImageFiles.Decode(TestImages.Png(100, 50))!;
        var large = ImageFiles.Decode(TestImages.Png(1000, 500))!;

        using var keptSmall = IconImages.Fit(small, 256);
        using var fitted = IconImages.Fit(large, 256);

        Assert.Same(small, keptSmall);
        Assert.Equal((256, 128), (fitted.Width, fitted.Height));
        Assert.Equal(TestImages.Ink, fitted.GetPixel(128, 64));
    }
}
