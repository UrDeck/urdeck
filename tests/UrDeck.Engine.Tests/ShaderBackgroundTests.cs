// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ShaderBackgroundTests : IDisposable
{
    private const string Tint = """
        uniform float4 tint;   // default: #ff0000
        half4 main(float2 p) { return half4(tint); }
        """;

    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static string Json(string name, string extra = "") =>
        $$"""{ "type": "shader", "name": "{{name}}"{{(extra.Length == 0 ? "" : ", " + extra)}} }""";

    private SKColor Pixel(string name, string extra = "")
    {
        using var background = _fixture.Create(Json(name, extra));
        Assert.NotNull(background);
        using var bitmap = BackgroundFixture.Paint(background, 8, 8);
        return bitmap.GetPixel(0, 0);
    }

    [Theory]
    [MemberData(nameof(BuiltInShaderTests.Names), MemberType = typeof(BuiltInShaderTests))]
    public void Each_built_in_compiles_and_paints_an_opaque_picture(string name)
    {
        using var background = _fixture.Create(Json(name, "\"scale\": 1"));

        Assert.NotNull(background);
        Assert.True(background.IsMoving);
        using var bitmap = BackgroundFixture.Paint(background, 64, 256, 3);
        Assert.False(BackgroundFixture.IsFlat(bitmap));
        for (int y = 0; y < bitmap.Height; y += 5)
            for (int x = 0; x < bitmap.Width; x += 5)
                Assert.Equal((byte)255, bitmap.GetPixel(x, y).Alpha);
        Assert.Equal("", _fixture.Log);
    }

    [Theory]
    [MemberData(nameof(BuiltInShaderTests.Names), MemberType = typeof(BuiltInShaderTests))]
    public void Each_built_in_changes_with_time(string name)
    {
        using var background = _fixture.Create(Json(name));
        Assert.NotNull(background);

        using var early = BackgroundFixture.Paint(background, 64, 256, 1);
        using var late = BackgroundFixture.Paint(background, 64, 256, 40);

        Assert.False(BackgroundFixture.Same(early, late));
    }

    [Fact]
    public void A_built_in_follows_the_theme()
    {
        using var dark = _fixture.Create(Json("plasma"), "default-dark");
        using var light = _fixture.Create(Json("plasma"), "default-light");

        using var a = BackgroundFixture.Paint(dark!, 32, 64, 2);
        using var b = BackgroundFixture.Paint(light!, 32, 64, 2);

        Assert.False(BackgroundFixture.Same(a, b));
    }

    [Fact]
    public void A_parameter_default_applies()
    {
        _fixture.WriteShader("tinted", Tint);

        Assert.Equal(new SKColor(255, 0, 0), Pixel("tinted"));
    }

    [Fact]
    public void A_configuration_value_overrides_the_default()
    {
        _fixture.WriteShader("level", "uniform float level;   // default: 0.25\nhalf4 main(float2 p) { return half4(level, 0, 0, 1); }");

        Assert.InRange(Pixel("level").Red, 63, 65);
        Assert.Equal(255, Pixel("level", "\"level\": 1").Red);
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void A_colour_parameter_arrives_as_straight_rgba()
    {
        _fixture.WriteShader("tinted", Tint);

        Assert.Equal(new SKColor(255, 136, 0), Pixel("tinted", "\"tint\": \"#ff8800\""));
    }

    [Fact]
    public void A_vector_parameter_takes_an_array()
    {
        _fixture.WriteShader("pair", "uniform float2 pair;   // default: 0, 1\nhalf4 main(float2 p) { return half4(pair.x, pair.y, 0, 1); }");

        Assert.Equal(new SKColor(0, 255, 0), Pixel("pair"));
        Assert.Equal(new SKColor(255, 0, 0), Pixel("pair", "\"pair\": [1, 0]"));
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void A_three_component_colour_drops_the_alpha()
    {
        _fixture.WriteShader("rgb", "uniform half3 rgb;   // default: #336699\nhalf4 main(float2 p) { return half4(rgb, 1); }");

        Assert.Equal(new SKColor(0x33, 0x66, 0x99), Pixel("rgb"));
    }

    [Fact]
    public void The_palette_reaches_the_four_colour_inputs()
    {
        _fixture.WriteShader("quarters", """
            uniform float2 iResolution;
            uniform float4 iColor0;
            uniform float4 iColor1;
            uniform float4 iColor2;
            uniform float4 iColor3;
            half4 main(float2 p) {
                float x = p.x / iResolution.x;
                return x < 0.25 ? half4(iColor0) : (x < 0.5 ? half4(iColor1) : (x < 0.75 ? half4(iColor2) : half4(iColor3)));
            }
            """);

        using var background = _fixture.Create(Json("quarters", "\"colors\": [\"#ff0000\", \"#00ff00\", \"#0000ff\", \"#ffff00\"]"));
        using var bitmap = BackgroundFixture.Paint(background!, 40, 4);

        Assert.Equal(new SKColor(255, 0, 0), bitmap.GetPixel(2, 1));
        Assert.Equal(new SKColor(0, 255, 0), bitmap.GetPixel(12, 1));
        Assert.Equal(new SKColor(0, 0, 255), bitmap.GetPixel(22, 1));
        Assert.Equal(new SKColor(255, 255, 0), bitmap.GetPixel(32, 1));
    }

    [Fact]
    public void A_shader_without_uniforms_is_valid_and_still()
    {
        _fixture.WriteShader("blue", "half4 main(float2 p) { return half4(0, 0, 1, 1); }");

        using var background = _fixture.Create(Json("blue"));

        Assert.NotNull(background);
        Assert.False(background.IsMoving);
        using var bitmap = BackgroundFixture.Paint(background, 4, 4);
        Assert.Equal(new SKColor(0, 0, 255), bitmap.GetPixel(1, 1));
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void A_key_that_names_no_parameter_warns_and_the_rest_is_shown()
    {
        _fixture.WriteShader("tinted", Tint);

        Assert.Equal(new SKColor(255, 0, 0), Pixel("tinted", "\"densty\": 3"));
        Assert.Contains("'densty' is not a parameter of the shader", _fixture.Log);
    }

    [Fact]
    public void A_value_of_the_wrong_form_warns_and_keeps_the_default()
    {
        _fixture.WriteShader("level", "uniform float level;   // default: 0.25\nhalf4 main(float2 p) { return half4(level, 0, 0, 1); }");

        Assert.InRange(Pixel("level", "\"level\": \"steep\"").Red, 63, 65);
        Assert.Contains("'level' \"steep\" must be a number", _fixture.Log);
    }

    [Fact]
    public void A_key_that_names_a_host_input_warns()
    {
        _fixture.WriteShader("clocked", "uniform float iTime;\nhalf4 main(float2 p) { return half4(iTime, 0, 0, 1); }");

        Assert.Equal(new SKColor(0, 0, 0), Pixel("clocked", "\"iTime\": 5"));
        Assert.Contains("'iTime' is an input the host sets", _fixture.Log);
    }

    [Fact]
    public void An_unsupported_uniform_type_warns_and_stays_zero()
    {
        _fixture.WriteShader("counted", "uniform int count;\nhalf4 main(float2 p) { return half4(1, 0, 0, 1); }");

        Assert.Equal(new SKColor(255, 0, 0), Pixel("counted"));
        Assert.Contains("uniform 'count' has type 'int'", _fixture.Log);
    }

    [Fact]
    public void A_default_that_does_not_fit_warns()
    {
        _fixture.WriteShader("odd", "uniform float2 pair;   // default: 1\nhalf4 main(float2 p) { return half4(pair.x, pair.y, 0, 1); }");

        Assert.Equal(new SKColor(0, 0, 0), Pixel("odd"));
        Assert.Contains("the default '1' of 'pair'", _fixture.Log);
    }

    [Fact]
    public void A_syntax_error_fails_with_the_compilers_message()
    {
        _fixture.WriteShader("broken", "half4 main(float2 p) { return half4(1, 0, 0, 1) }");

        Assert.Null(_fixture.Create(Json("broken")));
        Assert.Contains("Background 'broken': SHADER020 the shader does not compile:", _fixture.Log);
    }

    [Fact]
    public void A_child_shader_makes_the_file_unusable()
    {
        _fixture.WriteShader("child", "uniform shader image;\nhalf4 main(float2 p) { return image.eval(p); }");

        Assert.Null(_fixture.Create(Json("child")));
        Assert.Contains("child shader", _fixture.Log);
    }

    [Fact]
    public void An_unknown_name_fails_with_a_warning_naming_it()
    {
        Assert.Null(_fixture.Create(Json("nonesuch")));
        Assert.Contains("no shader named 'nonesuch'", _fixture.Log);
    }

    [Fact]
    public void A_user_file_replaces_a_built_in_of_the_same_name()
    {
        _fixture.WriteShader("plasma", "half4 main(float2 p) { return half4(0, 1, 0, 1); }");

        Assert.Equal(new SKColor(0, 255, 0), Pixel("plasma"));
        Assert.False(_fixture.Store.Load("plasma")!.IsBuiltIn);
    }

    [Fact]
    public void A_name_that_leaves_the_folder_reads_nothing()
    {
        File.WriteAllText(Path.Combine(_fixture.Root, "secret.sksl"), "half4 main(float2 p) { return half4(1); }");
        Directory.CreateDirectory(Path.Combine(_fixture.Root, "backgrounds"));

        Assert.Null(_fixture.Store.Load("../secret"));
        Assert.Null(_fixture.Store.Load("..\\secret"));
        Assert.Null(_fixture.Store.Load("a/b"));
        Assert.Contains("may only contain letters, digits", _fixture.Log);
    }

    [Fact]
    public void A_missing_folder_still_serves_the_built_ins()
    {
        Assert.False(Directory.Exists(Path.Combine(_fixture.Root, "backgrounds")));
        foreach (string name in ShaderStore.BuiltInNames)
            Assert.True(_fixture.Store.Load(name)!.IsBuiltIn);
    }

    [Fact]
    public void An_edited_user_shader_is_read_again()
    {
        _fixture.WriteShader("live", "half4 main(float2 p) { return half4(1, 0, 0, 1); }");
        Assert.Equal(new SKColor(255, 0, 0), Pixel("live"));

        _fixture.WriteShader("live", "half4 main(float2 p) { return half4(0, 0, 1, 1); }");
        Assert.Equal(new SKColor(0, 0, 255), Pixel("live"));
    }

    [Fact]
    public void Declarations_read_aliases_defaults_and_colours()
    {
        var declarations = ShaderDeclarations.Parse("""
            uniform vec2 a;       // default: 1.5, -2
            uniform half3 b;      // default: #ff0000
            uniform float4 c;     // default: #80ff0000
            uniform float d;
            // uniform float hidden;
            uniform shader e;
            """, "test");

        Assert.Equal(["a", "b", "c", "d"], declarations.Uniforms.Select(u => u.Name));
        Assert.Equal([2, 3, 4, 1], declarations.Uniforms.Select(u => u.Components));
        Assert.Equal([1.5f, -2f], declarations.Uniforms[0].Default!);
        Assert.Equal([1f, 0f, 0f], declarations.Uniforms[1].Default!);
        Assert.Equal(0x80 / 255f, declarations.Uniforms[2].Default![3], 4);
        Assert.Null(declarations.Uniforms[3].Default);
        Assert.True(declarations.HasChildShader);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(14399.5, 1, 14399.5)]
    [InlineData(14400, 1, 0)]
    [InlineData(14401, 1, 1)]
    [InlineData(100, 0, 0)]
    [InlineData(7200, 2, 0)]
    [InlineData(7201, 2, 2)]
    [InlineData(3, 2, 6)]
    public void The_clock_wraps_scaled_time(double elapsed, double speed, double expected)
    {
        Assert.Equal(expected, BackgroundClock.Wrap(elapsed, speed), 3);
    }

    [Fact]
    public void The_clock_stays_precise_after_three_days()
    {
        double three = 3 * 24 * 3600;

        float a = BackgroundClock.Wrap(three, 1);
        float b = BackgroundClock.Wrap(three + (1 / 30.0), 1);

        Assert.True(b - a is > 0.03f and < 0.04f);
    }
}
