// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Tests.Support;
using Xunit;
using Xunit.Sdk;

namespace UrDeck.Engine.Tests;

/// <summary>The golden-image comparer itself: what it lets through, what it refuses and what it leaves behind.</summary>
public sealed class GoldenImageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-golden-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _goldens;
    private readonly string _failures;

    public GoldenImageTests()
    {
        _goldens = Path.Combine(_root, "goldens");
        _failures = Path.Combine(_root, "failures");
        Directory.CreateDirectory(_goldens);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static SKBitmap Grey(int width, int height, byte value)
    {
        var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(value, value, value));
        return bitmap;
    }

    private void Keep(SKBitmap bitmap, string name)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(_goldens, name + ".png"), data.ToArray());
    }

    private void Compare(SKBitmap actual, string name, double fraction = 0, bool update = false) =>
        GoldenImage.Compare(actual, name, channelLimit: 2, pixelFraction: fraction, goldenFolder: _goldens, updateFolder: _goldens, failureFolder: _failures, update: update);

    [Fact]
    public void AnIdenticalPicture_Passes()
    {
        using var golden = Grey(40, 30, 100);
        Keep(golden, "same");

        Compare(Grey(40, 30, 100), "same");
    }

    [Fact]
    public void AChannelOffByTheLimit_Passes_AndOneMore_Fails()
    {
        using var golden = Grey(40, 30, 100);
        Keep(golden, "limit");

        Compare(Grey(40, 30, 102), "limit");
        Assert.Throws<FailException>(() => Compare(Grey(40, 30, 103), "limit"));
    }

    [Fact]
    public void ASmallAreaChanged_PassesWithinThePixelFraction_AndFailsBeyondIt()
    {
        using var golden = Grey(100, 100, 100);
        Keep(golden, "area");
        using var changed = Grey(100, 100, 100);
        for (int x = 0; x < 10; x++)
            changed.SetPixel(x, 0, SKColors.White);

        // 10 of 10,000 pixels is 0.1 %.
        Compare(changed, "area", fraction: 0.001);
        Assert.Throws<FailException>(() => Compare(changed, "area", fraction: 0.0009));
    }

    [Fact]
    public void ASizeDifference_Fails()
    {
        using var golden = Grey(40, 30, 100);
        Keep(golden, "size");

        var ex = Assert.Throws<FailException>(() => Compare(Grey(41, 30, 100), "size"));

        Assert.Contains("40x30", ex.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_failures, "size.actual.png")));
    }

    [Fact]
    public void AMismatch_WritesTheActualAndTheDifferencePictures_AndNamesThem()
    {
        using var golden = Grey(40, 30, 100);
        Keep(golden, "evidence");
        using var actual = Grey(40, 30, 100);
        actual.SetPixel(5, 5, SKColors.White);
        actual.SetPixel(6, 5, SKColors.White);

        var ex = Assert.Throws<FailException>(() => Compare(actual, "evidence"));

        Assert.True(File.Exists(Path.Combine(_failures, "evidence.actual.png")));
        Assert.True(File.Exists(Path.Combine(_failures, "evidence.diff.png")));
        Assert.Contains("evidence.actual.png", ex.Message, StringComparison.Ordinal);
        Assert.Contains("evidence.diff.png", ex.Message, StringComparison.Ordinal);
        Assert.Contains("2 pixels", ex.Message, StringComparison.Ordinal);
        Assert.Contains("largest channel difference is 155", ex.Message, StringComparison.Ordinal);
        using var diff = SKBitmap.Decode(Path.Combine(_failures, "evidence.diff.png"));
        Assert.Equal(SKColors.Red, diff.GetPixel(5, 5));
        Assert.NotEqual(SKColors.Red, diff.GetPixel(0, 0));
    }

    [Fact]
    public void AMissingGolden_Fails_AndNamesTheUpdateVariable()
    {
        var ex = Assert.Throws<FailException>(() => Compare(Grey(10, 10, 0), "absent"));

        Assert.Contains(GoldenImage.UpdateVariable, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateMode_WritesTheGolden_AndPasses()
    {
        using var actual = Grey(12, 8, 200);

        Compare(actual, "fresh", update: true);

        using var written = SKBitmap.Decode(Path.Combine(_goldens, "fresh.png"));
        Assert.Equal((12, 8), (written.Width, written.Height));
        Assert.Equal(new SKColor(200, 200, 200), written.GetPixel(3, 3));
        Compare(Grey(12, 8, 200), "fresh");
    }
}
