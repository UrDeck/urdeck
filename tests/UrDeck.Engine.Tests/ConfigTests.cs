// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
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
    public void Motion_IsAbsentByDefault()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        using var store = new ConfigStore(path);

        Assert.Null(store.Config.Motion);
        Assert.DoesNotContain("motion", File.ReadAllText(path));
    }

    [Fact]
    public void Motion_RoundTrips()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        File.WriteAllText(path, "{\"motion\":\"reduced\"}");
        using var store = new ConfigStore(path);

        Assert.Equal("reduced", store.Config.Motion);
        store.Save();

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("reduced", doc.RootElement.GetProperty("motion").GetString());
    }

    [Fact]
    public void Motion_UnknownValueLoadsAndIsTreatedAsAbsent()
    {
        string path = Path.Combine(_dir, "urdeck-config.json");
        File.WriteAllText(path, "{\"motion\":\"sideways\"}");
        using var store = new ConfigStore(path);

        Assert.Equal("sideways", store.Config.Motion);
        Assert.Null(Motion.MotionPolicy.Parse(store.Config.Motion));
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

public class ConfigVersionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-version-tests-" + Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(_dir, "urdeck-config.json");
    private string SamplePath => Path.Combine(_dir, "app", "urdeck-config.default.json");

    public ConfigVersionTests() => Directory.CreateDirectory(Path.Combine(_dir, "app"));

    public void Dispose()
    {
        try
        { Directory.Delete(_dir, true); }
        catch { }
    }

    private static string CaptureLog(string logPath, Action action)
    {
        string original = UrDeckLog.LogPath;
        UrDeckLog.LogPath = logPath;
        try
        {
            action();
        }
        finally
        {
            UrDeckLog.LogPath = original;
        }

        return File.Exists(logPath) ? File.ReadAllText(logPath) : "";
    }

    private string Log(Action action) => CaptureLog(Path.Combine(_dir, "test.log"), action);

    private static int VersionOf(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("version").GetInt32();
    }

    [Fact]
    public void NoVersion_Loads_AndIsSavedAsVersion1()
    {
        File.WriteAllText(ConfigPath, "{\"theme\":\"x\"}");
        using var store = new ConfigStore(ConfigPath);
        Assert.Equal(1, store.Config.Version);

        store.Save();

        Assert.Equal(1, VersionOf(ConfigPath));
        Assert.StartsWith("{\n  \"version\"", File.ReadAllText(ConfigPath).Replace("\r\n", "\n"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"x\"")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("1.5")]
    public void ABadVersion_WarnsAndIsReadAsVersion1(string value)
    {
        File.WriteAllText(ConfigPath, "{\"version\":" + value + ",\"theme\":\"x\"}");
        ConfigStore? store = null;

        string log = Log(() => store = new ConfigStore(ConfigPath));
        using (store)
        {
            Assert.Contains("version must be a positive whole number", log);
            Assert.Equal("x", store!.Config.Theme);
            store.Save();
            Assert.Equal(1, VersionOf(ConfigPath));
        }
    }

    [Fact]
    public void AMigration_RunsInOrder_KeepsTheOriginalAsABackup_AndNamesItInTheLog()
    {
        string original = "{\"version\":1,\"theme\":\"old\"}";
        File.WriteAllText(ConfigPath, original);
        var migrations = new List<(int From, Action<System.Text.Json.Nodes.JsonObject> Apply)>
        {
            (1, root => root["theme"] = "migrated"),
        };
        ConfigStore? store = null;

        string log = Log(() => store = new ConfigStore(ConfigPath, null, true, 2, migrations));
        using (store)
        {
            Assert.Equal("migrated", store!.Config.Theme);
            Assert.Contains("migrated from version 1 to 2", log);
            Assert.Equal(original, File.ReadAllText(ConfigPath)); // nothing written until the first save

            store.Save();
        }

        Assert.Equal(2, VersionOf(ConfigPath));
        Assert.Equal(original, File.ReadAllText(Path.Combine(_dir, "urdeck-config.v1.json")));
    }

    [Fact]
    public void ANewerFile_WarnsAndIsNeverWritten()
    {
        string original = "{\"version\":2,\"theme\":\"future\",\"somethingNew\":{\"a\":1}}";
        File.WriteAllText(ConfigPath, original);
        byte[] before = File.ReadAllBytes(ConfigPath);
        ConfigStore? store = null;

        string log = Log(() => store = new ConfigStore(ConfigPath));
        using (store)
        {
            Assert.Contains("version 2, newer than this build's 1", log);
            Assert.Equal("future", store!.Config.Theme);

            store.Config.Display = new UrDeck.Engine.Display.RememberedDisplay();
            store.Save();
            store.Save();
        }

        Assert.Equal(before, File.ReadAllBytes(ConfigPath));
        Assert.False(File.Exists(Path.Combine(_dir, "urdeck-config.v2.json")));
    }

    [Fact]
    public void FirstRun_CopiesTheSampleAndLogsIt()
    {
        File.WriteAllText(SamplePath, "{\"theme\":\"sample-theme\"}");
        ConfigStore? store = null;

        string log = Log(() => store = new ConfigStore(ConfigPath, SamplePath, createIfMissing: true));
        using (store)
        {
            Assert.Equal("sample-theme", store!.Config.Theme);
            Assert.Equal(File.ReadAllBytes(SamplePath), File.ReadAllBytes(ConfigPath));
            Assert.Contains($"Configuration created at {ConfigPath} from the sample page", log);
        }
    }

    [Fact]
    public void FirstRun_WithoutASample_WritesAnEmptyPage()
    {
        ConfigStore? store = null;

        string log = Log(() => store = new ConfigStore(ConfigPath, SamplePath, createIfMissing: true));
        using (store)
        {
            Assert.Single(store!.Config.Pages);
            Assert.True(File.Exists(ConfigPath));
            Assert.Contains($"Configuration created at {ConfigPath} with an empty page", log);
        }
    }

    [Fact]
    public void FirstRun_CreatesTheDataFolder()
    {
        string path = Path.Combine(_dir, "not", "yet", "urdeck-config.json");
        using var store = new ConfigStore(path, SamplePath);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void AnExistingFile_IsNeverReplacedBySample()
    {
        File.WriteAllText(SamplePath, "{\"theme\":\"sample-theme\",\"pages\":[{},{},{}]}");
        File.WriteAllText(ConfigPath, "{\"theme\":\"mine\",\"pages\":[{\"name\":\"One\"}]}");
        byte[] before = File.ReadAllBytes(ConfigPath);

        using var store = new ConfigStore(ConfigPath, SamplePath);

        Assert.Equal("mine", store.Config.Theme);
        Assert.Single(store.Config.Pages);
        Assert.Equal(before, File.ReadAllBytes(ConfigPath));
    }

    [Fact]
    public void DoNotCreate_ReadsTheSampleInMemory_AndLeavesNoFile()
    {
        File.WriteAllText(SamplePath, "{\"theme\":\"sample-theme\"}");

        using var store = new ConfigStore(ConfigPath, SamplePath, createIfMissing: false);

        Assert.Equal("sample-theme", store.Config.Theme);
        Assert.False(File.Exists(ConfigPath));
    }

    [Fact]
    public void DoNotCreate_WithoutASample_IsAnEmptyPage_AndLeavesNoFile()
    {
        using var store = new ConfigStore(ConfigPath, SamplePath, createIfMissing: false);

        Assert.Single(store.Config.Pages);
        Assert.False(File.Exists(ConfigPath));
    }
}
