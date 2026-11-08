// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using UrDeck.Engine.Editor;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;
using Xunit;

namespace UrDeck.Engine.Tests.Editor;

/// <summary>reading-picker spec, "Search And Filters": the entities and areas are made up.</summary>
public sealed class ReadingIndexTests
{
    private static ReadingDescriptor Light(string id, string label, string? area, string? category = "Lights") =>
        new(id, ReadingKind.OnOff, label, id) { Action = id + "/toggle", Area = area, Category = category };

    private static ReadingIndex Home() => new(
        ReadingIndex.EntriesOf("homeassistant",
        [
            Light("light.desk", "Desk lamp", "Office"),
            Light("light.desk_strip", "Desk strip", "Kitchen"),
            Light("light.hall", "Hallway", null),
            new("sensor.office_temperature", ReadingKind.Temperature, "Office temperature", "sensor.office_temperature") { Area = "Office", Category = "Sensors" },
            new("sensor.fridge", ReadingKind.Percent, "Café fridge", "sensor.fridge") { Area = "Kitchen", Category = "Sensors" },
            new("sensor.attic_desk_fan", ReadingKind.Number, "Attic fan", "sensor.attic_desk_fan") { Category = "Sensors" },
        ])
        .Concat(ReadingIndex.EntriesOf("system",
        [
            new("cpu/load", ReadingKind.Percent, "CPU", "CPU load") { Category = "CPU", Device = "Test CPU 9" },
            new("cpu/core/{n}/load", ReadingKind.Percent, "Core {n}", "CPU core {n} load") { Category = "CPU", Device = "Test CPU 9" },
            new("memory/load", ReadingKind.Percent, "Memory", "Memory load") { Category = "Memory" },
        ])));

    [Fact]
    public void EveryWordMustMatch_InTheLabelNameIdAreaOrCategory()
    {
        var result = Home().Search(new ReadingQuery("office lights"));

        Assert.Equal(["homeassistant:light.desk"], result.Hits.Select(h => h.Entry.Id));
        Assert.Equal(9, result.Total);
    }

    [Fact]
    public void Matching_IgnoresCaseAndAccents()
    {
        Assert.Equal(["homeassistant:sensor.fridge"], Home().Search(new ReadingQuery("CAFE")).Hits.Select(h => h.Entry.Id));
        Assert.Equal(["homeassistant:sensor.fridge"], Home().Search(new ReadingQuery("café")).Hits.Select(h => h.Entry.Id));
    }

    [Fact]
    public void ALabelBeginningRanksFirst_ThenLabelWords_ThenTheId_ThenTheRest()
    {
        var hits = Home().Search(new ReadingQuery("desk")).Hits;

        // "Desk lamp" and "Desk strip" start with it; "attic_desk_fan" only has it inside its id.
        Assert.Equal(["homeassistant:light.desk", "homeassistant:light.desk_strip", "homeassistant:sensor.attic_desk_fan"], hits.Select(h => h.Entry.Id));
    }

    [Fact]
    public void TheMatchedTextIsReportedForTheLabelAndTheId()
    {
        var hit = Home().Search(new ReadingQuery("desk")).Hits[0];

        Assert.Equal([new MatchRange(0, 4)], hit.LabelMatches);
        Assert.Equal([new MatchRange("homeassistant:light.".Length, 4)], hit.IdMatches);
    }

    [Fact]
    public void OverlappingSpansAreMerged()
    {
        var hit = Home().Search(new ReadingQuery("des desk")).Hits[0];

        Assert.Equal([new MatchRange(0, 4)], hit.LabelMatches);
    }

    [Fact]
    public void TheFilters_NarrowByProviderAreaAndCategory()
    {
        var index = Home();

        Assert.Equal(3, index.Search(new ReadingQuery(Provider: "system")).Hits.Count);
        Assert.Equal(["homeassistant:light.desk", "homeassistant:sensor.office_temperature"], index.Search(new ReadingQuery(Area: "Office")).Hits.Select(h => h.Entry.Id).Order());
        Assert.Equal(3, index.Search(new ReadingQuery(Provider: "homeassistant", Category: "Lights")).Hits.Count);
        Assert.Equal(["Kitchen", "Office"], index.Areas());
        Assert.Equal(["Lights", "Sensors"], index.Categories("homeassistant"));
        Assert.Equal(["homeassistant", "system"], index.Providers());
    }

    [Fact]
    public void WhatTheSettingAccepts_RestrictsTheListAndTheTotal()
    {
        var index = Home();

        var acting = index.Search(new ReadingQuery(Restrict: ReadingFilter.HasAction));
        Assert.Equal(3, acting.Total);
        Assert.All(acting.Hits, h => Assert.True(h.Entry.HasAction));

        var ranged = index.Search(new ReadingQuery(Restrict: ReadingFilter.HasRange));
        Assert.Contains(ranged.Hits, h => h.Entry.Id == "system:cpu/load");
        Assert.DoesNotContain(ranged.Hits, h => h.Entry.Id == "homeassistant:light.desk");
    }

    [Fact]
    public void APatternIsATemplate()
    {
        var index = Home();

        Assert.True(index.Find("SYSTEM:cpu/core/{n}/load")!.IsTemplate);
        Assert.False(index.Find("system:cpu/load")!.IsTemplate);
    }

    [Fact]
    public void Groups_AreCategoryThenArea_ThenDevice_ThenOther_WithOtherLast()
    {
        var groups = ReadingIndex.Group(Home().Search(new ReadingQuery()).Hits);

        Assert.Equal(["CPU", "Lights", "Lights · Kitchen", "Lights · Office", "Memory", "Sensors", "Sensors · Kitchen", "Sensors · Office"], groups.Select(g => g.Header));

        var bare = new ReadingIndex(ReadingIndex.EntriesOf("p",
        [
            new("a", ReadingKind.Number, "A", "A") { Device = "Gadget" },
            new("b", ReadingKind.Number, "B", "B"),
        ]));
        Assert.Equal(["Gadget", "Other"], ReadingIndex.Group(bare.Search(new ReadingQuery()).Hits).Select(g => g.Header));
    }

    [Fact]
    public void FifteenHundredEntries_AreSearchedInUnderAHundredMilliseconds()
    {
        string[] rooms = ["Office", "Kitchen", "Bedroom", "Garage", "Attic", "Hall"];
        string[] domains = ["light", "switch", "sensor", "climate", "cover"];
        var descriptors = Enumerable.Range(0, 1500).Select(i =>
            new ReadingDescriptor($"{domains[i % 5]}.item_{i}", ReadingKind.Number, $"{rooms[i % 6]} item {i}", $"{domains[i % 5]}.item_{i}")
            {
                Area = rooms[i % 6],
                Category = domains[i % 5],
            });
        var index = new ReadingIndex(ReadingIndex.EntriesOf("homeassistant", descriptors));
        index.Search(new ReadingQuery("warm up"));

        var watch = Stopwatch.StartNew();
        var result = index.Search(new ReadingQuery("office item 7"));
        var groups = ReadingIndex.Group(result.Hits);
        watch.Stop();

        Assert.NotEmpty(result.Hits);
        Assert.NotEmpty(groups);
        Assert.Equal(1500, result.Total);
        Assert.True(watch.ElapsedMilliseconds < 100, $"took {watch.ElapsedMilliseconds} ms");
    }
}
