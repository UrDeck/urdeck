// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Settings;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Widgets.Stats;
using Xunit;

namespace UrDeck.Engine.Tests.Editor;

public sealed class ReadingDescriptionCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"urdeck-readings-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private string CachePath => Path.Combine(_folder, "cache", "readings.json");

    private static ReadingDescriptor Light(string id, string label) =>
        new(id, ReadingKind.OnOff, label, id) { Action = id + "/toggle", Area = "Office", Category = "Lights", Icon = "home/light" };

    [Fact]
    public void OnlyTheReadingsInUseAreKept_AndReadBackWithEverythingTheyDescribe()
    {
        var cache = new ReadingDescriptionCache(CachePath);

        cache.Store("homeassistant", [Light("light.desk", "Desk lamp"), Light("light.hall", "Hallway")], ["homeassistant:light.desk", "system:cpu/load"]);

        var read = new ReadingDescriptionCache(CachePath).Read("homeassistant");
        Assert.Equal([Light("light.desk", "Desk lamp")], read);
    }

    [Fact]
    public void AProvidersEntriesAreReplaced_AndAnotherProvidersAreKept()
    {
        var cache = new ReadingDescriptionCache(CachePath);
        cache.Store("homeassistant", [Light("light.desk", "Desk lamp")], ["homeassistant:light.desk"]);
        cache.Store("system", [new ReadingDescriptor("cpu/load", ReadingKind.Percent, "CPU", "CPU load")], ["system:cpu/load"]);

        cache.Store("homeassistant", [Light("light.hall", "Hallway")], ["homeassistant:light.hall"]);

        Assert.Equal(["light.hall"], cache.Read("homeassistant").Select(d => d.Path));
        Assert.Equal(["cpu/load"], cache.Read("system").Select(d => d.Path));
    }

    [Fact]
    public void ACacheThatIsMissingOrBroken_IsEmpty()
    {
        Assert.Empty(new ReadingDescriptionCache(CachePath).Read("homeassistant"));

        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
        File.WriteAllText(CachePath, "{ not json");

        Assert.Empty(new ReadingDescriptionCache(CachePath).Read("homeassistant"));
    }

    [Fact]
    public void TheFileHoldsDescriptionsOnly()
    {
        var cache = new ReadingDescriptionCache(CachePath);
        cache.Store("homeassistant", [Light("light.desk", "Desk lamp")], ["homeassistant:light.desk"]);

        using var document = JsonDocument.Parse(File.ReadAllText(CachePath));
        var entry = document.RootElement.GetProperty("homeassistant:light.desk");
        Assert.Equal("onOff", entry.GetProperty("kind").GetString(), ignoreCase: true);
        Assert.False(entry.TryGetProperty("value", out _));
    }

    [Fact]
    public void TheUsedReadings_AreThoseOfTheWidgetsReadingSettings_ListItemsIncluded()
    {
        var schema = SettingSchema.Read(typeof(StatsConfig), typeof(WidgetConfig));
        var config = ConfigStore.Parse("""
            {
              "pages": [
                { "name": "One", "widgets": [
                  { "typeId": "urdeck.widgets.stats", "col": 0, "row": 0, "width": 4, "height": 2,
                    "slots": [ { "reading": "system:cpu/load" }, { "reading": "homeassistant:sensor.office_temperature" }, { "label": "none" } ] },
                  { "typeId": "urdeck.widgets.unknown", "col": 4, "row": 0, "slots": [ { "reading": "x:y" } ] }
                ] }
              ]
            }
            """);

        var used = UsedReadings.Of(config, type => type == "urdeck.widgets.stats" ? schema : null);

        Assert.Equal(["homeassistant:sensor.office_temperature", "system:cpu/load"], used.Order(StringComparer.Ordinal));
    }
}
