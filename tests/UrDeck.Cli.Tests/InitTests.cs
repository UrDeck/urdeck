// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Cli.Tests.Support;
using Xunit;

namespace UrDeck.Cli.Tests;

public sealed class InitTests : IDisposable
{
    private const string Body = "half4 main(float2 p) { return half4(1, 0, 0, 1); }\n";

    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    private string Loose(string name, string declarations = "", string parent = "")
    {
        string path = _work.At(Path.Combine(parent, name + ".sksl"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, declarations + (declarations.Length == 0 ? "" : "\n") + Body);
        return path;
    }

    private JsonDocument Manifest(string folder) => JsonDocument.Parse(File.ReadAllText(_work.At(folder + "/manifest.json")));

    private static JsonElement Parameter(JsonDocument manifest, string name) => manifest.RootElement.GetProperty("parameters").GetProperty(name);

    // The name function

    [Theory]
    [InlineData("zoom", "Zoom")]
    [InlineData("bandWidth", "Band width")]
    [InlineData("band_width", "Band width")]
    [InlineData("band-width", "Band width")]
    [InlineData("iColorMix", "I color mix")]
    [InlineData("x", "X")]
    [InlineData("speed2", "Speed2")]
    [InlineData("level2Up", "Level2 up")]
    [InlineData("RGBColor", "Rgbcolor")]
    [InlineData("__a__b__", "A b")]
    [InlineData("WAVES", "Waves")]
    public void A_name_becomes_words(string name, string expected) => Assert.Equal(expected, ManifestStub.Words(name));

    // init on a file

    [Fact]
    public void A_loose_shader_gets_a_folder_a_manifest_and_is_moved_in()
    {
        string loose = Loose("waves", "uniform float density;");

        var run = Workspace.Invoke("shader", "init", "waves.sksl");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.False(File.Exists(loose));
        Assert.True(File.Exists(_work.At("waves/waves.sksl")));
        Assert.Equal("uniform float density;\n" + Body, File.ReadAllText(_work.At("waves/waves.sksl")));
        using var manifest = Manifest("waves");
        Assert.Equal(1, manifest.RootElement.GetProperty("manifest").GetInt32());
        Assert.Equal("Waves", manifest.RootElement.GetProperty("title").GetString());
        Assert.Equal("", manifest.RootElement.GetProperty("description").GetString());
        Assert.Equal("Density", Parameter(manifest, "density").GetProperty("label").GetString());
        Assert.Equal("", Parameter(manifest, "density").GetProperty("description").GetString());
    }

    [Fact]
    public void The_output_names_the_folder_the_files_and_the_next_command()
    {
        Loose("waves", "uniform float density;");

        var run = Workspace.Invoke("shader", "init", "waves.sksl");

        Assert.Equal(
            "made waves/ and moved waves.sksl to waves/waves.sksl\n"
            + "wrote waves/manifest.json with 1 parameter\n"
            + "next: urdeck-cli shader validate waves\n",
            run.Out);
    }

    [Fact]
    public void The_manifest_keys_come_in_the_documented_order()
    {
        Loose("waves", "uniform float density;\nlayout(color) uniform half4 tint;");

        Workspace.Invoke("shader", "init", "waves.sksl");

        Assert.Equal(
            "{\n"
            + $"  \"$schema\": \"{ManifestStub.Schema}\",\n"
            + "  \"manifest\": 1,\n"
            + "  \"title\": \"Waves\",\n"
            + "  \"description\": \"\",\n"
            + "  \"parameters\": {\n"
            + "    \"density\": {\n      \"label\": \"Density\",\n      \"description\": \"\"\n    },\n"
            + "    \"tint\": {\n      \"label\": \"Tint\",\n      \"description\": \"\",\n      \"kind\": \"color\"\n    }\n"
            + "  }\n"
            + "}\n",
            File.ReadAllText(_work.At("waves/manifest.json")));
    }

    [Fact]
    public void The_schema_line_is_the_one_the_built_in_manifests_use()
    {
        foreach (string folder in Directory.GetDirectories(Workspace.BuiltIns()))
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
            Assert.Equal(ManifestStub.Schema, manifest.RootElement.GetProperty("$schema").GetString());
        }
    }

    [Fact]
    public void Parameters_come_in_the_shaders_order()
    {
        Loose("waves", "uniform float zeta;\nuniform float alpha;\nuniform float mid;");

        Workspace.Invoke("shader", "init", "waves.sksl");

        using var manifest = Manifest("waves");
        Assert.Equal(["zeta", "alpha", "mid"], manifest.RootElement.GetProperty("parameters").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void A_shader_with_no_parameters_gets_an_empty_object()
    {
        Loose("waves");

        var run = Workspace.Invoke("shader", "init", "waves.sksl");

        Assert.Equal(0, run.Code);
        Assert.Contains("with 0 parameters", run.Out);
        using var manifest = Manifest("waves");
        Assert.Empty(manifest.RootElement.GetProperty("parameters").EnumerateObject());
    }

    [Fact]
    public void Validate_after_init_has_no_errors_and_only_the_to_do_warnings()
    {
        Loose("waves", "uniform float density;");
        Workspace.Invoke("shader", "init", "waves.sksl");

        var run = Workspace.Invoke("shader", "validate", "waves");

        Assert.Equal(0, run.Code);
        Assert.Contains("0 errors", run.Out);
        foreach (string code in new[] { "SHADER041", "SHADER042", "SHADER050" })
            Assert.Contains(code, run.Out);
        var codes = System.Text.RegularExpressions.Regex.Matches(run.Out, "SHADER\\d{3}").Select(m => m.Value).ToHashSet();
        Assert.Equal(["SHADER041", "SHADER042", "SHADER050"], codes.Order());
    }

    [Fact]
    public void A_folder_with_only_the_shader_gets_its_manifest_in_place()
    {
        Directory.CreateDirectory(_work.At("waves"));
        File.WriteAllText(_work.At("waves/waves.sksl"), "uniform float density;\n" + Body);

        var run = Workspace.Invoke("shader", "init", "waves");

        Assert.Equal(0, run.Code);
        Assert.Equal("wrote waves/manifest.json with 1 parameter\nnext: urdeck-cli shader validate waves\n", run.Out);
        Assert.True(File.Exists(_work.At("waves/waves.sksl")));
        Assert.True(File.Exists(_work.At("waves/manifest.json")));
    }

    [Fact]
    public void An_existing_folder_without_the_shader_or_a_manifest_takes_the_loose_file()
    {
        Loose("waves");
        Directory.CreateDirectory(_work.At("waves"));
        File.WriteAllText(_work.At("waves/notes.txt"), "keep");

        var run = Workspace.Invoke("shader", "init", "waves.sksl");

        Assert.Equal(0, run.Code);
        Assert.True(File.Exists(_work.At("waves/waves.sksl")));
        Assert.True(File.Exists(_work.At("waves/manifest.json")));
        Assert.Equal("keep", File.ReadAllText(_work.At("waves/notes.txt")));
        Assert.False(File.Exists(_work.At("waves.sksl")));
    }

    // The colour guess

    [Theory]
    [InlineData("uniform float3 tint;   // default: #ff8800", true)]
    [InlineData("uniform float4 glow;   // default: #ff880080", true)]
    [InlineData("layout(color) uniform half4 glow;", true)]
    [InlineData("uniform float3 skyColor;", true)]
    [InlineData("uniform float3 sky_colour;", true)]
    [InlineData("uniform float4 TINT;", true)]
    [InlineData("uniform float3 offset;   // default: 0, 0, 1", false)]
    [InlineData("uniform float3 offset;", false)]
    [InlineData("uniform float colorCount;", false)]
    [InlineData("uniform float2 colorShift;", false)]
    public void A_colour_is_recognised_by_default_layout_or_name(string declaration, bool color)
    {
        Loose("waves", declaration);

        Assert.Equal(0, Workspace.Invoke("shader", "init", "waves.sksl").Code);

        using var manifest = Manifest("waves");
        var entry = manifest.RootElement.GetProperty("parameters").EnumerateObject().Single().Value;
        Assert.Equal(color, entry.TryGetProperty("kind", out var kind) && kind.GetString() == "color");
    }

    [Fact]
    public void Host_inputs_get_no_entry()
    {
        Loose("waves", "uniform float iTime;\nuniform float2 iResolution;\nuniform float4 iColor0;\nuniform float density;");

        Workspace.Invoke("shader", "init", "waves.sksl");

        using var manifest = Manifest("waves");
        Assert.Equal(["density"], manifest.RootElement.GetProperty("parameters").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void A_uniform_that_cannot_be_set_gets_no_entry_and_is_named()
    {
        Loose("waves", "uniform int count;\nuniform float density;");

        var run = Workspace.Invoke("shader", "init", "waves.sksl");

        Assert.Equal(0, run.Code);
        Assert.Contains("note: no entry for 'count'", run.Out);
        using var manifest = Manifest("waves");
        Assert.Equal(["density"], manifest.RootElement.GetProperty("parameters").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Nothing_else_is_guessed()
    {
        Loose("waves", "uniform float density;   // default: 2");

        Workspace.Invoke("shader", "init", "waves.sksl");

        using var manifest = Manifest("waves");
        var keys = manifest.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["$schema", "manifest", "title", "description", "parameters"], keys);
        Assert.Equal(["label", "description"], Parameter(manifest, "density").EnumerateObject().Select(p => p.Name));
    }

    // Refusals

    [Fact]
    public void An_existing_manifest_is_left_alone()
    {
        Directory.CreateDirectory(_work.At("waves"));
        File.WriteAllText(_work.At("waves/waves.sksl"), Body);
        File.WriteAllText(_work.At("waves/manifest.json"), "// mine\n{ \"manifest\": 1 }");

        var run = Workspace.Invoke("shader", "init", "waves");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("manifest.json exists", run.Error);
        Assert.Equal("// mine\n{ \"manifest\": 1 }", File.ReadAllText(_work.At("waves/manifest.json")));
    }

    [Fact]
    public void A_loose_shader_beside_a_folder_with_a_manifest_is_refused_and_stays()
    {
        string loose = Loose("waves");
        Directory.CreateDirectory(_work.At("waves"));
        File.WriteAllText(_work.At("waves/manifest.json"), "{ \"manifest\": 1 }");

        var run = Workspace.Invoke("shader", "init", "waves.sksl");

        Assert.Equal(2, run.Code);
        Assert.True(File.Exists(loose));
        Assert.False(File.Exists(_work.At("waves/waves.sksl")));
        Assert.Equal("{ \"manifest\": 1 }", File.ReadAllText(_work.At("waves/manifest.json")));
    }

    [Fact]
    public void A_folder_that_already_holds_the_shader_is_refused_and_nothing_moves()
    {
        string loose = Loose("waves");
        Directory.CreateDirectory(_work.At("waves"));
        File.WriteAllText(_work.At("waves/waves.sksl"), "old");

        var run = Workspace.Invoke("shader", "init", "waves.sksl");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("nothing was moved", run.Error);
        Assert.True(File.Exists(loose));
        Assert.Equal("old", File.ReadAllText(_work.At("waves/waves.sksl")));
        Assert.False(File.Exists(_work.At("waves/manifest.json")));
    }

    [Fact]
    public void A_folder_without_its_shader_is_refused()
    {
        Directory.CreateDirectory(_work.At("waves"));
        File.WriteAllText(_work.At("waves/other.sksl"), Body);

        var run = Workspace.Invoke("shader", "init", "waves");

        Assert.Equal(2, run.Code);
        Assert.Contains("holds no waves.sksl", run.Error);
        Assert.False(File.Exists(_work.At("waves/manifest.json")));
    }

    [Theory]
    [InlineData("my waves.sksl")]
    [InlineData("wa.ves.sksl")]
    public void A_name_that_is_not_valid_creates_nothing(string file)
    {
        File.WriteAllText(_work.At(file), Body);

        var run = Workspace.Invoke("shader", "init", file);

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("not a valid background name", run.Error);
        Assert.Equal([_work.At(file)], Directory.GetFileSystemEntries(_work.Root));
    }

    [Fact]
    public void A_folder_whose_name_is_not_valid_is_refused()
    {
        Directory.CreateDirectory(_work.At("my waves"));
        File.WriteAllText(_work.At("my waves/my waves.sksl"), Body);

        var run = Workspace.Invoke("shader", "init", "my waves");

        Assert.Equal(2, run.Code);
        Assert.Contains("not a valid background name", run.Error);
        Assert.False(File.Exists(_work.At("my waves/manifest.json")));
    }

    [Fact]
    public void A_path_that_does_not_exist_or_is_not_a_shader_is_refused()
    {
        File.WriteAllText(_work.At("notes.txt"), "x");

        Assert.Equal(2, Workspace.Invoke("shader", "init", "nosuch.sksl").Code);
        var run = Workspace.Invoke("shader", "init", "notes.txt");

        Assert.Equal(2, run.Code);
        Assert.Contains("is not a .sksl file", run.Error);
    }

    [Fact]
    public void A_shader_that_does_not_compile_still_gets_its_manifest()
    {
        File.WriteAllText(_work.At("waves.sksl"), "uniform float density;\nhalf4 main(float2 p) { return half4(oops; }\n");

        var run = Workspace.Invoke("shader", "init", "waves.sksl");

        Assert.Equal(0, run.Code);
        using var manifest = Manifest("waves");
        Assert.Equal("Density", Parameter(manifest, "density").GetProperty("label").GetString());
    }

    // --output

    [Fact]
    public void Output_makes_the_folder_in_another_directory()
    {
        string loose = Loose("waves", "uniform float density;");
        Directory.CreateDirectory(_work.At("out"));

        var run = Workspace.Invoke("shader", "init", "waves.sksl", "--output", "out");

        Assert.Equal(0, run.Code);
        Assert.False(File.Exists(loose));
        Assert.True(File.Exists(_work.At("out/waves/waves.sksl")));
        Assert.True(File.Exists(_work.At("out/waves/manifest.json")));
        Assert.False(Directory.Exists(_work.At("waves")));
        Assert.Contains("next: urdeck-cli shader validate out/waves\n", run.Out);
    }

    [Fact]
    public void An_output_directory_that_does_not_exist_is_refused_and_nothing_moves()
    {
        string loose = Loose("waves");

        var run = Workspace.Invoke("shader", "init", "waves.sksl", "--output", "nosuch");

        Assert.Equal(2, run.Code);
        Assert.Contains("does not exist", run.Error);
        Assert.True(File.Exists(loose));
        Assert.False(Directory.Exists(_work.At("nosuch")));
    }

    [Fact]
    public void Output_with_a_folder_is_refused()
    {
        Directory.CreateDirectory(_work.At("waves"));
        Directory.CreateDirectory(_work.At("out"));
        File.WriteAllText(_work.At("waves/waves.sksl"), Body);

        var run = Workspace.Invoke("shader", "init", "waves", "--output", "out");

        Assert.Equal(2, run.Code);
        Assert.False(File.Exists(_work.At("waves/manifest.json")));
    }

    // The stubs of the built-in shaders

    [Theory]
    [InlineData("aurora")]
    [InlineData("gradient")]
    [InlineData("julia")]
    [InlineData("plasma")]
    public void A_stub_made_from_a_built_in_shader_lints_with_no_error_and_no_kind_findings(string name)
    {
        string copy = _work.At(name + ".sksl");
        File.Copy(Path.Combine(Workspace.BuiltIns(), name, name + ".sksl"), copy);

        Assert.Equal(0, Workspace.Invoke("shader", "init", name + ".sksl").Code);
        var run = Workspace.Invoke("shader", "validate", name);

        Assert.Equal(0, run.Code);
        Assert.Contains("0 errors", run.Out);
        foreach (string code in new[] { "SHADER031", "SHADER033", "SHADER034" })
            Assert.DoesNotContain(code, run.Out);
    }
}
