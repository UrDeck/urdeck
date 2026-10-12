// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class BackgroundImageTests : IDisposable
{
    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void A_wider_picture_is_cropped_left_and_right()
    {
        // 3840x2160 on the 1100x3840 panel: scaled to the panel's height, the middle 618.75 pixels of its width are shown.
        var rect = BackgroundImage.CoverRect(new SKSizeI(3840, 2160), new SKSizeI(1100, 3840));

        Assert.Equal(2160, rect.Height, 2);
        Assert.Equal(618.75, rect.Width, 2);
        Assert.Equal((3840 - 618.75) / 2, rect.Left, 2);
        Assert.Equal(0, rect.Top, 2);
    }

    [Fact]
    public void A_taller_picture_is_cropped_top_and_bottom()
    {
        var rect = BackgroundImage.CoverRect(new SKSizeI(1000, 4000), new SKSizeI(100, 100));

        Assert.Equal(1000, rect.Width, 2);
        Assert.Equal(1000, rect.Height, 2);
        Assert.Equal(0, rect.Left, 2);
        Assert.Equal(1500, rect.Top, 2);
    }

    [Fact]
    public void A_picture_of_the_same_proportions_is_shown_whole()
    {
        var rect = BackgroundImage.CoverRect(new SKSizeI(550, 1920), new SKSizeI(1100, 3840));

        Assert.Equal(SKRect.Create(0, 0, 550, 1920), rect);
    }

    [Fact]
    public void An_empty_size_covers_nothing()
    {
        Assert.Equal(SKRect.Empty, BackgroundImage.CoverRect(new SKSizeI(0, 10), new SKSizeI(10, 10)));
        Assert.Equal(SKRect.Empty, BackgroundImage.CoverRect(new SKSizeI(10, 10), new SKSizeI(10, 0)));
    }

    [Fact]
    public void A_relative_path_is_relative_to_the_application_folder()
    {
        string resolved = BackgroundImage.ResolvePath("wall.png", _fixture.Root);

        Assert.Equal(Path.Combine(_fixture.Root, "wall.png"), resolved);
    }

    [Fact]
    public void An_absolute_path_is_kept()
    {
        string absolute = Path.Combine(Path.GetTempPath(), "other", "wall.png");

        Assert.Equal(absolute, BackgroundImage.ResolvePath(absolute, _fixture.Root));
    }

    [Fact]
    public void Environment_variables_are_expanded()
    {
        const string Variable = "URDECK_TEST_PICTURES";
        Environment.SetEnvironmentVariable(Variable, Path.Combine(_fixture.Root, "pictures"));
        try
        {
            Assert.Equal(Path.Combine(_fixture.Root, "pictures", "wall.jpg"), BackgroundImage.ResolvePath($"%{Variable}%/wall.jpg", "C:\\elsewhere"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, null);
        }
    }

    [Fact]
    public void A_picture_is_found_through_a_relative_path()
    {
        File.WriteAllBytes(Path.Combine(_fixture.Root, "wall.png"), TestImages.Png(8, 8));

        using var background = _fixture.Create("""{ "type": "image", "path": "wall.png" }""");

        Assert.NotNull(background);
    }

    [Fact]
    public void A_photos_stored_orientation_is_applied()
    {
        // 20x10: red left, blue right, stored with EXIF orientation 6 (rotate 90 degrees clockwise to show it).
        using var source = new SKBitmap(new SKImageInfo(20, 10, SKColorType.Rgba8888, SKAlphaType.Opaque));
        source.Erase(SKColors.Red);
        using (var canvas = new SKCanvas(source))
        using (var blue = new SKPaint { Color = SKColors.Blue })
            canvas.DrawRect(10, 0, 10, 10, blue);
        using var jpeg = SKImage.FromBitmap(source).Encode(SKEncodedImageFormat.Jpeg, 100);
        string path = Path.Combine(_fixture.Root, "rotated.jpg");
        File.WriteAllBytes(path, WithOrientation(jpeg.ToArray(), 6));

        using var prepared = BackgroundImage.Prepare(path, 10, 20);

        Assert.NotNull(prepared);
        Assert.Equal(10, prepared.Width);
        Assert.Equal(20, prepared.Height);
        SKColor top = prepared.GetPixel(5, 3);
        SKColor bottom = prepared.GetPixel(5, 16);
        Assert.True(top.Red > 200 && top.Blue < 80, $"the top should be red, was {top}");
        Assert.True(bottom.Blue > 200 && bottom.Red < 80, $"the bottom should be blue, was {bottom}");
    }

    [Fact]
    public void A_picture_without_an_orientation_is_not_turned()
    {
        using var source = new SKBitmap(new SKImageInfo(20, 10, SKColorType.Rgba8888, SKAlphaType.Opaque));
        source.Erase(SKColors.Red);
        using var png = SKImage.FromBitmap(source).Encode(SKEncodedImageFormat.Png, 100);
        string path = Path.Combine(_fixture.Root, "plain.png");
        File.WriteAllBytes(path, png.ToArray());

        using var prepared = BackgroundImage.Prepare(path, 20, 10);

        Assert.Equal(20, prepared!.Width);
        Assert.Equal(10, prepared.Height);
    }

    [Fact]
    public void A_small_picture_is_enlarged_to_cover()
    {
        string path = Path.Combine(_fixture.Root, "tiny.png");
        File.WriteAllBytes(path, TestImages.Png(4, 4, SKColors.Purple));

        using var prepared = BackgroundImage.Prepare(path, 100, 200);

        Assert.Equal(100, prepared!.Width);
        Assert.Equal(200, prepared.Height);
        Assert.Equal(SKColors.Purple, prepared.GetPixel(1, 1));
        Assert.Equal(SKColors.Purple, prepared.GetPixel(98, 198));
    }

    /// <summary>Inserts an EXIF block that holds only the orientation tag right after the JPEG's start marker.</summary>
    private static byte[] WithOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22,                         // APP1, length 34
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,             // "Exif\0\0"
            0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // big-endian TIFF header, first IFD at 8
            0x00, 0x01,                                     // one entry
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, // Orientation, SHORT, count 1
            (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,                         // no next IFD
        ];
        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }
}
