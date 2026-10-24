// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The built-in themes Prism and Obsidian: they load alone, in their own fonts and palettes, and pair with backgrounds.</summary>
public sealed class BuiltInPrismObsidianTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-prismobsidian-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public BuiltInPrismObsidianTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    public static TheoryData<string> Names() => ["prism", "obsidian"];

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    private ThemeStore NoFolder() => new(Path.Combine(_root, "no-themes"));

    private static JsonElement ColorsOf(string name)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "urdeck.slnx")))
            root = Path.GetDirectoryName(root)!;
        string path = Path.Combine(root, "src", "UrDeck.Engine", "Themes", "Builtin", name + ".json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("colors").Clone();
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void ABuiltIn_LoadsWithNoThemesFolder_AndNoFontOrWeightWarning(string name)
    {
        var loaded = NoFolder().Load(name);
        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Equal(name, loaded.Name);
        foreach (var role in Enum.GetValues<TextRole>())
            _ = theme.GetTypeface(role);
        Assert.Empty(Log);
    }

    [Fact]
    public void Prism_UsesBricolageGrotesqueThroughout()
    {
        using var theme = ThemeResolver.Resolve(NoFolder().Load("prism"), 275);

        foreach (var role in new[] { TextRole.Value, TextRole.Unit, TextRole.Label, TextRole.Body })
            Assert.Contains("Bricolage Grotesque", theme.GetTypeface(role).FamilyName);
    }

    [Fact]
    public void Obsidian_UsesBodoniModaForValues_AndJostForUnitsLabelsAndText()
    {
        using var theme = ThemeResolver.Resolve(NoFolder().Load("obsidian"), 275);

        Assert.Contains("Bodoni Moda", theme.GetTypeface(TextRole.Value).FamilyName);
        Assert.Contains("Jost", theme.GetTypeface(TextRole.Unit).FamilyName);
        Assert.Contains("Jost", theme.GetTypeface(TextRole.Label).FamilyName);
        Assert.Contains("Jost", theme.GetTypeface(TextRole.Body).FamilyName);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void EveryVersion1Colour_IsTheOneInTheThemesOwnFile(string name)
    {
        var expected = ColorsOf(name);
        var actual = NoFolder().Load(name).Definition.Colors!;

        Assert.Equal(expected.GetProperty("background").GetString(), actual.Background);
        Assert.Equal(expected.GetProperty("cardFill").GetString(), actual.CardFill);
        Assert.Equal(expected.GetProperty("cardBorder").GetString(), actual.CardBorder);
        Assert.Equal(expected.GetProperty("text").GetString(), actual.Text);
        Assert.Equal(expected.GetProperty("textMuted").GetString(), actual.TextMuted);
        Assert.Equal(expected.GetProperty("accent").GetString(), actual.Accent);
        Assert.Equal(expected.GetProperty("accentDim").GetString(), actual.AccentDim);
        Assert.Equal(expected.GetProperty("good").GetString(), actual.Good);
        Assert.Equal(expected.GetProperty("warning").GetString(), actual.Warning);
        Assert.Equal(expected.GetProperty("critical").GetString(), actual.Critical);
    }

    [Fact]
    public void TheCatalog_ListsBoth_AndEachRecommendsItsFirstPairedBackgroundFirst()
    {
        var entries = ThemeCatalog.List(NoFolder());
        var backgrounds = BackgroundCatalog.List(new ShaderStore(Path.Combine(_root, "no-backgrounds")));
        var index = new PairingIndex(entries, backgrounds);

        var prism = Assert.Single(entries, e => e.Name == "prism");
        var obsidian = Assert.Single(entries, e => e.Name == "obsidian");
        Assert.Equal(("Prism", "dark", true), (prism.DisplayName, prism.Mode, prism.IsBuiltIn));
        Assert.Equal(("Obsidian", "dark", true), (obsidian.DisplayName, obsidian.Mode, obsidian.IsBuiltIn));
        foreach (var entry in new[] { prism, obsidian })
        {
            Assert.NotEmpty(entry.PairsWithBackgrounds);
            Assert.All(entry.PairsWithBackgrounds, b => Assert.Contains(backgrounds, x => x.Name == b));
            Assert.Equal(entry.PairsWithBackgrounds[0], index.Recommend(entry, backgrounds, 5)[0].Background);
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void WithNoBackgroundInTheConfig_TheThemeDefaultShaderComesFirst_ThenItsStillGradient(string name)
    {
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, NoFolder().Load(name));

        Assert.Equal(BackgroundOrigin.ThemeDefault, plan.Candidates[0].Origin);
        Assert.Equal("nebula", plan.Candidates[0].Settings!.Name);
        Assert.Equal(BackgroundOrigin.ThemeStill, plan.Candidates[1].Origin);
        Assert.NotNull(plan.Candidates[1].Still!.Gradient);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void AUserThemeFolderOfTheSameName_TakesPrecedence(string name)
    {
        string folder = Path.Combine(_root, "themes", name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ThemeStore.SettingsFileName), """{ "colors": { "accent": "#123456" } }""");

        var loaded = new ThemeStore(Path.Combine(_root, "themes")).Load(name);

        Assert.Equal("#123456", loaded.Definition.Colors!.Accent);
    }
}
