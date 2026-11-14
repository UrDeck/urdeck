// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Data;
using UrDeck.Engine.Secrets;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Cli.Tests;

/// <summary><c>readings browse</c> over a provider that lists a list reading: its rows are printed, key and fields.</summary>
public sealed class ReadingsBrowseListTests
{
    private static (int Code, string Out, string Error) Browse(string format)
    {
        var registry = new ProviderRegistry();
        registry.TryRegister(ProviderDescriptor.TryCreate(typeof(ListyProvider), out _)!, out _);
        using var output = new StringWriter { NewLine = "\n" };
        using var error = new StringWriter { NewLine = "\n" };
        int code = ReadingsBrowseRunner.Run(
            new ReadingsBrowseOptions("listy", null, null, null, null, format), output, error, registry, new SecretStore(null, _ => null), TimeSpan.FromSeconds(2));
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void ARowOfAListIsPrinted_WithItsKeyAndTheFieldsItHas()
    {
        var run = Browse("text");

        Assert.Equal(0, run.Code);
        string[] lines = run.Out.TrimEnd('\n').Split('\n');
        Assert.Matches(@"^listy:friends/playing  list  2 playing  Friends playing$", lines[0]);
        Assert.Equal("    76561190000000001  name=Alex  game=Factorio  hours=12.3 h", lines[1]);
        Assert.Equal("    76561190000000002  name=Sam", lines[2]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void TheJsonCarriesTheRowsToo()
    {
        var run = Browse("json");

        Assert.Equal(0, run.Code);
        var reading = JsonDocument.Parse(run.Out).RootElement[0];
        Assert.Equal("list", reading.GetProperty("kind").GetString());
        var rows = reading.GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal("76561190000000001", rows[0].GetProperty("key").GetString());
        Assert.Equal("Factorio", rows[0].GetProperty("fields").GetProperty("game").GetString());
        Assert.False(rows[1].GetProperty("fields").TryGetProperty("game", out _));
    }

    [DataProvider("listy", "Listy")]
    public sealed class ListyProvider : DataProvider<ProviderSettings>, IReadingBrowser
    {
        private static readonly ReadingDescriptor Friends = new("friends/playing", ReadingKind.List, "Friends playing", "Friends playing")
        {
            Unit = "playing",
            Fields =
            [
                new ReadingField("name", ReadingKind.Text, "Name") { Role = FieldRole.Title },
                new ReadingField("game", ReadingKind.Text, "Game") { Role = FieldRole.Detail },
                new ReadingField("hours", ReadingKind.Number, "Hours") { Unit = "h", Decimals = 1 },
            ],
        };

        private IReadingSink? _sink;

        public override IReadOnlyList<ReadingDescriptor> Describe() => [Friends];

        public override void Start(IReadingSink sink, ProviderSettings settings) => _sink = sink;

        public override void SetDemand(IReadOnlyCollection<string> paths)
        {
        }

        public override Task SampleAsync(CancellationToken cancellationToken)
        {
            _sink!.Publish("friends/playing", ReadingValue.FromList(new ReadingList(
            [
                new ReadingRow("76561190000000001", [new("name", "Alex"), new("game", "Factorio"), new("hours", 12.25)]),
                new ReadingRow("76561190000000002", [new("name", "Sam")]),
            ])));
            return Task.CompletedTask;
        }

        public override void Shutdown()
        {
        }

        public Task<IReadOnlyList<ReadingDescriptor>> BrowseAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReadingDescriptor>>([Friends]);
    }
}
