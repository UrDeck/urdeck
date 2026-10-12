// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Tests.Support;
using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>A background read from a folder at any path: the name check, reading, rendering that folder, the blank warning.</summary>
public sealed class BackgroundFolderTests : IDisposable
{
    private const string Red = "half4 main(float2 p) { return half4(1, 0, 0, 1); }";
    private const string Blue = "half4 main(float2 p) { return half4(0, 0, 1, 1); }";
    private const string Empty = "half4 main(float2 p) { return half4(0); }";

    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private IReadOnlyList<ShaderDiagnostic> Check(string name) =>
        ShaderLint.Check(new DirectoryBackgroundSource(_fixture.FolderPath(name)), name);

    private static BackgroundSettings Shader(string name) => BackgroundFixture.Parse($$"""{ "type": "shader", "name": "{{name}}", "scale": 1 }""");

    // SHADER012

    [Theory]
    [InlineData("my waves")]
    [InlineData("wäves")]
    [InlineData("a.b")]
    public void An_invalid_folder_name_is_one_012_and_the_rest_is_still_checked(string name)
    {
        _fixture.WriteShader(name, Red);
        File.Delete(Path.Combine(_fixture.FolderPath(name), "sample.png"));

        var found = Check(name);

        var folder = Assert.Single(found, d => d.Code == ShaderCodes.FolderName);
        Assert.Equal(ShaderSeverity.Error, folder.Severity);
        Assert.Contains($"'{name}'", folder.Message);
        Assert.Contains("letters, digits", folder.Message);
        Assert.Equal(ShaderCodes.FolderName, found[0].Code);
        Assert.Contains(found, d => d.Code == ShaderCodes.SampleMissing);
    }

    [Fact]
    public void A_valid_folder_name_has_no_012()
    {
        _fixture.WriteShader("my-waves_2", Red);

        Assert.Empty(Check("my-waves_2"));
    }

    [Fact]
    public void An_invalid_name_does_not_hide_a_missing_manifest()
    {
        Directory.CreateDirectory(_fixture.FolderPath("two words"));

        Assert.Equal([ShaderCodes.FolderName, ShaderCodes.ManifestMissing], Check("two words").Select(d => d.Code));
    }

    [Theory]
    [InlineData("plasma")]
    [InlineData("aurora")]
    [InlineData("gradient")]
    [InlineData("julia")]
    public void The_built_ins_have_no_name_finding(string name)
    {
        Assert.Empty(ShaderLint.Check(new EmbeddedBackgroundSource(name), name));
    }

    // ShaderStore.TryRead

    [Fact]
    public void A_folder_outside_any_backgrounds_directory_is_read_by_its_leaf()
    {
        _fixture.WriteShader("waves", Red);

        var load = ShaderStore.TryRead(_fixture.FolderPath("waves"));

        Assert.NotNull(load.Package);
        Assert.Equal("waves", load.Package.Name);
        Assert.False(load.Package.IsBuiltIn);
        Assert.Equal(Red, load.Package.EntryText);
    }

    [Fact]
    public void A_folder_named_like_a_built_in_is_read_as_the_folder()
    {
        _fixture.WriteShader("plasma", Blue);

        var load = ShaderStore.TryRead(_fixture.FolderPath("plasma"));

        Assert.NotNull(load.Package);
        Assert.False(load.Package.IsBuiltIn);
        Assert.Equal(Blue, load.Package.EntryText);
    }

    [Fact]
    public void A_missing_folder_named_like_a_built_in_is_not_replaced_by_it()
    {
        var load = ShaderStore.TryRead(Path.Combine(_fixture.Root, "nowhere", "plasma"));

        Assert.Null(load.Package);
        Assert.Contains("does not exist", load.Failure);
    }

    [Fact]
    public void A_trailing_separator_does_not_change_the_name()
    {
        _fixture.WriteShader("waves", Red);

        var load = ShaderStore.TryRead(_fixture.FolderPath("waves") + Path.DirectorySeparatorChar);

        Assert.Equal("waves", load.Package?.Name);
    }

    [Fact]
    public void A_folder_without_a_manifest_is_the_001_failure()
    {
        Directory.CreateDirectory(_fixture.FolderPath("bare"));
        File.WriteAllText(Path.Combine(_fixture.FolderPath("bare"), "bare.sksl"), Red);

        var load = ShaderStore.TryRead(_fixture.FolderPath("bare"));

        Assert.Null(load.Package);
        Assert.Equal(ShaderCodes.ManifestMissing, load.Code);
    }

    [Fact]
    public void Reading_a_folder_logs_nothing()
    {
        _fixture.WriteShader("waves", Red);
        ShaderStore.TryRead(_fixture.FolderPath("waves"));
        ShaderStore.TryRead(Path.Combine(_fixture.Root, "nowhere"));

        Assert.Equal("", _fixture.Log);
    }

    // Rendering a package

    [Fact]
    public void The_folder_named_plasma_renders_its_own_shader_not_the_built_in()
    {
        _fixture.WriteShader("plasma", Blue);
        var package = ShaderStore.TryRead(_fixture.FolderPath("plasma")).Package!;

        using var result = BackgroundRender.Render(package, Shader("plasma"), _fixture.Theme(), 20, 10);

        Assert.NotNull(result.Bitmap);
        Assert.Equal(new SKColor(0, 0, 255), result.Bitmap.GetPixel(10, 5));
        Assert.True(BackgroundFixture.IsFlat(result.Bitmap));
    }

    [Fact]
    public void Rendering_a_package_that_does_not_compile_returns_the_reason()
    {
        _fixture.WriteShader("broken", "half4 main(float2 p) { return half4(1, 0, 0, 1) }");
        var package = ShaderStore.TryRead(_fixture.FolderPath("broken")).Package!;

        using var result = BackgroundRender.Render(package, Shader("broken"), _fixture.Theme(), 10, 10);

        Assert.Null(result.Bitmap);
        Assert.Contains("does not compile", result.Failure);
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void Rendering_a_package_and_by_name_give_the_same_picture()
    {
        _fixture.WriteShader("waves", "uniform float iTime;\nhalf4 main(float2 p) { return half4(fract(p.x / 20 + iTime), 0, 1, 1); }");
        var package = ShaderStore.TryRead(_fixture.FolderPath("waves")).Package!;

        using var byName = BackgroundRender.Render(Shader("waves"), _fixture.Theme(), _fixture.Store, 40, 30);
        using var byPackage = BackgroundRender.Render(package, Shader("waves"), _fixture.Theme(), 40, 30);

        Assert.True(BackgroundFixture.Same(byName.Bitmap!, byPackage.Bitmap!));
        Assert.False(BackgroundFixture.IsFlat(byPackage.Bitmap!));
    }

    // SHADER070

    [Fact]
    public void A_shader_that_draws_nothing_gets_a_070_warning()
    {
        _fixture.WriteShader("empty", Empty);
        var package = ShaderStore.TryRead(_fixture.FolderPath("empty")).Package!;

        using var result = BackgroundRender.Render(package, Shader("empty"), _fixture.Theme(), 12, 12);

        Assert.NotNull(result.Bitmap);
        var blank = Assert.Single(result.Diagnostics, d => d.Code == ShaderCodes.BlankRender);
        Assert.Equal(ShaderSeverity.Warning, blank.Severity);
    }

    [Fact]
    public void The_by_name_render_reports_a_blank_picture_too()
    {
        _fixture.WriteShader("empty", Empty);

        using var result = BackgroundRender.Render(Shader("empty"), _fixture.Theme(), _fixture.Store, 12, 12);

        Assert.Contains(result.Diagnostics, d => d.Code == ShaderCodes.BlankRender);
    }

    [Fact]
    public void A_shader_that_draws_something_has_no_070_even_when_flat()
    {
        _fixture.WriteShader("red", Red);
        var package = ShaderStore.TryRead(_fixture.FolderPath("red")).Package!;

        using var result = BackgroundRender.Render(package, Shader("red"), _fixture.Theme(), 12, 12);

        Assert.DoesNotContain(result.Diagnostics, d => d.Code == ShaderCodes.BlankRender);
    }

    [Theory]
    [InlineData("plasma")]
    [InlineData("aurora")]
    [InlineData("gradient")]
    [InlineData("julia")]
    public void The_built_ins_render_without_070(string name)
    {
        using var result = BackgroundRender.Render(Shader(name), _fixture.Theme(), _fixture.Store, 54, 96);

        Assert.NotNull(result.Bitmap);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == ShaderCodes.BlankRender);
    }

    [Fact]
    public void The_lint_never_returns_070()
    {
        _fixture.WriteShader("empty", Empty);

        Assert.DoesNotContain(Check("empty"), d => d.Code == ShaderCodes.BlankRender);
    }

    // Paths that leave the folder, on every platform

    [Theory]
    [InlineData("..\\\\other\\\\x.sksl")]
    [InlineData("../other/x.sksl")]
    [InlineData("sub/..\\\\..\\\\x.sksl")]
    [InlineData("sub\\\\../../x.sksl")]
    public void An_entry_that_leaves_the_folder_is_011_however_it_is_written(string entry)
    {
        _fixture.WriteShader("t", Red);
        File.WriteAllText(Path.Combine(_fixture.Root, "backgrounds", "x.sksl"), Red);
        _fixture.WriteFolder("t", new Dictionary<string, string>
        {
            ["manifest.json"] = $$"""{ "manifest": 1, "entry": "{{entry}}", "title": "t", "description": "d", "samples": [{ "file": "sample.png" }] }""",
        });

        Assert.Contains(Check("t"), d => d.Code == ShaderCodes.EntryLeavesFolder);
    }

    [Theory]
    [InlineData("..\\\\other\\\\x.png")]
    [InlineData("../other/x.png")]
    [InlineData("sub/..\\\\..\\\\x.png")]
    [InlineData("sub\\\\../../x.png")]
    public void A_sample_that_leaves_the_folder_is_052_however_it_is_written(string file)
    {
        _fixture.WriteShader("t", Red);
        _fixture.WriteFolder("t", new Dictionary<string, string>
        {
            ["manifest.json"] = $$"""{ "manifest": 1, "title": "t", "description": "d", "samples": [{ "file": "{{file}}" }] }""",
        });

        Assert.Contains(Check("t"), d => d.Code == ShaderCodes.SampleLeavesFolder);
    }

    // The log listener

    [Fact]
    public void A_listener_gets_the_lines_and_no_file_is_written()
    {
        _fixture.WriteShader("waves", "uniform float zoom;\nhalf4 main(float2 p) { return half4(zoom, 0, 0, 1); }");
        var package = ShaderStore.TryRead(_fixture.FolderPath("waves")).Package!;
        var settings = BackgroundFixture.Parse("""{ "type": "shader", "name": "waves", "zoom": "big", "nope": 1 }""");
        var lines = new List<(string Level, string Message)>();
        UrDeckLog.Listener = (level, message) => lines.Add((level, message));
        try
        {
            using var result = BackgroundRender.Render(package, settings, _fixture.Theme(), 8, 8);
            Assert.NotNull(result.Bitmap);
        }
        finally
        {
            UrDeckLog.Listener = null;
        }

        Assert.Contains(lines, l => l.Level == "WARN" && l.Message.Contains("'zoom' \"big\" must be", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Message.Contains("'nope' is not a parameter", StringComparison.Ordinal));
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void Without_a_listener_the_log_file_is_written_as_before()
    {
        UrDeckLog.Warn("hello");

        Assert.Contains("WARN hello", _fixture.Log);
    }

    [Fact]
    public void The_built_in_theme_names_load_as_themselves()
    {
        var themes = new ThemeStore(Path.Combine(_fixture.Root, "none"));

        foreach (string name in ThemeStore.BuiltInNames)
        {
            Assert.Equal(name, themes.Load(name).Name);
            Assert.Equal(name, ThemeStore.LoadBuiltIn(name)?.Name);
        }

        Assert.Null(ThemeStore.LoadBuiltIn("nosuch"));
    }

    [Theory]
    [InlineData("speed", true)]
    [InlineData("scale", true)]
    [InlineData("colors", true)]
    [InlineData("name", true)]
    [InlineData("iTime", true)]
    [InlineData("zoom", false)]
    public void Host_keys_are_public_for_tools(string key, bool host)
    {
        Assert.Equal(host, HostKeys.IsHostKey(key));
    }
}
