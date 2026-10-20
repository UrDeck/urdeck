// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The built-in themes Carbon and Paper: they load alone, in their own fonts and palettes, and pair with backgrounds.</summary>
public sealed class BuiltInCarbonPaperTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-carbonpaper-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public BuiltInCarbonPaperTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    public static TheoryData<string> Names() => ["carbon", "paper"];

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
    public void ABuiltIn_LoadsWithNoThemesFolder_AndNoFontWarning(string name)
    {
        var loaded = NoFolder().Load(name);
        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Equal(name, loaded.Name);
        Assert.Equal(2, loaded.Definition.Format);
        foreach (var role in Enum.GetValues<TextRole>())
            _ = theme.GetTypeface(role);
        Assert.Empty(Log);
    }

    [Fact]
    public void Carbon_UsesJetBrainsMonoForNumbersAndLabels_AndInstrumentSansForText()
    {
        using var theme = ThemeResolver.Resolve(NoFolder().Load("carbon"), 275);

        Assert.Contains("JetBrains Mono", theme.GetTypeface(TextRole.Value).FamilyName);
        Assert.Contains("JetBrains Mono", theme.GetTypeface(TextRole.Unit).FamilyName);
        Assert.Contains("JetBrains Mono", theme.GetTypeface(TextRole.Label).FamilyName);
        Assert.Contains("Instrument Sans", theme.GetTypeface(TextRole.Body).FamilyName);
    }

    [Fact]
    public void Paper_UsesInstrumentSansForValues_AndJetBrainsMonoForUnitsAndLabels()
    {
        using var theme = ThemeResolver.Resolve(NoFolder().Load("paper"), 275);

        Assert.Contains("Instrument Sans", theme.GetTypeface(TextRole.Value).FamilyName);
        Assert.Contains("Instrument Sans", theme.GetTypeface(TextRole.Body).FamilyName);
        Assert.Contains("JetBrains Mono", theme.GetTypeface(TextRole.Unit).FamilyName);
        Assert.Contains("JetBrains Mono", theme.GetTypeface(TextRole.Label).FamilyName);
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
    public void TheCatalog_ListsBoth_AndEachRecommendsContourFirst()
    {
        var entries = ThemeCatalog.List(NoFolder());
        var backgrounds = BackgroundCatalog.List(new ShaderStore(Path.Combine(_root, "no-backgrounds")));
        var index = new PairingIndex(entries, backgrounds);

        var carbon = Assert.Single(entries, e => e.Name == "carbon");
        var paper = Assert.Single(entries, e => e.Name == "paper");
        Assert.Equal(("Carbon", "dark", true), (carbon.DisplayName, carbon.Mode, carbon.IsBuiltIn));
        Assert.Equal(("Paper", "light", true), (paper.DisplayName, paper.Mode, paper.IsBuiltIn));
        foreach (var entry in new[] { carbon, paper })
        {
            Assert.NotEmpty(entry.PairsWithBackgrounds);
            Assert.All(entry.PairsWithBackgrounds, b => Assert.Contains(backgrounds, x => x.Name == b));
            Assert.Equal("contour", index.Recommend(entry, backgrounds, 5)[0].Background);
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void WithNoBackgroundInTheConfig_TheThemeDefaultShaderComesFirst_ThenItsStillColour(string name)
    {
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, NoFolder().Load(name));

        Assert.Equal(BackgroundOrigin.ThemeDefault, plan.Candidates[0].Origin);
        Assert.Equal("contour", plan.Candidates[0].Settings!.Name);
        Assert.Equal(BackgroundOrigin.ThemeStill, plan.Candidates[1].Origin);
        Assert.NotNull(plan.Candidates[1].Still!.Color);
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
