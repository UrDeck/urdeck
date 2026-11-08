// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Nodes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Display;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests.Config;

public sealed class ConfigStoreDocumentTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-store-doc-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTime _time = new();

    public ConfigStoreDocumentTests() => Directory.CreateDirectory(_dir);

    private string ConfigPath => Path.Combine(_dir, "urdeck-config.json");

    public void Dispose()
    {
        try
        { Directory.Delete(_dir, true); }
        catch { }
    }

    private ConfigStore Open(string json)
    {
        File.WriteAllText(ConfigPath, json);
        return new ConfigStore(ConfigPath, null, true, ConfigStore.CurrentVersion, [], _time, action => action());
    }

    [Fact]
    public void AHostThatChangesNothing_LeavesTheFileByteForByte_CommentsIncluded()
    {
        File.WriteAllText(ConfigPath, "{ // my notes\n  \"theme\": \"glass\", }\n");
        byte[] before = File.ReadAllBytes(ConfigPath);

        using (var store = new ConfigStore(ConfigPath))
            store.Document.Flush();

        Assert.Equal(before, File.ReadAllBytes(ConfigPath));
    }

    [Fact]
    public void AChangePendingAtExit_IsWrittenByFlush()
    {
        using (var store = Open("""{ "theme": "glass" }"""))
        {
            store.Document.Edit("Theme", e => e.Set("/theme", "rigline"));
            store.Document.Flush();
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        Assert.Equal("rigline", doc.RootElement.GetProperty("theme").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());
        Assert.False(File.Exists(ConfigPath + ".tmp"));
    }

    [Fact]
    public void TwentyChangesInASecond_AreOneReload_WithTheLastValue()
    {
        using var store = Open("""{ "theme": "a" }""");
        var seen = new List<string>();
        store.ConfigChanged += config => seen.Add(config.Theme);

        for (int i = 1; i <= 20; i++)
        {
            int n = i;
            store.Document.Edit("Theme", e => e.Set("/theme", "t" + n));
            _time.Advance(TimeSpan.FromMilliseconds(40));
        }

        Assert.Empty(seen);
        _time.Advance(ConfigStore.CoalesceDelay);

        Assert.Equal(["t20"], seen);
    }

    [Fact]
    public void ARemembersDisplay_IsAHostEdit_WrittenWithoutAReload()
    {
        using var store = Open("""{ "theme": "a", "futureKey": 1 }""");
        int reloads = 0;
        store.ConfigChanged += _ => reloads++;

        store.Config.Display = new RememberedDisplay { Name = "Y70", Model = "RTD1100", Path = "p", Selector = "tallest" };
        store.CommitDisplay();
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(0, reloads);
        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        var display = doc.RootElement.GetProperty("display");
        Assert.Equal(("Y70", "RTD1100", "p", "tallest"), (display.GetProperty("name").GetString(), display.GetProperty("model").GetString(),
            display.GetProperty("path").GetString(), display.GetProperty("selector").GetString()));
        Assert.Equal(1, doc.RootElement.GetProperty("futureKey").GetInt32());
        Assert.Equal("a", doc.RootElement.GetProperty("theme").GetString());
        Assert.False(store.Document.History.CanUndo);

        store.Config.Display = null;
        store.CommitDisplay();
        store.Document.Flush();
        Assert.False(JsonDocument.Parse(File.ReadAllText(ConfigPath)).RootElement.TryGetProperty("display", out _));
    }

    [Fact]
    public void CreatedOnFirstRun_IsTrueForANewFile_AndFalseForAnExistingOne()
    {
        using (var first = new ConfigStore(ConfigPath, null, true, ConfigStore.CurrentVersion, [], _time, action => action()))
        {
            Assert.True(first.CreatedOnFirstRun);
            Assert.True(File.Exists(ConfigPath));
        }

        using var second = new ConfigStore(ConfigPath, null, true, ConfigStore.CurrentVersion, [], _time, action => action());
        Assert.False(second.CreatedOnFirstRun);
    }

    [Fact]
    public void CreatedOnFirstRun_IsFalseInAReadOnlyDocument()
    {
        using var store = new ConfigStore(ConfigPath, null, createIfMissing: false);
        Assert.False(store.CreatedOnFirstRun);
    }

    [Fact]
    public void ADocumentOpenedWithoutCreating_IsReadOnly_AndWritesNothing()
    {
        using var store = new ConfigStore(ConfigPath, null, createIfMissing: false);

        Assert.True(store.Document.IsReadOnly);
        Assert.False(store.Document.Edit("x", e => e.Set("/theme", "b")));
        store.CommitDisplay();
        store.Document.Flush();
        Assert.False(File.Exists(ConfigPath));
    }

    [Fact]
    public void ANewerFile_IsReadOnly_AndByteForByteUnchanged()
    {
        File.WriteAllText(ConfigPath, """{ "version": 2, "theme": "future", "somethingNew": { "a": 1 } }""");
        byte[] before = File.ReadAllBytes(ConfigPath);

        using (var store = new ConfigStore(ConfigPath))
        {
            Assert.True(store.IsNewerThanHost);
            Assert.False(store.Document.Edit("x", e => e.Set("/theme", "b")));
            store.Config.Display = new RememberedDisplay();
            store.CommitDisplay();
            store.Document.Flush();
        }

        Assert.Equal(before, File.ReadAllBytes(ConfigPath));
    }

    [Fact]
    public void TheMovedSecret_IsRemovedFromTheFile_AsAHostEdit_AndOtherKeysStay()
    {
        var backend = new InMemorySecretBackend();
        var secrets = new SecretStore(backend, _ => null);
        var registry = new ProviderRegistry();
        registry.TryRegister(new TypedFakeProvider().Descriptor(), out _);
        using var store = Open("""{"providers":{"typed":{"url":"https://ha.home","token":"abc","future":1}}}""");

        bool changed = SecretStores.MoveFromConfigAndSave(store, registry, secrets);
        store.Document.Flush();

        Assert.True(changed);
        Assert.Equal("abc", backend.Read("typed", "token")!.Value);
        string text = File.ReadAllText(ConfigPath);
        Assert.DoesNotContain("abc", text);
        using var doc = JsonDocument.Parse(text);
        var typed = doc.RootElement.GetProperty("providers").GetProperty("typed");
        Assert.Equal("https://ha.home", typed.GetProperty("url").GetString());
        Assert.Equal(1, typed.GetProperty("future").GetInt32());
        Assert.False(store.Document.History.CanUndo);
        Assert.Null(((JsonObject)store.Document.Root["providers"]!["typed"]!)["token"]);
    }
}
