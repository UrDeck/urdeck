// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Data;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The sample file of a data provider (sample-data, "Provider Sample File", "Sample Values Over Time" and the rest).</summary>
public sealed class SampleFileTests
{
    private static readonly DateTime Moment = new(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);

    private static SampleFile Read(string readings)
    {
        var file = SampleFile.Parse($$"""{ "version": 1, "readings": { {{readings}} } }""", out var findings);
        Assert.True(file != null, string.Join("\n", findings));
        Assert.Empty(findings);
        return file!;
    }

    private static IReadOnlyList<string> Findings(string json)
    {
        var file = SampleFile.Parse(json, out var findings);
        Assert.Null(file);
        Assert.NotEmpty(findings);
        return findings;
    }

    private static IReadOnlyList<string> ReadingFindings(string readings) =>
        Findings($$"""{ "version": 1, "readings": { {{readings}} } }""");

    private static ReadingValue At(SampleFile file, string path, double seconds, DateTime? moment = null)
    {
        var reading = file.Match(path, out var bindings);
        Assert.NotNull(reading);
        var value = reading!.ValueAt(seconds, moment ?? Moment, bindings);
        Assert.NotNull(value);
        return value!.Value;
    }

    [Fact]
    public void AReading_IsDescribedAsItsCatalogEntry()
    {
        var file = Read("""
            "gpu/temperature": { "kind": "temperature", "label": "GPU", "name": "GPU temperature", "device": "Sample GPU",
              "min": 0, "max": 100, "warning": 80, "critical": 90, "displayUnit": "celsius", "decimals": 1, "value": 61 },
            "x/rain": { "kind": "depth", "label": "Rain", "name": "Rain", "depthUnit": "centimetre", "unit": "cm",
              "attribution": { "text": "Data by X", "shortText": "X", "url": "https://x.invalid/" }, "value": 2 }
            """);

        Assert.Equal(
            new ReadingDescriptor("gpu/temperature", ReadingKind.Temperature, "GPU", "GPU temperature")
            {
                Device = "Sample GPU",
                Min = 0,
                Max = 100,
                Warning = 80,
                Critical = 90,
                DisplayUnit = DisplayUnit.Celsius,
                Decimals = 1,
            },
            file.Describe()[0]);
        Assert.Equal(
            new ReadingDescriptor("x/rain", ReadingKind.Depth, "Rain", "Rain")
            {
                DepthUnit = DepthUnit.Centimetre,
                Unit = "cm",
                Attribution = new ReadingAttribution("Data by X", "X", "https://x.invalid/"),
            },
            file.Describe()[1]);
    }

    [Fact]
    public void Defaults_ApplyToEveryReadingThatDoesNotSetThem()
    {
        var file = SampleFile.Parse("""
            { "version": 1, "defaults": { "device": "{location}", "attribution": { "text": "Credit" } },
              "readings": {
                "{location}/a": { "kind": "number", "label": "A", "name": "A", "value": 1 },
                "{location}/b": { "kind": "number", "label": "B", "name": "B", "device": "Own", "value": 2 } } }
            """, out var findings);

        Assert.Empty(findings);
        Assert.Equal("{location}", file!.Describe()[0].Device);
        Assert.Equal(new ReadingAttribution("Credit"), file.Describe()[0].Attribution);
        Assert.Equal("Own", file.Describe()[1].Device);
    }

    [Fact]
    public void AFixedValue_ReadsTheSameAtEveryMoment()
    {
        var file = Read(""" "cpu/load": { "kind": "percent", "label": "CPU", "name": "CPU load", "value": 68 } """);

        foreach (double t in new[] { -30.0, 0, 1, 7.5, 3600 })
            Assert.Equal(ReadingValue.FromNumber(68), At(file, "cpu/load", t));
    }

    [Fact]
    public void Keyframes_ChangeANumberInAStraightLine_AndHoldAfterTheLast()
    {
        var file = Read("""
            "cpu/load": { "kind": "percent", "label": "CPU", "name": "CPU load",
              "keys": [ { "at": 0, "value": 41 }, { "at": 2, "value": 68 }, { "at": 4, "value": 55 } ] }
            """);

        Assert.Equal(41, At(file, "cpu/load", 0).Number);
        Assert.Equal(54.5, At(file, "cpu/load", 1).Number);
        Assert.Equal(68, At(file, "cpu/load", 2).Number);
        Assert.Equal(55, At(file, "cpu/load", 4).Number);
        Assert.Equal(55, At(file, "cpu/load", 100).Number);
        Assert.Equal(41, At(file, "cpu/load", -5).Number);
    }

    [Fact]
    public void ARepeatingText_StepsAndStartsAgain()
    {
        var file = Read("""
            "x/condition": { "kind": "text", "label": "Condition", "name": "Condition", "period": 6,
              "keys": [ { "at": 0, "value": "partly-cloudy" }, { "at": 3, "value": "rain" } ] }
            """);

        Assert.Equal("partly-cloudy", At(file, "x/condition", 2.9).Text);
        Assert.Equal("partly-cloudy", At(file, "x/condition", 7).Text);
        Assert.Equal("rain", At(file, "x/condition", 10).Text);
        Assert.Equal("rain", At(file, "x/condition", -1).Text);
    }

    [Fact]
    public void ARepeatingNumber_ContinuesBackwards_AndClosesItsLoop()
    {
        var file = Read("""
            "cpu/load": { "kind": "percent", "label": "CPU", "name": "CPU load", "period": 4,
              "keys": [ { "at": 0, "value": 40 }, { "at": 2, "value": 60 } ] }
            """);

        Assert.Equal(50, At(file, "cpu/load", 1).Number);
        Assert.Equal(50, At(file, "cpu/load", 3).Number);
        Assert.Equal(50, At(file, "cpu/load", -1).Number);
        Assert.Equal(40, At(file, "cpu/load", -4).Number);
        Assert.Equal(At(file, "cpu/load", 1.25), At(file, "cpu/load", 1.25 - 400));
    }

    [Fact]
    public void OnOff_Steps()
    {
        var file = Read("""
            "x/day": { "kind": "onOff", "label": "Day", "name": "Day", "keys": [ { "at": 0, "value": false }, { "at": 1, "value": true } ] }
            """);

        Assert.False(At(file, "x/day", 0.99).IsOn);
        Assert.True(At(file, "x/day", 1).IsOn);
    }

    [Fact]
    public void ATime_IsThatTimeOfDayOnTheDayDrawn()
    {
        var file = Read(""" "x/sunrise": { "kind": "time", "label": "Sunrise", "name": "Sunrise", "value": "07:14" } """);

        var time = At(file, "x/sunrise", 0).Time;

        Assert.Equal(new DateTime(2026, 10, 13, 7, 14, 0), time.DateTime);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 13, 7, 14, 0)), time.Offset);
    }

    [Fact]
    public void ADate_IsCountedInDaysFromTheDayDrawn()
    {
        var file = Read(""" "x/day": { "kind": "date", "label": "Day", "name": "Day", "value": 1 } """);

        Assert.Equal(new DateTime(2026, 10, 14), At(file, "x/day", 0).Time.DateTime);
        Assert.Equal(new DateTime(2027, 1, 1), At(file, "x/day", 0, new DateTime(2026, 12, 31, 23, 0, 0)).Time.DateTime);
    }

    [Fact]
    public void APlaceholderPath_AnswersEveryPlace_AndATextEchoesIt()
    {
        var file = Read("""
            "{location}/current/temperature": { "kind": "temperature", "label": "Now", "name": "Temperature", "value": 18 },
            "{location}/place": { "kind": "text", "label": "Place", "name": "Place", "value": "{location}" }
            """);

        Assert.Equal(18, At(file, "Zurich/current/temperature", 0).Number);
        Assert.Equal(18, At(file, "Portland, OR/current/temperature", 0).Number);
        Assert.Equal("Zurich", At(file, "Zurich/place", 0).Text);
        Assert.Null(file.Match("Zurich/current/humidity", out _));
        Assert.Null(file.Match("place", out _));
    }

    [Fact]
    public void AnExactPath_WinsOverAPlaceholder()
    {
        var file = Read("""
            "{location}/place": { "kind": "text", "label": "Place", "name": "Place", "value": "{location}" },
            "Home/place": { "kind": "text", "label": "Place", "name": "Place", "value": "My home" }
            """);

        Assert.Equal("My home", At(file, "home/place", 0).Text);
        Assert.Equal("Bern", At(file, "Bern/place", 0).Text);
    }

    [Fact]
    public void AnUnavailableReading_HasNoValueAndAReason()
    {
        var file = Read(""" "gpu/fan": { "kind": "percent", "label": "Fan", "name": "GPU fan", "unavailable": "no fan reading" } """);

        var reading = file.Match("gpu/fan", out var bindings);

        Assert.Null(reading!.ValueAt(0, Moment, bindings));
        Assert.Equal("no fan reading", reading.Unavailable);
    }

    [Fact]
    public void NotJson_IsOneFinding() =>
        Assert.Single(Findings("{ \"version\": 1, "));

    [Theory]
    [InlineData("""{ "readings": {} }""", "version: missing")]
    [InlineData("""{ "version": 2, "readings": {} }""", "version: must be 1")]
    [InlineData("""{ "version": 1 }""", "readings: missing")]
    [InlineData("""{ "version": 1, "readings": [] }""", "readings: must be an object")]
    [InlineData("""{ "version": 1, "readings": {}, "extra": 1 }""", "'extra' is not a key of a sample file")]
    [InlineData("""{ "version": 1, "defaults": { "kind": "text" }, "readings": {} }""", "defaults: 'kind' cannot be a default")]
    [InlineData("""[]""", "must be a JSON object")]
    public void TheFile_IsCheckedStrictly(string json, string expected) =>
        Assert.Contains(Findings(json), f => f.Contains(expected, StringComparison.Ordinal));

    [Theory]
    [InlineData(""" "a": { "label": "A", "name": "A", "value": 1 } """, "'a': kind is missing")]
    [InlineData(""" "a": { "kind": "colour", "label": "A", "name": "A", "value": 1 } """, "'a': kind must be one of number, percent")]
    [InlineData(""" "a": { "kind": "number", "name": "A", "value": 1 } """, "'a': label is missing")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "value": 1 } """, "'a': name is missing")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "colour": 1, "value": 1 } """, "'a': 'colour' is not a key of a sample reading")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A" } """, "'a': give exactly one of value, keys and unavailable")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "value": 1, "unavailable": "x" } """, "give exactly one of value")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "value": "high" } """, "'a': value: a number reading's value must be a number")]
    [InlineData(""" "a": { "kind": "text", "label": "A", "name": "A", "value": 3 } """, "a text reading's value must be a text")]
    [InlineData(""" "a": { "kind": "onOff", "label": "A", "name": "A", "value": 1 } """, "must be true or false")]
    [InlineData(""" "a": { "kind": "time", "label": "A", "name": "A", "value": "25:00" } """, "must be a time of day")]
    [InlineData(""" "a": { "kind": "date", "label": "A", "name": "A", "value": 1.5 } """, "a whole number of days")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "value": 1, "period": 4 } """, "'a': period needs keys")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "keys": [] } """, "'a': keys must be a list")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "keys": [ { "at": 1, "value": 1 } ] } """, "'a': keys[0].at: the first key must be at 0")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "keys": [ { "at": 0, "value": 1 }, { "at": 0, "value": 2 } ] } """, "'a': keys[1].at: the keys must be in ascending order")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "keys": [ { "at": 0 } ] } """, "'a': keys[0].value: missing")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "keys": [ { "value": 1 } ] } """, "'a': keys[0].at: missing")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "keys": [ { "at": 0, "value": 1, "ease": 1 } ] } """, "'ease' is not a key of a keyframe")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "period": 2, "keys": [ { "at": 0, "value": 1 }, { "at": 2, "value": 2 } ] } """, "'a': period must be longer than the time of the last key")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "period": 0, "keys": [ { "at": 0, "value": 1 } ] } """, "period must be a number of seconds above zero")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "unavailable": "" } """, "unavailable must be the reason")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "min": "0", "value": 1 } """, "'a': min must be a number")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "decimals": -1, "value": 1 } """, "'a': decimals must be a whole number")]
    [InlineData(""" "a": { "kind": "temperature", "label": "A", "name": "A", "displayUnit": "kelvin", "value": 1 } """, "'a': displayUnit must be one of celsius, fahrenheit")]
    [InlineData(""" "a": { "kind": "number", "label": "A", "name": "A", "attribution": { "url": "x" }, "value": 1 } """, "'a': attribution.text is missing")]
    [InlineData(""" "a/{x": { "kind": "number", "label": "A", "name": "A", "value": 1 } """, "a placeholder must be a whole segment")]
    [InlineData(""" "{x}/{x}": { "kind": "number", "label": "A", "name": "A", "value": 1 } """, "each name once")]
    [InlineData(""" "/a": { "kind": "number", "label": "A", "name": "A", "value": 1 } """, "'/a': not a reading path")]
    [InlineData(""" "a": 3 """, "'a': must be an object")]
    [InlineData(""" "a": { "kind": "number", "kind": "text", "label": "A", "name": "A", "value": 1 } """, "'a': 'kind' is given twice")]
    public void AReading_IsCheckedStrictly_AndTheFindingNamesIt(string readings, string expected)
    {
        var findings = ReadingFindings(readings);

        Assert.Contains(findings, f => f.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void EveryProblem_IsItsOwnFinding()
    {
        var findings = ReadingFindings("""
            "a": { "kind": "number", "label": "A", "name": "A", "value": "x" },
            "b": { "kind": "text", "label": "B", "value": "y" },
            "c": { "kind": "number", "label": "C", "name": "C", "min": "low", "value": 1 }
            """);

        Assert.Equal(3, findings.Count);
    }
}
