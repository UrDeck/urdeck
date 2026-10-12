// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class ShaderLintTests : IDisposable
{
    private const string Level = "uniform float level;   // default: 1\nhalf4 main(float2 p) { return half4(level, 0, 0, 1); }";

    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static string Manifest(string top = "", string parameter = "\"label\": \"Level\", \"description\": \"How red.\"",
        string samples = "[{ \"file\": \"sample.png\" }]") =>
        $$"""{ "manifest": 1, {{top}} "title": "Test", "description": "A test.", "parameters": { "level": { {{parameter}} } }, "samples": {{samples}} }""";

    // A background that lints clean, with the manifest replaced by the one given.
    private IReadOnlyList<ShaderDiagnostic> Check(string manifest, string shader = Level)
    {
        _fixture.WriteShader("t", shader);
        _fixture.WriteFolder("t", new Dictionary<string, string> { ["manifest.json"] = manifest });
        return ShaderLint.Check(new DirectoryBackgroundSource(_fixture.FolderPath("t")), "t");
    }

    private static void AssertOnly(string code, IReadOnlyList<ShaderDiagnostic> found)
    {
        Assert.Equal([code], found.Select(d => d.Code));
    }

    [Fact]
    public void A_clean_folder_returns_nothing()
    {
        Assert.Empty(Check(Manifest()));
    }

    [Fact]
    public void A_missing_manifest_is_001()
    {
        _fixture.WriteShader("t", Level);
        File.Delete(Path.Combine(_fixture.FolderPath("t"), "manifest.json"));

        var found = ShaderLint.Check(new DirectoryBackgroundSource(_fixture.FolderPath("t")), "t");

        AssertOnly("SHADER001", found);
        Assert.Equal(ShaderSeverity.Error, found[0].Severity);
    }

    [Theory]
    [InlineData("{ \"manifest\": 1,", "SHADER002")]
    [InlineData("[1, 2]", "SHADER002")]
    [InlineData("{ \"title\": \"x\" }", "SHADER003")]
    [InlineData("{ \"manifest\": \"1\" }", "SHADER003")]
    [InlineData("{ \"manifest\": 1.5 }", "SHADER003")]
    [InlineData("{ \"manifest\": 2 }", "SHADER003")]
    public void A_manifest_that_cannot_be_read_is_one_error(string manifest, string code)
    {
        var found = Check(manifest);

        AssertOnly(code, found);
        Assert.Equal(ShaderSeverity.Error, found[0].Severity);
    }

    [Fact]
    public void The_unsupported_version_names_the_version()
    {
        Assert.Contains("version 1", Check("{ \"manifest\": 2 }")[0].Message);
    }

    [Fact]
    public void Comments_and_trailing_commas_are_accepted()
    {
        Assert.Empty(Check("// a note\n" + Manifest().TrimEnd('}').TrimEnd() + ", }"));
    }

    [Fact]
    public void An_unknown_key_is_004_in_the_manifest_a_parameter_and_a_sample()
    {
        AssertOnly("SHADER004", Check(Manifest("\"rating\": 5,")));
        AssertOnly("SHADER004", Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"colour\": 1")));
        AssertOnly("SHADER004", Check(Manifest(samples: "[{ \"file\": \"sample.png\", \"alt\": \"x\" }]")));
    }

    [Fact]
    public void A_value_of_the_wrong_type_is_005()
    {
        AssertOnly("SHADER005", Check(Manifest("\"author\": 3,")));
        AssertOnly("SHADER005", Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"min\": \"low\"")));
        AssertOnly("SHADER005", Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"hidden\": \"yes\"")));
        AssertOnly("SHADER005", Check(Manifest("\"tags\": [1],")));
    }

    [Fact]
    public void A_missing_entry_is_010()
    {
        AssertOnly("SHADER010", Check(Manifest("\"entry\": \"nothing.sksl\",")));
    }

    [Theory]
    [InlineData("../other/x.sksl")]
    [InlineData("..\\\\other\\\\x.sksl")]
    [InlineData("/etc/x.sksl")]
    [InlineData("C:\\\\x.sksl")]
    public void An_entry_outside_the_folder_is_011(string entry)
    {
        AssertOnly("SHADER011", Check(Manifest($"\"entry\": \"{entry}\",")));
    }

    [Fact]
    public void An_entry_inside_the_folder_is_used()
    {
        _fixture.WriteShader("t", Level);
        _fixture.WriteFolder("t", new Dictionary<string, string> { ["manifest.json"] = Manifest("\"entry\": \"sub/main.sksl\","), ["sub/main.sksl"] = Level });

        Assert.Empty(ShaderLint.Check(new DirectoryBackgroundSource(_fixture.FolderPath("t")), "t"));
    }

    [Fact]
    public void A_shader_that_does_not_compile_is_020_with_the_message()
    {
        var found = Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\""), "uniform float level;\nhalf4 main(float2 p) { return half4(1) }");

        Assert.Contains("SHADER020", found.Select(d => d.Code));
        Assert.Contains("does not compile", found.First(d => d.Code == "SHADER020").Message);
    }

    [Fact]
    public void A_child_shader_is_021()
    {
        var found = Check(Manifest(), "uniform shader image;\nuniform float level;\nhalf4 main(float2 p) { return image.eval(p); }");

        Assert.Contains("SHADER021", found.Select(d => d.Code));
    }

    [Fact]
    public void A_uniform_that_cannot_be_set_is_022()
    {
        AssertOnly("SHADER022", Check(Manifest(), "uniform int count;\n" + Level));
    }

    [Fact]
    public void A_default_that_cannot_be_read_is_023()
    {
        AssertOnly("SHADER023", Check(Manifest(), "uniform float level;   // default: lots\nhalf4 main(float2 p) { return half4(level, 0, 0, 1); }"));
    }

    [Fact]
    public void An_annotation_without_a_uniform_is_030()
    {
        var found = Check(Manifest().Replace("\"parameters\": {", "\"parameters\": { \"densty\": { \"description\": \"d\" },"));

        AssertOnly("SHADER030", found);
        Assert.Contains("densty", found[0].Message);
    }

    [Fact]
    public void A_uniform_without_an_annotation_is_031()
    {
        var found = Check(Manifest(), "uniform float level;   // default: 1\nuniform float density;\nhalf4 main(float2 p) { return half4(level, density, 0, 1); }");

        AssertOnly("SHADER031", found);
        Assert.Contains("density", found[0].Message);
    }

    [Theory]
    [InlineData("speed")]
    [InlineData("scale")]
    [InlineData("colors")]
    [InlineData("iTime")]
    [InlineData("iColor2")]
    public void An_annotation_of_a_host_key_is_032_error(string key)
    {
        var found = Check(Manifest().Replace("\"parameters\": {", $"\"parameters\": {{ \"{key}\": {{ \"description\": \"d\" }},"));

        AssertOnly("SHADER032", found);
        Assert.Equal(ShaderSeverity.Error, found[0].Severity);
    }

    [Fact]
    public void Kind_color_on_a_number_is_033_and_on_a_vector_is_fine()
    {
        AssertOnly("SHADER033", Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"kind\": \"color\"")));

        string vector = "uniform float4 level;   // default: #ff0000\nhalf4 main(float2 p) { return half4(level); }";
        Assert.Empty(Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"kind\": \"color\""), vector));
    }

    [Fact]
    public void An_unknown_kind_is_034()
    {
        AssertOnly("SHADER034", Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"kind\": \"vector\"")));
    }

    [Fact]
    public void A_range_that_does_not_fit_is_035()
    {
        AssertOnly("SHADER035", Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"min\": 3, \"max\": 1")));
        AssertOnly("SHADER035", Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"max\": 0.5")));
    }

    [Fact]
    public void A_missing_title_or_description_is_040_and_041()
    {
        Assert.Equal(["SHADER040"], Check(Manifest().Replace("\"title\": \"Test\", ", "")).Select(d => d.Code));
        Assert.Equal(["SHADER041"], Check(Manifest().Replace("\"description\": \"A test.\", ", "")).Select(d => d.Code));
    }

    [Fact]
    public void A_parameter_without_a_description_is_042_unless_hidden()
    {
        AssertOnly("SHADER042", Check(Manifest(parameter: "\"label\": \"L\"")));
        Assert.Empty(Check(Manifest(parameter: "\"label\": \"L\", \"hidden\": true")));
    }

    [Fact]
    public void No_samples_is_050()
    {
        AssertOnly("SHADER050", Check(Manifest(samples: "[]")));
    }

    [Fact]
    public void A_sample_file_that_is_missing_is_051_and_one_that_leaves_the_folder_is_052()
    {
        var missing = Check(Manifest(samples: "[{ \"file\": \"none.png\" }]"));
        AssertOnly("SHADER051", missing);
        Assert.Equal(ShaderSeverity.Error, missing[0].Severity);

        var outside = Check(Manifest(samples: "[{ \"file\": \"../sample.png\" }]"));
        AssertOnly("SHADER052", outside);
        Assert.Equal(ShaderSeverity.Error, outside[0].Severity);
    }

    [Fact]
    public void An_unknown_sample_type_is_053_and_video_is_accepted()
    {
        AssertOnly("SHADER053", Check(Manifest(samples: "[{ \"type\": \"gif\", \"file\": \"sample.png\" }]")));
        Assert.Empty(Check(Manifest(samples: "[{ \"type\": \"video\", \"file\": \"sample.png\" }]")));
    }

    [Fact]
    public void Layout_color_against_kind_number_is_060_info_and_agreeing_is_clean()
    {
        string shader = "layout(color) uniform half4 level;   // default: #ff8800\nhalf4 main(float2 p) { return level; }";

        var disagree = Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"kind\": \"number\""), shader);
        AssertOnly("SHADER060", disagree);
        Assert.Equal(ShaderSeverity.Info, disagree[0].Severity);

        Assert.Empty(Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"kind\": \"color\""), shader));
        Assert.Empty(Check(Manifest(), shader));
    }

    [Fact]
    public void Every_diagnostic_names_its_file()
    {
        var found = Check(Manifest(samples: "[{ \"file\": \"none.png\" }]", top: "\"rating\": 5,"));

        Assert.All(found, d => Assert.False(string.IsNullOrEmpty(d.File)));
        Assert.Equal("none.png", found.First(d => d.Code == "SHADER051").File);
        Assert.Equal("manifest.json", found.First(d => d.Code == "SHADER004").File);
    }

    [Theory]
    [InlineData("plasma")]
    [InlineData("aurora")]
    [InlineData("gradient")]
    [InlineData("julia")]
    public void The_built_ins_have_no_diagnostic_of_any_severity(string name)
    {
        Assert.Empty(ShaderLint.Check(new EmbeddedBackgroundSource(name), name));
    }

    [Fact]
    public void The_built_in_names_are_the_four_embedded_folders()
    {
        foreach (string name in ShaderStore.BuiltInNames)
        {
            var source = new EmbeddedBackgroundSource(name);
            Assert.True(source.Exists("manifest.json"));
            Assert.True(source.Exists(name + ".sksl"));
            Assert.True(source.Exists("sample.png"));
            Assert.False(source.Exists("../plasma/plasma.sksl"));
        }
    }

    [Fact]
    public void Hidden_and_range_checks_do_not_apply_without_a_range()
    {
        Assert.Empty(Check(Manifest(parameter: "\"label\": \"L\", \"description\": \"d\", \"step\": 0.5, \"unit\": \"x\"")));
    }

    [Fact]
    public void Loading_logs_one_line_per_diagnostic_and_none_for_an_unrelated_reload()
    {
        _fixture.WriteShader("t", Level);
        _fixture.WriteFolder("t", new Dictionary<string, string> { ["manifest.json"] = Manifest("\"rating\": 5,", "\"label\": \"L\"") });
        const string Json = """{ "type": "shader", "name": "t" }""";
        string Key(string json) => DeckBackground.RequestKey(BackgroundFixture.Parse(json), _fixture.Theme(), _fixture.Store, _fixture.Root)!;

        string before = Key(Json);
        using var background = _fixture.Create(Json);

        Assert.NotNull(background);
        string log = _fixture.Log;
        Assert.Equal(1, Count(log, "Background 't': SHADER004 "));
        Assert.Equal(1, Count(log, "Background 't': SHADER042 "));
        Assert.Contains("(manifest.json)", log);
        Assert.Equal(before, Key(Json));
        Assert.Equal(before, background.Key);
    }

    [Fact]
    public void Errors_that_stop_a_background_stop_it_and_the_others_do_not()
    {
        _fixture.WriteShader("t", Level);
        _fixture.WriteFolder("t", new Dictionary<string, string> { ["manifest.json"] = Manifest(samples: "[{ \"file\": \"none.png\" }]") });
        using var shown = _fixture.Create("""{ "type": "shader", "name": "t" }""");
        Assert.NotNull(shown);
        Assert.Contains("SHADER051", _fixture.Log);

        _fixture.WriteFolder("t", new Dictionary<string, string> { ["manifest.json"] = Manifest("\"entry\": \"gone.sksl\",") });
        Assert.Null(_fixture.Create("""{ "type": "shader", "name": "t" }"""));
        Assert.Contains("Background 't': SHADER010", _fixture.Log);
    }

    private static int Count(string text, string part)
    {
        int count = 0;
        for (int at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
