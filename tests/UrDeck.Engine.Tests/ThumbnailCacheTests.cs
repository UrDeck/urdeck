// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Engine.Editor;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ThumbnailCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "urdeck-thumbs-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    [Fact]
    public void A_stored_picture_is_found_by_its_key_and_a_new_key_replaces_it()
    {
        var cache = new ThumbnailCache(_folder);
        string first = ThumbnailCache.Key("page", "a");
        string second = ThumbnailCache.Key("page", "b");
        Assert.NotEqual(first, second);
        Assert.Null(cache.Find("carbon", first));

        Assert.NotNull(cache.Store("carbon", first, [1, 2, 3]));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(cache.Find("carbon", first)!));

        cache.Store("carbon", second, [4]);
        Assert.Null(cache.Find("carbon", first));
        Assert.NotNull(cache.Find("carbon", second));
    }

    [Fact]
    public void Storing_one_theme_keeps_the_pictures_of_a_theme_whose_name_starts_the_same()
    {
        var cache = new ThumbnailCache(_folder);
        string key = ThumbnailCache.Key("x");
        cache.Store("carbon-paper", key, [1]);
        cache.Store("carbon", ThumbnailCache.Key("y"), [2]);

        Assert.NotNull(cache.Find("carbon-paper", key));
    }

    [Fact]
    public void A_name_cannot_leave_the_folder()
    {
        var cache = new ThumbnailCache(_folder);
        string path = cache.PathFor(@"..\..\evil", "0123456789abcdef");

        Assert.Equal(Path.GetFullPath(_folder), Path.GetDirectoryName(Path.GetFullPath(path)));
    }

    [Fact]
    public void The_preview_config_changes_the_theme_and_motion_and_keeps_the_rest()
    {
        var root = JsonNode.Parse("""
            { "version": 1, "theme": "default-dark", "motion": "full",
              "background": { "type": "shader", "name": "nebula" },
              "pages": [ { "id": "a", "name": "Main", "widgets": [] } ] }
            """)!.AsObject();

        var config = ThemePreviewConfig.For(root, "carbon");

        Assert.Equal("carbon", config.Theme);
        Assert.Equal("reduced", config.Motion);
        Assert.NotNull(config.Background);
        Assert.Equal("default-dark", root["theme"]!.GetValue<string>());
        Assert.Equal("full", root["motion"]!.GetValue<string>());
    }

    [Fact]
    public void The_content_key_follows_the_page_and_the_background_but_not_the_theme()
    {
        JsonObject Config(string theme, string name, string background) => JsonNode.Parse($$"""
            { "theme": "{{theme}}", "background": { "type": "shader", "name": "{{background}}" },
              "pages": [ { "id": "a", "name": "{{name}}", "widgets": [] } ] }
            """)!.AsObject();

        string key = ThemePreviewConfig.ContentKey(Config("x", "Main", "nebula"), "a");
        Assert.Equal(key, ThemePreviewConfig.ContentKey(Config("y", "Main", "nebula"), "a"));
        Assert.NotEqual(key, ThemePreviewConfig.ContentKey(Config("x", "Other", "nebula"), "a"));
        Assert.NotEqual(key, ThemePreviewConfig.ContentKey(Config("x", "Main", "plasma"), "a"));
    }

    [Fact]
    public void The_pictures_do_not_depend_on_how_the_background_is_tuned()
    {
        JsonObject Config(string background) => JsonNode.Parse($$"""
            { "theme": "x", "background": {{background}},
              "pages": [ { "id": "a", "name": "Main", "widgets": [] } ] }
            """)!.AsObject();

        string key = ThemePreviewConfig.ContentKey(Config("""{ "type": "shader", "name": "nebula" }"""), "a");
        Assert.Equal(key, ThemePreviewConfig.ContentKey(Config("""{ "type": "shader", "name": "nebula", "density": 3, "dim": 0.4, "speed": 0 }"""), "a"));
        Assert.NotEqual(key, ThemePreviewConfig.ContentKey(Config("""{ "type": "shader", "name": "plasma" }"""), "a"));

        var stripped = ThemePreviewConfig.For(Config("""{ "type": "shader", "name": "nebula", "density": 3, "dim": 0.4 }"""), "carbon");
        Assert.Equal("nebula", stripped.Background!.Value.GetProperty("name").GetString());
        Assert.False(stripped.Background.Value.TryGetProperty("density", out _));
        Assert.False(stripped.Background.Value.TryGetProperty("dim", out _));
    }
}
