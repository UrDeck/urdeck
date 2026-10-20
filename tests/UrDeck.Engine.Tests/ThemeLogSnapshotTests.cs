// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using UrDeck.Preview;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// What the engine logs when it loads and resolves a theme, recorded as text before the findings were given codes and a
/// sink: the deck's log must read the same afterwards. Covers every built-in theme, every fixture theme, and a theme with
/// one failing value for each kind of check and each rule. Rewrite the file with <c>URDECK_UPDATE_SNAPSHOTS=1</c> only for
/// a change that is meant to alter the log.
/// </summary>
public sealed class ThemeLogSnapshotTests : IDisposable
{
    private const string Mono = "JetBrainsMono-Variable.ttf";
    private const string Sans = "InstrumentSans-Variable.ttf";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-themelog-tests-" + Guid.NewGuid().ToString("N"));

    public ThemeLogSnapshotTests() => Directory.CreateDirectory(Path.Combine(_root, "themes"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "themes");

    private static string SnapshotPath
    {
        get
        {
            string? directory = AppContext.BaseDirectory;
            while (directory != null && !File.Exists(Path.Combine(directory, "urdeck.slnx")))
                directory = Path.GetDirectoryName(directory);
            return Path.Combine(directory ?? throw new InvalidOperationException("urdeck.slnx was not found above the tests."),
                "tests", "UrDeck.Engine.Tests", "Snapshots", "theme-log.txt");
        }
    }

    /// <summary>One failing value for each kind of check, each rule between values and each resolver finding.</summary>
    private static readonly (string Name, string Json)[] Failing =
    [
        ("colour", """{ "colors": { "background": "red", "text": "#12345" } }"""),
        ("palette", """{ "colors": { "backgroundPalette": ["#000000", "nope"] } }"""),
        ("palette-long", """{ "colors": { "backgroundPalette": ["#000000", "#111111", "#222222", "#333333", "#444444"] } }"""),
        ("number", """{ "card": { "shadow": { "blur": 5 }, "radius": 7 } }"""),
        ("integer", """{ "format": 0, "gauge": { "ticks": { "count": 1 } } }"""),
        ("angle", """{ "card": { "borderGradient": { "colors": ["#ff0000", "#00ff00"], "angle": 900 } } }"""),
        ("enum", """{ "card": { "borderStyle": "zigzag", "shape": "blob" }, "meta": { "mode": "grey" } }"""),
        ("enum-case", """{ "card": { "borderStyle": "Dashed", "borderWidth": 0.01 } }"""),
        ("colour-list", """{ "colors": { "ramp": ["#ff0000"], "cardTints": ["#ff0000", "bad"] } }"""),
        ("string-list", """{ "gauge": { "scale": { "labels": ["only"] }, "status": { "labels": ["a", "b"] } }, "meta": { "tags": [1, 2] } }"""),
        ("number-list", """{ "gauge": { "status": { "thresholds": [0.5, 7] } } }"""),
        ("name-list", """{ "card": { "cutCorners": ["tl", "up", "TL", "br"] } }"""),
        ("wrong-kind", """{ "card": { "shadow": { "blur": "big" }, "cutCorners": "tl" } }"""),
        ("wrong-kind-format1", """{ "card": { "gap": "wide" } }"""),
        ("missing-member", """{ "card": { "fillGradient": { "angle": 10 }, "borderGradient": { "kind": "conic" } }, "background": { "gradient": { "angle": 1 } } }"""),
        ("icon-tint", """{ "icons": { "tint": "purple" } }"""),
        ("stroke", """{ "stroke": { "thickness": 4, "cap": "flat" }, "gauge": { "style": "dial" } }"""),
        ("newer-format", """{ "format": 9 }"""),
        ("rule-border-none", """{ "card": { "borderWidth": 0, "borderStyle": "dashed", "borderGradient": { "colors": ["#ff0000", "#00ff00"] } } }"""),
        ("rule-spin", """{ "card": { "borderWidth": 0.01, "borderAnimate": { "mode": "spin" } } }"""),
        ("rule-pattern", """{ "pattern": { "kind": "dots", "spacing": 0.02, "thickness": 0.01 } }"""),
        ("rule-corner-marks", """{ "card": { "gap": 0.02, "cornerMarks": { "style": "bracket", "inset": -0.1 } } }"""),
        ("rule-animations", """{ "card": { "borderWidth": 0.01, "borderGradient": { "kind": "conic", "colors": ["#ff0000", "#00ff00"] }, "borderAnimate": { "mode": "spin" } } }"""),
        ("reach", """{ "colors": { "cardFill": "#20ffffff" }, "card": { "gap": 0.02, "shadow": { "blur": 0.3, "y": 0.1 }, "glow": { "blur": 0.4 } } }"""),
        ("hairline", """{ "card": { "borderWidth": 0.0004, "borderStyle": "solid" } }"""),
        ("font-missing", """{ "typography": { "font": "Nothing.ttf" } }"""),
        ("font-leaves", """{ "typography": { "font": "..\\Other.ttf", "fonts": { "value": "..\\Other.ttf", "unit": "/x/Abs.ttf" } } }"""),
        ("font-role-missing", $$"""{ "typography": { "font": "{{Mono}}", "fonts": { "value": "Gone.ttf", "label": "Gone2.otf" } } }"""),
        ("weight-range", $$"""{ "typography": { "font": "{{Mono}}", "weights": { "value": 900, "label": 50 } } }"""),
        ("weight-family", """{ "typography": { "font": "No Such Family", "weights": { "value": 300 } } }"""),
        ("compact-capsule", """{ "card": { "compact": { "shape": "circle" } } }"""),
    ];

    private static void Render(LoadedTheme loaded)
    {
        using var page = SamplePage.Build();
        using var bitmap = ThemePreview.Render(loaded, page, 1100, 1920, dock: true);
    }

    private string Capture(string title, Action work)
    {
        var text = new StringBuilder("== ").Append(title).Append('\n');
        var before = UrDeckLog.Listener;
        UrDeckLog.Listener = (level, message) =>
            text.Append(level).Append(' ').Append(message.Replace(_root, "<tmp>", StringComparison.Ordinal).Replace('\\', '/')).Append('\n');
        try
        {
            work();
        }
        finally
        {
            UrDeckLog.Listener = before;
        }
        return text.ToString();
    }

    private string Everything()
    {
        var all = new StringBuilder();

        foreach (string name in ThemeStore.BuiltInNames)
            all.Append(Capture($"built-in {name}", () => Render(new ThemeStore("").Load(name))));

        foreach (string path in Directory.GetDirectories(FixtureFolder).OrderBy(p => p, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(path);
            all.Append(Capture($"fixture {name}", () => Render(new ThemeStore(FixtureFolder).Load(name))));
        }

        string themes = Path.Combine(_root, "themes");
        foreach (var (name, json) in Failing)
        {
            string folder = Path.Combine(themes, name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, ThemeStore.SettingsFileName), json);
            foreach (string font in new[] { Mono, Sans })
            {
                using var stream = ThemeStore.OpenBuiltInFile(font);
                using var file = File.Create(Path.Combine(folder, font));
                stream!.CopyTo(file);
            }

            all.Append(Capture($"failing {name}", () => Render(new ThemeStore(themes).Load(name))));
        }

        all.Append(Capture("unknown", () => new ThemeStore(themes).Load("no-such-theme")));
        return all.ToString();
    }

    [Fact]
    public void The_log_text_is_the_recorded_text()
    {
        string actual = Everything();

        if (Environment.GetEnvironmentVariable("URDECK_UPDATE_SNAPSHOTS") == "1")
        {
            File.WriteAllText(SnapshotPath, actual);
            return;
        }

        Assert.True(File.Exists(SnapshotPath), "the snapshot is missing; record it with URDECK_UPDATE_SNAPSHOTS=1.");
        Assert.Equal(File.ReadAllText(SnapshotPath).ReplaceLineEndings("\n"), actual);
    }

    /// <summary>The recorded text must exercise every kind of check, or a call site could change unseen.</summary>
    [Theory]
    [InlineData("is not #RRGGBB or #AARRGGBB")]
    [InlineData("must be one to four colours")]
    [InlineData("is outside 0..1")]
    [InlineData("is outside 2..60")]
    [InlineData("is outside -360..360")]
    [InlineData("must be one of 'round', 'chamfer'")]
    [InlineData("colours (#RRGGBB or #AARRGGBB); using the default")]
    [InlineData("strings; using the default")]
    [InlineData("numbers between 0 and 1")]
    [InlineData("not one of 'tl', 'tr', 'br', 'bl'; dropped")]
    [InlineData("must be a number; using the default")]
    [InlineData("needs 'colors'; ignoring it")]
    [InlineData("must be 'accent', 'accent2'")]
    [InlineData("stroke.cap 'flat'")]
    [InlineData("gauge.style 'dial'")]
    [InlineData("declares format 9")]
    [InlineData("has no effect with card.borderWidth 0")]
    [InlineData("needs card.borderGradient of kind 'conic'")]
    [InlineData("pattern is too dense")]
    [InlineData("reaches past card.gap")]
    [InlineData("the cards are translucent")]
    [InlineData("under one pixel")]
    [InlineData("could not be loaded; using the default theme's font")]
    [InlineData("for the value role (typography.fonts.value) could not be loaded")]
    [InlineData("leaves the theme folder and is ignored")]
    [InlineData("for the value role leaves the theme folder")]
    [InlineData("is outside the weight range")]
    [InlineData("was not found; using")]
    public void The_recorded_text_covers_the_kind(string fragment)
    {
        Assert.Contains(fragment, File.ReadAllText(SnapshotPath));
    }
}
