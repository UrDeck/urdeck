// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using Json.Schema;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>
/// The built-in themes against the published schema: each must satisfy it and load without a warning.
/// </summary>
public sealed class ThemeDraftTests : IDisposable
{
    private static readonly string[] Drafts = ["carbon", "paper", "prism", "obsidian", "ember", "blueprint", "pop", "rigline"];

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

    private static string Log => File.Exists(UrDeckLog.LogPath) ? File.ReadAllText(UrDeckLog.LogPath) : "";

    // Built once: the library registers a schema under its $id, and a second copy is refused.
    private static readonly JsonSchema Schema = LoadSchema();

    private static JsonSchema LoadSchema()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "urdeck.slnx")))
            root = Path.GetDirectoryName(root)!;
        return JsonSchema.FromText(File.ReadAllText(Path.Combine(root, "docs", "schemas", "v1", "theme.json")));
    }

    private static string BuiltInText(string name)
    {
        using var stream = ThemeStore.OpenBuiltInFile(name + ".json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static bool IsValid(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List }).IsValid;
    }

    [Theory]
    [MemberData(nameof(DraftNames))]
    public void ABuiltIn_SatisfiesTheSchema(string name)
    {
        Assert.True(IsValid(BuiltInText(name)));
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
    public void ABuiltIn_LoadsWithNoWarning(string name)
    {
        var loaded = new ThemeStore("").Load(name);

        Assert.Equal(name, loaded.Name);
        Assert.NotNull(loaded.Definition.Meta?.Name);
        Assert.Empty(Log);
    }
}
