// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using Xunit;

namespace UrDeck.Engine.Tests.Support;

/// <summary>
/// Compares a rendered picture with a PNG kept in the repository (<c>tests/UrDeck.Engine.Tests/Goldens</c>). A picture passes
/// when no more than <see cref="PixelFraction"/> of its pixels have a channel more than <see cref="ChannelLimit"/> away from the
/// golden. On a mismatch the actual picture and a difference picture are written to <c>artifacts/golden-failures/</c>.
/// <c>URDECK_UPDATE_GOLDENS=1</c> rewrites the golden instead (see CONTRIBUTING.md, "Golden images").
/// </summary>
internal static class GoldenImage
{
    public const string UpdateVariable = "URDECK_UPDATE_GOLDENS";

    /// <summary>The most a colour channel may differ before a pixel counts as different.</summary>
    public const int ChannelLimit = 2;

    /// <summary>The share of pixels that may differ by more than <see cref="ChannelLimit"/>.</summary>
    public const double PixelFraction = 0.0001;

    /// <summary>Where the goldens are read from: the copy next to the test assembly.</summary>
    public static string GoldenFolder => Path.Combine(AppContext.BaseDirectory, "Goldens");

    /// <summary>Where an update writes: the folder in the sources, so the change shows in git.</summary>
    public static string SourceGoldenFolder => Path.Combine(RepositoryRoot(), "tests", "UrDeck.Engine.Tests", "Goldens");

    public static string FailureFolder => Path.Combine(RepositoryRoot(), "artifacts", "golden-failures");

    public static bool UpdateRequested => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    /// <summary>
    /// Passes when <paramref name="actual"/> matches the golden <paramref name="name"/> within the limits. The folders and the
    /// update switch can be given for the comparer's own tests; they default to the repository's.
    /// </summary>
    public static void Compare(
        SKBitmap actual,
        string name,
        int channelLimit = ChannelLimit,
        double pixelFraction = PixelFraction,
        string? goldenFolder = null,
        string? updateFolder = null,
        string? failureFolder = null,
        bool? update = null)
    {
        if (update ?? UpdateRequested)
        {
            string target = Path.Combine(updateFolder ?? SourceGoldenFolder, name + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Save(actual, target);
            return;
        }

        string path = Path.Combine(goldenFolder ?? GoldenFolder, name + ".png");
        if (!File.Exists(path))
            Assert.Fail($"No golden image '{name}' at {path}. Set {UpdateVariable}=1 and run the test again to create it, then look at it.");

        using var golden = SKBitmap.Decode(path);
        string failures = failureFolder ?? FailureFolder;
        if (golden.Width != actual.Width || golden.Height != actual.Height)
        {
            WriteEvidence(actual, null, name, failures, channelLimit);
            Assert.Fail($"Golden '{name}' is {golden.Width}x{golden.Height}, the picture is {actual.Width}x{actual.Height}. Actual picture: {Path.Combine(failures, name + ".actual.png")}");
        }

        var (maxDifference, differing) = Measure(golden, actual, channelLimit);
        long allowed = (long)Math.Floor(actual.Width * (double)actual.Height * pixelFraction);
        if (differing <= allowed)
            return;

        WriteEvidence(actual, golden, name, failures, channelLimit);
        Assert.Fail(
            $"Golden '{name}' differs: {differing} pixels are more than {channelLimit} off in a channel (allowed {allowed}), the largest channel difference is {maxDifference}.{Environment.NewLine}" +
            $"Actual: {Path.Combine(failures, name + ".actual.png")}{Environment.NewLine}Difference: {Path.Combine(failures, name + ".diff.png")}{Environment.NewLine}" +
            $"If the change is intended, run with {UpdateVariable}=1 and say why in the pull request.");
    }

    /// <summary>The largest channel difference of two pictures of one size, and how many pixels exceed <paramref name="channelLimit"/>.</summary>
    public static (int MaxDifference, long Differing) Measure(SKBitmap expected, SKBitmap actual, int channelLimit)
    {
        var a = expected.Pixels;
        var b = actual.Pixels;
        int max = 0;
        long differing = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int d = Math.Max(Math.Max(Math.Abs(a[i].Red - b[i].Red), Math.Abs(a[i].Green - b[i].Green)),
                Math.Max(Math.Abs(a[i].Blue - b[i].Blue), Math.Abs(a[i].Alpha - b[i].Alpha)));
            if (d > max)
                max = d;
            if (d > channelLimit)
                differing++;
        }

        return (max, differing);
    }

    private static void WriteEvidence(SKBitmap actual, SKBitmap? golden, string name, string folder, int channelLimit)
    {
        Directory.CreateDirectory(folder);
        Save(actual, Path.Combine(folder, name + ".actual.png"));
        if (golden == null)
            return;

        // The differing pixels in red over a faded golden.
        using var diff = new SKBitmap(actual.Width, actual.Height);
        var a = golden.Pixels;
        var b = actual.Pixels;
        var output = new SKColor[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            int d = Math.Max(Math.Max(Math.Abs(a[i].Red - b[i].Red), Math.Abs(a[i].Green - b[i].Green)),
                Math.Max(Math.Abs(a[i].Blue - b[i].Blue), Math.Abs(a[i].Alpha - b[i].Alpha)));
            output[i] = d > channelLimit
                ? SKColors.Red
                : new SKColor((byte)(255 - (255 - a[i].Red) / 4), (byte)(255 - (255 - a[i].Green) / 4), (byte)(255 - (255 - a[i].Blue) / 4));
        }

        diff.Pixels = output;
        Save(diff, Path.Combine(folder, name + ".diff.png"));
    }

    private static void Save(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "urdeck.slnx")))
                return dir.FullName;
        }

        throw new InvalidOperationException("urdeck.slnx was not found above " + AppContext.BaseDirectory);
    }
}
