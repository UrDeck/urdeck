// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Cli.Tests.Support;
using UrDeck.Engine.Backgrounds;
using Xunit;

namespace UrDeck.Cli.Tests;

public sealed class ValidateTests : IDisposable
{
    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    [Fact]
    public void A_clean_folder_is_0_and_only_the_summary()
    {
        _work.Background("waves");

        var run = Workspace.Invoke("shader", "validate", "waves");

        Assert.Equal(0, run.Code);
        Assert.Equal("1 background checked: 0 errors, 0 warnings, 0 remarks.\n", run.Out);
        Assert.Equal("", run.Error);
    }

    [Fact]
    public void Warnings_only_are_printed_and_0()
    {
        _work.Background("waves", description: false);

        var run = Workspace.Invoke("shader", "validate", "waves");

        Assert.Equal(0, run.Code);
        Assert.Contains("SHADER041", run.Out);
    }

    [Fact]
    public void Warnings_fail_on_request()
    {
        _work.Background("waves", description: false);

        var run = Workspace.Invoke("shader", "validate", "waves", "--fail-on-warnings");

        Assert.Equal(1, run.Code);
        Assert.Contains("SHADER041", run.Out);
    }

    [Fact]
    public void An_error_is_1()
    {
        _work.Background("broken", "half4 main(float2 p) { return half4(1, 0, 0, 1) }");

        var run = Workspace.Invoke("shader", "validate", "broken");

        Assert.Equal(1, run.Code);
        Assert.Contains("error SHADER020:", run.Out);
    }

    [Fact]
    public void A_folder_that_does_not_exist_is_2_and_the_error_names_it()
    {
        var run = Workspace.Invoke("shader", "validate", "missing/dir");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("missing/dir", run.Error);
    }

    [Fact]
    public void A_file_in_place_of_the_folder_is_2()
    {
        File.WriteAllText("notes.txt", "x");

        var run = Workspace.Invoke("shader", "validate", "notes.txt");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
    }

    [Fact]
    public void Dot_checks_the_folder_the_tool_is_run_in_under_its_name()
    {
        string folder = _work.Background("waves", description: false);
        Directory.SetCurrentDirectory(folder);

        var run = Workspace.Invoke("shader", "validate", ".", "--format", "json");

        using var json = System.Text.Json.JsonDocument.Parse(run.Out);
        var background = json.RootElement.GetProperty("backgrounds")[0];
        Assert.Equal("waves", background.GetProperty("name").GetString());
        Assert.Equal("manifest.json", background.GetProperty("diagnostics")[0].GetProperty("file").GetString());
        Assert.Equal(0, run.Code);
    }

    [Fact]
    public void A_folder_with_only_a_shader_has_one_error_that_the_manifest_is_missing()
    {
        Directory.CreateDirectory("bare");
        File.WriteAllText("bare/bare.sksl", Workspace.Red);

        var run = Workspace.Invoke("shader", "validate", "bare");

        Assert.Equal(1, run.Code);
        Assert.Equal("bare/manifest.json: error SHADER001: manifest.json is missing; a background needs one (\"manifest\": 1).\n"
            + "1 background checked: 1 error, 0 warnings, 0 remarks.\n", run.Out);
    }

    [Fact]
    public void A_folder_name_that_is_not_a_name_is_012()
    {
        _work.Background("my waves");

        var run = Workspace.Invoke("shader", "validate", "my waves");

        Assert.Equal(1, run.Code);
        Assert.Contains("my waves: error SHADER012:", run.Out);
    }

    [Fact]
    public void The_findings_are_those_of_the_lint()
    {
        string folder = _work.Background("waves", Workspace.Level, description: false, sample: false);

        var run = Workspace.Invoke("shader", "validate", "waves", "--format", "json");

        var expected = ShaderLint.Check(new DirectoryBackgroundSource(folder), "waves");
        using var json = System.Text.Json.JsonDocument.Parse(run.Out);
        var found = json.RootElement.GetProperty("backgrounds")[0].GetProperty("diagnostics").EnumerateArray()
            .Select(d => (d.GetProperty("code").GetString()!, d.GetProperty("message").GetString()!));
        Assert.NotEmpty(expected);
        Assert.Equal(expected.Select(d => (d.Code, d.Message)), found);
    }

    // --all

    [Fact]
    public void All_checks_each_folder_in_name_order_and_the_worst_decides()
    {
        _work.Background("zeta", parent: "set");
        _work.Background("alpha", parent: "set");
        _work.Background("mid", "half4 main(float2 p) { return half4(1, 0, 0, 1) }", parent: "set");
        _work.Background("beta", parent: "set");
        File.WriteAllText("set/readme.txt", "not a folder");
        Directory.CreateDirectory("set/.git");

        var run = Workspace.Invoke("shader", "validate", "--all", "set");

        Assert.Equal(1, run.Code);
        string[] lines = run.Out.TrimEnd().Split('\n');
        Assert.StartsWith("set/mid/mid.sksl: error SHADER020:", lines[0]);
        Assert.Equal("4 backgrounds checked: 1 error, 0 warnings, 0 remarks.", lines[^1]);

        var json = Workspace.Invoke("shader", "validate", "--all", "set", "--format", "json");
        using var document = System.Text.Json.JsonDocument.Parse(json.Out);
        Assert.Equal(["alpha", "beta", "mid", "zeta"],
            document.RootElement.GetProperty("backgrounds").EnumerateArray().Select(b => b.GetProperty("name").GetString()));
    }

    [Fact]
    public void All_on_an_empty_directory_is_2()
    {
        Directory.CreateDirectory("empty");

        var run = Workspace.Invoke("shader", "validate", "--all", "empty");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("no backgrounds were found", run.Error);
    }

    [Fact]
    public void All_on_a_missing_directory_is_2()
    {
        var run = Workspace.Invoke("shader", "validate", "--all", "nowhere");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
    }

    [Fact]
    public void All_with_a_folder_is_2()
    {
        _work.Background("waves");

        var run = Workspace.Invoke("shader", "validate", "waves", "--all", ".");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("not both", run.Error);
    }

    [Fact]
    public void Neither_a_folder_nor_all_is_2()
    {
        var run = Workspace.Invoke("shader", "validate");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
    }

    [Fact]
    public void A_folder_without_a_manifest_among_the_others_is_a_finding_not_skipped()
    {
        _work.Background("good", parent: "set");
        Directory.CreateDirectory("set/stray");

        var run = Workspace.Invoke("shader", "validate", "--all", "set");

        Assert.Equal(1, run.Code);
        Assert.Contains("set/stray/manifest.json: error SHADER001:", run.Out);
    }

    [Fact]
    public void The_built_in_backgrounds_are_clean_even_with_fail_on_warnings()
    {
        var run = Workspace.Invoke("shader", "validate", "--all", Workspace.BuiltIns(), "--fail-on-warnings");

        Assert.Equal(0, run.Code);
        Assert.Equal("16 backgrounds checked: 0 errors, 0 warnings, 0 remarks.\n", run.Out);
    }

    [Theory]
    [InlineData("..\\\\other\\\\x.sksl")]
    [InlineData("../other/x.sksl")]
    public void An_entry_that_leaves_the_folder_is_refused_on_every_platform(string entry)
    {
        _work.Background("waves");
        File.WriteAllText("waves/manifest.json",
            $$"""{ "manifest": 1, "entry": "{{entry}}", "title": "t", "description": "d", "samples": [{ "file": "sample.png" }] }""");
        File.WriteAllText("other.sksl", Workspace.Red);

        var run = Workspace.Invoke("shader", "validate", "waves");

        Assert.Equal(1, run.Code);
        Assert.Contains("error SHADER011:", run.Out);
    }
}
