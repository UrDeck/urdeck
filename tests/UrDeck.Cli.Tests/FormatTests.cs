// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Cli.Tests.Support;
using UrDeck.Engine.Backgrounds;
using Xunit;

namespace UrDeck.Cli.Tests;

public sealed class FormatTests : IDisposable
{
    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    // A folder with one error (the sample file is missing) and one warning (no description).
    private void Mixed() => _work.Background("waves", description: false, sample: false);

    [Fact]
    public void Text_has_one_line_per_finding_and_a_summary()
    {
        _work.Background("waves", description: false);

        var run = Workspace.Invoke("shader", "validate", "waves");

        Assert.Equal("waves/manifest.json: warning SHADER041: the manifest has no description.\n"
            + "1 background checked: 0 errors, 1 warning, 0 remarks.\n", run.Out);
    }

    [Fact]
    public void Text_is_the_default_format()
    {
        Mixed();

        Assert.Equal(Workspace.Invoke("shader", "validate", "waves").Out, Workspace.Invoke("shader", "validate", "waves", "--format", "text").Out);
    }

    [Fact]
    public void A_multi_line_message_stays_on_one_line_in_text()
    {
        _work.Background("broken", "half4 main(float2 p) {\n  return half4(1, 0, 0, 1)\n}");

        var run = Workspace.Invoke("shader", "validate", "broken");

        string[] lines = run.Out.TrimEnd().Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("broken/broken.sksl: error SHADER020:", lines[0]);
    }

    [Fact]
    public void Markdown_has_a_heading_and_a_table_for_each_background()
    {
        Mixed();
        _work.Background("clean");

        var run = Workspace.Invoke("shader", "validate", "--all", ".", "--format", "markdown");

        Assert.Contains("## clean\n\nNo findings.\n", run.Out);
        Assert.Contains("## waves\n\n| Severity | Code | File | Message |\n| --- | --- | --- | --- |\n", run.Out);
        Assert.Contains("| warning | SHADER041 | waves/manifest.json | the manifest has no description. |", run.Out);
        Assert.Contains("| error | SHADER051 | waves/sample.png |", run.Out);
        Assert.EndsWith("2 backgrounds checked: 1 error, 1 warning, 0 remarks.\n", run.Out);
    }

    [Fact]
    public void Markdown_escapes_a_bar_in_a_message()
    {
        var report = new BackgroundReport("x", Path.Combine(_work.Root, "x"), [new ShaderDiagnostic("SHADER999", ShaderSeverity.Info, "a | b", "manifest.json")]);
        using var writer = new StringWriter();

        ReportWriters.Write("markdown", [report], writer);

        Assert.Contains("a \\| b", writer.ToString());
    }

    [Fact]
    public void Json_is_the_whole_output_with_the_documented_shape()
    {
        Mixed();

        var run = Workspace.Invoke("shader", "validate", "waves", "--format", "json");

        Assert.Equal(1, run.Code);
        using var json = JsonDocument.Parse(run.Out);
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("urdeck-cli", root.GetProperty("tool").GetProperty("name").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("tool").GetProperty("version").GetString()));
        var background = Assert.Single(root.GetProperty("backgrounds").EnumerateArray());
        Assert.Equal("waves", background.GetProperty("name").GetString());
        Assert.Equal("waves", background.GetProperty("folder").GetString());
        var diagnostics = background.GetProperty("diagnostics").EnumerateArray().ToList();
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal(["code", "severity", "message", "file"], d.EnumerateObject().Select(p => p.Name)));
        Assert.Equal(("warning", "SHADER041"), (diagnostics[0].GetProperty("severity").GetString(), diagnostics[0].GetProperty("code").GetString()));
        Assert.Equal(("error", "SHADER051"), (diagnostics[1].GetProperty("severity").GetString(), diagnostics[1].GetProperty("code").GetString()));
        var summary = root.GetProperty("summary");
        Assert.Equal((1, 1, 0), (summary.GetProperty("errors").GetInt32(), summary.GetProperty("warnings").GetInt32(), summary.GetProperty("infos").GetInt32()));
        Assert.Equal("", run.Error);
    }

    [Fact]
    public void Sarif_has_a_rule_for_each_code_and_a_result_for_each_finding()
    {
        Mixed();

        var run = Workspace.Invoke("shader", "validate", "waves", "--format", "sarif");

        Assert.Equal(1, run.Code);
        using var json = JsonDocument.Parse(run.Out);
        var root = json.RootElement;
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        var sarifRun = Assert.Single(root.GetProperty("runs").EnumerateArray());
        var driver = sarifRun.GetProperty("tool").GetProperty("driver");
        Assert.Equal("urdeck-cli", driver.GetProperty("name").GetString());
        Assert.Equal(["SHADER041", "SHADER051"], driver.GetProperty("rules").EnumerateArray().Select(r => r.GetProperty("id").GetString()));
        var results = sarifRun.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal([("SHADER041", "warning"), ("SHADER051", "error")], results.Select(r => (r.GetProperty("ruleId").GetString()!, r.GetProperty("level").GetString()!)));
        var location = results[0].GetProperty("locations")[0].GetProperty("physicalLocation");
        Assert.Equal("waves/manifest.json", location.GetProperty("artifactLocation").GetProperty("uri").GetString());
        Assert.False(location.TryGetProperty("region", out _));
    }

    [Fact]
    public void Sarif_maps_info_to_note()
    {
        var report = new BackgroundReport("x", Path.Combine(_work.Root, "x"), [new ShaderDiagnostic("SHADER060", ShaderSeverity.Info, "remark", "manifest.json")]);
        using var writer = new StringWriter();

        ReportWriters.Write("sarif", [report], writer);

        using var json = JsonDocument.Parse(writer.ToString());
        Assert.Equal("note", json.RootElement.GetProperty("runs")[0].GetProperty("results")[0].GetProperty("level").GetString());
    }

    [Theory]
    [InlineData("text")]
    [InlineData("markdown")]
    [InlineData("json")]
    [InlineData("sarif")]
    public void A_clean_run_is_0_in_every_format(string format)
    {
        _work.Background("waves");

        var run = Workspace.Invoke("shader", "validate", "waves", "--format", format);

        Assert.Equal(0, run.Code);
        if (format is "json" or "sarif")
            JsonDocument.Parse(run.Out).Dispose();
    }

    [Fact]
    public void A_clean_run_in_json_has_no_diagnostics_and_zero_counts()
    {
        _work.Background("waves");

        var run = Workspace.Invoke("shader", "validate", "waves", "--format", "json");

        using var json = JsonDocument.Parse(run.Out);
        Assert.Empty(json.RootElement.GetProperty("backgrounds")[0].GetProperty("diagnostics").EnumerateArray());
        Assert.All(json.RootElement.GetProperty("summary").EnumerateObject(), p => Assert.Equal(0, p.Value.GetInt32()));
    }

    [Fact]
    public void A_clean_run_in_sarif_has_no_results()
    {
        _work.Background("waves");

        var run = Workspace.Invoke("shader", "validate", "waves", "--format", "sarif");

        using var json = JsonDocument.Parse(run.Out);
        Assert.Empty(json.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray());
    }

    [Theory]
    [InlineData("text")]
    [InlineData("markdown")]
    [InlineData("json")]
    [InlineData("sarif")]
    public void Paths_use_forward_slashes_on_every_platform(string format)
    {
        _work.Background("inner", description: false, parent: "set/deeper");

        var run = Workspace.Invoke("shader", "validate", "--all", Path.Combine("set", "deeper"), "--format", format);

        Assert.DoesNotContain("\\\\", run.Out);
        if (format is "text" or "markdown")
            Assert.DoesNotContain('\\', run.Out);
        Assert.Contains("set/deeper/inner/manifest.json", run.Out);
    }

    [Fact]
    public void A_path_outside_the_working_directory_is_printed_absolute_with_slashes()
    {
        string folder = _work.Background("waves", description: false);
        string other = Path.Combine(Path.GetTempPath(), "urdeck-cli-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(other);
        try
        {
            Directory.SetCurrentDirectory(other);

            var run = Workspace.Invoke("shader", "validate", folder);

            Assert.StartsWith(folder.Replace('\\', '/') + "/manifest.json: warning SHADER041", run.Out);
        }
        finally
        {
            Directory.SetCurrentDirectory(_work.Root);
            Directory.Delete(other);
        }
    }
}
