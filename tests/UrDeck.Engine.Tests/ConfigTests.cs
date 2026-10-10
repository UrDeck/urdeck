// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Sdk;
using Xunit;

namespace UrDeck.Engine.Tests;

public class WidgetConfigTests
{
    [Fact]
    public void Serialize_IncludesAllPublicProperties()
    {
        string json = JsonSerializer.Serialize(new WidgetConfig { WidgetTypeId = "clock" });
        using var doc = JsonDocument.Parse(json);
        foreach (string? key in new[] { "typeId", "col", "row", "width", "height", "parameters", "isVisible" })
            Assert.True(doc.RootElement.TryGetProperty(key, out _), key);
    }

    [Fact]
    public void Deserialize_MissingProperties_KeepDefaults()
    {
        var c = JsonSerializer.Deserialize<WidgetConfig>("{}")!;
        Assert.Equal(1, c.Width);
        Assert.Equal(1, c.Height);
        Assert.True(c.IsVisible);
        Assert.NotNull(c.Parameters);
    }
}

public class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-tests-" + Guid.NewGuid().ToString("N"));

    public ConfigStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        { Directory.Delete(_dir, true); }
        catch { }
    }

    [Fact]
    public void MissingFile_CreatesFileWithOneDefaultPage()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        var store = new ConfigStore(path);
        Assert.True(File.Exists(path));
        Assert.Single(store.Config.Pages);
        Assert.Equal("Default", store.Config.Pages[0].Name);
    }

    [Fact]
    public void ProvidersSection_IsAbsentByDefault()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        using var store = new ConfigStore(path);

        Assert.Null(store.Config.Providers);
        Assert.DoesNotContain("providers", File.ReadAllText(path));
    }

    [Fact]
    public void ProvidersSection_RoundTripsWithUnknownProperties()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        File.WriteAllText(path, "{\"providers\":{\"system\":{\"intervalMs\":500,\"future\":\"x\"},\"weather\":{\"token\":\"t\"}}}");
        using var store = new ConfigStore(path);

        Assert.Equal(500, store.Config.Providers!["system"].IntervalMs);
        Assert.Null(store.Config.Providers["weather"].IntervalMs);

        store.Save();

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var providers = doc.RootElement.GetProperty("providers");
        Assert.Equal(500, providers.GetProperty("system").GetProperty("intervalMs").GetInt32());
        Assert.Equal("x", providers.GetProperty("system").GetProperty("future").GetString());
        Assert.Equal("t", providers.GetProperty("weather").GetProperty("token").GetString());
        Assert.False(providers.GetProperty("weather").TryGetProperty("intervalMs", out _));
    }

    [Fact]
    public void Dock_IsAnEmptyList_WhenAbsentOrNull()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        File.WriteAllText(path, "{\"pages\":[{\"name\":\"Main\",\"widgets\":[]}]}");
        using (var absent = new ConfigStore(path))
            Assert.Empty(absent.Config.Dock);

        File.WriteAllText(path, "{\"dock\":null}");
        using var empty = new ConfigStore(path);
        Assert.Empty(empty.Config.Dock);
    }

    [Fact]
    public void Dock_TwoShortcuts_RoundTripWithTheirSettings_AndAnUnknownProperty()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        File.WriteAllText(path, """
            { "dock": [
                { "typeId": "urdeck.widgets.shortcut", "target": "notepad.exe", "label": "Notes", "future": { "x": 1 } },
                { "typeId": "urdeck.widgets.shortcut", "target": "https://example.org" }
            ] }
            """);
        using (var store = new ConfigStore(path))
        {
            Assert.Equal(2, store.Config.Dock.Count);
            Assert.All(store.Config.Dock, e => Assert.Equal("urdeck.widgets.shortcut", e.WidgetTypeId));
            Assert.Equal("notepad.exe", store.Config.Dock[0].ExtensionData!["target"].GetString());
            store.Save();
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var dock = doc.RootElement.GetProperty("dock");
        Assert.Equal(2, dock.GetArrayLength());
        Assert.Equal("urdeck.widgets.shortcut", dock[0].GetProperty("typeId").GetString());
        Assert.Equal("notepad.exe", dock[0].GetProperty("target").GetString());
        Assert.Equal("Notes", dock[0].GetProperty("label").GetString());
        Assert.Equal(1, dock[0].GetProperty("future").GetProperty("x").GetInt32());
        Assert.Equal("https://example.org", dock[1].GetProperty("target").GetString());
    }

    [Fact]
    public void Dock_AnEntryInTheOlderShape_Loads_AndKeepsItsPropertiesThroughASave()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        File.WriteAllText(path, "{\"dock\":[{\"type\":\"app\",\"command\":\"notepad.exe\",\"url\":\"https://example.org\"}]}");
        using (var store = new ConfigStore(path))
        {
            var entry = store.Config.Dock.Single();
            Assert.Equal("", entry.WidgetTypeId);
            store.Save();
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var saved = doc.RootElement.GetProperty("dock")[0];
        Assert.Equal("app", saved.GetProperty("type").GetString());
        Assert.Equal("notepad.exe", saved.GetProperty("command").GetString());
        Assert.Equal("https://example.org", saved.GetProperty("url").GetString());
    }

    [Fact]
    public void Dock_AFifthEntry_StaysInTheFileThroughASave()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        string entries = string.Join(",", Enumerable.Range(1, 5).Select(i => $"{{\"typeId\":\"urdeck.widgets.shortcut\",\"target\":\"app{i}.exe\"}}"));
        File.WriteAllText(path, "{\"dock\":[" + entries + "]}");
        using (var store = new ConfigStore(path))
        {
            UrDeck.Engine.Layout.DockLayout.Select(store.Config.Dock, _ => null);
            store.Save();
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var dock = doc.RootElement.GetProperty("dock");
        Assert.Equal(5, dock.GetArrayLength());
        Assert.Equal("app5.exe", dock[4].GetProperty("target").GetString());
    }

    [Fact]
    public void LoadedWidget_RoundTripsGridFields()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        File.WriteAllText(path, "{\"pages\":[{\"name\":\"Main\",\"widgets\":[{\"typeId\":\"clock\",\"col\":1,\"row\":2,\"width\":3,\"height\":4}]}]}");
        var store = new ConfigStore(path);
        var w = store.Config.Pages[0].Widgets.Single();
        Assert.Equal((1, 2, 3, 4), (w.Col, w.Row, w.Width, w.Height));

        store.Save();
        var store2 = new ConfigStore(path);
        var w2 = store2.Config.Pages[0].Widgets.Single();
        Assert.Equal(("clock", 1, 2, 3, 4), (w2.WidgetTypeId, w2.Col, w2.Row, w2.Width, w2.Height));
    }
}
