// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections.Concurrent;
using UrDeck.Engine.Data;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Tests.Support;

/// <summary>A provider a test controls: it records every call and runs the test's code inside a sample.</summary>
internal sealed class FakeProvider(TimeProvider time) : IDataProvider
{
    private readonly object _gate = new();
    private readonly List<string> _calls = [];

    public IReadingSink? Sink { get; private set; }

    public ConcurrentQueue<TimeSpan> SampleTimes { get; } = new();

    public IReadOnlyList<ReadingDescriptor> Catalog { get; set; } =
    [
        new ReadingDescriptor("a", ReadingKind.Percent, "A", "Reading a") { Min = 0, Max = 100 },
        new ReadingDescriptor("b", ReadingKind.Percent, "B", "Reading b") { Min = 0, Max = 100 },
        new ReadingDescriptor("c", ReadingKind.Number, "C", "Reading c") { Unit = "W" },
    ];

    /// <summary>Runs inside every sample with the zero-based sample number; the default publishes the sample number to a and b.</summary>
    public Func<int, IReadingSink, CancellationToken, Task>? OnSample { get; set; }

    public Exception? DescribeError { get; set; }

    public Exception? StartError { get; set; }

    public int Samples { get; private set; }

    public int SamplesRunning;

    public int MaxConcurrentSamples { get; private set; }

    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_gate)
                return _calls.ToArray();
        }
    }

    public IReadOnlyCollection<string> Demand { get; private set; } = [];

    public int Count(string call) => Calls.Count(c => c == call);

    private void Record(string call)
    {
        lock (_gate)
            _calls.Add(call);
    }

    public IReadOnlyList<ReadingDescriptor> Describe()
    {
        Record("describe");
        if (DescribeError != null)
            throw DescribeError;
        return Catalog;
    }

    public void Start(IReadingSink sink)
    {
        Record("start");
        if (StartError != null)
            throw StartError;
        Sink = sink;
    }

    public void SetDemand(IReadOnlyCollection<string> paths)
    {
        Demand = paths.OrderBy(p => p, StringComparer.Ordinal).ToArray();
        Record("demand:" + string.Join(",", Demand));
    }

    public async Task SampleAsync(CancellationToken cancellationToken)
    {
        Record("sample");
        SampleTimes.Enqueue(time.GetUtcNow() - new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        int running = Interlocked.Increment(ref SamplesRunning);
        MaxConcurrentSamples = Math.Max(MaxConcurrentSamples, running);
        int number = Samples++;
        try
        {
            if (OnSample != null)
                await OnSample(number, Sink!, cancellationToken);
            else
            {
                Sink!.Publish("a", number);
                Sink.Publish("b", number);
            }
        }
        finally
        {
            Interlocked.Decrement(ref SamplesRunning);
        }
    }

    public void Shutdown() => Record("stop");

    public ProviderDescriptor Descriptor(string id = "fake", int defaultMs = 1000, int minMs = 250) =>
        new(id, "Fake", typeof(FakeProvider), defaultMs, minMs, "tests") { Factory = () => this };
}

internal static class HubTestExtensions
{
    public static bool WaitFor(Func<bool> condition, int timeoutMs = 5000)
    {
        long started = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - started > timeoutMs)
                return false;
            Thread.Sleep(2);
        }

        return true;
    }
}
