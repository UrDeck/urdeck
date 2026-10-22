// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>What holds for every built-in background together: the list, the pairing data and the time wrap.</summary>
public sealed class BuiltInShaderTests : IDisposable
{
    private const string Prefix = "UrDeck.Backgrounds.";

    private readonly BackgroundFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    public static TheoryData<string> Names() => [.. ShaderStore.BuiltInNames];

    // The four first built-ins drift without a period and jump once at the wrap (docs/backgrounds.md, "Time"); the twelve
    // added later are periodic.
    public static TheoryData<string> PeriodicNames() => [.. ShaderStore.BuiltInNames.Except(["plasma", "aurora", "gradient", "julia"])];

    private static string[] ReadNames(JsonElement list) => [.. list.EnumerateArray().Select(e => e.GetString()!)];

    // Background name -> the themes its manifest names.
    private static Dictionary<string, string[]> BackgroundPairs() =>
        ShaderStore.BuiltInNames.ToDictionary(name => name, name =>
        {
            using var manifest = JsonDocument.Parse(new EmbeddedBackgroundSource(name).ReadText("manifest.json")!);
            return manifest.RootElement.TryGetProperty("pairsWith", out var pairs) && pairs.TryGetProperty("themes", out var themes)
                ? ReadNames(themes)
                : [];
        });

    // Theme name -> the backgrounds its meta names: the built-in themes that have a pairing list.
    private static Dictionary<string, string[]> ThemePairs()
    {
        var texts = new Dictionary<string, string>();
        foreach (string name in new[] { "carbon", "paper", "prism", "obsidian", "ember", "blueprint", "pop", "rigline" })
            texts[name] = EmbeddedThemeText(name);

        return texts.ToDictionary(t => t.Key, t =>
        {
            using var theme = JsonDocument.Parse(t.Value);
            return theme.RootElement.GetProperty("meta").TryGetProperty("pairsWith", out var pairs) && pairs.TryGetProperty("backgrounds", out var list)
                ? ReadNames(list)
                : [];
        });
    }

    private static string EmbeddedThemeText(string name)
    {
        using var stream = typeof(ShaderStore).Assembly.GetManifestResourceStream("UrDeck.Themes." + name + ".json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Every problem in the pairing data of the given backgrounds and themes; empty when it is sound.</summary>
    private static List<string> PairingProblems(Dictionary<string, string[]> backgrounds, Dictionary<string, string[]> themes)
    {
        var problems = new List<string>();
        foreach ((string background, string[] named) in backgrounds)
        {
            foreach (string theme in named)
            {
                if (!themes.TryGetValue(theme, out string[]? back))
                    problems.Add($"background '{background}' names the unknown theme '{theme}'");
                else if (!back.Contains(background))
                    problems.Add($"background '{background}' names '{theme}', which does not list it");
            }

            if (named.Length < 2)
                problems.Add($"background '{background}' names {named.Length} themes, at least two are required");
        }

        foreach ((string theme, string[] named) in themes)
        {
            foreach (string background in named)
            {
                if (!backgrounds.TryGetValue(background, out string[]? back))
                    problems.Add($"theme '{theme}' names the unknown background '{background}'");
                else if (!back.Contains(theme))
                    problems.Add($"theme '{theme}' names '{background}', which does not list it");
            }

            if (named.Length is < 2 or > 7)
                problems.Add($"theme '{theme}' names {named.Length} backgrounds, between two and seven are required");
        }

        return problems;
    }

    [Fact]
    public void The_built_in_names_and_the_embedded_folders_agree()
    {
        string[] embedded = [.. typeof(ShaderStore).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith("/manifest.json", StringComparison.Ordinal))
            .Select(n => n[Prefix.Length..n.LastIndexOf('/')])
            .Order(StringComparer.Ordinal)];

        Assert.Equal(16, ShaderStore.BuiltInNames.Count);
        Assert.Equal(ShaderStore.BuiltInNames.Order(StringComparer.Ordinal), embedded);
    }

    [Theory]
    [InlineData("docs", "backgrounds.md")]
    [InlineData("site", "src", "content", "docs", "backgrounds.md")]
    public void Every_built_in_is_listed_in_the_documentation(params string[] path)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "urdeck.slnx")))
            root = Path.GetDirectoryName(root)!;
        string text = File.ReadAllText(Path.Combine([root, .. path]));

        foreach (string name in ShaderStore.BuiltInNames)
            Assert.Contains($"| `{name}` |", text);
    }

    [Fact]
    public void The_built_in_pairing_data_is_mutual()
    {
        // Only the twelve new backgrounds carry pairing data; the first four are generic washes and name no theme.
        var backgrounds = BackgroundPairs().Where(p => p.Value.Length > 0).ToDictionary(p => p.Key, p => p.Value);

        Assert.Equal(12, backgrounds.Count);
        Assert.Empty(PairingProblems(backgrounds, ThemePairs()));
    }

    [Fact]
    public void The_pairing_check_fails_on_a_pair_named_by_one_side()
    {
        var backgrounds = new Dictionary<string, string[]> { ["a"] = ["t1", "t2"], ["b"] = ["t1", "t2"] };
        var themes = new Dictionary<string, string[]> { ["t1"] = ["a", "b"], ["t2"] = ["a", "b"] };
        Assert.Empty(PairingProblems(backgrounds, themes));

        themes["t2"] = ["a", "b", "c"];
        backgrounds["c"] = ["t1", "t2"];
        Assert.Contains(PairingProblems(backgrounds, themes), p => p.Contains("'t1'") && p.Contains("'c'"));

        backgrounds["c"] = ["t2", "t3"];
        Assert.Contains(PairingProblems(backgrounds, themes), p => p.Contains("unknown theme 't3'"));
    }

    [Fact]
    public void The_pairing_check_fails_on_an_unknown_background_and_on_counts()
    {
        var backgrounds = new Dictionary<string, string[]> { ["a"] = ["t1"], ["b"] = ["t1", "t2"] };
        var themes = new Dictionary<string, string[]> { ["t1"] = ["a", "b", "zz"], ["t2"] = ["b"] };

        var problems = PairingProblems(backgrounds, themes);

        Assert.Contains(problems, p => p.Contains("unknown background 'zz'"));
        Assert.Contains(problems, p => p.Contains("background 'a' names 1 themes"));
        Assert.Contains(problems, p => p.Contains("theme 't2' names 1 backgrounds"));
    }

    [Theory]
    [MemberData(nameof(PeriodicNames))]
    public void A_built_in_repeats_across_the_time_wrap(string name)
    {
        using var background = _fixture.Create($$"""{ "type": "shader", "name": "{{name}}" }""");
        Assert.NotNull(background);

        using var start = Render(background, 0);
        using var wrapped = Render(background, (float)BackgroundClock.WrapSeconds);

        // A float sine of a large argument moves a bar edge by a pixel (equalizer): rounding, not a jump.
        int differing = 0;
        for (int y = 0; y < start.Height; y++)
            for (int x = 0; x < start.Width; x++)
            {
                SKColor a = start.GetPixel(x, y), b = wrapped.GetPixel(x, y);
                if (Math.Max(Math.Abs(a.Red - b.Red), Math.Max(Math.Abs(a.Green - b.Green), Math.Abs(a.Blue - b.Blue))) > 3)
                    differing++;
            }

        Assert.True(differing <= start.Width * start.Height / 100,
            $"{name}: {differing} pixels differ between time 0 and {BackgroundClock.WrapSeconds}");
    }

    private static SKBitmap Render(DeckBackground background, float time)
    {
        var bitmap = new SKBitmap(new SKImageInfo(64, 256, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        background.PaintAtTime(canvas, 64, 256, time);
        return bitmap;
    }
}
