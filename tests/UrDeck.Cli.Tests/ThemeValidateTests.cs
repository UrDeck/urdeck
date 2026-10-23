// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Cli.Tests.Support;
using Xunit;

namespace UrDeck.Cli.Tests;

/// <summary>The scenarios of the <c>urdeck-cli</c> spec for <c>theme validate</c>: one folder, many, the built-in themes and the formats.</summary>
public sealed class ThemeValidateTests : IDisposable
{
    private readonly Workspace _work = new();

    public void Dispose() => _work.Dispose();

    [Fact]
    public void A_clean_folder_is_0_and_only_the_summary()
    {
        _work.Theme("THEME080", "mine", "pass");

        var run = Workspace.Invoke("theme", "validate", "mine");

        Assert.Equal(0, run.Code);
        Assert.Equal("1 theme checked: 0 errors, 0 warnings, 0 remarks.\n", run.Out);
        Assert.Equal("", run.Error);
    }

    [Fact]
    public void Warnings_only_are_printed_and_0_and_fail_on_request()
    {
        _work.Theme("THEME002", "mine");

        var plain = Workspace.Invoke("theme", "validate", "mine");
        var strict = Workspace.Invoke("theme", "validate", "mine", "--fail-on-warnings");

        Assert.Equal(0, plain.Code);
        Assert.Contains("THEME002", plain.Out);
        Assert.Equal(1, strict.Code);
        Assert.Equal(plain.Out, strict.Out);
    }

    [Fact]
    public void An_error_is_1()
    {
        _work.Theme("THEME020-missing", "mine");

        var run = Workspace.Invoke("theme", "validate", "mine");

        Assert.Equal(1, run.Code);
        Assert.Contains("error THEME020:", run.Out);
    }

    [Fact]
    public void A_remark_never_changes_the_exit_code()
    {
        _work.Theme("THEME062", "mine");

        var run = Workspace.Invoke("theme", "validate", "mine", "--fail-on-warnings");

        Assert.Equal(0, run.Code);
        Assert.Contains("info THEME062:", run.Out);
        Assert.EndsWith("1 theme checked: 0 errors, 0 warnings, 1 remark.\n", run.Out);
    }

    [Fact]
    public void The_findings_are_those_of_the_deck_with_the_decks_message()
    {
        _work.Theme("THEME004", "mine");

        var run = Workspace.Invoke("theme", "validate", "mine");

        Assert.Contains("mine/theme.json: warning THEME004: card.shadow.blur 5 is outside 0..1; using the default.\n", run.Out);
    }

    [Fact]
    public void A_missing_settings_file_is_one_error_and_1()
    {
        Directory.CreateDirectory("empty");

        var run = Workspace.Invoke("theme", "validate", "empty");

        Assert.Equal(1, run.Code);
        Assert.Equal(1, run.Out.Split('\n').Count(l => l.Contains("THEME001", StringComparison.Ordinal)));
        Assert.Contains("empty/theme.json: error THEME001:", run.Out);
    }

    [Fact]
    public void The_current_directory_is_checked_as_the_theme_of_its_name()
    {
        string folder = _work.Theme("THEME002", "mine");
        Directory.SetCurrentDirectory(folder);

        var run = Workspace.Invoke("theme", "validate", ".", "--format", "json");

        using var json = JsonDocument.Parse(run.Out);
        var theme = json.RootElement.GetProperty("themes")[0];
        Assert.Equal("mine", theme.GetProperty("name").GetString());
        Assert.Equal("theme.json", theme.GetProperty("diagnostics")[0].GetProperty("file").GetString());
    }

    [Fact]
    public void A_font_path_that_leaves_the_folder_is_an_error()
    {
        _work.Theme("THEME020-outside", "mine");

        var run = Workspace.Invoke("theme", "validate", "mine");

        Assert.Equal(1, run.Code);
        Assert.Contains("error THEME020:", run.Out);
        Assert.Contains("leaves the theme folder", run.Out);
    }

    [Fact]
    public void A_folder_named_like_a_built_in_is_checked_as_the_folder()
    {
        _work.Theme("THEME002", "carbon");

        var run = Workspace.Invoke("theme", "validate", "carbon");

        Assert.Contains("carbon/theme.json: warning THEME002:", run.Out);
    }

    [Fact]
    public void A_background_directory_of_the_users_makes_a_pairing_known()
    {
        _work.Theme("THEME041", "mine");
        Directory.CreateDirectory("mybackgrounds/nosuch");

        var without = Workspace.Invoke("theme", "validate", "mine");
        var with = Workspace.Invoke("theme", "validate", "mine", "--background-dir", "mybackgrounds");

        Assert.Contains("THEME041", without.Out);
        Assert.DoesNotContain("THEME041", with.Out);
    }

    [Fact]
    public void A_background_directory_that_does_not_exist_is_2()
    {
        _work.Theme("THEME080", "mine", "pass");

        var run = Workspace.Invoke("theme", "validate", "mine", "--background-dir", "missing");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("missing", run.Error);
    }

    [Fact]
    public void A_folder_that_does_not_exist_is_2_and_the_error_names_it()
    {
        var run = Workspace.Invoke("theme", "validate", "missing/dir");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("missing/dir", run.Error);
    }

    [Fact]
    public void An_unknown_option_is_2()
    {
        Assert.Equal(2, Workspace.Invoke("theme", "validate", "--nosuch").Code);
    }

    [Fact]
    public void Nothing_to_check_is_2()
    {
        var run = Workspace.Invoke("theme", "validate");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
    }

    [Fact]
    public void One_bad_among_good_is_named_and_decides_the_exit_code()
    {
        _work.Theme("THEME080", "a", "pass", parent: "themes");
        _work.Theme("THEME080", "b", "pass", parent: "themes");
        _work.Theme("THEME002", "c", parent: "themes");
        _work.Theme("THEME080", "d", "pass", parent: "themes");

        var plain = Workspace.Invoke("theme", "validate", "--all", "themes");
        var strict = Workspace.Invoke("theme", "validate", "--all", "themes", "--fail-on-warnings");

        Assert.Equal(0, plain.Code);
        Assert.Equal(1, strict.Code);
        Assert.Contains("themes/c/theme.json: warning THEME002:", plain.Out);
        Assert.EndsWith("4 themes checked: 0 errors, 1 warning, 0 remarks.\n", plain.Out);
    }

    [Fact]
    public void The_folders_are_checked_in_ordinal_order_and_hidden_ones_and_files_are_skipped()
    {
        _work.Theme("THEME002", "b", parent: "themes");
        _work.Theme("THEME002", "Z", parent: "themes");
        _work.Theme("THEME002", "a", parent: "themes");
        _work.Theme("THEME002", ".hidden", parent: "themes");
        File.WriteAllText("themes/notes.txt", "x");

        var run = Workspace.Invoke("theme", "validate", "--all", "themes", "--format", "json");

        using var json = JsonDocument.Parse(run.Out);
        Assert.Equal(["Z", "a", "b"], json.RootElement.GetProperty("themes").EnumerateArray().Select(t => t.GetProperty("name").GetString()));
    }

    [Fact]
    public void An_empty_directory_is_2()
    {
        Directory.CreateDirectory("none");

        var run = Workspace.Invoke("theme", "validate", "--all", "none");

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
        Assert.Contains("no themes were found", run.Error);
    }

    [Fact]
    public void Folders_are_not_searched_for_themes()
    {
        Directory.CreateDirectory("themes/group");
        _work.Theme("THEME080", "inner", "pass", parent: "themes/group");

        var run = Workspace.Invoke("theme", "validate", "--all", "themes");

        Assert.Equal(1, run.Code);
        Assert.Contains("themes/group/theme.json: error THEME001:", run.Out);
        Assert.DoesNotContain("inner", run.Out);
    }

    [Fact]
    public void The_built_in_themes_check_clean_with_warnings_counted()
    {
        var run = Workspace.Invoke("theme", "validate", "--builtin", "--fail-on-warnings");

        Assert.Equal(0, run.Code);
        Assert.Equal("11 themes checked: 0 errors, 0 warnings, 0 remarks.\n", run.Out);
    }

    [Theory]
    [InlineData("--builtin", "--all", "x")]
    [InlineData("--builtin", "mine")]
    [InlineData("--all", "x", "mine")]
    public void Options_that_exclude_each_other_are_2(params string[] args)
    {
        _work.Theme("THEME080", "mine", "pass");
        Directory.CreateDirectory("x");

        var run = Workspace.Invoke(["theme", "validate", .. args]);

        Assert.Equal(2, run.Code);
        Assert.Equal("", run.Out);
    }

    [Fact]
    public void A_built_in_theme_names_its_file_as_builtin_name_slash_theme_json()
    {
        var run = Workspace.Invoke("theme", "validate", "--builtin", "--format", "json");

        using var json = JsonDocument.Parse(run.Out);
        var names = json.RootElement.GetProperty("themes").EnumerateArray().Select(t => t.GetProperty("folder").GetString()).ToList();
        Assert.Equal(["builtin:blueprint", "builtin:carbon", "builtin:default-dark", "builtin:default-light", "builtin:ember", "builtin:glass", "builtin:obsidian", "builtin:paper", "builtin:pop", "builtin:prism", "builtin:rigline"], names);
    }

    [Fact]
    public void The_file_of_a_finding_in_a_built_in_theme_is_printed_with_the_builtin_prefix()
    {
        var report = SubjectReport.Theme("carbon", "builtin:carbon", [new UrDeck.Engine.Themes.ThemeDiagnostic("THEME080", UrDeck.Engine.Themes.ThemeSeverity.Warning, "x", "theme.json", "meta")], "builtin:carbon");
        using var writer = new StringWriter { NewLine = "\n" };

        ReportWriters.Write("text", [report], writer);

        Assert.StartsWith("builtin:carbon/theme.json: warning THEME080: x\n", writer.ToString());
    }

    // ------------------------------------------------------------------ formats

    [Fact]
    public void Text_has_one_line_per_finding_and_a_summary()
    {
        _work.Theme("THEME002", "mine");

        var run = Workspace.Invoke("theme", "validate", "mine");

        Assert.Equal(
            "mine/theme.json: warning THEME002: card.borderWith is not a key the deck reads and it is ignored; did you mean card.borderWidth?\n"
            + "1 theme checked: 0 errors, 1 warning, 0 remarks.\n",
            run.Out);
    }

    [Fact]
    public void Markdown_has_a_heading_and_a_table_for_each_theme()
    {
        _work.Theme("THEME002", "bad", parent: "themes");
        _work.Theme("THEME080", "good", "pass", parent: "themes");

        var run = Workspace.Invoke("theme", "validate", "--all", "themes", "--format", "markdown");

        Assert.Contains("## good\n\nNo findings.\n", run.Out);
        Assert.Contains("## bad\n\n| Severity | Code | File | Message |\n| --- | --- | --- | --- |\n", run.Out);
        Assert.Contains("| warning | THEME002 | themes/bad/theme.json |", run.Out);
        Assert.EndsWith("2 themes checked: 0 errors, 1 warning, 0 remarks.\n", run.Out);
    }

    [Fact]
    public void Json_is_one_object_with_version_themes_keys_and_a_summary()
    {
        _work.Theme("THEME004", "mine");

        var run = Workspace.Invoke("theme", "validate", "mine", "--format", "json");

        using var json = JsonDocument.Parse(run.Out);
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.False(root.TryGetProperty("backgrounds", out _));
        var theme = root.GetProperty("themes")[0];
        Assert.Equal("mine", theme.GetProperty("name").GetString());
        Assert.Equal("mine", theme.GetProperty("folder").GetString());
        var diagnostic = theme.GetProperty("diagnostics")[0];
        Assert.Equal("THEME004", diagnostic.GetProperty("code").GetString());
        Assert.Equal("warning", diagnostic.GetProperty("severity").GetString());
        Assert.Equal("card.shadow.blur", diagnostic.GetProperty("key").GetString());
        Assert.Equal("mine/theme.json", diagnostic.GetProperty("file").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("warnings").GetInt32());
    }

    [Fact]
    public void A_finding_that_is_not_about_a_key_has_no_key_in_json()
    {
        Directory.CreateDirectory("empty");

        var run = Workspace.Invoke("theme", "validate", "empty", "--format", "json");

        using var json = JsonDocument.Parse(run.Out);
        Assert.False(json.RootElement.GetProperty("themes")[0].GetProperty("diagnostics")[0].TryGetProperty("key", out _));
    }

    [Fact]
    public void Sarif_has_a_result_for_each_finding_and_a_rule_for_each_code()
    {
        _work.Theme("THEME020-missing", "mine");
        File.WriteAllText("mine/theme.json", File.ReadAllText("mine/theme.json").Replace("\"meta\"", "\"format\": 2, \"meta\"", StringComparison.Ordinal));

        var run = Workspace.Invoke("theme", "validate", "mine", "--format", "sarif");

        Assert.Equal(1, run.Code);
        using var json = JsonDocument.Parse(run.Out);
        var sarif = json.RootElement.GetProperty("runs")[0];
        var results = sarif.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(["error", "warning"], results.Select(r => r.GetProperty("level").GetString()).Order(StringComparer.Ordinal));
        var rules = sarif.GetProperty("tool").GetProperty("driver").GetProperty("rules").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToList();
        Assert.Equal(["THEME002", "THEME020"], rules);
        Assert.All(results, r => Assert.Equal("mine/theme.json", r.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString()));
    }

    [Theory]
    [InlineData("text")]
    [InlineData("markdown")]
    [InlineData("json")]
    [InlineData("sarif")]
    public void The_format_never_changes_the_exit_code(string format)
    {
        _work.Theme("THEME020-missing", "mine");

        Assert.Equal(1, Workspace.Invoke("theme", "validate", "mine", "--format", format).Code);
    }

    [Fact]
    public void Paths_are_printed_with_forward_slashes()
    {
        _work.Theme("THEME002", "mine", parent: "a/b");

        var run = Workspace.Invoke("theme", "validate", Path.Combine("a", "b", "mine"));

        Assert.Contains("a/b/mine/theme.json: warning", run.Out);
        Assert.DoesNotContain('\\', run.Out);
    }

    // ------------------------------------------------------------------ help

    [Theory]
    [InlineData("theme")]
    [InlineData("theme", "validate")]
    public void Help_names_the_exit_codes(params string[] command)
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
        var run = Workspace.Invoke("theme", "validate", "--help");

        foreach (string option in new[] { "--all", "--builtin", "--background-dir", "--format", "--fail-on-warnings" })
            Assert.Contains(option, run.Out);
        Assert.Contains("sarif", run.Out);
    }
}
