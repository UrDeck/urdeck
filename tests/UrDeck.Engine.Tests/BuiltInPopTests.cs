// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The built-in theme Pop: it loads alone, in its own font and palette, and pairs with backgrounds.</summary>
public sealed class BuiltInPopTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-pop-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public BuiltInPopTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    private ThemeStore NoFolder() => new(Path.Combine(_root, "no-themes"));

    private static JsonElement ColorsOfPop()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "urdeck.slnx")))
            root = Path.GetDirectoryName(root)!;
        string path = Path.Combine(root, "src", "UrDeck.Engine", "Themes", "Builtin", "pop.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("colors").Clone();
    }

    [Fact]
    public void Pop_LoadsWithNoThemesFolder_AndNoFontOrWeightWarning()
    {
        var loaded = NoFolder().Load("pop");
        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Equal("pop", loaded.Name);
        Assert.Equal(2, loaded.Definition.Format);
        foreach (var role in Enum.GetValues<TextRole>())
            _ = theme.GetTypeface(role);
        Assert.Empty(Log);
    }

    [Fact]
    public void Pop_UsesArchivoForEveryRole_AndWeight900ForValues()
    {
        using var theme = ThemeResolver.Resolve(NoFolder().Load("pop"), 275);

        foreach (var role in Enum.GetValues<TextRole>())
            Assert.Contains("Archivo", theme.GetTypeface(role).FamilyName);
        Assert.Equal(900, theme.GetTypeface(TextRole.Value).FontWeight);
    }

    [Fact]
    public void Pop_DropsTheValueScaleThatHadNoEffect()
    {
        var typography = NoFolder().Load("pop").Definition.Typography!;

        Assert.Null(typography.ValueScale);
    }

    [Fact]
    public void EveryVersion1Colour_IsTheOneInPopsOwnFile()
    {
        var expected = ColorsOfPop();
        var actual = NoFolder().Load("pop").Definition.Colors!;

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
    public void TheCatalog_ListsPop_AndRecommendsShapesFirst()
    {
        var entries = ThemeCatalog.List(NoFolder());
        var backgrounds = BackgroundCatalog.List(new ShaderStore(Path.Combine(_root, "no-backgrounds")));
        var index = new PairingIndex(entries, backgrounds);

        var pop = Assert.Single(entries, e => e.Name == "pop");
        Assert.Equal(("Pop", "light", true), (pop.DisplayName, pop.Mode, pop.IsBuiltIn));
        Assert.Equal(["shapes", "ripple"], pop.PairsWithBackgrounds);
        Assert.All(pop.PairsWithBackgrounds, b => Assert.Contains(backgrounds, x => x.Name == b));
        Assert.Equal("shapes", index.Recommend(pop, backgrounds, 5)[0].Background);
    }

    [Fact]
    public void WithNoBackgroundInTheConfig_TheShapesShaderComesFirst_ThenItsStillColour()
    {
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, NoFolder().Load("pop"));

        Assert.Equal(BackgroundOrigin.ThemeDefault, plan.Candidates[0].Origin);
        Assert.Equal("shapes", plan.Candidates[0].Settings!.Name);
        Assert.Equal(BackgroundOrigin.ThemeStill, plan.Candidates[1].Origin);
        Assert.NotNull(plan.Candidates[1].Still!.Color);
    }

    [Fact]
    public void AUserThemeFolderNamedPop_TakesPrecedence()
    {
        string folder = Path.Combine(_root, "themes", "pop");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ThemeStore.SettingsFileName), """{ "colors": { "accent": "#123456" } }""");

        var loaded = new ThemeStore(Path.Combine(_root, "themes")).Load("pop");

        Assert.Equal("#123456", loaded.Definition.Colors!.Accent);
    }
}
