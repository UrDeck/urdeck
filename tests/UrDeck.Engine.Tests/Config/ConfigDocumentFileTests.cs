// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests.Config;

public class ConfigDocumentFileTests
{
    private static readonly TimeSpan Delay = ConfigDocument.WriteDelay;

    private sealed class Harness
    {
        public FakeTime Time { get; } = new();

        public FakeConfigFileSystem Files { get; } = new();

        public ConfigDocument Document { get; }

        public List<ConfigProblem> Problems { get; } = [];

        public List<ConfigChange> Changes { get; } = [];

        public Harness(string initial)
        {
            Files.Content = Encoding.UTF8.GetBytes(initial);
            var root = Read(initial, Files.Content).Root;
            Document = new ConfigDocument(root, readOnly: false, Files, Read, Time, action => action());
            Document.NoteFileContent(Files.Content);
            Document.Problem += Problems.Add;
            Document.Changed += Changes.Add;
        }

        // The reader the store gives the document, reduced: comments allowed, version 2 and up is newer.
        private static ParsedConfig Read(string text, byte[] original)
        {
            var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            var root = (JsonObject)JsonNode.Parse(text, documentOptions: options)!;
            bool newer = root["version"] is JsonValue v && v.TryGetValue<int>(out int n) && n > 1;
            return new ParsedConfig(root, newer, null, null);
        }
    }

    [Fact]
    public void ThreeQuickEdits_MakeOneWrite_HalfASecondAfterTheLast()
    {
        var h = new Harness("""{ "version": 1, "n": 0 }""");

        for (int i = 1; i <= 3; i++)
        {
            int n = i;
            h.Document.Edit("n", e => e.Set("/n", n));
            h.Time.Advance(TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(0, h.Files.Writes);
        h.Time.Advance(Delay - TimeSpan.FromMilliseconds(101));
        Assert.Equal(0, h.Files.Writes);
        h.Time.Advance(TimeSpan.FromMilliseconds(2));

        Assert.Equal(1, h.Files.Writes);
        Assert.Contains("\"n\": 3", h.Files.Text);
        Assert.EndsWith("\n", h.Files.Text);
    }

    [Fact]
    public void NoEdit_NoWrite()
    {
        var h = new Harness("{ \"version\": 1, // kept\n \"n\": 0 }");

        h.Time.Advance(TimeSpan.FromSeconds(5));
        h.Document.Flush();

        Assert.Equal(0, h.Files.Writes);
        Assert.Contains("// kept", h.Files.Text);
    }

    [Fact]
    public void Flush_WritesAtOnce_AndLeavesNothingPending()
    {
        var h = new Harness("""{ "version": 1, "n": 0 }""");
        h.Document.Edit("n", e => e.Set("/n", 1));

        h.Document.Flush();
        h.Time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(1, h.Files.Writes);
    }

    [Fact]
    public void ABytesEqualWrite_IsSkipped()
    {
        var h = new Harness("""{ "version": 1, "n": 0 }""");
        h.Document.Edit("n", e => e.Set("/n", 1));
        h.Document.Flush();

        h.Document.Edit("back", e => e.Set("/n", 2));
        h.Document.Edit("again", e => e.Set("/n", 1)); // the file already holds exactly this
        h.Document.Flush();

        Assert.Equal(1, h.Files.Writes);
    }

    [Fact]
    public void AFileWithoutAVersion_GetsOneAsTheFirstKey_BeforeTheFirstWrite()
    {
        var h = new Harness("""{ "theme": "x" }""");

        h.Document.Edit("theme", e => e.Set("/theme", "y"));
        h.Document.Flush();

        Assert.StartsWith("{\n  \"version\": 1,", h.Files.Text.Replace("\r\n", "\n"));
        h.Document.History.Undo();
        Assert.Equal("x", (string)h.Document.Root["theme"]!);
        Assert.Equal(1, (int)h.Document.Root["version"]!);
    }

    [Fact]
    public void AFailedWrite_IsRetried_ThenReportedOnce_AndTriedAgainWithTheNextChange()
    {
        var h = new Harness("""{ "version": 1, "n": 0 }""");
        h.Files.FailWrites = true;
        h.Document.Edit("n", e => e.Set("/n", 1));

        h.Time.Advance(Delay);
        h.Time.Advance(TimeSpan.FromSeconds(1));

        Assert.Single(h.Problems);
        Assert.StartsWith("Could not save", h.Problems[0].Message);
        Assert.Equal(0, h.Files.Writes);

        h.Files.FailWrites = false;
        h.Document.Edit("n", e => e.Set("/n", 2));
        h.Time.Advance(Delay);

        Assert.Equal(1, h.Files.Writes);
        Assert.Contains("\"n\": 2", h.Files.Text);
        Assert.Single(h.Problems);
    }

    [Fact]
    public void AMigrationBackup_IsWrittenBeforeTheFirstWrite_Once()
    {
        var h = new Harness("""{ "version": 1, "n": 0 }""");
        byte[] original = Encoding.UTF8.GetBytes("old");
        h.Document.NoteMigration(1, original);

        h.Document.Edit("n", e => e.Set("/n", 1));
        h.Document.Flush();
        h.Document.Edit("n", e => e.Set("/n", 2));
        h.Document.Flush();

        var backup = Assert.Single(h.Files.Backups);
        Assert.Equal("urdeck-config.v1.json", backup.Name);
        Assert.Equal(original, backup.Bytes);
    }

    // ---- the file changes outside

    [Fact]
    public void OwnWrite_IsNotAnOutsideChange()
    {
        var h = new Harness("""{ "version": 1, "n": 0 }""");
        h.Document.Edit("n", e => e.Set("/n", 1));
        h.Document.Flush();
        h.Changes.Clear();

        h.Files.SaveOutside(h.Files.Text); // the watcher reports what the document wrote

        Assert.Empty(h.Changes);
        Assert.Empty(h.Problems);
        Assert.Equal(1, h.Document.Revision);
    }

    [Fact]
    public void AValidOutsideChange_IsTaken_Announced_AndUndoable()
    {
        var h = new Harness("""{ "version": 1, "theme": "a" }""");

        h.Files.SaveOutside("""{ "version": 1, "theme": "b" }""");

        Assert.Equal("b", h.Document.Typed.Theme);
        var change = Assert.Single(h.Changes);
        Assert.Equal((EditOrigin.Outside, "Changed outside the editor"), (change.Origin, change.Label));
        Assert.True(h.Document.History.CanUndo);

        h.Document.History.Undo();
        Assert.Equal("a", h.Document.Typed.Theme);
    }

    [Fact]
    public void AnOutsideChange_CancelsAPendingWrite()
    {
        var h = new Harness("""{ "version": 1, "theme": "a" }""");
        h.Document.Edit("theme", e => e.Set("/theme", "mine"));

        h.Files.SaveOutside("""{ "version": 1, "theme": "theirs" }""");
        h.Time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(0, h.Files.Writes);
        Assert.Contains("theirs", h.Files.Text);
        Assert.Equal("theirs", h.Document.Typed.Theme);
    }

    [Fact]
    public void AnOutsideChange_BackToWhatTheDocumentHolds_IsNotAChange()
    {
        var h = new Harness("""{ "version": 1, "theme": "a" }""");

        h.Files.SaveOutside("{ \"version\": 1, // a comment\n \"theme\": \"a\" }");

        Assert.Empty(h.Changes);
        Assert.False(h.Document.History.CanUndo);
    }

    [Fact]
    public void InvalidJson_KeepsTheContent_AndReportsLineAndColumn()
    {
        var h = new Harness("""{ "version": 1, "theme": "a" }""");

        h.Files.SaveOutside("{\n  \"version\": 1,\n  \"theme\": \"b\"\n");

        Assert.Equal("a", h.Document.Typed.Theme);
        Assert.Empty(h.Changes);
        var problem = Assert.Single(h.Problems);
        Assert.NotNull(problem.Line);
        Assert.True(problem.Line >= 3);
        Assert.NotNull(problem.Column);
    }

    [Fact]
    public void AValueOfTheWrongType_IsAProblem_NotAChange()
    {
        var h = new Harness("""{ "version": 1, "theme": "a" }""");

        h.Files.SaveOutside("""{ "version": 1, "monitor": "two" }""");

        Assert.Equal("a", h.Document.Typed.Theme);
        Assert.Single(h.Problems);
    }

    [Fact]
    public void AfterAProblem_AFixedFileIsTaken()
    {
        var h = new Harness("""{ "version": 1, "theme": "a" }""");
        h.Files.SaveOutside("{ \"theme\": ");

        h.Files.SaveOutside("""{ "version": 1, "theme": "c" }""");

        Assert.Equal("c", h.Document.Typed.Theme);
    }

    [Fact]
    public void ANewerFile_BecomesReadOnly_IsTaken_ClearsTheHistory_AndIsNeverWritten()
    {
        var h = new Harness("""{ "version": 1, "theme": "a" }""");
        h.Document.Edit("theme", e => e.Set("/theme", "b"));
        h.Document.Flush();
        int writes = h.Files.Writes;

        h.Files.SaveOutside("""{ "version": 2, "theme": "future", "somethingNew": { "a": 1 } }""");

        Assert.True(h.Document.IsReadOnly);
        Assert.True(h.Document.IsNewerThanHost);
        Assert.Equal("future", h.Document.Typed.Theme);
        Assert.False(h.Document.History.CanUndo);
        Assert.False(h.Document.Edit("theme", e => e.Set("/theme", "z"), EditOrigin.Host));
        Assert.False(h.Document.Edit("theme", e => e.Set("/theme", "z")));
        h.Document.Flush();
        h.Time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(writes, h.Files.Writes);
        Assert.Contains("somethingNew", h.Files.Text);
    }

    // ---- the real thing, end to end

    [Fact]
    public async Task TheRealWatcher_ReportsAnEditInATextEditor_ButNotTheDocumentsOwnWrite()
    {
        string dir = Path.Combine(Path.GetTempPath(), "urdeck-doc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "urdeck-config.json");
        try
        {
            File.WriteAllText(path, """{ "version": 1, "theme": "a" }""");
            var disk = new DiskConfigFileSystem(path);
            var root = (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;
            using var document = new ConfigDocument(root, false, disk, (text, _) => new ParsedConfig((JsonObject)JsonNode.Parse(text)!, false, null, null));
            document.NoteFileContent(File.ReadAllBytes(path));
            var outside = new TaskCompletionSource<ConfigChange>(TaskCreationOptions.RunContinuationsAsynchronously);
            document.Changed += c =>
            {
                if (c.Origin == EditOrigin.Outside)
                    outside.TrySetResult(c);
            };

            document.Edit("theme", e => e.Set("/theme", "mine"));
            document.Flush();
            Assert.False(Directory.EnumerateFiles(dir, "*.tmp").Any());
            Assert.False(await Task.WhenAny(outside.Task, Task.Delay(2500, TestContext.Current.CancellationToken)) == outside.Task, "own write reported as an outside change");

            File.WriteAllText(path, """{ "version": 1, "theme": "theirs" }""");

            Assert.True(await Task.WhenAny(outside.Task, Task.Delay(10000, TestContext.Current.CancellationToken)) == outside.Task, "the outside change was not reported");
            Assert.Equal("theirs", document.Typed.Theme);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
