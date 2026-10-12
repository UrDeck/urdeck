// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Cli.Tests.Support;
using Xunit;

namespace UrDeck.Cli.Tests;

public sealed class RenderTests : IDisposable
{
    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    private static SKColor Pixel(string file)
    {
        using var bitmap = Workspace.Decode(file);
        return bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
    }

    [Fact]
    public void A_picture_of_the_size_asked_for_is_written()
    {
        _work.Background("waves");

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "sample.png", "--size", "600x400");

        Assert.Equal(0, run.Code);
        Assert.Equal("wrote sample.png (600x400)\n", run.Out);
        using var bitmap = Workspace.Decode("sample.png");
        Assert.Equal((600, 400), (bitmap.Width, bitmap.Height));
        Assert.Equal(new SKColor(255, 0, 0), bitmap.GetPixel(300, 200));
    }

    [Fact]
    public void The_default_size_is_270_by_480()
    {
        _work.Background("waves");

        Assert.Equal(0, Workspace.Invoke("shader", "render", "waves", "-o", "sample.png").Code);

        using var bitmap = Workspace.Decode("sample.png");
        Assert.Equal((270, 480), (bitmap.Width, bitmap.Height));
    }

    [Fact]
    public void A_parameter_changes_the_picture()
    {
        _work.Background("waves", Workspace.Level);

        Workspace.Invoke("shader", "render", "waves", "-o", "default.png", "--size", "8x8");
        var run = Workspace.Invoke("shader", "render", "waves", "-o", "level.png", "--size", "8x8", "--param", "level=1");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.NotEqual(Pixel("default.png"), Pixel("level.png"));
        Assert.Equal(new SKColor(255, 0, 0), Pixel("level.png"));
    }

    [Fact]
    public void Colors_and_theme_change_the_picture()
    {
        _work.Background("waves", Workspace.Palette);

        Workspace.Invoke("shader", "render", "waves", "-o", "dark.png", "--size", "8x8");
        Workspace.Invoke("shader", "render", "waves", "-o", "light.png", "--size", "8x8", "--theme", "default-light");
        var colors = Workspace.Invoke("shader", "render", "waves", "-o", "colors.png", "--size", "8x8", "--colors", "#00ff00,#0000ff");

        Assert.Equal(0, colors.Code);
        Assert.NotEqual(Pixel("dark.png"), Pixel("light.png"));
        Assert.Equal(new SKColor(0, 255, 0), Pixel("colors.png"));
    }

    [Fact]
    public void A_shader_that_does_not_compile_writes_no_file_and_is_1()
    {
        _work.Background("broken", "half4 main(float2 p) { return half4(1, 0, 0, 1) }");

        var run = Workspace.Invoke("shader", "render", "broken", "-o", "out.png");

        Assert.Equal(1, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("does not compile", run.Error);
        Assert.False(File.Exists("out.png"));
    }

    [Fact]
    public void A_folder_without_a_manifest_writes_no_file_and_is_1()
    {
        Directory.CreateDirectory("bare");
        File.WriteAllText("bare/bare.sksl", Workspace.Red);

        var run = Workspace.Invoke("shader", "render", "bare", "-o", "out.png");

        Assert.Equal(1, run.Code);
        Assert.Contains("SHADER001", run.Error);
        Assert.False(File.Exists("out.png"));
    }

    [Fact]
    public void A_blank_picture_is_written_and_reported_and_0()
    {
        _work.Background("empty", Workspace.Blank);

        var run = Workspace.Invoke("shader", "render", "empty", "-o", "out.png", "--size", "8x8");

        Assert.Equal(0, run.Code);
        Assert.Contains("empty: warning SHADER070:", run.Out);
        Assert.True(File.Exists("out.png"));
    }

    [Fact]
    public void A_blank_picture_fails_with_fail_on_warnings_and_is_still_written()
    {
        _work.Background("empty", Workspace.Blank);

        var run = Workspace.Invoke("shader", "render", "empty", "-o", "out.png", "--size", "8x8", "--fail-on-warnings");

        Assert.Equal(1, run.Code);
        Assert.Contains("SHADER070", run.Out);
        Assert.True(File.Exists("out.png"));
    }

    [Fact]
    public void Render_does_not_repeat_the_lints_warnings()
    {
        _work.Background("waves", description: false);

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "out.png", "--size", "8x8", "--fail-on-warnings");

        Assert.Equal(0, run.Code);
        Assert.DoesNotContain("SHADER041", run.Out + run.Error);
    }

    [Fact]
    public void A_missing_folder_whose_last_part_is_a_built_in_name_is_2_and_renders_nothing()
    {
        var run = Workspace.Invoke("shader", "render", "nowhere/plasma", "-o", "out.png");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("nowhere/plasma", run.Error);
        Assert.False(File.Exists("out.png"));
    }

    [Fact]
    public void A_folder_named_like_a_built_in_renders_its_own_shader()
    {
        _work.Background("plasma", Workspace.Red);

        Assert.Equal(0, Workspace.Invoke("shader", "render", "plasma", "-o", "out.png", "--size", "8x8").Code);

        Assert.Equal(new SKColor(255, 0, 0), Pixel("out.png"));
    }

    [Fact]
    public void An_unknown_theme_is_2_and_names_the_theme()
    {
        _work.Background("waves");

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "out.png", "--theme", "nosuch");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("'nosuch'", run.Error);
        Assert.False(File.Exists("out.png"));
    }

    [Fact]
    public void An_existing_output_file_is_replaced()
    {
        _work.Background("waves");
        File.WriteAllText("out.png", "not a picture");

        Assert.Equal(0, Workspace.Invoke("shader", "render", "waves", "-o", "out.png", "--size", "4x4").Code);

        Assert.Equal(new SKColor(255, 0, 0), Pixel("out.png"));
    }

    [Fact]
    public void A_refused_parameter_value_is_reported_as_the_configuration_reports_it()
    {
        _work.Background("waves", Workspace.Level);

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "out.png", "--size", "4x4", "--param", "level=high", "--param", "nope=1");

        Assert.Equal(0, run.Code);
        Assert.Contains("warning: Background 'waves': 'level' \"high\" must be", run.Error);
        Assert.Contains("warning: Background 'waves': 'nope' is not a parameter of the shader.", run.Error);
        Assert.True(File.Exists("out.png"));
        Assert.False(File.Exists(Path.Combine(AppContext.BaseDirectory, "urdeck.log")));
    }

    [Theory]
    [InlineData("level=0.5")]
    [InlineData("level=1e0")]
    public void A_number_is_read_as_json(string param)
    {
        _work.Background("waves", Workspace.Level);

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "out.png", "--size", "4x4", "--param", param);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
    }

    [Fact]
    public void An_array_and_a_colour_are_read_as_the_configuration_reads_them()
    {
        _work.Background("waves", "uniform float2 offset;\nuniform float3 tint;   // layout(color)\nhalf4 main(float2 p) { return half4(offset.x, offset.y, tint.b, 1); }");

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "out.png", "--size", "4x4", "--param", "offset=[1,0]", "--param", "tint=#0000ff");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.Equal(new SKColor(255, 0, 255), Pixel("out.png"));
    }

    [Theory]
    [InlineData("--size", "270")]
    [InlineData("--size", "0x10")]
    [InlineData("--size", "10x0")]
    [InlineData("--size", "axb")]
    [InlineData("--size", "99999x10")]
    [InlineData("--param", "level")]
    [InlineData("--param", "=1")]
    [InlineData("--param", "speed=2")]
    [InlineData("--param", "scale=0.5")]
    [InlineData("--param", "iTime=3")]
    [InlineData("--param", "colors=[1]")]
    public void Malformed_options_are_2_and_write_nothing(string option, string value)
    {
        _work.Background("waves", Workspace.Level);

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "out.png", option, value);

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.StartsWith("error:", run.Error);
        Assert.False(File.Exists("out.png"));
    }

    [Fact]
    public void A_missing_folder_is_2()
    {
        var run = Workspace.Invoke("shader", "render", "missing", "-o", "out.png");

        Assert.Equal(2, run.Code);
        Assert.Contains("missing", run.Error);
    }

    [Fact]
    public void An_output_that_cannot_be_written_is_2()
    {
        _work.Background("waves");
        Directory.CreateDirectory("taken");

        var inMissingFolder = Workspace.Invoke("shader", "render", "waves", "-o", "no/such/dir/out.png");
        var onAFolder = Workspace.Invoke("shader", "render", "waves", "-o", "taken");

        Assert.Equal(2, inMissingFolder.Code);
        Assert.Equal(2, onAFolder.Code);
        Assert.Equal("", inMissingFolder.Out + onAFolder.Out);
    }

    [Fact]
    public void A_folder_name_that_is_not_a_name_is_2()
    {
        _work.Background("my waves");

        var run = Workspace.Invoke("shader", "render", "my waves", "-o", "out.png");

        Assert.Equal(2, run.Code);
        Assert.Contains("SHADER012", run.Error);
    }

    [Theory]
    [InlineData("plasma")]
    [InlineData("aurora")]
    [InlineData("gradient")]
    [InlineData("julia")]
    public void The_built_ins_render_with_fail_on_warnings(string name)
    {
        var run = Workspace.Invoke("shader", "render", Path.Combine(Workspace.BuiltIns(), name), "-o", name + ".png", "--fail-on-warnings");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.True(File.Exists(name + ".png"));
    }

    [Fact]
    public void The_same_input_gives_the_same_picture()
    {
        string plasma = Path.Combine(Workspace.BuiltIns(), "plasma");

        Workspace.Invoke("shader", "render", plasma, "-o", "a.png", "--size", "40x60");
        Workspace.Invoke("shader", "render", plasma, "-o", "b.png", "--size", "40x60");

        Assert.Equal(File.ReadAllBytes("a.png"), File.ReadAllBytes("b.png"));
    }

    [Fact]
    public void A_different_time_gives_a_different_picture()
    {
        string plasma = Path.Combine(Workspace.BuiltIns(), "plasma");

        Workspace.Invoke("shader", "render", plasma, "-o", "a.png", "--size", "40x60", "--time", "1");
        Workspace.Invoke("shader", "render", plasma, "-o", "b.png", "--size", "40x60", "--time", "5");

        Assert.NotEqual(File.ReadAllBytes("a.png"), File.ReadAllBytes("b.png"));
    }
}
