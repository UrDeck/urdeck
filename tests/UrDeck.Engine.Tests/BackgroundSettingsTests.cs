// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Config;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class BackgroundSettingsTests : IDisposable
{
    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Absent_and_null_are_no_background_without_a_warning()
    {
        Assert.Equal(BackgroundKind.None, BackgroundSettings.Parse(null).Kind);
        Assert.Equal(BackgroundKind.None, BackgroundFixture.Parse("null").Kind);
        Assert.Equal("", _fixture.Log);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("\"plasma\"")]
    [InlineData("[1,2]")]
    public void A_value_that_is_not_an_object_warns(string json)
    {
        Assert.Equal(BackgroundKind.None, BackgroundFixture.Parse(json).Kind);
        Assert.Contains("Background: 'background' must be an object", _fixture.Log);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"type\": 3 }")]
    [InlineData("{ \"type\": \"video\", \"path\": \"a.mp4\" }")]
    public void A_missing_or_unknown_type_warns(string json)
    {
        Assert.Equal(BackgroundKind.None, BackgroundFixture.Parse(json).Kind);
        Assert.Contains("Background: 'type'", _fixture.Log);
    }

    [Fact]
    public void A_shader_needs_a_name()
    {
        Assert.Equal(BackgroundKind.None, BackgroundFixture.Parse("{ \"type\": \"shader\" }").Kind);
        Assert.Contains("Background: a shader background needs a 'name'", _fixture.Log);
    }

    [Theory]
    [InlineData("../themes/x")]
    [InlineData("a/b")]
    [InlineData("a\\\\b")]
    [InlineData("a b")]
    [InlineData("name.sksl")]
    public void A_name_outside_letters_digits_dash_and_underscore_is_refused(string name)
    {
        string json = JsonSerializer.Serialize(new { type = "shader", name });
        Assert.Equal(BackgroundKind.None, BackgroundFixture.Parse(json).Kind);
        Assert.Contains("Background: shader name", _fixture.Log);
    }

    [Fact]
    public void A_shader_gets_its_defaults()
    {
        var settings = BackgroundFixture.Parse("{ \"type\": \"shader\", \"name\": \"plasma\" }");

        Assert.Equal(BackgroundKind.Shader, settings.Kind);
        Assert.Equal("plasma", settings.Name);
        Assert.Equal(1, settings.Speed);
        Assert.Equal(0.5, settings.Scale);
        Assert.Null(settings.Colors);
        Assert.Empty(settings.Parameters);
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void Speed_and_scale_are_read()
    {
        var settings = BackgroundFixture.Parse("{ \"type\": \"shader\", \"name\": \"a\", \"speed\": 2.5, \"scale\": 0.25 }");

        Assert.Equal(2.5, settings.Speed);
        Assert.Equal(0.25, settings.Scale);
        Assert.Equal("", _fixture.Log);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("11")]
    [InlineData("\"fast\"")]
    public void A_speed_out_of_range_uses_1(string speed)
    {
        var settings = BackgroundFixture.Parse($"{{ \"type\": \"shader\", \"name\": \"a\", \"speed\": {speed} }}");

        Assert.Equal(1, settings.Speed);
        Assert.Contains("Background: 'speed'", _fixture.Log);
    }

    [Theory]
    [InlineData("0.05")]
    [InlineData("1.5")]
    [InlineData("0")]
    public void A_scale_out_of_range_uses_one_half(string scale)
    {
        var settings = BackgroundFixture.Parse($"{{ \"type\": \"shader\", \"name\": \"a\", \"scale\": {scale} }}");

        Assert.Equal(0.5, settings.Scale);
        Assert.Contains("Background: 'scale'", _fixture.Log);
    }

    [Fact]
    public void Speed_zero_is_valid()
    {
        Assert.Equal(0, BackgroundFixture.Parse("{ \"type\": \"shader\", \"name\": \"a\", \"speed\": 0 }").Speed);
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void Colours_are_kept_as_written()
    {
        var settings = BackgroundFixture.Parse("{ \"type\": \"shader\", \"name\": \"a\", \"colors\": [\"#003300\", \"#ff00aa55\"] }");

        Assert.Equal(["#003300", "#ff00aa55"], settings.Colors);
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void More_than_four_colours_use_the_first_four()
    {
        var settings = BackgroundFixture.Parse(
            "{ \"type\": \"shader\", \"name\": \"a\", \"colors\": [\"#000001\", \"#000002\", \"#000003\", \"#000004\", \"#000005\"] }");

        Assert.Equal(["#000001", "#000002", "#000003", "#000004"], settings.Colors);
        Assert.Contains("only the first 4", _fixture.Log);
    }

    [Theory]
    [InlineData("[\"#003300\", \"green\"]")]
    [InlineData("\"#003300\"")]
    [InlineData("[3]")]
    [InlineData("[]")]
    public void Invalid_colours_are_dropped_as_a_whole(string colors)
    {
        var settings = BackgroundFixture.Parse($"{{ \"type\": \"shader\", \"name\": \"a\", \"colors\": {colors} }}");

        Assert.Null(settings.Colors);
        Assert.Contains("Background: 'colors'", _fixture.Log);
    }

    [Fact]
    public void Other_keys_are_parameters()
    {
        var settings = BackgroundFixture.Parse("{ \"type\": \"shader\", \"name\": \"a\", \"angle\": 75, \"glow\": \"#ff8800\" }");

        Assert.Equal(["angle", "glow"], settings.Parameters.Keys.Order());
        Assert.Equal(75, settings.Parameters["angle"].GetDouble());
    }

    [Fact]
    public void An_image_needs_a_path()
    {
        Assert.Equal(BackgroundKind.None, BackgroundFixture.Parse("{ \"type\": \"image\" }").Kind);
        Assert.Contains("Background: an image background needs a 'path'", _fixture.Log);
    }

    [Fact]
    public void An_image_is_read()
    {
        var settings = BackgroundFixture.Parse("{ \"type\": \"image\", \"path\": \"D:/Pictures/wall.jpg\" }");

        Assert.Equal(BackgroundKind.Image, settings.Kind);
        Assert.Equal("D:/Pictures/wall.jpg", settings.Path);
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void Extra_keys_on_an_image_warn()
    {
        var settings = BackgroundFixture.Parse("{ \"type\": \"image\", \"path\": \"a.png\", \"speed\": 2 }");

        Assert.Equal(BackgroundKind.Image, settings.Kind);
        Assert.Contains("Background: 'speed' has no meaning for an image", _fixture.Log);
    }

    [Fact]
    public void A_page_with_its_own_background_logs_one_line()
    {
        var config = new UrDeckConfig();
        using (var document = JsonDocument.Parse("{ \"type\": \"shader\", \"name\": \"plasma\" }"))
            config.Pages[0].Background = document.RootElement.Clone();

        BackgroundSettings.NotePageBackgrounds(config);

        Assert.Contains("a page's own 'background' is not supported", _fixture.Log);
    }

    [Fact]
    public void Pages_without_a_background_log_nothing()
    {
        BackgroundSettings.NotePageBackgrounds(new UrDeckConfig());

        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void The_background_survives_a_save_and_a_load()
    {
        string path = Path.Combine(_fixture.Root, "config.json");
        File.WriteAllText(path, "{ \"background\": { \"type\": \"shader\", \"name\": \"plasma\", \"angle\": 30 } }");

        using (var store = new ConfigStore(path))
        {
            Assert.Equal(BackgroundKind.Shader, BackgroundSettings.Parse(store.Config.Background).Kind);
            store.Save();
        }

        string saved = File.ReadAllText(path);
        Assert.Contains("\"background\"", saved);
        Assert.Contains("\"angle\": 30", saved);
    }

    [Fact]
    public void A_configuration_without_a_background_is_written_without_one()
    {
        string path = Path.Combine(_fixture.Root, "config.json");
        File.WriteAllText(path, "{ \"theme\": \"glass\" }");

        using (var store = new ConfigStore(path))
        {
            Assert.Null(store.Config.Background);
            store.Save();
        }

        Assert.DoesNotContain("background", File.ReadAllText(path));
    }

    [Fact]
    public void A_page_background_is_noted_when_the_file_loads()
    {
        string path = Path.Combine(_fixture.Root, "config.json");
        File.WriteAllText(path, "{ \"pages\": [ { \"name\": \"A\", \"background\": { \"type\": \"image\", \"path\": \"x.png\" } } ] }");

        using var store = new ConfigStore(path);

        Assert.Contains("a page's own 'background' is not supported", _fixture.Log);
    }
}
