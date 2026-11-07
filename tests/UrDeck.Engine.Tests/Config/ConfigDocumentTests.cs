// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests.Config;

/// <summary>A file system in memory: the document's logic without a disk or a watcher.</summary>
internal sealed class FakeConfigFileSystem : IConfigFileSystem
{
    public byte[]? Content { get; set; }

    public int Writes { get; private set; }

    public bool FailWrites { get; set; }

    public List<(string Name, byte[] Bytes)> Backups { get; } = [];

    public string Description => "urdeck-config.json";

    public event Action? Changed;

    public string Text => Encoding.UTF8.GetString(Content!);

    public byte[]? Read() => Content;

    public void Write(byte[] bytes)
    {
        if (FailWrites)
            throw new IOException("locked");
        Content = bytes;
        Writes++;
    }

    public void WriteBackup(string name, byte[] bytes) => Backups.Add((name, bytes));

    /// <summary>Another program saved the file.</summary>
    public void SaveOutside(string text)
    {
        Content = Encoding.UTF8.GetBytes(text);
        Changed?.Invoke();
    }

    public void Dispose()
    {
    }
}

public class ConfigDocumentTests
{
    private const string Widgets = "widgets";

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ConfigDocument", name)).Replace("\r\n", "\n");

    private static ConfigDocument Open(string json, FakeTime? time = null) =>
        new((JsonObject)JsonNode.Parse(json)!, readOnly: false, time: time ?? new FakeTime(), post: action => action());

    private static string Text(ConfigDocument document) => document.Root.ToJsonString(UrDeckJson.Options).Replace("\r\n", "\n") + "\n";

    [Theory]
    [InlineData("set-nested")]
    [InlineData("remove-key")]
    [InlineData("insert-move")]
    [InlineData("unknown-survive")]
    public void Edits_ProduceTheExpectedText(string name)
    {
        var document = Open(Fixture(Widgets + ".input.json"));

        document.Edit(name, e =>
        {
            switch (name)
            {
                case "set-nested":
                    e.Set("/pages/0/widgets/0/format", "12h");
                    break;
                case "remove-key":
                    e.Set("/pages/0/widgets/0/format", null); // null removes
                    break;
                case "insert-move":
                    e.Insert("/pages/0/widgets", 2, JsonNode.Parse("""{ "typeId": "urdeck.widgets.stats", "col": 0, "row": 1 }""")!);
                    e.Move("/pages/0/widgets", 1, 0);
                    break;
                case "unknown-survive":
                    e.Set("/pages/0/widgets/0/label", "Time");
                    break;
            }
        });

        Assert.Equal(Fixture(name + ".expected.json"), Text(document));
    }

    [Fact]
    public void Paths_UnescapeSlashAndTilde()
    {
        var document = Open("""{ "a/b": { "c~d": 1 } }""");

        document.Edit("set", e => e.Set("/a~1b/c~0d", 2));

        Assert.Equal(2, (int)document.Root["a/b"]!["c~d"]!);
        Assert.Equal("/a~1b/c~0d", JsonPointer.Join(["a/b", "c~d"]));
    }

    [Fact]
    public void SettingCreatesMissingObjects_ButNotMissingArrayEntries()
    {
        var document = Open("""{ "pages": [] }""");

        document.Edit("set", e => e.Set("/providers/ha/url", "x"));
        Assert.Equal("x", (string)document.Root["providers"]!["ha"]!["url"]!);

        Assert.Throws<ArgumentException>(() => document.Edit("bad", e => e.Set("/pages/3/name", "x")));
        Assert.Throws<ArgumentException>(() => document.Edit("bad", e => e.Insert("/pages", 5, JsonValue.Create(1))));
        Assert.Throws<ArgumentException>(() => document.Edit("bad", e => e.Move("/pages", 0, 1)));
    }

    [Fact]
    public void ARemovedMissingKey_IsNothing_AndNothingIsAnnounced()
    {
        var document = Open("""{ "theme": "a" }""");
        int announced = 0;
        document.Changed += _ => announced++;

        document.Edit("remove", e => e.Remove("/nope/deeper"));
        document.Edit("same", e => e.Set("/theme", "a"));

        Assert.Equal(0, announced);
        Assert.Equal(0, document.Revision);
        Assert.False(document.History.CanUndo);
    }

    [Fact]
    public void AFailedTransaction_ChangesNothing_AndAnnouncesNothing()
    {
        var document = Open("""{ "theme": "a", "pages": [] }""");
        int announced = 0;
        document.Changed += _ => announced++;

        Assert.Throws<ArgumentException>(() => document.Edit("two", e =>
        {
            e.Set("/theme", "b");
            e.Set("/pages/9/name", "x");
        }));

        Assert.Equal("a", (string)document.Root["theme"]!);
        Assert.Equal(0, announced);
        Assert.Equal(0, document.Revision);
        Assert.False(document.History.CanUndo);
    }

    [Fact]
    public void ChangedCarriesTheOriginAndThePaths_AndTypedIsCurrent()
    {
        var document = Open("""{ "theme": "a" }""");
        ConfigChange? seen = null;
        string? themeSeen = null;
        document.Changed += c =>
        {
            seen = c;
            themeSeen = document.Typed.Theme;
        };

        document.Edit("Theme", e => e.Set("/theme", "b"), EditOrigin.Host);

        Assert.Equal(new[] { "/theme" }, seen!.Paths);
        Assert.Equal((EditOrigin.Host, "Theme", 1), (seen.Origin, seen.Label, seen.Revision));
        Assert.Equal("b", themeSeen);
    }

    [Fact]
    public void AValueOfTheWrongType_IsRefused_AndRolledBack()
    {
        var document = Open("""{ "pages": [] }""");

        Assert.ThrowsAny<JsonException>(() => document.Edit("bad", e => e.Set("/activePage", "not a number")));

        Assert.Null(document.Root["activePage"]);
        Assert.Equal(0, document.Revision);
    }

    // ---- undo and redo

    [Fact]
    public void UndoEverything_RestoresTheOriginal_AndRedoEverything_TheFinal()
    {
        var document = Open(Fixture("widgets.input.json"));
        string original = Text(document);
        for (int i = 1; i <= 5; i++)
        {
            int n = i;
            document.Edit($"Change {n}", e => e.Set("/pages/0/name", $"Page {n}"));
        }

        string final = Text(document);
        while (document.History.Undo())
        {
        }

        Assert.Equal(original, Text(document));
        while (document.History.Redo())
        {
        }

        Assert.Equal(final, Text(document));
    }

    [Fact]
    public void UndoAndRedo_AreAnnouncedAsUserTransactions_WithLabels()
    {
        var document = Open("""{ "theme": "a" }""");
        var seen = new List<ConfigChange>();
        document.Changed += seen.Add;
        document.Edit("Set theme", e => e.Set("/theme", "b"));

        Assert.Equal("Set theme", document.History.UndoLabel);
        document.History.Undo();
        Assert.Equal("Set theme", document.History.RedoLabel);
        document.History.Redo();

        Assert.Equal(["Set theme", "Undo Set theme", "Redo Set theme"], seen.Select(c => c.Label));
        Assert.All(seen, c => Assert.Equal(EditOrigin.User, c.Origin));
        Assert.Equal(new[] { "/" }, seen[1].Paths);
        Assert.Equal("b", document.Typed.Theme);
    }

    [Fact]
    public void ANewEdit_ClearsRedo()
    {
        var document = Open("""{ "theme": "a" }""");
        document.Edit("one", e => e.Set("/theme", "b"));
        document.History.Undo();
        Assert.True(document.History.CanRedo);

        document.Edit("two", e => e.Set("/theme", "c"));

        Assert.False(document.History.CanRedo);
    }

    [Fact]
    public void TypingIntoOneField_IsOneStep_WithinASecond_AndNotAfter()
    {
        var time = new FakeTime();
        var document = Open("""{ "pages": [ { "name": "Main", "widgets": [ { "label": "" } ] } ] }""", time);
        const string Path = "/pages/0/widgets/0/label";

        foreach (string text in new[] { "G", "GP", "GPU", "GPU t" })
        {
            document.Edit("Label", e => e.Set(Path, text), mergeKey: Path);
            time.Advance(TimeSpan.FromMilliseconds(300));
        }

        document.History.Undo();
        Assert.Equal("", (string)document.Root["pages"]![0]!["widgets"]![0]!["label"]!);
        Assert.False(document.History.CanUndo);

        document.Edit("Label", e => e.Set(Path, "a"), mergeKey: Path);
        time.Advance(TimeSpan.FromMilliseconds(1100));
        document.Edit("Label", e => e.Set(Path, "ab"), mergeKey: Path);
        document.History.Undo();
        Assert.Equal("a", (string)document.Root["pages"]![0]!["widgets"]![0]!["label"]!);
        Assert.True(document.History.CanUndo);
    }

    [Fact]
    public void ADifferentKey_OrNoKey_IsNeverMerged()
    {
        var document = Open("""{ "a": 1, "b": 1 }""");
        document.Edit("a", e => e.Set("/a", 2), mergeKey: "/a");
        document.Edit("b", e => e.Set("/b", 2), mergeKey: "/b");
        document.Edit("b again", e => e.Set("/b", 3));
        document.Edit("b again", e => e.Set("/b", 4));

        int steps = 0;
        while (document.History.Undo())
            steps++;

        Assert.Equal(4, steps);
    }

    [Fact]
    public void AHostEdit_IsNotAStep_AndSurvivesUndo()
    {
        var document = Open("""{ "theme": "a" }""");
        document.Edit("Theme", e => e.Set("/theme", "b"));
        document.Edit("Remember the display", e => e.Set("/display", JsonNode.Parse("""{ "name": "Y70" }""")), EditOrigin.Host);

        document.History.Undo();

        Assert.Equal("a", document.Typed.Theme);
        Assert.Equal("Y70", document.Typed.Display!.Name);
        Assert.False(document.History.CanUndo);

        document.History.Redo();
        Assert.Equal("b", document.Typed.Theme);
        Assert.Equal("Y70", document.Typed.Display!.Name);
    }

    [Fact]
    public void AHostRemoval_IsAlsoCarriedIntoTheHistory()
    {
        var document = Open("""{ "providers": { "ha": { "url": "x", "token": "secret" } } }""");
        document.Edit("Url", e => e.Set("/providers/ha/url", "y"));
        document.Edit("Move secret", e => e.Remove("/providers/ha/token"), EditOrigin.Host);

        document.History.Undo();

        Assert.Equal("x", (string)document.Root["providers"]!["ha"]!["url"]!);
        Assert.Null(document.Root["providers"]!["ha"]!["token"]);
    }

    [Fact]
    public void TheHistoryKeepsAHundredSteps()
    {
        var document = Open("""{ "n": 0 }""");
        for (int i = 1; i <= 120; i++)
        {
            int n = i;
            document.Edit("n", e => e.Set("/n", n));
        }

        int steps = 0;
        while (document.History.Undo())
            steps++;

        Assert.Equal(ConfigHistory.Limit, steps);
        Assert.Equal(20, (int)document.Root["n"]!);
    }

    [Fact]
    public void StateChanged_IsRaisedWhenUndoOrRedoBecomesPossible()
    {
        var document = Open("""{ "n": 0 }""");
        int raised = 0;
        document.History.StateChanged += () => raised++;

        document.Edit("n", e => e.Set("/n", 1));
        document.History.Undo();

        Assert.Equal(2, raised);
    }

    // ---- read-only

    [Fact]
    public void AReadOnlyDocument_RefusesEdits()
    {
        var document = new ConfigDocument(new JsonObject { ["theme"] = "a" }, readOnly: true);

        Assert.False(document.Edit("x", e => e.Set("/theme", "b")));
        Assert.Equal("a", (string)document.Root["theme"]!);
    }
}
