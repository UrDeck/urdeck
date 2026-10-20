// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The one load path of a theme: folder and embedded sources give what the deck's own paths give.</summary>
public sealed class ThemeLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-loader-tests-" + Guid.NewGuid().ToString("N"));

    public ThemeLoaderTests() => Directory.CreateDirectory(Path.Combine(_root, "themes"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    public static TheoryData<string> BuiltIns()
    {
        var data = new TheoryData<string>();
        foreach (string name in ThemeStore.BuiltInNames)
            data.Add(name);
        return data;
    }

    private static string Describe(LoadedTheme theme) =>
        JsonSerializer.Serialize(theme.Definition, UrDeckJson.Options)
        + "\nfont=" + theme.Font.Spec
        + "\nroles=" + string.Join(",", theme.RoleFonts.OrderBy(r => r.Key).Select(r => $"{r.Key}:{r.Value.Spec}"));

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void The_fast_path_and_the_loader_over_the_embedded_theme_give_the_same_definition(string name)
    {
        var baseline = ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!;
        var findings = new CollectedFindings();

        var loaded = ThemeLoader.Load(new EmbeddedThemeSource(name), baseline, findings);

        Assert.NotNull(loaded);
        Assert.Empty(findings.Found);
        Assert.Equal(Describe(ThemeStore.LoadBuiltIn(name)!), Describe(loaded));
        Assert.Null(loaded.Folder);
    }

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void The_fonts_the_embedded_loader_names_can_be_opened(string name)
    {
        var source = new EmbeddedThemeSource(name);
        var loaded = ThemeLoader.Load(source, ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!, new CollectedFindings())!;

        foreach (var font in loaded.RoleFonts.Values.Append(loaded.Font))
        {
            using var stream = font.Open(font.Spec);
            Assert.NotNull(stream);
        }
    }

    [Fact]
    public void An_embedded_source_names_the_files_it_ships_and_refuses_a_path()
    {
        var source = new EmbeddedThemeSource("carbon");

        Assert.NotNull(source.ReadSettings());
        Assert.Contains("JetBrainsMono-Variable.ttf", source.Files());
        Assert.Contains("JetBrainsMono-OFL.txt", source.Files());
        Assert.True(source.Exists("JetBrainsMono-Variable.ttf"));
        Assert.True(source.Length("JetBrainsMono-Variable.ttf") > 0);
        Assert.False(source.Exists("NotShipped.ttf"));
        Assert.Null(source.Length("NotShipped.ttf"));
        foreach (string refused in new[] { "..\\x.ttf", "../x.ttf", "sub/x.ttf", "C:\\x.ttf", "", ".." })
        {
            Assert.False(source.IsInside(refused), refused);
            Assert.Null(source.Open(refused));
        }
    }

    [Fact]
    public void An_unknown_built_in_has_no_settings()
    {
        Assert.Null(new EmbeddedThemeSource("nosuch").ReadSettings());
        Assert.Null(ThemeLoader.Load(new EmbeddedThemeSource("nosuch"), ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!, new CollectedFindings()));
    }

    private string Theme(string name, string json)
    {
        string folder = Path.Combine(_root, "themes", name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ThemeStore.SettingsFileName), json);
        return folder;
    }

    [Fact]
    public void A_folder_named_like_a_built_in_is_read_as_the_folder()
    {
        Theme("carbon", """{ "colors": { "accent": "#123456" } }""");

        var loaded = new ThemeStore(Path.Combine(_root, "themes")).Load("carbon");

        Assert.Equal("#123456", loaded.Definition.Colors!.Accent);
        Assert.NotNull(loaded.Folder);
    }

    [Fact]
    public void The_loader_reads_a_folder_as_the_store_does()
    {
        string folder = Theme("mine", """{ "colors": { "accent": "#123456" }, "card": { "shadow": { "blur": 5 } } }""");
        var baseline = ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!;
        var findings = new CollectedFindings();

        var loaded = ThemeLoader.Load(new DirectoryThemeSource(folder), baseline, findings)!;

        Assert.Equal("mine", loaded.Name);
        Assert.Equal(Path.GetFullPath(folder), loaded.Folder);
        Assert.Equal("#123456", loaded.Definition.Colors!.Accent);
        Assert.Equal(ThemeCodes.OutOfRange, Assert.Single(findings.Found).Code);
    }

    [Fact]
    public void A_folder_without_settings_is_not_a_theme()
    {
        string folder = Path.Combine(_root, "themes", "empty");
        Directory.CreateDirectory(folder);

        Assert.Null(ThemeLoader.Load(new DirectoryThemeSource(folder), ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!, new CollectedFindings()));
    }

    [Fact]
    public void Settings_that_are_not_json_are_a_finding_and_no_theme()
    {
        string folder = Theme("broken", """{ "card": """);
        var findings = new CollectedFindings();

        var loaded = ThemeLoader.Load(new DirectoryThemeSource(folder), ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!, findings);

        Assert.Null(loaded);
        var finding = Assert.Single(findings.Found);
        Assert.Equal(ThemeCodes.FileUnusable, finding.Code);
        Assert.Equal(ThemeSeverity.Error, finding.Severity);
    }

    [Theory]
    [InlineData("..\\outside.ttf")]
    [InlineData("../outside.ttf")]
    [InlineData("sub\\..\\..\\outside.ttf")]
    public void A_font_path_that_leaves_the_folder_is_refused_and_nothing_outside_is_opened(string spec)
    {
        File.WriteAllBytes(Path.Combine(_root, "themes", "outside.ttf"), [1, 2, 3]);
        string folder = Theme("mine", JsonSerializer.Serialize(new { typography = new { font = spec, fonts = new { value = spec } } }));
        var findings = new CollectedFindings();
        var source = new DirectoryThemeSource(folder);

        var loaded = ThemeLoader.Load(source, ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!, findings)!;

        Assert.Equal(ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!.Font.Spec, loaded.Font.Spec);
        Assert.Empty(loaded.RoleFonts);
        Assert.Equal(["typography.font", "typography.fonts.value"], findings.Found.Select(f => f.Key).Order(StringComparer.Ordinal));
        Assert.All(findings.Found, f => Assert.Equal(ThemeCodes.FontFile, f.Code));
        Assert.False(source.IsInside(spec));
        Assert.False(source.Exists(spec));
        Assert.Null(source.Length(spec));
        Assert.Null(source.Open(spec));
    }

    [Fact]
    public void An_absolute_font_path_is_refused()
    {
        string outside = Path.Combine(_root, "themes", "outside.ttf");
        File.WriteAllBytes(outside, [1, 2, 3]);
        string folder = Theme("mine", JsonSerializer.Serialize(new { typography = new { font = outside } }));
        var findings = new CollectedFindings();
        var source = new DirectoryThemeSource(folder);

        ThemeLoader.Load(source, ThemeStore.LoadBuiltIn(ThemeStore.DefaultName)!, findings);

        Assert.Equal(ThemeCodes.FontFile, Assert.Single(findings.Found).Code);
        Assert.Null(source.Open(outside));
    }

    [Fact]
    public void A_font_inside_the_folder_is_opened_and_its_files_are_listed()
    {
        string folder = Theme("mine", """{ "typography": { "font": "Face.ttf" } }""");
        File.WriteAllBytes(Path.Combine(folder, "Face.ttf"), [1, 2, 3, 4]);
        File.WriteAllText(Path.Combine(folder, "Face-OFL.txt"), "licence");
        var source = new DirectoryThemeSource(folder);

        Assert.True(source.IsInside("Face.ttf"));
        Assert.Equal(4, source.Length("Face.ttf"));
        using (var stream = source.Open("Face.ttf"))
            Assert.NotNull(stream);
        Assert.Equal(["Face-OFL.txt", "Face.ttf", "theme.json"], source.Files());
    }
}
