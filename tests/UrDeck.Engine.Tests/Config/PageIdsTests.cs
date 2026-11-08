// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests.Config;

public sealed partial class PageIdsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-page-ids-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTime _time = new();
    private readonly string _logBefore = UrDeckLog.LogPath;

    public PageIdsTests()
    {
        Directory.CreateDirectory(_dir);
        UrDeckLog.LogPath = Path.Combine(_dir, "test.log");
    }

    private string ConfigPath => Path.Combine(_dir, "urdeck-config.json");

    public void Dispose()
    {
        UrDeckLog.LogPath = _logBefore;
        try
        { Directory.Delete(_dir, true); }
        catch { }
    }

    [GeneratedRegex("^p-[0-9a-z]{6}$")]
    private static partial Regex IdFormat();

    private ConfigStore Open(string json)
    {
        File.WriteAllText(ConfigPath, json);
        return new ConfigStore(ConfigPath, null, true, ConfigStore.CurrentVersion, [], _time, action => action());
    }

    private static UrDeckConfig Config(string json) => ConfigStore.Parse(json);

    [Fact]
    public void Resolve_FindsTheStartPageById()
    {
        var config = Config("""{ "pages": [ { "id": "p-main", "name": "Main" }, { "id": "p-home", "name": "Home" } ], "startPage": "p-home" }""");

        Assert.Equal(1, StartPageResolver.Resolve(config));
        Assert.True(StartPageResolver.IsSet(config));
    }

    [Fact]
    public void Resolve_AMissingStartPage_IsTheFirst_AndTheLogSaysSo()
    {
        var config = Config("""{ "pages": [ { "id": "p-main", "name": "Main" }, { "id": "p-home", "name": "Home" } ], "startPage": "p-gone" }""");

        Assert.Equal(0, StartPageResolver.Resolve(config, log: true));
        Assert.False(StartPageResolver.IsSet(config));
        Assert.Contains("Start page 'p-gone' not found; starting on the first page", File.ReadAllText(UrDeckLog.LogPath));
    }

    [Fact]
    public void Resolve_AnAbsentStartPage_IsTheFirst()
    {
        var config = Config("""{ "pages": [ { "id": "p-main", "name": "Main" }, { "id": "p-home", "name": "Home" } ], "activePage": 1 }""");

        Assert.Equal(0, StartPageResolver.Resolve(config));
        Assert.Null(config.StartPage);
    }

    [Fact]
    public void Resolve_ReorderingKeepsTheStartPage()
    {
        var config = Config("""{ "pages": [ { "id": "p-a", "name": "A" }, { "id": "p-b", "name": "B" }, { "id": "p-main", "name": "Main" } ], "startPage": "p-main" }""");

        Assert.Equal(2, StartPageResolver.Resolve(config));
    }

    [Fact]
    public void NewIds_HaveTheFormat_AndAreNotTaken()
    {
        var taken = new HashSet<string>();
        for (int i = 0; i < 200; i++)
        {
            string id = PageIds.New(taken);
            Assert.Matches(IdFormat(), id);
            Assert.True(taken.Add(id));
        }
    }

    [Fact]
    public void Missing_GivesPagesWithoutAnId_AndDuplicates_ANewOne_TheFirstKeepsIts()
    {
        var root = (JsonObject)JsonNode.Parse("""{ "pages": [ { "id": "p-a" }, {}, { "id": "p-a" }, { "id": "" } ] }""")!;

        var missing = PageIds.Missing(root);

        Assert.Equal([1, 2, 3], missing.Select(m => m.Index));
        var all = missing.Select(m => m.Id).Append("p-a").ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void OnLoad_PagesGetIds_WrittenOnce_AndNotAnUndoStep()
    {
        using var store = Open("""{ "pages": [ { "name": "A", "widgets": [] }, { "name": "B", "widgets": [] } ], "futureKey": 3 }""");

        var ids = store.Config.Pages.Select(p => p.Id).ToList();
        Assert.All(ids, id => Assert.Matches(IdFormat(), id!));
        Assert.NotEqual(ids[0], ids[1]);
        Assert.False(store.Document.History.CanUndo);

        _time.Advance(TimeSpan.FromSeconds(1));
        DateTime written = File.GetLastWriteTimeUtc(ConfigPath);
        using (var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath)))
        {
            var pages = doc.RootElement.GetProperty("pages");
            Assert.Equal(ids, pages.EnumerateArray().Select(p => p.GetProperty("id").GetString()).ToList());
            Assert.Equal(3, doc.RootElement.GetProperty("futureKey").GetInt32());
        }

        // Nothing more to write: a second load assigns nothing.
        byte[] after = File.ReadAllBytes(ConfigPath);
        store.Dispose();
        using var again = Open(Encoding.UTF8.GetString(after));
        _time.Advance(TimeSpan.FromSeconds(1));
        again.Document.Flush();
        Assert.Equal(ids, again.Config.Pages.Select(p => p.Id).ToList());
        Assert.Equal(after, File.ReadAllBytes(ConfigPath));
    }

    [Fact]
    public void PagesWithIds_AreNotWritten()
    {
        string json = """{ "version": 1, "pages": [ { "id": "p-main01", "name": "A", "widgets": [] } ] }""";
        File.WriteAllText(ConfigPath, json);
        byte[] before = File.ReadAllBytes(ConfigPath);

        using (var store = new ConfigStore(ConfigPath))
            store.Document.Flush();

        Assert.Equal(before, File.ReadAllBytes(ConfigPath));
    }

    [Fact]
    public void AFileChangedOutside_WithoutIds_GetsThem_InTheNewContent()
    {
        using var store = Open("""{ "pages": [ { "id": "p-a00000", "name": "A", "widgets": [] } ] }""");
        _time.Advance(TimeSpan.FromSeconds(1));

        store.Document.HandleFileContent(Encoding.UTF8.GetBytes("""{ "pages": [ { "id": "p-a00000", "name": "A" }, { "name": "New" } ] }"""));

        var ids = store.Config.Pages.Select(p => p.Id).ToList();
        Assert.Equal("p-a00000", ids[0]);
        Assert.Matches(IdFormat(), ids[1]!);
        Assert.True(store.Document.History.CanUndo);
    }

    [Fact]
    public void AReadOnlyDocument_AssignsNothing()
    {
        File.WriteAllText(ConfigPath, """{ "pages": [ { "name": "A" } ] }""");
        byte[] before = File.ReadAllBytes(ConfigPath);

        using (var store = new ConfigStore(ConfigPath, null, createIfMissing: false))
            Assert.Null(store.Config.Pages[0].Id);

        Assert.Equal(before, File.ReadAllBytes(ConfigPath));
    }
}
