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
