// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Tests.Support;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The hub and list readings: data-providers, "List Readings", "List Bounds" and "A List Read As One Value".</summary>
public sealed class ReadingHubListTests : IDisposable
{
    private readonly FakeTime _time = new();
    private readonly ProviderRegistry _registry = new();
    private readonly string _logPath = Path.Combine(Path.GetTempPath(), $"urdeck-lists-{Guid.NewGuid():N}.log");
    private readonly List<IReadOnlyCollection<string>> _changes = [];

    public ReadingHubListTests()
    {
        UrDeckLog.LogPath = _logPath;
        Provider = new FakeProvider(_time)
        {
            Catalog =
            [
                new ReadingDescriptor("a", ReadingKind.Number, "A", "Reading a"),
                new ReadingDescriptor("friends", ReadingKind.List, "Friends", "Friends playing")
                {
                    Unit = "playing",
                    EmptyText = "Nobody is playing",
                    Fields =
                    [
                        new ReadingField("name", ReadingKind.Text, "Name") { Role = FieldRole.Title },
                        new ReadingField("game", ReadingKind.Text, "Game") { Role = FieldRole.Detail },
                    ],
                },
            ],
            OnSample = (_, _, _) => Task.CompletedTask,
        };
        _registry.TryRegister(Provider.Descriptor(), out _);
        Hub = new ReadingHub(_registry, _time, regionUsesFahrenheit: false);
        Hub.ReadingsChanged += ids =>
        {
            lock (_changes)
                _changes.Add(ids);
        };
    }

    private FakeProvider Provider { get; }

    private ReadingHub Hub { get; }

    public void Dispose()
    {
        Hub.Dispose();
        if (File.Exists(_logPath))
            File.Delete(_logPath);
    }

    private string Log() => File.Exists(_logPath) ? File.ReadAllText(_logPath) : "";

    private static bool Wait(Func<bool> condition) => HubTestExtensions.WaitFor(condition);

    private IReadingSink Started()
    {
        Hub.Subscribe(["fake:friends"]);
        Assert.True(Wait(() => Provider.Sink != null && Provider.Samples > 0 && Provider.IsSettled));
        return Provider.Sink!;
    }

    private int Changes()
    {
        lock (_changes)
            return _changes.Count(c => c.Contains("fake:friends", StringComparer.OrdinalIgnoreCase));
    }

    private static ReadingRow Row(string key, string name, string? game = null)
    {
        var fields = new List<KeyValuePair<string, ReadingValue>> { new("name", name) };
        if (game != null)
            fields.Add(new("game", game));
        return new ReadingRow(key, fields);
    }

    private static ReadingList Rows(int count) =>
        new(Enumerable.Range(0, count).Select(i => Row(i.ToString(CultureInfo.InvariantCulture), "Friend " + i.ToString(CultureInfo.InvariantCulture))));

    private int Count(string id = "fake:friends") => Hub.Read(id).Value!.Value.List.Count;

    [Fact]
    public void AListReplacedByAnother_IsSeenWhole()
    {
        var sink = Started();

        sink.Publish("friends", ReadingValue.FromList(Rows(5)));
        Assert.Equal(5, Count());
        sink.Publish("friends", ReadingValue.FromList(Rows(4)));

        Assert.Equal(4, Count());
        Assert.Equal(ReadingState.Ok, Hub.Read("fake:friends").State);
    }

    [Fact]
    public void TheSameListPublishedAgain_IsNotAChange()
    {
        var sink = Started();
        sink.Publish("friends", ReadingValue.FromList(Rows(3)));
        int before = Changes();

        sink.Publish("friends", ReadingValue.FromList(Rows(3)));
        sink.Publish("friends", ReadingValue.FromList(Rows(3)));

        Assert.Equal(before, Changes());
        sink.Publish("friends", ReadingValue.FromList(Rows(2)));
        Assert.Equal(before + 1, Changes());
    }

    [Fact]
    public void TooManyRows_AreCutToTheFirst200_AndLoggedOnce()
    {
        var sink = Started();

        sink.Publish("friends", ReadingValue.FromList(Rows(350)));
        sink.Publish("friends", ReadingValue.FromList(Rows(351)));

        var list = Hub.Read("fake:friends").Value!.Value.List;
        Assert.Equal(ReadingList.MaxRows, list.Count);
        Assert.Equal("0", list[0].Key);
        Assert.Equal(1, Occurrences(Log(), "beyond the bounds"));
        Assert.Contains("Provider 'fake'", Log());
    }

    [Fact]
    public void ACutIsLoggedAgain_AfterTheListWasWithinTheBounds()
    {
        var sink = Started();

        sink.Publish("friends", ReadingValue.FromList(Rows(300)));
        sink.Publish("friends", ReadingValue.FromList(Rows(10)));
        sink.Publish("friends", ReadingValue.FromList(Rows(300)));

        Assert.Equal(2, Occurrences(Log(), "beyond the bounds"));
    }

    [Fact]
    public void ALongText_IsCutToAHundredCharactersLess_EndingInAnEllipsis()
    {
        var sink = Started();
        string longName = new('x', 800);

        sink.Publish("friends", ReadingValue.FromList(new ReadingList([Row("1", longName)])));

        string name = Hub.Read("fake:friends").Value!.Value.List[0].Get("name")!.Value.Text;
        Assert.Equal(ReadingList.MaxTextLength, name.Length);
        Assert.EndsWith("…", name, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoRowsWithOneKey_KeepThePreviousValue_AndTheReasonIsLoggedOnce()
    {
        var sink = Started();
        sink.Publish("friends", ReadingValue.FromList(Rows(3)));
        var duplicated = new ReadingList([Row("1", "A"), Row("1", "B")]);

        sink.Publish("friends", ReadingValue.FromList(duplicated));
        sink.Publish("friends", ReadingValue.FromList(duplicated));

        Assert.Equal(3, Count());
        Assert.Equal(1, Occurrences(Log(), "two rows have the key '1'"));
    }

    [Fact]
    public void AFieldHoldingAList_KeepsThePreviousValue()
    {
        var sink = Started();
        sink.Publish("friends", ReadingValue.FromList(Rows(3)));
        var nested = new ReadingList([new ReadingRow("1", [new("name", ReadingValue.FromList(Rows(1)))])]);

        sink.Publish("friends", ReadingValue.FromList(nested));

        Assert.Equal(3, Count());
        Assert.Contains("holding a list", Log());
    }

    [Fact]
    public void AnInvalidFirstList_LeavesTheReadingPending()
    {
        var sink = Started();

        sink.Publish("friends", ReadingValue.FromList(new ReadingList([Row("1", "A"), Row("1", "B")])));

        Assert.Equal(ReadingState.Pending, Hub.Read("fake:friends").State);
    }

    [Fact]
    public void AnInterruption_KeepsTheLastRows_AsAStaleList()
    {
        var sink = Started();
        sink.Publish("friends", ReadingValue.FromList(Rows(3)));

        sink.Interrupted("connection lost");

        var reading = Hub.Read("fake:friends");
        Assert.Equal(ReadingState.Stale, reading.State);
        Assert.Equal(3, reading.Value!.Value.List.Count);
    }

    [Fact]
    public void ARowWithoutAField_ReadsItAsAbsent()
    {
        var sink = Started();

        sink.Publish("friends", ReadingValue.FromList(new ReadingList([Row("1", "Alex", "Factorio"), Row("2", "Sam")])));

        var list = Hub.Read("fake:friends").Value!.Value.List;
        Assert.True(list[0].TryGet("game", out _));
        Assert.False(list[1].TryGet("game", out _));
    }

    [Fact]
    public void AListReadAsOneValue_IsItsCount()
    {
        var sink = Started();
        sink.Publish("friends", ReadingValue.FromList(Rows(3)));

        var text = ReadingFormatter.Format(Hub.Read("fake:friends"), Hub.Describe("fake:friends"), null, Hub);

        Assert.Equal("3", text.Value);
        Assert.Equal("playing", text.Unit);
    }

    private static int Occurrences(string text, string needle)
    {
        int count = 0;
        for (int at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
