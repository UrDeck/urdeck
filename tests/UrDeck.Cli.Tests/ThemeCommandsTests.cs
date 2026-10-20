// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Cli.Tests.Support;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Cli.Tests;

/// <summary>The scenarios of the <c>urdeck-cli</c> spec for <c>theme render</c>, <c>theme init</c> and <c>theme schema</c>.</summary>
public sealed class ThemeCommandsTests : IDisposable
{
    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    private static string RepositoryFile(params string[] parts)
    {
        string? directory = AppContext.BaseDirectory;
        while (directory != null && !File.Exists(Path.Combine(directory, "urdeck.slnx")))
            directory = Path.GetDirectoryName(directory);
        return Path.Combine([directory ?? throw new InvalidOperationException("urdeck.slnx was not found above the tests."), .. parts]);
    }

    // ------------------------------------------------------------------ render

    [Fact]
    public void Render_writes_the_sample_page_at_the_default_size()
    {
        _work.Theme("THEME080", "mine", "pass");

        var run = Workspace.Invoke("theme", "render", "mine", "-o", "out.png");

        Assert.Equal(0, run.Code);
        Assert.Equal("wrote out.png (1100x1920)\n", run.Out);
        Assert.Equal("", run.Error);
        using var picture = Workspace.Decode("out.png");
        Assert.Equal((1100, 1920), (picture.Width, picture.Height));
    }

    [Fact]
    public void Render_takes_the_size_it_is_given()
    {
        var run = Workspace.Invoke("theme", "render", "--builtin", "carbon", "-o", "out.png", "--size", "550x960");

        Assert.Equal(0, run.Code);
        using var picture = Workspace.Decode("out.png");
        Assert.Equal((550, 960), (picture.Width, picture.Height));
    }

    [Fact]
    public void Render_gives_the_same_bytes_twice()
    {
        Workspace.Invoke("theme", "render", "--builtin", "paper", "-o", "one.png", "--page", "dock");
        Workspace.Invoke("theme", "render", "--builtin", "paper", "-o", "two.png", "--page", "dock");

        Assert.Equal(File.ReadAllBytes("one.png"), File.ReadAllBytes("two.png"));
    }

    [Fact]
    public void Render_of_the_dock_draws_more_than_the_page()
    {
        Workspace.Invoke("theme", "render", "--builtin", "carbon", "-o", "page.png");
        Workspace.Invoke("theme", "render", "--builtin", "carbon", "-o", "dock.png", "--page", "dock");

        Assert.NotEqual(File.ReadAllBytes("page.png"), File.ReadAllBytes("dock.png"));
    }

    [WindowsGlyphTheory]
    [InlineData("carbon", "page", "carbon-page")]
    [InlineData("paper", "page", "paper-page")]
    [InlineData("carbon", "dock", "carbon-dock")]
    [InlineData("paper", "dock", "paper-dock")]
    [InlineData("default-dark", "page", "default-dark-page")]
    [InlineData("glass", "page", "glass-page")]
    public void Render_of_a_built_in_is_the_golden_picture_where_text_is_drawn_the_same(string theme, string page, string golden)
    {
        var run = Workspace.Invoke("theme", "render", "--builtin", theme, "-o", "out.png", "--page", page);

        Assert.Equal(0, run.Code);
        using var picture = Workspace.Decode("out.png");
        GoldenImage.Compare(picture, golden);
    }

    [Fact]
    public void Render_of_a_folder_that_cannot_be_read_writes_nothing_and_is_1()
    {
        Directory.CreateDirectory("empty");

        var run = Workspace.Invoke("theme", "render", "empty", "-o", "out.png");

        Assert.Equal(1, run.Code);
        Assert.False(File.Exists("out.png"));
        Assert.Contains("THEME001", run.Error);
        Assert.Equal("", run.Out);
    }

    [Fact]
    public void Render_prints_the_findings_to_the_error_stream_and_still_draws()
    {
        _work.Theme("THEME004", "mine");

        var run = Workspace.Invoke("theme", "render", "mine", "-o", "out.png");

        Assert.Equal(0, run.Code);
        Assert.True(File.Exists("out.png"));
        Assert.Contains("mine/theme.json: warning THEME004:", run.Error);
        Assert.Equal("wrote out.png (1100x1920)\n", run.Out);
    }

    [Fact]
    public void Render_fails_on_a_warning_only_when_asked_and_still_writes_the_picture()
    {
        _work.Theme("THEME004", "mine");

        var run = Workspace.Invoke("theme", "render", "mine", "-o", "out.png", "--fail-on-warnings");

        Assert.Equal(1, run.Code);
        Assert.True(File.Exists("out.png"));
    }

    [Fact]
    public void Render_replaces_an_existing_file()
    {
        File.WriteAllText("out.png", "old");

        Workspace.Invoke("theme", "render", "--builtin", "carbon", "-o", "out.png", "--size", "100x100");

        using var picture = Workspace.Decode("out.png");
        Assert.Equal(100, picture.Width);
    }

    [Theory]
    [InlineData("--builtin", "nosuch")]
    [InlineData("--size", "big")]
    [InlineData("--size", "0x10")]
    public void Render_with_arguments_it_cannot_use_is_2(string option, string value)
    {
        string[] args = option == "--builtin"
            ? ["theme", "render", "--builtin", value, "-o", "out.png"]
            : ["theme", "render", "--builtin", "carbon", "-o", "out.png", option, value];

        var run = Workspace.Invoke(args);

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.False(File.Exists("out.png"));
    }

    [Fact]
    public void Render_of_a_folder_and_a_built_in_together_is_2()
    {
        _work.Theme("THEME080", "mine", "pass");

        Assert.Equal(2, Workspace.Invoke("theme", "render", "mine", "--builtin", "carbon", "-o", "out.png").Code);
        Assert.Equal(2, Workspace.Invoke("theme", "render", "-o", "out.png").Code);
    }

    [Fact]
    public void Render_into_a_folder_that_does_not_exist_is_2()
    {
        var run = Workspace.Invoke("theme", "render", "--builtin", "carbon", "-o", "nodir/out.png");

        Assert.Equal(2, run.Code);
        Assert.Contains("nodir", run.Error);
    }

    // ------------------------------------------------------------------ init

    [Fact]
    public void Init_makes_a_starter_that_validates_without_an_error()
    {
        var run = Workspace.Invoke("theme", "init", "mine");

        Assert.Equal(0, run.Code);
        Assert.Contains("made mine", run.Out);
        Assert.Contains("next: urdeck-cli theme validate mine", run.Out);
        using var json = JsonDocument.Parse(File.ReadAllText("mine/theme.json"));
        var root = json.RootElement;
        Assert.Equal("https://urdeck.app/schemas/v2/theme.json", root.GetProperty("$schema").GetString());
        Assert.Equal(2, root.GetProperty("format").GetInt32());
        Assert.Equal("mine", root.GetProperty("meta").GetProperty("name").GetString());
        Assert.Equal("", root.GetProperty("meta").GetProperty("description").GetString());
        Assert.Equal("dark", root.GetProperty("meta").GetProperty("mode").GetString());
        Assert.Equal("#0f0f1a", root.GetProperty("colors").GetProperty("background").GetString());

        var check = Workspace.Invoke("theme", "validate", "mine", "--format", "json");
        Assert.Equal(0, check.Code);
        using var report = JsonDocument.Parse(check.Out);
        Assert.Equal(0, report.RootElement.GetProperty("summary").GetProperty("errors").GetInt32());
        // The description is left empty for the author to write, which is the one thing the check asks for.
        Assert.Equal(["THEME080"], report.RootElement.GetProperty("themes")[0].GetProperty("diagnostics").EnumerateArray().Select(d => d.GetProperty("code").GetString()));
    }

    [Theory]
    [InlineData("default-dark")]
    [InlineData("default-light")]
    [InlineData("glass")]
    [InlineData("carbon")]
    [InlineData("paper")]
    public void Init_from_a_built_in_makes_a_self_contained_copy_that_checks_clean(string from)
    {
        var run = Workspace.Invoke("theme", "init", "copy", "--from", from);

        Assert.Equal(0, run.Code);
        using var json = JsonDocument.Parse(File.ReadAllText("copy/theme.json"));
        Assert.Equal("copy", json.RootElement.GetProperty("meta").GetProperty("name").GetString());

        var check = Workspace.Invoke("theme", "validate", "copy", "--fail-on-warnings");
        Assert.Equal(0, check.Code);
        Assert.Equal("1 theme checked: 0 errors, 0 warnings, 0 remarks.\n", check.Out);
    }

    [Fact]
    public void Init_from_carbon_copies_its_fonts_and_their_licences()
    {
        Workspace.Invoke("theme", "init", "copy", "--from", "carbon");

        string[] files = Directory.GetFiles("copy").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;
        Assert.Contains("JetBrainsMono-Variable.ttf", files);
        Assert.Contains("JetBrainsMono-OFL.txt", files);
        Assert.Contains("theme.json", files);
    }

    [Fact]
    public void Init_in_another_directory_makes_the_folder_there()
    {
        Directory.CreateDirectory("elsewhere");

        var run = Workspace.Invoke("theme", "init", "mine", "--output", "elsewhere");

        Assert.Equal(0, run.Code);
        Assert.True(File.Exists("elsewhere/mine/theme.json"));
        Assert.False(Directory.Exists("mine"));
    }

    [Fact]
    public void Init_leaves_an_existing_folder_as_it_is()
    {
        Directory.CreateDirectory("mine");
        File.WriteAllText("mine/theme.json", "mine");

        var run = Workspace.Invoke("theme", "init", "mine");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("already exists", run.Error);
        Assert.Equal("mine", File.ReadAllText("mine/theme.json"));
        Assert.Single(Directory.GetFiles("mine"));
    }

    [Theory]
    [InlineData("my theme")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("naïve")]
    public void Init_with_a_name_that_is_not_valid_creates_nothing_and_is_2(string name)
    {
        var run = Workspace.Invoke("theme", "init", name);

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("letters, digits", run.Error);
        Assert.Empty(Directory.GetFileSystemEntries(_work.Root));
    }

    [Fact]
    public void Init_from_an_unknown_built_in_creates_nothing_and_is_2()
    {
        var run = Workspace.Invoke("theme", "init", "mine", "--from", "nosuch");

        Assert.Equal(2, run.Code);
        Assert.Contains("nosuch", run.Error);
        Assert.False(Directory.Exists("mine"));
    }

    [Fact]
    public void Init_into_a_directory_that_does_not_exist_is_2()
    {
        var run = Workspace.Invoke("theme", "init", "mine", "--output", "missing");

        Assert.Equal(2, run.Code);
        Assert.Contains("missing", run.Error);
    }

    // ------------------------------------------------------------------ schema

    [Fact]
    public void Schema_prints_the_published_file_byte_for_byte()
    {
        var run = Workspace.Invoke("theme", "schema");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.Equal(File.ReadAllText(RepositoryFile("docs", "schemas", "v2", "theme.json")).ReplaceLineEndings("\n"), run.Out);
    }

    [Fact]
    public void Schema_to_a_file_prints_nothing_and_replaces_the_file()
    {
        File.WriteAllText("theme.schema.json", "old");

        var run = Workspace.Invoke("theme", "schema", "--output", "theme.schema.json");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Out);
        Assert.Equal(
            File.ReadAllBytes(RepositoryFile("docs", "schemas", "v2", "theme.json")),
            File.ReadAllBytes("theme.schema.json"));
    }

    [Fact]
    public void Schema_to_a_folder_that_does_not_exist_is_2()
    {
        var run = Workspace.Invoke("theme", "schema", "--output", "nodir/theme.schema.json");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
    }

    // ------------------------------------------------------------------ help

    [Theory]
    [InlineData("render")]
    [InlineData("init")]
    [InlineData("schema")]
    public void Help_names_the_exit_codes(string command)
    {
        var run = Workspace.Invoke("theme", command, "--help");

        Assert.Equal(0, run.Code);
        Assert.Contains("Exit codes:", run.Out);
        Assert.Contains("  0  ", run.Out);
        Assert.Contains("  1  ", run.Out);
        Assert.Contains("  2  ", run.Out);
    }

    [Fact]
    public void Render_help_lists_its_options()
    {
        var run = Workspace.Invoke("theme", "render", "--help");

        foreach (string option in new[] { "--builtin", "--output", "--page", "--size", "--fail-on-warnings" })
            Assert.Contains(option, run.Out);
        Assert.Contains("dock", run.Out);
    }

    [Fact]
    public void Init_help_lists_its_argument_and_options()
    {
        var run = Workspace.Invoke("theme", "init", "--help");

        Assert.Contains("--output", run.Out);
        Assert.Contains("--from", run.Out);
        Assert.Contains("<name>", run.Out);
        Assert.Contains("Never overwrites", run.Out);
    }

    [Fact]
    public void The_root_help_names_themes_as_well_as_backgrounds()
    {
        var run = Workspace.Invoke("--help");

        Assert.Contains("theme", run.Out);
        Assert.Contains("shader", run.Out);
    }
}
