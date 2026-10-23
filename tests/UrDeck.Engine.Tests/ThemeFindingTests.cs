// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The findings the theme checks report to a sink: each kind of check gives one finding with its code, the key it is about
/// and its severity, whatever the log says about it (the log text is held by <see cref="ThemeLogSnapshotTests"/>).
/// </summary>
public sealed class ThemeFindingTests
{
    private static ThemeFinding Only(IEnumerable<ThemeFinding> found)
    {
        var list = found.ToList();
        return Assert.Single(list);
    }

    private static List<ThemeFinding> Sanitize(string json)
    {
        var sink = new CollectedFindings();
        var definition = ThemeLenientJson.Read(json, sink, UrDeckJson.Options, out var reported);
        definition.Sanitize(sink, reported);
        return sink.Found.ToList();
    }

    private static List<ThemeFinding> Rules(string json)
    {
        var sink = new CollectedFindings();
        var definition = JsonSerializer.Deserialize<ThemeDefinition>(json, UrDeckJson.Options)!;
        definition.CheckRules(sink);
        return sink.Found.ToList();
    }

    [Theory]
    [InlineData("""{ "colors": { "background": "red" } }""", ThemeCodes.BadColor, "colors.background")]
    [InlineData("""{ "colors": { "backgroundPalette": ["#000000", "nope"] } }""", ThemeCodes.BadList, "colors.backgroundPalette")]
    [InlineData("""{ "card": { "shadow": { "blur": 5 } } }""", ThemeCodes.OutOfRange, "card.shadow.blur")]
    [InlineData("""{ "format": 0 }""", ThemeCodes.OutOfRange, "format")]
    [InlineData("""{ "gauge": { "ticks": { "count": 1 } } }""", ThemeCodes.OutOfRange, "gauge.ticks.count")]
    [InlineData("""{ "card": { "borderGradient": { "colors": ["#ff0000", "#00ff00"], "angle": 900 }, "borderWidth": 0.01 } }""", ThemeCodes.OutOfRange, "card.borderGradient.angle")]
    [InlineData("""{ "card": { "shape": "blob" } }""", ThemeCodes.NotOneOf, "card.shape")]
    [InlineData("""{ "colors": { "ramp": ["#ff0000"] } }""", ThemeCodes.BadList, "colors.ramp")]
    [InlineData("""{ "meta": { "tags": [1, 2] } }""", ThemeCodes.WrongType, "meta.tags")]
    [InlineData("""{ "gauge": { "scale": { "labels": ["only"] } } }""", ThemeCodes.BadList, "gauge.scale.labels")]
    [InlineData("""{ "gauge": { "status": { "thresholds": [0.5, 7] } } }""", ThemeCodes.BadList, "gauge.status.thresholds")]
    [InlineData("""{ "card": { "cutCorners": ["tl", "up"] } }""", ThemeCodes.NotOneOf, "card.cutCorners")]
    [InlineData("""{ "card": { "shadow": { "blur": "big" } } }""", ThemeCodes.WrongType, "card.shadow.blur")]
    [InlineData("""{ "card": { "fillGradient": { "angle": 10 } } }""", ThemeCodes.MissingMember, "card.fillGradient")]
    [InlineData("""{ "icons": { "tint": "purple" } }""", ThemeCodes.NotOneOf, "icons.tint")]
    [InlineData("""{ "stroke": { "cap": "flat" } }""", ThemeCodes.NotOneOf, "stroke.cap")]
    [InlineData("""{ "gauge": { "style": "dial" } }""", ThemeCodes.NotOneOf, "gauge.style")]
    [InlineData("""{ "format": 9 }""", ThemeCodes.NewerFormat, "format")]
    public void A_value_that_fails_a_check_is_one_finding_with_its_code_and_key(string json, string code, string key)
    {
        var finding = Only(Sanitize(json));

        Assert.Equal(code, finding.Code);
        Assert.Equal(key, finding.Key);
        Assert.Equal(ThemeSeverity.Warning, finding.Severity);
        Assert.False(string.IsNullOrWhiteSpace(finding.Message));
        Assert.DoesNotContain("Theme '", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_message_of_a_finding_is_the_log_line_without_its_theme_prefix()
    {
        var finding = Only(Sanitize("""{ "card": { "shadow": { "blur": 5 } } }"""));

        Assert.Equal("card.shadow.blur 5 is outside 0..1; using the default.", finding.Message);
    }

    [Fact]
    public void A_value_in_another_case_is_no_finding()
    {
        Assert.Empty(Sanitize("""{ "card": { "borderStyle": "Dashed" } }"""));
    }

    [Theory]
    [InlineData("""{ "card": { "borderWidth": 0, "borderStyle": "dashed" } }""", ThemeCodes.BorderWithoutWidth, "card.borderWidth", ThemeSeverity.Warning)]
    [InlineData("""{ "card": { "borderWidth": 0.01, "borderAnimate": { "mode": "spin" } } }""", ThemeCodes.SpinWithoutConic, "card.borderAnimate.mode", ThemeSeverity.Warning)]
    [InlineData("""{ "card": { "borderWidth": 0.01, "borderStyle": "dashed", "borderAnimate": { "mode": "pulse" } } }""", ThemeCodes.AnimatedDashedBorder, "card.borderAnimate.mode", ThemeSeverity.Warning)]
    [InlineData("""{ "pattern": { "kind": "dots", "spacing": 0.02, "thickness": 0.01 } }""", ThemeCodes.PatternDense, "pattern.spacing", ThemeSeverity.Warning)]
    [InlineData("""{ "card": { "gap": 0.02, "cornerMarks": { "style": "bracket", "inset": -0.1 } } }""", ThemeCodes.CornerMarksReach, "card.cornerMarks.inset", ThemeSeverity.Warning)]
    public void A_rule_between_values_is_one_finding_with_its_own_code(string json, string code, string key, ThemeSeverity severity)
    {
        var finding = Only(Rules(json));

        Assert.Equal(code, finding.Code);
        Assert.Equal(key, finding.Key);
        Assert.Equal(severity, finding.Severity);
    }

    [Fact]
    public void Several_continuous_animations_are_an_information_finding()
    {
        var sink = new CollectedFindings();

        ThemeDefinition.NoteAnimations(new ThemeValueCheck(sink), 2);

        var finding = Only(sink.Found);
        Assert.Equal(ThemeCodes.Animations, finding.Code);
        Assert.Equal(ThemeSeverity.Info, finding.Severity);
        Assert.Null(finding.Key);
    }

    private static List<ThemeFinding> Resolved(string json)
    {
        string root = Path.Combine(Path.GetTempPath(), "urdeck-findings-tests-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "probe");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, ThemeStore.SettingsFileName), json);
            foreach (string font in new[] { "JetBrainsMono-Variable.ttf" })
            {
                using var stream = ThemeStore.OpenBuiltInFile(font);
                using var file = File.Create(Path.Combine(folder, font));
                stream!.CopyTo(file);
            }

            var sink = new CollectedFindings();
            var before = UrDeck.Engine.Diagnostics.UrDeckLog.Listener;
            UrDeck.Engine.Diagnostics.UrDeckLog.Listener = (_, _) => { };
            LoadedTheme loaded;
            try
            {
                loaded = new ThemeStore(root).Load("probe") with { Findings = sink };
            }
            finally
            {
                UrDeck.Engine.Diagnostics.UrDeckLog.Listener = before;
            }

            using var bitmap = ThemePreview.Render(loaded, 1100, 1920, dock: true);
            // A second render of the same load reports nothing more: each finding is once per load.
            int once = sink.Found.Count;
            using var again = ThemePreview.Render(loaded, 1100, 1920, dock: true);
            Assert.Equal(once, sink.Found.Count);
            return sink.Found.ToList();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_weight_the_font_lacks_is_a_font_weight_finding_once_per_load()
    {
        var finding = Only(Resolved("""{ "typography": { "font": "JetBrainsMono-Variable.ttf", "weights": { "value": 900 } } }"""));

        Assert.Equal(ThemeCodes.FontWeight, finding.Code);
        Assert.Equal("typography.weights.value", finding.Key);
        Assert.Equal(ThemeSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void A_font_that_cannot_be_loaded_is_a_font_file_error()
    {
        var finding = Only(Resolved("""{ "typography": { "font": "Nothing.ttf" } }"""));

        Assert.Equal(ThemeCodes.FontFile, finding.Code);
        Assert.Equal("typography.font", finding.Key);
        Assert.Equal(ThemeSeverity.Error, finding.Severity);
    }

    [Fact]
    public void A_reach_the_deck_limits_is_a_warning_the_deck_logs_as_information()
    {
        var finding = Only(Resolved("""{ "colors": { "cardFill": "#20ffffff" }, "card": { "gap": 0.02, "shadow": { "blur": 0.3, "y": 0.1 } } }"""));

        Assert.Equal(ThemeCodes.EffectReach, finding.Code);
        Assert.Equal(ThemeSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void A_line_under_a_pixel_is_a_hairline_finding()
    {
        var finding = Only(Resolved("""{ "card": { "borderWidth": 0.0004, "borderStyle": "solid" } }"""));

        Assert.Equal(ThemeCodes.Hairline, finding.Code);
        Assert.Equal("card.borderWidth", finding.Key);
    }

    /// <summary>
    /// A code nobody can look up is no use to an author: every code is in the finding catalog with how to fix it. The site's
    /// troubleshooting page is made from that catalog and its build fails when a code is missing from it.
    /// </summary>
    [Fact]
    public void Every_code_is_in_the_finding_catalog()
    {
        var codes = typeof(ThemeCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(codes);
        Assert.Equal(codes.Count, codes.Distinct().Count());
        foreach (string code in codes)
        {
            var listed = Assert.Single(UrDeck.Engine.Diagnostics.FindingCatalog.All, f => f.Code == code);
            Assert.False(string.IsNullOrWhiteSpace(listed.Summary), code);
        }
    }

    [Fact]
    public void The_deck_logs_a_finding_with_the_theme_name_in_front()
    {
        var lines = new List<(string Level, string Text)>();
        var before = UrDeck.Engine.Diagnostics.UrDeckLog.Listener;
        UrDeck.Engine.Diagnostics.UrDeckLog.Listener = (level, text) => lines.Add((level, text));
        try
        {
            var log = new LogFindings("mine");
            log.Report(new ThemeFinding(ThemeCodes.OutOfRange, ThemeSeverity.Warning, "card.gap", "card.gap 5 is outside 0..1; using the default."));
            log.Report(new ThemeFinding(ThemeCodes.NewerFormat, ThemeSeverity.Warning, "format", "declares format 9.") { Form = ThemeLogForm.Space });
            log.Report(new ThemeFinding(ThemeCodes.FontFile, ThemeSeverity.Error, null, "Font 'x' could not be read.") { Form = ThemeLogForm.Bare });
            log.Report(new ThemeFinding(ThemeCodes.EffectReach, ThemeSeverity.Warning, "card.shadow", "limited.") { LoggedAsInfo = true });
            log.Report(new ThemeFinding(ThemeCodes.CompactCapsule, ThemeSeverity.Info, null, "a capsule."));
        }
        finally
        {
            UrDeck.Engine.Diagnostics.UrDeckLog.Listener = before;
        }

        Assert.Equal(
            [
                ("WARN", "Theme 'mine': card.gap 5 is outside 0..1; using the default."),
                ("WARN", "Theme 'mine' declares format 9."),
                ("WARN", "Font 'x' could not be read."),
                ("INFO", "Theme 'mine': limited."),
                ("INFO", "Theme 'mine': a capsule."),
            ],
            lines);
    }
}
