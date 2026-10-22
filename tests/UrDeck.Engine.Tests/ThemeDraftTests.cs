// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using Json.Schema;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The four themes the design pass drafted that are not built in (Fixtures/themes), kept as fixtures: they are not built in, but each must satisfy
/// the published schema and load without a warning.
/// </summary>
public sealed class ThemeDraftTests : IDisposable
{
    private static readonly string[] Drafts = ["rigline", "ember", "blueprint", "pop"];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-draft-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _logBefore = UrDeckLog.LogPath;

    public ThemeDraftTests()
    {
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = Path.Combine(_root, "test.log");
    }

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        Directory.Delete(_root, recursive: true);
    }

    public static TheoryData<string> DraftNames() => [.. Drafts];

    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "themes");

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    // Built once: the library registers a schema under its $id, and a second copy is refused.
    private static readonly JsonSchema Schema = LoadSchema();

    private static JsonSchema LoadSchema()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "urdeck.slnx")))
            root = Path.GetDirectoryName(root)!;
        return JsonSchema.FromText(File.ReadAllText(Path.Combine(root, "docs", "schemas", "v2", "theme.json")));
    }

    private static bool IsValid(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List }).IsValid;
    }

    [Theory]
    [MemberData(nameof(DraftNames))]
    public void ADraft_SatisfiesTheSchema(string name)
    {
        Assert.True(IsValid(File.ReadAllText(Path.Combine(FixtureFolder, name, ThemeStore.SettingsFileName))));
    }

    [Fact]
    public void TheSchema_RefusesAThemeThatBreaksIt()
    {
        Assert.True(IsValid("""{ "card": { "shadow": { "blur": 0.2 } } }"""));
        Assert.False(IsValid("""{ "card": { "shadow": { "blur": 5 } } }"""));
        Assert.False(IsValid("""{ "card": { "borderStyle": "wavy" } }"""));
        Assert.False(IsValid("""{ "colors": { "ramp": ["#ff0000"] } }"""));
        Assert.False(IsValid("""{ "gauge": { "style": "plain" } }"""));
    }

    [Theory]
    [MemberData(nameof(DraftNames))]
    public void ADraft_LoadsWithNoWarning(string name)
    {
        var loaded = new ThemeStore(FixtureFolder).Load(name);

        Assert.Equal(name, loaded.Name);
        Assert.Equal(2, loaded.Definition.Format);
        Assert.NotNull(loaded.Definition.Meta?.Name);
        Assert.Empty(Log);
    }

    [Fact]
    public void TheDrafts_AreNotBuiltIn()
    {
        Assert.Equal(["default-dark", "default-light", "glass", "carbon", "paper", "prism", "obsidian"], ThemeStore.BuiltInNames);

        var loaded = new ThemeStore(Path.Combine(_root, "themes")).Load("rigline");

        Assert.Equal(ThemeStore.DefaultName, loaded.Name);
        Assert.Contains("Theme 'rigline' was not found", Log, StringComparison.Ordinal);
        foreach (string name in Drafts)
            Assert.Null(ThemeStore.LoadBuiltIn(name));
    }
}
