// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Machine;

/// <summary>
/// The processes using the most processor time (<c>processes/top</c>): a list of at most ten, most first. A share needs two
/// enumerations, so the first sample after a subscription publishes nothing; processes are enumerated only while the reading
/// is shown. Names are published to widgets and never written to the log.
/// </summary>
internal sealed class ProcessReadings(IProcessPlatform platform)
{
    public const string Top = "processes/top";

    private const int MaxRows = 10;
    private const double BytesPerMegabyte = 1024 * 1024;

    private Dictionary<int, TimeSpan>? _baseline;
    private TimeSpan _baselineAt;

    /// <summary>Counted so a test can see that an unwanted reading is not enumerated.</summary>
    public int Queries { get; private set; }

    public static void Describe(List<ReadingDescriptor> catalog) =>
        catalog.Add(new(Top, ReadingKind.List, "Busy processes", "Processes using the most CPU")
        {
            Unit = "processes",
            EmptyText = "No busy processes",
            Fields =
            [
                new ReadingField("name", ReadingKind.Text, "Process") { Role = FieldRole.Title },
                new ReadingField("cpu", ReadingKind.Percent, "CPU") { Role = FieldRole.Status, Decimals = 1, Min = 0, Max = 100 },
                new ReadingField("memory", ReadingKind.Number, "Memory") { Role = FieldRole.Detail, Unit = "MB", Decimals = 0 },
            ],
        });

    public void Sample(IReadingSink sink)
    {
        Queries++;
        var now = platform.Now();
        var processes = platform.Enumerate();
        var previous = _baseline;
        var previousAt = _baselineAt;
        _baseline = processes.ToDictionary(p => p.Id, p => p.CpuTime);
        _baselineAt = now;

        double elapsed = (now - previousAt).TotalSeconds;
        if (previous == null || elapsed <= 0)
            return;

        double capacity = elapsed * Math.Max(1, platform.ProcessorCount);
        var rows = processes
            .Where(p => previous.ContainsKey(p.Id))
            .Select(p => (Process: p, Share: Math.Clamp((p.CpuTime - previous[p.Id]).TotalSeconds / capacity * 100, 0, 100)))
            .Where(p => Math.Round(p.Share, 1) > 0)
            .OrderByDescending(p => p.Share)
            .ThenBy(p => p.Process.Id)
            .Take(MaxRows)
            .Select(p => new ReadingRow(
                p.Process.Id.ToString(CultureInfo.InvariantCulture),
                [
                    new("name", p.Process.Name),
                    new("cpu", Math.Round(p.Share, 1)),
                    new("memory", Math.Round(p.Process.WorkingSetBytes / BytesPerMegabyte)),
                ]));
        sink.Publish(Top, ReadingValue.FromList(new ReadingList(rows)));
    }

    /// <summary>Forgets the baseline, so that the next subscription starts with two fresh enumerations.</summary>
    public void DropBaseline() => _baseline = null;
}
