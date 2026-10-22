// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The built-in theme Rigline: it loads alone, in its own font and palette, pairs with backgrounds and turns its border.</summary>
public sealed class BuiltInRiglineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-rigline-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public BuiltInRiglineTests()
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

    private static string PathOfRigline()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "urdeck.slnx")))
            root = Path.GetDirectoryName(root)!;
        return Path.Combine(root, "src", "UrDeck.Engine", "Themes", "Builtin", "rigline.json");
    }

    private static JsonElement ColorsOfRigline()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(PathOfRigline()));
        return document.RootElement.GetProperty("colors").Clone();
    }

    [Fact]
    public void Rigline_LoadsWithNoThemesFolder_AndNoFontOrWeightWarning()
    {
        var loaded = NoFolder().Load("rigline");
        using var theme = ThemeResolver.Resolve(loaded, 275);

        Assert.Equal("rigline", loaded.Name);
        Assert.Equal(2, loaded.Definition.Format);
        foreach (var role in Enum.GetValues<TextRole>())
            _ = theme.GetTypeface(role);
        Assert.Empty(Log);
    }

    [Fact]
    public void Rigline_UsesChakraPetchBoldForEveryRole()
    {
        using var theme = ThemeResolver.Resolve(NoFolder().Load("rigline"), 275);

        foreach (var role in Enum.GetValues<TextRole>())
        {
            Assert.Contains("Chakra Petch", theme.GetTypeface(role).FamilyName);
            Assert.Equal(700, theme.GetTypeface(role).FontWeight);
        }
    }

    [Fact]
    public void Rigline_AsksForNothingTheFileCannotDo()
    {
        var typography = NoFolder().Load("rigline").Definition.Typography!;

        Assert.True((typography.ValueScale ?? 1) <= 1);
        Assert.Equal(10, typography.Slant);
        Assert.All([typography.Weights!.Value, typography.Weights.Unit, typography.Weights.Label, typography.Weights.Body, typography.Weights.Title], w => Assert.Equal(700, w));

        string text = File.ReadAllText(PathOfRigline());
        Assert.DoesNotContain("\"animate\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"scroll\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryVersion1Colour_IsTheOneInRiglinesOwnFile()
    {
        var expected = ColorsOfRigline();
        var actual = NoFolder().Load("rigline").Definition.Colors!;

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
    public void TheCatalog_ListsRigline_AndRecommendsHorizonFirst()
    {
        var entries = ThemeCatalog.List(NoFolder());
        var backgrounds = BackgroundCatalog.List(new ShaderStore(Path.Combine(_root, "no-backgrounds")));
        var index = new PairingIndex(entries, backgrounds);

        var rigline = Assert.Single(entries, e => e.Name == "rigline");
        Assert.Equal(("Rigline", "dark", true), (rigline.DisplayName, rigline.Mode, rigline.IsBuiltIn));
        Assert.Equal("horizon", rigline.PairsWithBackgrounds[0]);
        Assert.All(rigline.PairsWithBackgrounds, b => Assert.Contains(backgrounds, x => x.Name == b));
        Assert.Equal("horizon", index.Recommend(rigline, backgrounds, 5)[0].Background);
    }

    [Fact]
    public void WithNoBackgroundInTheConfig_HorizonComesFirst_ThenItsStillGradient()
    {
        var plan = BackgroundPlan.Resolve(BackgroundSettings.None, NoFolder().Load("rigline"));

        Assert.Equal(BackgroundOrigin.ThemeDefault, plan.Candidates[0].Origin);
        Assert.Equal("horizon", plan.Candidates[0].Settings!.Name);
        Assert.Equal(BackgroundOrigin.ThemeStill, plan.Candidates[1].Origin);
        Assert.NotNull(plan.Candidates[1].Still!.Gradient);
    }

    [Fact]
    public void AUserThemeFolderNamedRigline_TakesPrecedence()
    {
        string folder = Path.Combine(_root, "themes", "rigline");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ThemeStore.SettingsFileName), """{ "colors": { "accent": "#123456" } }""");

        var loaded = new ThemeStore(Path.Combine(_root, "themes")).Load("rigline");

        Assert.Equal("#123456", loaded.Definition.Colors!.Accent);
    }

    [Theory]
    [InlineData(null, MotionLevel.Full, true)]
    [InlineData("reduced", MotionLevel.Reduced, false)]
    [InlineData("off", MotionLevel.Off, false)]
    public void TheBorderTurnsOnlyAtFull(string? configured, MotionLevel expected, bool turns)
    {
        var loaded = NoFolder().Load("rigline");
        using var style = ResolvedCardStyle.Resolve(loaded, 275);

        var decision = MotionPolicy.Decide(configured, windowsAnimations: true, loaded.Definition.Motion!.Level);

        Assert.Equal(expected, decision.Effective);
        Assert.Equal(BorderAnimationMode.Spin, style.BorderAnimation!.Mode);
        Assert.Equal(7, style.BorderAnimation.PeriodSeconds);
        // The host starts the sprites only at Full; the style itself is the same at every level.
        Assert.Equal(turns, decision.Effective == MotionLevel.Full);
    }
}
