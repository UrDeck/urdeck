// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The findings of <see cref="ThemeLint"/>: for each code of the <c>theme-lint</c> spec a minimal failing folder and a passing one
/// under <c>Fixtures/lint</c> (the command line tool's tests use the same folders).
/// </summary>
public sealed class ThemeLintTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-lint-tests-" + Guid.NewGuid().ToString("N"));

    public ThemeLintTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string FixtureRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "lint");

    /// <summary>The severity each failing fixture is expected to give its code; a folder not listed is a warning.</summary>
    private static readonly Dictionary<string, ThemeSeverity> Severities = new(StringComparer.Ordinal)
    {
        ["THEME001-missing"] = ThemeSeverity.Error,
        ["THEME001-notjson"] = ThemeSeverity.Error,
        ["THEME001-noobject"] = ThemeSeverity.Error,
        ["THEME020-missing"] = ThemeSeverity.Error,
        ["THEME020-extension"] = ThemeSeverity.Error,
        ["THEME020-outside"] = ThemeSeverity.Error,
        ["THEME062"] = ThemeSeverity.Info,
        ["THEME070"] = ThemeSeverity.Error,
        ["THEME071"] = ThemeSeverity.Error,
        ["THEME072"] = ThemeSeverity.Error,
        ["THEME076"] = ThemeSeverity.Info,
    };

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (string folder in Directory.GetDirectories(FixtureRoot).OrderBy(f => f, StringComparer.Ordinal))
            data.Add(Path.GetFileName(folder));
        return data;
    }

    private static IReadOnlyList<ThemeDiagnostic> Check(string folder, ThemeLintOptions? options = null) =>
        ThemeLint.Check(new DirectoryThemeSource(folder), options);

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_failing_folder_has_its_code_and_no_other(string name)
    {
        string code = name.Split('-')[0];

        var found = Check(Path.Combine(FixtureRoot, name, "fail"));

        Assert.Equal([code], found.Select(f => f.Code).Distinct());
        Assert.All(found, f => Assert.Equal(Severities.GetValueOrDefault(name, ThemeSeverity.Warning), f.Severity));
        Assert.All(found, f => Assert.False(string.IsNullOrWhiteSpace(f.Message)));
        Assert.All(found, f => Assert.DoesNotContain('\n', f.Message));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_passing_folder_has_no_finding(string name)
    {
        var found = Check(Path.Combine(FixtureRoot, name, "pass"));

        Assert.Empty(found);
    }

    [Fact]
    public void Every_fixture_folder_has_a_failing_and_a_passing_side()
    {
        foreach (string folder in Directory.GetDirectories(FixtureRoot))
        {
            Assert.True(Directory.Exists(Path.Combine(folder, "fail")), folder);
            Assert.True(Directory.Exists(Path.Combine(folder, "pass")), folder);
        }
    }

    [Fact]
    public void A_missing_file_stops_the_check_with_one_error()
    {
        var found = Check(Path.Combine(FixtureRoot, "THEME001-missing", "fail"));

        var finding = Assert.Single(found);
        Assert.Equal("theme.json", finding.File);
        Assert.Contains("missing", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Not_json_says_where_the_reader_stopped()
    {
        var finding = Assert.Single(Check(Path.Combine(FixtureRoot, "THEME001-notjson", "fail")));

        Assert.Contains("not valid JSON", finding.Message, StringComparison.Ordinal);
        Assert.Contains("line 1", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_misspelt_key_suggests_the_key_that_is_read()
    {
        var finding = Assert.Single(Check(Path.Combine(FixtureRoot, "THEME002", "fail")));

        Assert.Equal("card.borderWith", finding.Key);
        Assert.Contains("card.borderWidth", finding.Message, StringComparison.Ordinal);
        Assert.Equal("theme.json", finding.File);
    }

    [Fact]
    public void A_key_that_is_far_from_every_key_has_no_suggestion()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "far")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"), """{ "meta": { "name": "x", "description": "x", "mode": "dark" }, "card": { "zzzzzzzzzz": 1 } }""");

        var finding = Assert.Single(Check(folder));

        Assert.Equal(ThemeCodes.UnknownKey, finding.Code);
        Assert.DoesNotContain("did you mean", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Keys_in_groups_inside_lists_and_the_gauge_gradient_are_walked()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "walk")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"), """
            {
              "meta": { "name": "x", "description": "x", "mode": "dark", "samples": [ { "file": "a.png", "captoin": "x" } ] },
              "gauge": { "gradient": { "colors": ["#ff0000", "#00ff00"], "angel": 10 } },
              "deferred": 1
            }
            """);

        var keys = Check(folder).Where(f => f.Code == ThemeCodes.UnknownKey).Select(f => f.Key).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["deferred", "gauge.gradient.angel", "meta.samples[0].captoin"], keys);
    }

    [Fact]
    public void The_schema_line_is_accepted()
    {
        Assert.DoesNotContain(Check(Path.Combine(FixtureRoot, "THEME080", "pass")), f => f.Code == ThemeCodes.UnknownKey);
    }

    [Fact]
    public void The_message_of_a_value_finding_is_the_deck_log_line_without_the_theme_prefix()
    {
        var finding = Assert.Single(Check(Path.Combine(FixtureRoot, "THEME004", "fail")));

        Assert.Equal("card.shadow.blur", finding.Key);
        Assert.Equal("card.shadow.blur 5 is outside 0..1; using the default.", finding.Message);
    }

    [Fact]
    public void A_font_that_leaves_the_folder_is_one_error_and_no_file_is_read()
    {
        var finding = Assert.Single(Check(Path.Combine(FixtureRoot, "THEME020-outside", "fail")));

        Assert.Equal(ThemeCodes.FontFile, finding.Code);
        Assert.Contains("leaves the theme folder", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("THEME020-outside", "Put the font file in the theme's folder")]
    [InlineData("THEME050", "Give card.borderWidth a value above 0")]
    [InlineData("THEME051", "Set card.borderGradient to kind conic")]
    [InlineData("THEME052", "Raise pattern.spacing")]
    [InlineData("THEME053", "widen card.gap")]
    [InlineData("THEME054", "Reduce the blur, offset or spread")]
    [InlineData("THEME055", "at least one pixel")]
    [InlineData("THEME057", "Use a solid card.borderStyle")]
    public void A_line_of_the_deck_that_gives_no_fix_gets_one(string fixture, string fix)
    {
        var finding = Assert.Single(Check(Path.Combine(FixtureRoot, fixture, "fail")));

        Assert.Contains(fix, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_font_over_three_megabytes_is_a_finding()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "big")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"), """{ "meta": { "name": "x", "description": "x", "mode": "dark" }, "typography": { "font": "Big.ttf" } }""");
        File.WriteAllBytes(Path.Combine(folder, "Big.ttf"), new byte[(3 * 1024 * 1024) + 1]);
        File.WriteAllText(Path.Combine(folder, "Big-OFL.txt"), "licence");

        var finding = Assert.Single(Check(folder));

        Assert.Equal(ThemeCodes.FontFileSize, finding.Code);
        Assert.Equal("Big.ttf", finding.File);
        Assert.Contains("over 3 MB", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_weight_the_font_lacks_is_a_finding_for_the_role()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "weight")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"),
            """{ "meta": { "name": "x", "description": "x", "mode": "dark" }, "typography": { "font": "JetBrainsMono-Variable.ttf", "weights": { "value": 900 } } }""");
        foreach (string name in new[] { "JetBrainsMono-Variable.ttf", "JetBrainsMono-OFL.txt" })
        {
            using var stream = ThemeStore.OpenBuiltInFile(name);
            using var file = File.Create(Path.Combine(folder, name));
            stream!.CopyTo(file);
        }

        var finding = Assert.Single(Check(folder));

        Assert.Equal(ThemeCodes.FontWeight, finding.Code);
        Assert.Equal("typography.weights.value", finding.Key);
    }

    [Fact]
    public void A_still_picture_over_eight_megabytes_is_a_finding()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "still")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"),
            """{ "meta": { "name": "x", "description": "x", "mode": "dark" }, "background": { "static": { "image": "big.png" } } }""");
        File.WriteAllBytes(Path.Combine(folder, "big.png"), new byte[(8 * 1024 * 1024) + 1]);

        var finding = Assert.Single(Check(folder));

        Assert.Equal(ThemeCodes.StillPicture, finding.Code);
        Assert.Contains("over 8 MB", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_still_picture_that_leaves_the_folder_is_a_finding()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "leave")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"),
            """{ "meta": { "name": "x", "description": "x", "mode": "dark" }, "background": { "static": { "image": "..\\x.png" } } }""");

        var finding = Assert.Single(Check(folder));

        Assert.Equal(ThemeCodes.StillPicture, finding.Code);
        Assert.Contains("leaves the theme's folder", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_background_in_a_directory_the_tool_is_given_exists()
    {
        string backgrounds = Directory.CreateDirectory(Path.Combine(_root, "backgrounds")).FullName;
        Directory.CreateDirectory(Path.Combine(backgrounds, "mine"));
        string folder = Directory.CreateDirectory(Path.Combine(_root, "pairs")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"),
            """{ "meta": { "name": "x", "description": "x", "mode": "dark", "pairsWith": { "backgrounds": ["mine"] } }, "background": { "default": { "name": "mine" } } }""");

        Assert.Equal([ThemeCodes.UnknownBackground, ThemeCodes.UnknownPairing], Check(folder).Select(f => f.Code).Order(StringComparer.Ordinal));
        Assert.Empty(Check(folder, new ThemeLintOptions([backgrounds])));
    }

    [Fact]
    public void Pairing_names_only_the_missing_background()
    {
        var found = Check(Path.Combine(FixtureRoot, "THEME041", "fail"));

        var finding = Assert.Single(found);
        Assert.Contains("nosuch", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'contour'", finding.Message, StringComparison.Ordinal);
        Assert.Equal("meta.pairsWith.backgrounds[1]", finding.Key);
    }

    [Fact]
    public void Low_contrast_gives_the_ratio_and_the_colours()
    {
        var finding = Assert.Single(Check(Path.Combine(FixtureRoot, "THEME060", "fail")));

        Assert.Contains("#808080", finding.Message, StringComparison.Ordinal);
        Assert.Contains("#909090", finding.Message, StringComparison.Ordinal);
        Assert.Contains("1.2:1", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("composited", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_translucent_fill_is_composited_over_the_background_and_the_message_says_so()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "glass")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"), """
            { "meta": { "name": "x", "description": "x", "mode": "dark" },
              "colors": { "background": "#000000", "cardFill": "#20ffffff", "text": "#404040", "textMuted": "#ffffff" } }
            """);

        var finding = Assert.Single(Check(folder));

        Assert.Equal(ThemeCodes.ContrastText, finding.Code);
        Assert.Contains("colors.background", finding.Message, StringComparison.Ordinal);
        Assert.Contains("moving background", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_gradient_fill_is_checked_at_its_lightest_and_darkest_stop()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "gradient")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"), """
            { "meta": { "name": "x", "description": "x", "mode": "dark" },
              "colors": { "background": "#000000", "text": "#f0f0f0", "textMuted": "#808080" },
              "card": { "fillGradient": { "colors": ["#202020", "#808080", "#f8f8f8"] } } }
            """);

        var finding = Assert.Single(Check(folder));

        Assert.Equal(ThemeCodes.ContrastText, finding.Code);
        Assert.Contains("stop 3", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tint_finding_names_the_position_in_the_list()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "tints")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"), """
            { "meta": { "name": "x", "description": "x", "mode": "dark" },
              "colors": { "cardTints": ["#202020", "#f0f0f0", "#303030"] } }
            """);

        var finding = Assert.Single(Check(folder));

        Assert.Equal(ThemeCodes.ContrastTint, finding.Code);
        Assert.Equal("colors.cardTints[1]", finding.Key);
    }

    [Fact]
    public void Missing_metadata_lists_what_is_missing()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "meta")).FullName;
        File.WriteAllText(Path.Combine(folder, "theme.json"), """{ "meta": { "name": "x" } }""");

        var finding = Assert.Single(Check(folder));

        Assert.Equal(ThemeCodes.NoMetadata, finding.Code);
        Assert.Contains("description, mode", finding.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> BuiltIns()
    {
        var data = new TheoryData<string>();
        foreach (string name in ThemeStore.BuiltInNames)
            data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void A_built_in_theme_checks_clean_as_the_product_ships_it(string name)
    {
        Assert.Empty(ThemeLint.Check(new EmbeddedThemeSource(name)));
    }

    [Fact]
    public void The_same_content_gives_the_same_findings_from_a_folder_and_from_the_product()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_root, "carbon")).FullName;
        var source = new EmbeddedThemeSource("carbon");
        File.WriteAllText(Path.Combine(folder, "theme.json"), source.ReadSettings()!);
        foreach (string name in source.Files().Where(f => !f.EndsWith(".json", StringComparison.Ordinal)))
        {
            using var stream = source.Open(name);
            using var file = File.Create(Path.Combine(folder, name));
            stream!.CopyTo(file);
        }

        Assert.Equal(
            ThemeLint.Check(source).Select(f => (f.Code, f.Key, f.Message)),
            Check(folder).Select(f => (f.Code, f.Key, f.Message)));
    }
}
