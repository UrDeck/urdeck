// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

public sealed class BackgroundPackageTests : IDisposable
{
    private const string Level = "uniform float level;   // default: 1\nhalf4 main(float2 p) { return half4(level / 10, 0, 0, 1); }";

    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static string Json(string name, string extra = "") =>
        $$"""{ "type": "shader", "name": "{{name}}"{{(extra.Length == 0 ? "" : ", " + extra)}} }""";

    private string Key(string name) =>
        DeckBackground.RequestKey(BackgroundFixture.Parse(Json(name)), _fixture.Theme(), _fixture.Store, _fixture.Root)!;

    private void WriteRanged(string range)
    {
        _fixture.WriteShader("r", Level);
        _fixture.WriteFolder("r", new Dictionary<string, string>
        {
            ["manifest.json"] = $$"""{ "manifest": 1, "title": "R", "description": "R.", "parameters": { "level": { "label": "L", "description": "d"{{range}} } }, "samples": [{ "file": "sample.png" }] }""",
        });
    }

    private int Red(string json)
    {
        using var background = _fixture.Create(json);
        Assert.NotNull(background);
        using var bitmap = BackgroundFixture.Paint(background, 4, 4);
        return bitmap.GetPixel(0, 0).Red;
    }

    // The paths

    [Theory]
    [InlineData("a.png", "a.png")]
    [InlineData("./a.png", "a.png")]
    [InlineData("sub/a.png", "sub/a.png")]
    [InlineData("sub\\a.png", "sub/a.png")]
    [InlineData("sub/../a.png", "a.png")]
    [InlineData("sub//a.png", "sub/a.png")]
    public void A_relative_path_inside_the_folder_is_normalised(string path, string expected)
    {
        Assert.Equal(expected, BackgroundPaths.Normalize(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("..")]
    [InlineData("../a.png")]
    [InlineData("..\\a.png")]
    [InlineData("sub/../../a.png")]
    [InlineData("sub\\..\\..\\a.png")]
    [InlineData("sub/..\\..\\a.png")]
    [InlineData("/a.png")]
    [InlineData("\\a.png")]
    [InlineData("C:\\a.png")]
    [InlineData("C:/a.png")]
    [InlineData(".")]
    public void A_rooted_or_escaping_path_is_refused(string path)
    {
        Assert.Null(BackgroundPaths.Normalize(path));
    }

    [Fact]
    public void A_directory_source_never_reads_outside_its_folder()
    {
        _fixture.WriteShader("t", Level);
        File.WriteAllText(Path.Combine(_fixture.Root, "backgrounds", "secret.txt"), "secret");
        var source = new DirectoryBackgroundSource(_fixture.FolderPath("t"));

        Assert.True(source.Exists("t.sksl"));
        Assert.False(source.Exists("../secret.txt"));
        Assert.Null(source.ReadText("../secret.txt"));
        Assert.Null(source.Open("..\\secret.txt"));
        Assert.False(source.Exists(Path.Combine(_fixture.Root, "backgrounds", "secret.txt")));
    }

    [Fact]
    public void A_file_an_editor_holds_open_can_be_read()
    {
        _fixture.WriteShader("t", Level);
        string path = Path.Combine(_fixture.FolderPath("t"), "t.sksl");

        using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        Assert.Equal(Level, new DirectoryBackgroundSource(_fixture.FolderPath("t")).ReadText("t.sksl"));
        Assert.NotNull(_fixture.Store.Load("t"));
    }

    // The store

    [Fact]
    public void A_folder_is_a_package_with_its_manifest_and_entry()
    {
        _fixture.WriteShader("waves", Level);

        var package = _fixture.Store.Load("waves");

        Assert.NotNull(package);
        Assert.False(package.IsBuiltIn);
        Assert.Equal("waves", package.Manifest.Title);
        Assert.Equal("waves.sksl", package.EntryPath);
        Assert.Equal(Level, package.EntryText);
        Assert.Equal(_fixture.FolderPath("waves"), package.Origin);
    }

    [Fact]
    public void The_manifest_names_another_entry()
    {
        _fixture.WriteShader("waves", Level);
        _fixture.WriteFolder("waves", new Dictionary<string, string>
        {
            ["manifest.json"] = """{ "manifest": 1, "entry": "main.sksl" }""",
            ["main.sksl"] = "half4 main(float2 p) { return half4(0, 1, 0, 1); }",
        });

        Assert.Equal("main.sksl", _fixture.Store.Load("waves")!.EntryPath);
        Assert.Equal(0, Red("""{ "type": "shader", "name": "waves" }"""));
    }

    [Fact]
    public void A_built_in_is_a_package_with_a_manifest()
    {
        var package = _fixture.Store.Load("plasma");

        Assert.NotNull(package);
        Assert.True(package.IsBuiltIn);
        Assert.Equal("built-in", package.Origin);
        Assert.Equal("Plasma", package.Manifest.Title);
        Assert.Equal(["angle", "zoom"], package.Manifest.Parameters.Keys.Order());
    }

    [Fact]
    public void A_user_folder_wins_over_a_built_in_and_a_broken_one_does_not_fall_back()
    {
        _fixture.WriteShader("plasma", Level);
        Assert.False(_fixture.Store.Load("plasma")!.IsBuiltIn);

        File.Delete(Path.Combine(_fixture.FolderPath("plasma"), "manifest.json"));
        Assert.Null(_fixture.Store.Load("plasma"));
    }

    [Fact]
    public void A_folder_with_only_the_shader_fails_and_the_log_says_what_to_add()
    {
        _fixture.WriteFolder("bare", new Dictionary<string, string> { ["bare.sksl"] = Level });

        Assert.Null(_fixture.Create(Json("bare")));

        string log = _fixture.Log;
        Assert.Contains("Background 'bare': SHADER001", log);
        Assert.Contains("manifest.json", log);
    }

    [Theory]
    [InlineData("{", "SHADER002")]
    [InlineData("{ \"manifest\": 2 }", "SHADER003")]
    [InlineData("{ \"manifest\": 1, \"entry\": \"../x.sksl\" }", "SHADER011")]
    [InlineData("{ \"manifest\": 1, \"entry\": \"nope.sksl\" }", "SHADER010")]
    public void A_background_that_cannot_load_logs_one_warning_with_the_code(string manifest, string code)
    {
        _fixture.WriteFolder("bad", new Dictionary<string, string> { ["bad.sksl"] = Level, ["manifest.json"] = manifest });

        Assert.Null(_fixture.Create(Json("bad")));

        Assert.Single(_fixture.Log.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains($"Background 'bad': {code}", _fixture.Log);
    }

    [Fact]
    public void A_loose_file_gets_the_hint_and_is_never_loaded()
    {
        Directory.CreateDirectory(Path.Combine(_fixture.Root, "backgrounds"));
        File.WriteAllText(Path.Combine(_fixture.Root, "backgrounds", "waves.sksl"), Level);

        Assert.Null(_fixture.Create(Json("waves")));

        string log = _fixture.Log;
        Assert.Contains("loose file", log);
        Assert.Contains("manifest.json", log);
        Assert.Contains(Path.Combine("backgrounds", "waves"), log);
    }

    [Fact]
    public void A_missing_backgrounds_folder_is_not_an_error_for_a_built_in()
    {
        Assert.False(Directory.Exists(Path.Combine(_fixture.Root, "backgrounds")));
        Assert.NotNull(_fixture.Store.Load("julia", warn: false));
        Assert.Equal("", _fixture.Log);
    }

    // The key

    [Fact]
    public void Editing_the_manifest_changes_the_key_and_an_unrelated_reload_does_not()
    {
        _fixture.WriteShader("k", Level);
        string before = Key("k");
        Assert.Equal(before, Key("k"));

        File.AppendAllText(Path.Combine(_fixture.FolderPath("k"), "manifest.json"), "\n// edited\n");
        string after = Key("k");

        Assert.NotEqual(before, after);
        Assert.Equal(after, Key("k"));
    }

    // Layout and the join

    [Theory]
    [InlineData("layout(color) uniform half4 x;")]
    [InlineData("layout(color)uniform half4 x;")]
    [InlineData("  layout( color ) uniform half4 x;")]
    [InlineData("layout(color, set = 0) uniform half4 x;")]
    public void A_layout_prefix_is_accepted_and_color_is_recorded(string line)
    {
        var uniform = Assert.Single(ShaderDeclarations.Parse(line, "t").Uniforms);

        Assert.Equal("x", uniform.Name);
        Assert.Equal(4, uniform.Components);
        Assert.True(uniform.IsColor);
    }

    [Fact]
    public void Another_layout_content_is_read_but_is_not_a_colour()
    {
        var uniform = Assert.Single(ShaderDeclarations.Parse("layout(binding = 1) uniform float x;   // default: 2", "t").Uniforms);

        Assert.False(uniform.IsColor);
        Assert.Equal([2f], uniform.Default!);
    }

    [Theory]
    [InlineData("uniform float3 x;   // default: #ff8800", true)]
    [InlineData("uniform float4 x;   // default: #ff880080", true)]
    [InlineData("uniform float3 x;   // default: 1, 0, 0", false)]
    [InlineData("uniform float3 x;", false)]
    [InlineData("uniform float x;   // default: #ff8800", false)]
    public void A_default_written_as_a_colour_is_recorded(string line, bool written)
    {
        var uniform = Assert.Single(ShaderDeclarations.Parse(line, "t").Uniforms);

        Assert.Equal(written, uniform.DefaultIsColor);
        Assert.False(uniform.IsColor);
    }

    [Fact]
    public void A_colour_default_on_a_float_is_still_a_bad_default()
    {
        var declarations = ShaderDeclarations.Parse("uniform float x;   // default: #ff8800", "t");

        Assert.Contains(declarations.Diagnostics, d => d.Code == ShaderCodes.BadDefault);
        Assert.Null(Assert.Single(declarations.Uniforms).Default);
    }

    [Fact]
    public void A_sample_keeps_its_settings_after_the_reader_returns()
    {
        var result = BackgroundManifestReader.Read(
            """{ "manifest": 1, "samples": [ { "file": "a.png", "settings": { "size": "270x480", "time": 10, "theme": "default", "note": "evening" } }, { "file": "b.png" } ] }""");

        var settings = result.Manifest!.Samples[0].Settings!.Value;
        Assert.Equal("270x480", settings.GetProperty("size").GetString());
        Assert.Equal(10, settings.GetProperty("time").GetInt32());
        Assert.Equal("default", settings.GetProperty("theme").GetString());
        Assert.Null(result.Manifest.Samples[1].Settings);
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("note", StringComparison.Ordinal));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Settings_that_are_not_an_object_stay_a_warning_and_give_null()
    {
        var result = BackgroundManifestReader.Read("""{ "manifest": 1, "samples": [ { "file": "a.png", "settings": 5 } ] }""");

        Assert.Null(result.Manifest!.Samples[0].Settings);
        Assert.Single(result.Diagnostics, d => d.Code == ShaderCodes.WrongType);
    }

    [Fact]
    public void A_line_that_is_not_a_declaration_is_not_read()
    {
        var declarations = ShaderDeclarations.Parse("layout(color) half4 x;\nlayout(color) uniform;\n// layout(color) uniform float y;", "t");

        Assert.Empty(declarations.Uniforms);
    }

    [Fact]
    public void A_layout_color_default_works_through_the_configuration()
    {
        _fixture.WriteShader("tint", "layout(color) uniform half4 tint;   // default: #ff0000\nhalf4 main(float2 p) { return tint; }");

        Assert.Equal(255, Red(Json("tint")));
        Assert.Equal(0, Red(Json("tint", "\"tint\": \"#000000\"")));
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void The_join_takes_the_manifest_kind_over_layout_and_hides_what_it_says()
    {
        var declarations = ShaderDeclarations.Parse("layout(color) uniform half4 a;\nlayout(color) uniform half4 b;\nuniform float c;\nuniform float speed;\nuniform float iTime;", "t");
        var manifest = BackgroundManifestReader.Read(
            """{ "manifest": 1, "parameters": { "b": { "kind": "number", "hidden": true }, "c": { "min": 1, "max": 2, "label": "C" } } }""").Manifest;

        var joined = BackgroundParameter.Join(declarations, manifest).ToDictionary(p => p.Name);

        Assert.Equal(["a", "b", "c"], joined.Keys.Order());
        Assert.Equal(ParameterKind.Color, joined["a"].Kind);
        Assert.Equal(ParameterKind.Number, joined["b"].Kind);
        Assert.True(joined["b"].Hidden);
        Assert.False(joined["a"].Hidden);
        Assert.Equal((1, 2, "C"), (joined["c"].Min, joined["c"].Max, joined["c"].Label));
    }

    // Ranges

    [Fact]
    public void A_value_in_range_is_used_and_one_out_of_range_keeps_the_default_with_one_warning()
    {
        WriteRanged(", \"min\": 0, \"max\": 3");

        Assert.Equal(51, Red(Json("r", "\"level\": 2")));
        Assert.Equal("", _fixture.Log);

        Assert.Equal(Red(Json("r")), Red(Json("r", "\"level\": 9")));
        Assert.Single(_fixture.Log.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("'level' 9 is outside the range 0 to 3; using 1", _fixture.Log);
    }

    [Fact]
    public void A_range_with_one_end_checks_that_end_and_no_range_checks_nothing()
    {
        WriteRanged(", \"max\": 3");
        Assert.Equal(Red(Json("r")), Red(Json("r", "\"level\": 5")));
        Assert.Contains("at most 3", _fixture.Log);

        WriteRanged("");
        Assert.NotEqual(Red(Json("r")), Red(Json("r", "\"level\": 5")));
    }

    [Fact]
    public void A_vector_is_checked_per_component()
    {
        _fixture.WriteShader("v", "uniform float2 pair;   // default: 0, 0\nhalf4 main(float2 p) { return half4(pair.x / 10, pair.y / 10, 0, 1); }");
        _fixture.WriteFolder("v", new Dictionary<string, string>
        {
            ["manifest.json"] = """{ "manifest": 1, "parameters": { "pair": { "min": 0, "max": 5 } } }""",
        });

        using var inRange = _fixture.Create(Json("v", "\"pair\": [1, 5]"));
        using var bitmap = BackgroundFixture.Paint(inRange!, 2, 2);
        Assert.Equal(new SKColor(26, 128, 0), bitmap.GetPixel(0, 0));
        Assert.DoesNotContain("outside", _fixture.Log);

        Assert.Equal(0, Red(Json("v", "\"pair\": [1, 6]")));
        Assert.Contains("'pair' [1, 6] is outside the range 0 to 5", _fixture.Log);
    }

    [Fact]
    public void A_default_alone_is_never_range_checked_at_run_time()
    {
        WriteRanged(", \"max\": 0.5");

        using var background = _fixture.Create(Json("r"));

        Assert.NotNull(background);
        Assert.DoesNotContain("; using", _fixture.Log);
    }

    [Fact]
    public void A_hidden_parameter_can_still_be_set()
    {
        WriteRanged(", \"hidden\": true");

        Assert.NotEqual(Red(Json("r")), Red(Json("r", "\"level\": 7")));
    }

    // Rendering

    [Fact]
    public void Rendering_gives_a_picture_of_the_size_over_the_theme_colour()
    {
        _fixture.WriteShader("clear", "half4 main(float2 p) { return half4(0); }");
        var settings = BackgroundFixture.Parse(Json("clear"));
        var theme = _fixture.Theme();

        using var result = BackgroundRender.Render(settings, theme, _fixture.Store, 60, 40);

        Assert.NotNull(result.Bitmap);
        Assert.Equal((60, 40), (result.Bitmap.Width, result.Bitmap.Height));
        using var resolved = UrDeck.Engine.Themes.ThemeResolver.Resolve(theme, 15);
        Assert.Equal(resolved.Background, result.Bitmap.GetPixel(30, 20));
    }

    [Fact]
    public void Rendering_one_input_twice_gives_the_same_picture()
    {
        var settings = BackgroundFixture.Parse(Json("plasma", "\"scale\": 1"));

        using var a = BackgroundRender.Render(settings, _fixture.Theme(), _fixture.Store, 90, 160);
        using var b = BackgroundRender.Render(settings, _fixture.Theme(), _fixture.Store, 90, 160);

        Assert.True(BackgroundFixture.Same(a.Bitmap!, b.Bitmap!));
        Assert.False(BackgroundFixture.IsFlat(a.Bitmap!));
    }

    [Fact]
    public void Rendering_a_shader_that_does_not_compile_returns_the_reason_and_logs_nothing()
    {
        _fixture.WriteShader("broken", "half4 main(float2 p) { return half4(1, 0, 0, 1) }");

        using var result = BackgroundRender.Render(BackgroundFixture.Parse(Json("broken")), _fixture.Theme(), _fixture.Store, 10, 10);

        Assert.Null(result.Bitmap);
        Assert.Contains("does not compile", result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "SHADER020");
        Assert.Equal("", _fixture.Log);
    }

    [Fact]
    public void Rendering_a_name_that_is_not_there_returns_the_reason()
    {
        using var result = BackgroundRender.Render(BackgroundFixture.Parse(Json("nebula")), _fixture.Theme(), _fixture.Store, 10, 10);

        Assert.Null(result.Bitmap);
        Assert.Contains("no shader named 'nebula'", result.Failure);
    }

    // Pairing

    [Fact]
    public void Pairing_themes_are_read_in_order_without_a_diagnostic()
    {
        var result = BackgroundManifestReader.Read("""{ "manifest": 1, "pairsWith": { "themes": ["rigline", "carbon"] } }""");
        Assert.Equal(["rigline", "carbon"], result.Manifest!.PairsWith!.Themes);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Pairing_of_the_wrong_type_is_one_warning_and_no_pairing()
    {
        var result = BackgroundManifestReader.Read("""{ "manifest": 1, "pairsWith": { "themes": "carbon" } }""");
        Assert.Null(result.Manifest!.PairsWith);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ShaderCodes.WrongType, diagnostic.Code);
        Assert.Contains("pairsWith.themes", diagnostic.Message);

        Assert.Null(BackgroundManifestReader.Read("""{ "manifest": 1, "pairsWith": [] }""").Manifest!.PairsWith);
    }

    [Fact]
    public void An_unknown_key_inside_pairing_is_one_warning_and_the_themes_are_kept()
    {
        var result = BackgroundManifestReader.Read("""{ "manifest": 1, "pairsWith": { "themes": ["carbon"], "moods": ["calm"] } }""");
        Assert.Equal(["carbon"], result.Manifest!.PairsWith!.Themes);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ShaderCodes.UnknownKey, diagnostic.Code);
        Assert.Contains("moods", diagnostic.Message);
    }

    [Fact]
    public void A_manifest_without_pairing_has_none()
    {
        Assert.Null(BackgroundManifestReader.Read("""{ "manifest": 1 }""").Manifest!.PairsWith);
    }

    // The schema

    [Fact]
    public void The_schema_matches_the_readers_key_tables()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "urdeck.slnx")))
            root = Path.GetDirectoryName(root)!;

        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "schemas", "v1", "shader-manifest.json")));
        var top = schema.RootElement;

        static string[] Names(JsonElement properties) => properties.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
        static string[] Sorted(IEnumerable<string> names) => names.Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(Sorted(BackgroundManifestReader.TopKeys), Names(top.GetProperty("properties")));
        Assert.Equal(["manifest"], top.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
        Assert.True(top.GetProperty("additionalProperties").GetBoolean());

        Assert.Equal(Sorted(BackgroundManifestReader.PairsWithKeys),
            Names(top.GetProperty("properties").GetProperty("pairsWith").GetProperty("properties")));

        var parameter = top.GetProperty("properties").GetProperty("parameters").GetProperty("additionalProperties");
        Assert.Equal(Sorted(BackgroundManifestReader.ParameterKeys), Names(parameter.GetProperty("properties")));
        Assert.Equal(Sorted(BackgroundManifestReader.Kinds),
            Sorted(parameter.GetProperty("properties").GetProperty("kind").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!)));

        var sample = top.GetProperty("properties").GetProperty("samples").GetProperty("items");
        Assert.Equal(Sorted(BackgroundManifestReader.SampleKeys), Names(sample.GetProperty("properties")));
        Assert.Equal(Sorted(BackgroundManifestReader.SampleTypes),
            Sorted(sample.GetProperty("properties").GetProperty("type").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!)));

        Assert.Equal(1, top.GetProperty("properties").GetProperty("manifest").GetProperty("const").GetInt32());

        foreach (string name in ShaderStore.BuiltInNames)
        {
            using var manifest = JsonDocument.Parse(new EmbeddedBackgroundSource(name).ReadText("manifest.json")!);
            Assert.Equal(top.GetProperty("$id").GetString(), manifest.RootElement.GetProperty("$schema").GetString());
        }
    }
}
