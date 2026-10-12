// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Cli.Tests.Support;
using Xunit;

namespace UrDeck.Cli.Tests;

public sealed class CliAppTests : IDisposable
{
    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    [Theory]
    [InlineData("nosuch")]
    [InlineData("shader", "nosuch")]
    [InlineData("shader", "validate", "--nosuch", "x", "y")]
    [InlineData("shader", "validate", "x", "--nosuch")]
    [InlineData("shader", "validate", "--format", "nosuch", "x")]
    [InlineData("shader", "render", "x")]
    [InlineData("shader", "render", "-o")]
    public void A_parse_error_is_2_on_the_error_stream_and_nothing_on_the_output(params string[] args)
    {
        var run = Workspace.Invoke(args);

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.StartsWith("error:", run.Error);
    }

    [Fact]
    public void An_unknown_option_in_place_of_the_folder_is_2()
    {
        var run = Workspace.Invoke("shader", "validate", "--nosuch");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("--nosuch", run.Error);
    }

    [Theory]
    [InlineData("shader", "validate")]
    [InlineData("shader", "render")]
    [InlineData("shader")]
    [InlineData]
    public void Help_is_0_and_names_the_exit_codes(params string[] command)
    {
        var run = Workspace.Invoke([.. command, "--help"]);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.Contains("Exit codes:", run.Out);
        Assert.Contains("  0  ", run.Out);
        Assert.Contains("  1  ", run.Out);
        Assert.Contains("  2  ", run.Out);
    }

    [Fact]
    public void Validate_help_lists_its_options()
    {
        var run = Workspace.Invoke("shader", "validate", "--help");

        Assert.Contains("--all", run.Out);
        Assert.Contains("--format", run.Out);
        Assert.Contains("--fail-on-warnings", run.Out);
        Assert.Contains("text", run.Out);
        Assert.Contains("sarif", run.Out);
    }

    [Fact]
    public void Render_help_lists_its_options()
    {
        var run = Workspace.Invoke("shader", "render", "--help");

        foreach (string option in new[] { "--output", "--size", "--time", "--theme", "--colors", "--param", "--fail-on-warnings" })
            Assert.Contains(option, run.Out);
        Assert.DoesNotContain("--scale", run.Out);
    }

    [Fact]
    public void Version_is_0_and_prints_a_version()
    {
        var run = Workspace.Invoke("--version");

        Assert.Equal(0, run.Code);
        Assert.Matches(@"^\d+\.\d+\.\d+", run.Out);
        Assert.Equal("", run.Error);
    }

    [Fact]
    public void Running_a_command_does_not_create_a_log_file()
    {
        _work.Background("waves");
        Workspace.Invoke("shader", "validate", "waves");
        Workspace.Invoke("shader", "render", "waves", "-o", "out.png", "--param", "nope=1");

        Assert.False(File.Exists(Path.Combine(AppContext.BaseDirectory, "urdeck.log")));
        Assert.False(File.Exists("urdeck.log"));
    }
}
