// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Cli.Tests.Support;
using Xunit;

namespace UrDeck.Cli.Tests;

public sealed class UpdateManifestTests : IDisposable
{
    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    private string ManifestPath(string folder = "waves") => _work.At(folder + "/manifest.json");

    private JsonDocument Manifest(string folder = "waves") => JsonDocument.Parse(File.ReadAllText(ManifestPath(folder)));

    private void WriteManifest(string text, string shader = Workspace.Red)
    {
        Directory.CreateDirectory(_work.At("waves"));
        File.WriteAllText(_work.At("waves/waves.sksl"), shader);
        File.WriteAllText(ManifestPath(), text);
    }

    private static JsonElement Sample(JsonDocument manifest, int index = 0) => manifest.RootElement.GetProperty("samples")[index];

    // What is recorded

    [Fact]
    public void The_first_sample_is_added_with_how_it_was_made_and_validate_stops_warning_about_samples()
    {
        _work.Background("waves", sample: false);
        File.WriteAllText(ManifestPath(), "{ \"manifest\": 1, \"title\": \"Waves\", \"description\": \"x\" }");
        Assert.Contains("SHADER050", Workspace.Invoke("shader", "validate", "waves").Out);

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--update-manifest");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.Equal("wrote waves/sample.png (270x480)\nadded sample.png in the samples of waves/manifest.json\n", run.Out);
        using var manifest = Manifest();
        var sample = Sample(manifest);
        Assert.Equal(1, manifest.RootElement.GetProperty("samples").GetArrayLength());
        Assert.Equal("image", sample.GetProperty("type").GetString());
        Assert.Equal("sample.png", sample.GetProperty("file").GetString());
        var settings = sample.GetProperty("settings");
        Assert.Equal("270x480", settings.GetProperty("size").GetString());
        Assert.Equal(10, settings.GetProperty("time").GetInt32());
        Assert.Equal("default-dark", settings.GetProperty("theme").GetString());
        Assert.False(settings.TryGetProperty("colors", out _));
        Assert.False(settings.TryGetProperty("parameters", out _));
        Assert.DoesNotContain("SHADER050", Workspace.Invoke("shader", "validate", "waves").Out);
    }

    [Fact]
    public void Rendering_the_same_file_again_updates_its_entry_and_keeps_the_rest_of_it()
    {
        _work.Background("waves", Workspace.Level);
        File.WriteAllText(ManifestPath(),
            "{ \"manifest\": 1, \"title\": \"Waves\", \"description\": \"x\", \"parameters\": { \"level\": { \"label\": \"L\", \"description\": \"d\" } },"
            + " \"samples\": [ { \"type\": \"image\", \"file\": \"sample.png\", \"order\": 2, \"caption\": \"Mine\", \"extra\": true, \"settings\": { \"size\": \"1x1\" } } ] }");

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--size", "20x30", "--time", "2.5",
            "--theme", "glass", "--colors", "#112233,#445566", "--param", "level=1", "--update-manifest");

        Assert.Equal("", run.Error);
        Assert.Equal(0, run.Code);
        Assert.Contains("updated sample.png in the samples of waves/manifest.json", run.Out);
        using var manifest = Manifest();
        Assert.Equal(1, manifest.RootElement.GetProperty("samples").GetArrayLength());
        var sample = Sample(manifest);
        Assert.Equal(["type", "file", "order", "caption", "extra", "settings"], sample.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Mine", sample.GetProperty("caption").GetString());
        Assert.Equal(2, sample.GetProperty("order").GetInt32());
        Assert.True(sample.GetProperty("extra").GetBoolean());
        var settings = sample.GetProperty("settings");
        Assert.Equal(["size", "time", "theme", "colors", "parameters"], settings.EnumerateObject().Select(p => p.Name));
        Assert.Equal("20x30", settings.GetProperty("size").GetString());
        Assert.Equal(2.5, settings.GetProperty("time").GetDouble());
        Assert.Equal("glass", settings.GetProperty("theme").GetString());
        Assert.Equal(["#112233", "#445566"], settings.GetProperty("colors").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(1, settings.GetProperty("parameters").GetProperty("level").GetInt32());
    }

    [Fact]
    public void The_settings_of_an_entry_are_replaced_not_merged()
    {
        _work.Background("waves", Workspace.Level);
        Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--param", "level=1", "--update-manifest");

        Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--update-manifest");

        using var manifest = Manifest();
        Assert.False(Sample(manifest).GetProperty("settings").TryGetProperty("parameters", out _));
    }

    [Fact]
    public void A_picture_not_yet_listed_is_added_at_the_end()
    {
        _work.Background("waves");

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/second.png", "--update-manifest");

        Assert.Equal(0, run.Code);
        using var manifest = Manifest();
        Assert.Equal(["sample.png", "second.png"], manifest.RootElement.GetProperty("samples").EnumerateArray().Select(s => s.GetProperty("file").GetString()));
    }

    [Fact]
    public void The_rest_of_the_manifest_is_kept_in_order()
    {
        WriteManifest("{ \"zeta\": [3, 1, 2], \"manifest\": 1, \"rating\": 5, \"description\": \"Kept\", \"title\": \"T\", \"samples\": [] }");

        Assert.Equal(0, Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--update-manifest").Code);

        using var manifest = Manifest();
        Assert.Equal(["zeta", "manifest", "rating", "description", "title", "samples"], manifest.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Kept", manifest.RootElement.GetProperty("description").GetString());
        Assert.Equal(5, manifest.RootElement.GetProperty("rating").GetInt32());
        Assert.Equal([3, 1, 2], manifest.RootElement.GetProperty("zeta").EnumerateArray().Select(e => e.GetInt32()));
    }

    [Fact]
    public void A_manifest_without_samples_gets_the_key_at_the_end()
    {
        WriteManifest("{ \"manifest\": 1, \"title\": \"T\" }");

        Assert.Equal(0, Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--update-manifest").Code);

        using var manifest = Manifest();
        Assert.Equal(["manifest", "title", "samples"], manifest.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void The_manifest_is_written_as_indented_json_with_a_final_newline_and_no_temporary_file()
    {
        WriteManifest("{ \"manifest\": 1 }");

        Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--update-manifest");

        Assert.Equal(
            "{\n  \"manifest\": 1,\n  \"samples\": [\n    {\n      \"type\": \"image\",\n      \"file\": \"sample.png\",\n      \"settings\": {\n"
            + "        \"size\": \"270x480\",\n        \"time\": 10,\n        \"theme\": \"default-dark\"\n      }\n    }\n  ]\n}\n",
            File.ReadAllText(ManifestPath()));
        Assert.DoesNotContain(Directory.GetFiles(_work.At("waves")), f => f.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void A_sample_in_a_subfolder_is_named_with_forward_slashes()
    {
        WriteManifest("{ \"manifest\": 1 }");
        Directory.CreateDirectory(_work.At("waves/samples"));

        var run = Workspace.Invoke("shader", "render", "waves", "-o", Path.Combine("waves", "samples", "a.png"), "--update-manifest");

        Assert.Equal(0, run.Code);
        using var manifest = Manifest();
        Assert.Equal("samples/a.png", Sample(manifest).GetProperty("file").GetString());
    }

    [Fact]
    public void An_entry_written_with_backslashes_is_found_and_updated()
    {
        WriteManifest("{ \"manifest\": 1, \"samples\": [ { \"file\": \"samples\\\\a.png\", \"caption\": \"c\" } ] }");
        Directory.CreateDirectory(_work.At("waves/samples"));

        Assert.Equal(0, Workspace.Invoke("shader", "render", "waves", "-o", Path.Combine("waves", "samples", "a.png"), "--update-manifest").Code);

        using var manifest = Manifest();
        Assert.Equal(1, manifest.RootElement.GetProperty("samples").GetArrayLength());
        Assert.Equal("c", Sample(manifest).GetProperty("caption").GetString());
        Assert.True(Sample(manifest).TryGetProperty("settings", out _));
    }

    [Fact]
    public void A_blank_picture_is_recorded_and_still_warns()
    {
        _work.Background("waves", Workspace.Blank, sample: false);

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--update-manifest");

        Assert.Equal(0, run.Code);
        Assert.Contains("SHADER070", run.Out);
        using var manifest = Manifest();
        Assert.Contains(manifest.RootElement.GetProperty("samples").EnumerateArray(), s => s.GetProperty("file").GetString() == "sample.png");
    }

    [Fact]
    public void A_blank_picture_that_fails_the_run_is_still_recorded()
    {
        _work.Background("waves", Workspace.Blank, sample: false);

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/blank.png", "--update-manifest", "--fail-on-warnings");

        Assert.Equal(1, run.Code);
        using var manifest = Manifest();
        Assert.Contains(manifest.RootElement.GetProperty("samples").EnumerateArray(), s => s.GetProperty("file").GetString() == "blank.png");
    }

    // Refusals

    [Theory]
    [InlineData("{ // my notes\n \"manifest\": 1 }", "a comment")]
    [InlineData("{ \"manifest\": 1, /* x */ \"title\": \"T\" }", "a comment")]
    [InlineData("{ \"manifest\": 1, \"title\": \"T\", }", "a trailing comma")]
    [InlineData("{ \"manifest\": 1, \"title\": \"A\", \"title\": \"B\" }", "the key 'title' twice")]
    public void A_manifest_a_rewrite_would_damage_is_refused_before_anything_is_written(string text, string loss)
    {
        WriteManifest(text);

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--size", "20x30", "--param", "level=2", "--update-manifest");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains(loss, run.Error);
        Assert.Contains("\"file\": \"sample.png\"", run.Error);
        Assert.Contains("\"size\": \"20x30\"", run.Error);
        Assert.Contains("\"level\": 2", run.Error);
        Assert.False(File.Exists(_work.At("waves/sample.png")));
        Assert.Equal(text, File.ReadAllText(ManifestPath()));
    }

    [Fact]
    public void Samples_that_is_not_an_array_is_refused()
    {
        WriteManifest("{ \"manifest\": 1, \"samples\": {} }");

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--update-manifest");

        Assert.Equal(2, run.Code);
        Assert.Contains("'samples' is not an array", run.Error);
        Assert.False(File.Exists(_work.At("waves/sample.png")));
        Assert.Equal("{ \"manifest\": 1, \"samples\": {} }", File.ReadAllText(ManifestPath()));
    }

    [Fact]
    public void The_refusal_names_every_loss()
    {
        WriteManifest("{ // c\n \"manifest\": 1, }");

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--update-manifest");

        Assert.Contains("a comment", run.Error);
        Assert.Contains("a trailing comma", run.Error);
    }

    [Theory]
    [InlineData("../sample.png")]
    [InlineData("sample.png")]
    public void An_output_outside_the_folder_renders_nothing(string output)
    {
        _work.Background("waves");
        string before = File.ReadAllText(ManifestPath());

        var run = Workspace.Invoke("shader", "render", "waves", "-o", output, "--update-manifest");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("inside the background folder", run.Error);
        Assert.False(File.Exists(_work.At(output)));
        Assert.Equal(before, File.ReadAllText(ManifestPath()));
    }

    [Fact]
    public void An_output_that_is_the_manifest_is_refused()
    {
        _work.Background("waves");
        string before = File.ReadAllText(ManifestPath());

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/manifest.json", "--update-manifest");

        Assert.Equal(2, run.Code);
        Assert.Equal(before, File.ReadAllText(ManifestPath()));
    }

    [Fact]
    public void No_picture_means_no_update()
    {
        _work.Background("waves", "half4 main(float2 p) { return half4(oops; }");
        string before = File.ReadAllText(ManifestPath());

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/new.png", "--update-manifest");

        Assert.Equal(1, run.Code);
        Assert.False(File.Exists(_work.At("waves/new.png")));
        Assert.Equal(before, File.ReadAllText(ManifestPath()));
    }

    [Fact]
    public void A_missing_manifest_is_the_renders_own_failure()
    {
        Directory.CreateDirectory(_work.At("waves"));
        File.WriteAllText(_work.At("waves/waves.sksl"), Workspace.Red);

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--update-manifest");
        var plain = Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png");

        Assert.Equal(1, run.Code);
        Assert.Equal(plain.Code, run.Code);
        Assert.Equal(plain.Error, run.Error);
        Assert.False(File.Exists(_work.At("waves/manifest.json")));
        Assert.False(File.Exists(_work.At("waves/sample.png")));
    }

    [Fact]
    public void A_manifest_that_is_not_json_is_the_renders_own_failure_and_stays()
    {
        WriteManifest("not json");

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png", "--update-manifest");

        Assert.Equal(1, run.Code);
        Assert.Equal("not json", File.ReadAllText(ManifestPath()));
    }

    [Fact]
    public void Without_the_option_the_manifest_is_never_touched()
    {
        WriteManifest("{ // mine\n \"manifest\": 1 }");

        var run = Workspace.Invoke("shader", "render", "waves", "-o", "waves/sample.png");

        Assert.Equal(0, run.Code);
        Assert.Equal("{ // mine\n \"manifest\": 1 }", File.ReadAllText(ManifestPath()));
    }
}
