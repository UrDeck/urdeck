// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Nodes;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Config;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class PairingTests : IDisposable
{
    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static ThemeCatalogEntry Theme(string name, string[]? lists = null, string? defaultBackground = null, string[]? tags = null) =>
        new(name, name, null, null, "dark", tags ?? [], lists ?? [], [], new ThemeSwatch("#000000", "#111111", "#222222", "#ffffff"), defaultBackground, false);

    private static BackgroundCatalogEntry Background(string name, string[]? lists = null, string[]? tags = null) =>
        new(name, name, null, null, tags ?? [], lists ?? [], new Dictionary<string, ManifestParameter>(), [], false);

    private static string Names(IEnumerable<Recommendation> list) => string.Join(",", list.Select(r => $"{r.Background}:{r.Reason}"));

    // 4.3 and 4.4 The index and the recommendations

    [Fact]
    public void A_pair_only_the_theme_names_is_listed_and_not_mutual()
    {
        var index = new PairingIndex([Theme("carbon", ["contour"])], [Background("contour")]);

        Assert.True(index.IsListed("carbon", "contour"));
        Assert.False(index.IsMutual("carbon", "contour"));
        Assert.Equal([new PairedBackground("contour", false)], index.BackgroundsFor("carbon"));
        Assert.Equal([new PairedTheme("carbon", false)], index.ThemesFor("contour"));
    }

    [Fact]
    public void A_pair_only_the_background_names_is_listed()
    {
        var index = new PairingIndex([Theme("carbon")], [Background("contour", ["carbon"])]);

        Assert.True(index.IsListed("carbon", "contour"));
        Assert.False(index.IsMutual("carbon", "contour"));
        Assert.Equal([new PairedBackground("contour", false)], index.BackgroundsFor("carbon"));
    }

    [Fact]
    public void A_pair_both_sides_name_is_mutual()
    {
        var index = new PairingIndex([Theme("carbon", ["contour"])], [Background("contour", ["carbon"])]);

        Assert.True(index.IsMutual("carbon", "contour"));
        Assert.Equal([new PairedBackground("contour", true)], index.BackgroundsFor("carbon"));
        Assert.Equal([new PairedTheme("carbon", true)], index.ThemesFor("contour"));
    }

    [Fact]
    public void Unrelated_names_are_not_listed_and_names_are_exact()
    {
        var index = new PairingIndex([Theme("carbon", ["contour"])], [Background("contour"), Background("streams")]);

        Assert.False(index.IsListed("carbon", "streams"));
        Assert.False(new PairingIndex([Theme("Carbon", ["contour"])], [Background("contour")]).IsListed("carbon", "contour"));
        Assert.False(new PairingIndex([Theme("carbon", ["Contour"])], [Background("contour")]).IsListed("carbon", "contour"));
    }

    [Fact]
    public void A_name_that_does_not_exist_is_skipped_without_a_log_line()
    {
        var themes = new[] { Theme("carbon", ["ghost", "contour"], defaultBackground: "ghost") };
        var backgrounds = new[] { Background("contour", ["nowhere"]) };
        var index = new PairingIndex(themes, backgrounds);

        Assert.Equal(["contour"], index.BackgroundsFor("carbon").Select(p => p.Name));
        Assert.Equal("contour:ThemeListed", Names(index.Recommend(themes[0], backgrounds, 5)));
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void The_theme_default_comes_first_then_the_others()
    {
        var theme = Theme("carbon", ["streams", "contour"], defaultBackground: "contour");
        var backgrounds = new[] { Background("streams"), Background("contour") };

        Assert.Equal("contour:ThemeDefault,streams:ThemeListed", Names(new PairingIndex([theme], backgrounds).Recommend(theme, backgrounds, 5)));
    }

    [Fact]
    public void Mutual_comes_before_one_sided()
    {
        var theme = Theme("carbon", ["a", "b"]);
        var backgrounds = new[] { Background("a"), Background("b", ["carbon"]) };

        Assert.Equal("b:Mutual,a:ThemeListed", Names(new PairingIndex([theme], backgrounds).Recommend(theme, backgrounds, 5)));
    }

    [Fact]
    public void Each_rung_has_its_reason_in_order()
    {
        var theme = Theme("carbon", ["mutual", "mine", "theirs"], defaultBackground: "default", tags: ["calm"]);
        var backgrounds = new[]
        {
            Background("tagged", tags: ["calm"]),
            Background("listing", ["carbon"]),
            Background("theirs"),
            Background("mine"),
            Background("mutual", ["carbon"]),
            Background("default"),
        };

        Assert.Equal(
            "default:ThemeDefault,mutual:Mutual,mine:ThemeListed,theirs:ThemeListed,listing:BackgroundListed,tagged:TagMatch",
            Names(new PairingIndex([theme], backgrounds).Recommend(theme, backgrounds, 10)));
    }

    [Fact]
    public void The_limit_cuts_the_list_in_order()
    {
        var theme = Theme("carbon", ["a", "b", "c", "d", "e", "f", "g"]);
        var backgrounds = "abcdefg".Select(c => Background(c.ToString())).ToList();
        var index = new PairingIndex([theme], backgrounds);

        Assert.Equal("a:ThemeListed,b:ThemeListed,c:ThemeListed", Names(index.Recommend(theme, backgrounds, 3)));
        Assert.Empty(index.Recommend(theme, backgrounds, 0));
    }

    [Fact]
    public void A_tag_match_is_only_for_a_background_with_no_pairing_data()
    {
        var theme = Theme("carbon", tags: ["calm"]);
        var backgrounds = new[]
        {
            Background("plain", tags: ["calm"]),
            Background("elsewhere", ["other"], ["calm"]),
            Background("named", tags: ["calm"]),
        };
        var other = Theme("other", ["named"]);
        var index = new PairingIndex([theme, other], backgrounds);

        Assert.Equal("plain:TagMatch", Names(index.Recommend(theme, backgrounds, 5)));
    }

    [Fact]
    public void A_theme_with_no_data_gets_an_empty_list()
    {
        var theme = Theme("bare");
        var backgrounds = new[] { Background("a"), Background("b", ["other"]) };

        Assert.Empty(new PairingIndex([theme], backgrounds).Recommend(theme, backgrounds, 5));
    }

    [Fact]
    public void A_background_appears_once_with_the_first_rung()
    {
        var theme = Theme("carbon", ["a", "a"], defaultBackground: "a");
        var backgrounds = new[] { Background("a", ["carbon"]), Background("a", ["carbon"]) };

        Assert.Equal("a:ThemeDefault", Names(new PairingIndex([theme], backgrounds).Recommend(theme, backgrounds, 5)));
    }

    [Fact]
    public void The_same_input_gives_the_same_list_twice()
    {
        var theme = Theme("carbon", ["b", "a"], defaultBackground: "c", tags: ["x"]);
        var backgrounds = new[] { Background("a", ["carbon"]), Background("b"), Background("c"), Background("d", tags: ["x"]), Background("e", ["carbon"]) };
        var index = new PairingIndex([theme], backgrounds);

        Assert.Equal(Names(index.Recommend(theme, backgrounds, 10)), Names(index.Recommend(theme, backgrounds, 10)));
        Assert.Equal(Names(index.Recommend(theme, backgrounds, 10)),
            Names(new PairingIndex([theme], backgrounds).Recommend(theme, backgrounds, 10)));
    }

    // 4.1 The theme catalog

    [Fact]
    public void The_theme_catalog_lists_built_in_and_user_themes_with_swatches()
    {
        _fixture.WriteTheme("mine", """{ "colors": { "accent": "#ff00ff" }, "meta": { "name": "Mine", "mode": "light", "tags": ["calm"], "pairsWith": { "backgrounds": ["plasma"] } }, "background": { "default": { "name": "plasma" } } }""");

        var list = ThemeCatalog.List(_fixture.Themes);

        Assert.Equal(["default-dark", "default-light", "glass", "carbon", "paper", "prism", "obsidian", "ember", "blueprint", "pop", "rigline", "mine"], list.Select(e => e.Name));
        var mine = list.Single(e => e.Name == "mine");
        Assert.Equal("Mine", mine.DisplayName);
        Assert.Equal("light", mine.Mode);
        Assert.Equal(["calm"], mine.Tags);
        Assert.Equal(["plasma"], mine.PairsWithBackgrounds);
        Assert.Equal("plasma", mine.DefaultBackground);
        Assert.Equal("#ff00ff", mine.Swatch.Accent);
        Assert.False(mine.IsBuiltIn);
        Assert.All(list.Where(e => e.Name != "mine"), e => Assert.True(e.IsBuiltIn));
        Assert.All(list, e => Assert.False(string.IsNullOrEmpty(e.Swatch.Background)));
    }

    [Fact]
    public void A_user_theme_replaces_a_built_in_of_the_same_name()
    {
        _fixture.WriteTheme("glass", """{ "meta": { "name": "My Glass" } }""");

        var list = ThemeCatalog.List(_fixture.Themes);

        Assert.Equal(1, list.Count(e => e.Name == "glass"));
        Assert.Equal("My Glass", list.Single(e => e.Name == "glass").DisplayName);
        Assert.False(list.Single(e => e.Name == "glass").IsBuiltIn);
        Assert.Equal(11, list.Count);
    }

    [Fact]
    public void A_broken_theme_file_is_left_out_with_a_warning()
    {
        _fixture.WriteTheme("fine", """{ }""");
        _fixture.WriteTheme("broken", "{ not json");

        var list = ThemeCatalog.List(_fixture.Themes);

        Assert.Contains(list, e => e.Name == "fine");
        Assert.DoesNotContain(list, e => e.Name == "broken");
        Assert.Contains("broken", _fixture.Log);
    }

    [Fact]
    public void The_built_in_themes_have_meta()
    {
        var list = ThemeCatalog.List(_fixture.Themes);

        Assert.Equal(("Default Dark", "dark"), (list[0].DisplayName, list[0].Mode));
        Assert.Equal(("Default Light", "light"), (list[1].DisplayName, list[1].Mode));
        Assert.Equal(("Glass", "dark"), (list[2].DisplayName, list[2].Mode));
        Assert.All(list, e => Assert.False(string.IsNullOrWhiteSpace(e.Description)));
        Assert.Equal(("Carbon", "dark"), (list[3].DisplayName, list[3].Mode));
        Assert.Equal(("Paper", "light"), (list[4].DisplayName, list[4].Mode));
        Assert.Equal(("Prism", "dark"), (list[5].DisplayName, list[5].Mode));
        Assert.Equal(("Obsidian", "dark"), (list[6].DisplayName, list[6].Mode));
        Assert.Equal(("Ember", "dark"), (list[7].DisplayName, list[7].Mode));
        Assert.Equal(("Blueprint", "dark"), (list[8].DisplayName, list[8].Mode));
        Assert.Equal(("Pop", "light"), (list[9].DisplayName, list[9].Mode));
        Assert.Equal("shapes", list[9].DefaultBackground);
        Assert.Equal(("Rigline", "dark"), (list[10].DisplayName, list[10].Mode));
        Assert.Equal("horizon", list[10].DefaultBackground);
        Assert.All(list.Take(3), e => Assert.Null(e.DefaultBackground));
        Assert.All(list.Skip(3).Take(2), e => Assert.Equal("contour", e.DefaultBackground));
        Assert.All(list.Skip(5).Take(2), e => Assert.Equal("nebula", e.DefaultBackground));
        Assert.Equal("embers", list[7].DefaultBackground);
        Assert.Equal("plotter", list[8].DefaultBackground);
    }

    // 4.2 The background catalog

    [Fact]
    public void The_background_catalog_lists_built_in_and_user_backgrounds_without_compiling()
    {
        _fixture.WriteFolder("calm", new Dictionary<string, string>
        {
            ["calm.sksl"] = "this is not valid sksl at all",
            ["manifest.json"] = """{ "manifest": 1, "title": "Calm", "description": "Slow.", "tags": ["calm"], "pairsWith": { "themes": ["carbon"] }, "parameters": { "level": { "label": "L" } } }""",
        });

        var list = BackgroundCatalog.List(_fixture.Store);

        Assert.Equal(ShaderStore.BuiltInNames.Count + 1, list.Count);
        var calm = list.Single(e => e.Name == "calm");
        Assert.Equal("Calm", calm.Title);
        Assert.Equal(["carbon"], calm.PairsWithThemes);
        Assert.Equal(["calm"], calm.Tags);
        Assert.Equal("L", calm.Parameters["level"].Label);
        Assert.False(calm.IsBuiltIn);
        Assert.All(ShaderStore.BuiltInNames, n => Assert.True(list.Single(e => e.Name == n).IsBuiltIn));
    }

    [Fact]
    public void A_user_background_replaces_a_built_in_and_a_broken_one_is_skipped()
    {
        _fixture.WriteShader("plasma", "half4 main(float2 p) { return half4(1); }");
        _fixture.WriteFolder("nomanifest", new Dictionary<string, string> { ["nomanifest.sksl"] = "half4 main(float2 p) { return half4(1); }" });

        var list = BackgroundCatalog.List(_fixture.Store);

        Assert.Equal(1, list.Count(e => e.Name == "plasma"));
        Assert.False(list.Single(e => e.Name == "plasma").IsBuiltIn);
        Assert.DoesNotContain(list, e => e.Name == "nomanifest");
        Assert.Contains("nomanifest", _fixture.Log);
    }

    // 4.5 Meta changes nothing that is drawn

    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("glass")]
    public void Meta_does_not_change_the_picture(string name)
    {
        var withMeta = ThemeStore.LoadBuiltIn(name)!;
        Assert.NotNull(withMeta.Definition.Meta);

        var stream = typeof(ThemeStore).Assembly.GetManifestResourceStream($"UrDeck.Themes.{name}.json")!;
        var node = JsonNode.Parse(stream)!.AsObject();
        Assert.True(node.Remove("meta"));
        var bare = JsonSerializer.Deserialize<ThemeDefinition>(node.ToJsonString(), UrDeckJson.Options)!;
        if (name != ThemeStore.DefaultName)
            bare = bare.MergeOver(ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!.Definition);
        var without = withMeta with { Definition = bare };
        Assert.Null(without.Definition.Meta);

        byte[] Render(LoadedTheme theme)
        {
            using var bitmap = PageRenderer.RenderToBitmap(220, 768, new List<(WidgetConfig, IWidget)>(), theme, new DateTime(2026, 10, 11));
            return bitmap.Bytes;
        }

        Assert.Equal(Render(without), Render(withMeta));
    }
}
