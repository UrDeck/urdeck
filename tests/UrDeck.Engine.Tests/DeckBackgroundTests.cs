// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class DeckBackgroundTests : IDisposable
{
    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static string Shader(string name, string extra = "") =>
        $$"""{ "type": "shader", "name": "{{name}}"{{(extra.Length == 0 ? "" : ", " + extra)}} }""";

    private string Key(string json, string theme = "default-dark") =>
        DeckBackground.RequestKey(BackgroundFixture.Parse(json), _fixture.Theme(theme), _fixture.Store, _fixture.Root)!;

    private SKBitmap Snapshot(DeckBackground? background, string theme = "default-dark") =>
        PageRenderer.RenderToBitmap(110, 384, new List<(WidgetConfig, IWidget)>(), _fixture.Theme(theme), new DateTime(2026, 10, 11),
            background: background);

    private string WritePicture(string name, byte[] png)
    {
        string path = Path.Combine(_fixture.Root, name);
        File.WriteAllBytes(path, png);
        return path;
    }

    [Fact]
    public void Equal_settings_have_equal_keys()
    {
        Assert.Equal(Key(Shader("plasma", "\"angle\": 10")), Key(Shader("plasma", "\"angle\": 10")));
        Assert.Equal(Key(Shader("plasma")), _fixture.Create(Shader("plasma"))!.Key);
    }

    [Theory]
    [InlineData("\"angle\": 10", "\"angle\": 20")]
    [InlineData("\"speed\": 1", "\"speed\": 2")]
    [InlineData("\"scale\": 0.5", "\"scale\": 0.25")]
    [InlineData("\"colors\": [\"#000001\"]", "\"colors\": [\"#000002\"]")]
    public void A_changed_setting_changes_the_key(string before, string after)
    {
        Assert.NotEqual(Key(Shader("plasma", before)), Key(Shader("plasma", after)));
    }

    [Fact]
    public void A_changed_theme_or_shader_text_changes_the_key()
    {
        Assert.NotEqual(Key(Shader("plasma"), "default-dark"), Key(Shader("plasma"), "default-light"));

        _fixture.WriteShader("mine", "half4 main(float2 p) { return half4(1); }");
        string before = Key(Shader("mine"));
        _fixture.WriteShader("mine", "half4 main(float2 p) { return half4(0.5); }");
        Assert.NotEqual(before, Key(Shader("mine")));
    }

    [Fact]
    public void A_picture_key_follows_the_file()
    {
        string path = WritePicture("wall.png", TestImages.Png(8, 8)).Replace('\\', '/');
        string json = $$"""{ "type": "image", "path": "{{path}}" }""";
        string before = Key(json);

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-3));

        Assert.NotEqual(before, Key(json));
    }

    [Fact]
    public void A_background_that_is_none_has_no_key_and_is_not_created()
    {
        Assert.Null(DeckBackground.RequestKey(BackgroundSettings.None, _fixture.Theme(), _fixture.Store, _fixture.Root));
        Assert.Null(DeckBackground.Create(BackgroundSettings.None, _fixture.Theme(), _fixture.Store, _fixture.Root));
    }

    [Fact]
    public void Only_a_shader_with_speed_and_iTime_moves()
    {
        _fixture.WriteShader("still", "half4 main(float2 p) { return half4(1); }");

        using var moving = _fixture.Create(Shader("plasma"));
        using var stopped = _fixture.Create(Shader("plasma", "\"speed\": 0"));
        using var timeless = _fixture.Create(Shader("still"));

        Assert.True(moving!.IsMoving);
        Assert.False(stopped!.IsMoving);
        Assert.False(timeless!.IsMoving);
        Assert.Equal(0.5, moving.Scale);
    }

    [Fact]
    public void The_description_names_the_shader_and_where_it_came_from()
    {
        _fixture.WriteShader("mine", "half4 main(float2 p) { return half4(1); }");

        using var builtIn = _fixture.Create(Shader("plasma"));
        using var user = _fixture.Create(Shader("mine"));

        Assert.Equal("shader 'plasma' (built-in)", builtIn!.Describe);
        Assert.StartsWith("shader 'mine' (", user!.Describe);
        Assert.EndsWith("mine.sksl)", user.Describe);
    }

    [Fact]
    public void A_snapshot_with_a_shader_differs_from_one_without_and_repeats()
    {
        using var plain = Snapshot(null);
        using var first = _fixture.Create(Shader("plasma"));
        using var a = Snapshot(first);
        using var second = _fixture.Create(Shader("plasma", "\"speed\": 3"));
        using var b = Snapshot(second);

        Assert.False(BackgroundFixture.Same(plain, a));
        Assert.True(BackgroundFixture.Same(a, b), "a snapshot is at a fixed time whatever the speed");
        using var again = Snapshot(first);
        Assert.True(BackgroundFixture.Same(a, again));
    }

    [Fact]
    public void A_snapshot_uses_the_palette_of_its_theme()
    {
        using var dark = Snapshot(_fixture.Create(Shader("plasma"), "default-dark"), "default-dark");
        using var light = Snapshot(_fixture.Create(Shader("plasma"), "default-light"), "default-light");

        Assert.False(BackgroundFixture.Same(dark, light));
    }

    [Fact]
    public void A_snapshot_without_a_background_is_the_theme_colour()
    {
        using var plain = Snapshot(null);
        SKColor background = _fixture.Theme().Definition.Colors!.Background is { } hex ? SKColor.Parse(hex) : default;

        Assert.True(BackgroundFixture.IsFlat(plain));
        Assert.Equal(background, plain.GetPixel(3, 3));
    }

    [Fact]
    public void A_background_that_failed_leaves_the_page_as_it_was()
    {
        _fixture.WriteShader("broken", "half4 main(float2 p) { return }");
        using var plain = Snapshot(null);
        using var failed = Snapshot(_fixture.Create(Shader("broken")));

        Assert.True(BackgroundFixture.Same(plain, failed));
    }

    [Fact]
    public void A_picture_that_vanishes_before_painting_leaves_the_page_as_it_was()
    {
        string path = WritePicture("gone.png", TestImages.Png(8, 8)).Replace('\\', '/');
        using var background = _fixture.Create($$"""{ "type": "image", "path": "{{path}}" }""");
        File.Delete(path);

        using var plain = Snapshot(null);
        using var shown = Snapshot(background);

        Assert.True(BackgroundFixture.Same(plain, shown));
        Assert.Contains("does not exist", _fixture.Log);
    }

    [Fact]
    public void A_snapshot_shows_the_picture_covering_the_page()
    {
        using var source = new SKBitmap(new SKImageInfo(40, 20, SKColorType.Rgba8888, SKAlphaType.Premul));
        source.Erase(SKColors.Red);
        using (var canvas = new SKCanvas(source))
        using (var blue = new SKPaint { Color = SKColors.Blue })
            canvas.DrawRect(20, 0, 20, 20, blue);
        using var encoded = SKImage.FromBitmap(source).Encode(SKEncodedImageFormat.Png, 100);
        string path = WritePicture("halves.png", encoded.ToArray()).Replace('\\', '/');

        using var background = _fixture.Create($$"""{ "type": "image", "path": "{{path}}" }""");
        Assert.False(background!.IsMoving);
        Assert.Equal(1, background.Scale);
        using var bitmap = Snapshot(background);

        // 40x20 covering 110x384: the middle 11x20 of the picture is shown, red on the left of x=20 and blue on the right.
        Assert.Equal(SKColors.Red, bitmap.GetPixel(2, 2));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(2, 380));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(107, 2));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(107, 380));
    }

    [Fact]
    public void A_missing_picture_fails_with_its_path_in_the_warning()
    {
        string path = Path.Combine(_fixture.Root, "nope.png").Replace('\\', '/');

        Assert.Null(_fixture.Create($$"""{ "type": "image", "path": "{{path}}" }"""));
        Assert.Contains("nope.png", _fixture.Log);
        Assert.Contains("does not exist", _fixture.Log);
    }

    [Fact]
    public async Task A_file_that_is_not_a_picture_fails_when_prepared()
    {
        string path = Path.Combine(_fixture.Root, "text.png");
        File.WriteAllText(path, "not a picture");

        using var background = _fixture.Create($$"""{ "type": "image", "path": "{{path.Replace('\\', '/')}}" }""");

        Assert.NotNull(background);
        Assert.False(await background.PrepareAsync(10, 10));
        Assert.Contains("text.png is not a picture", _fixture.Log);
    }

    [Fact]
    public async Task A_picture_is_scaled_again_for_another_size()
    {
        string path = WritePicture("one.png", TestImages.Png(16, 16, SKColors.Green)).Replace('\\', '/');
        using var background = _fixture.Create($$"""{ "type": "image", "path": "{{path}}" }""");

        Assert.False(background!.IsReadyFor(10, 20));
        Assert.True(await background.PrepareAsync(10, 20));
        Assert.True(background.IsReadyFor(10, 20));
        Assert.False(background.IsReadyFor(20, 40));

        using var bitmap = BackgroundFixture.Paint(background, 20, 40);
        Assert.Equal(SKColors.Green, bitmap.GetPixel(10, 20));
    }
}
