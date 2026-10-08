// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>
/// A source of readings. The engine creates it when a reading of it is first needed, tells it what is wanted, samples it
/// on a thread-pool thread and stops it when nothing needs it. A provider that throws only loses its own readings.
/// A sample that never returns holds one thread-pool thread until the plugin is reloaded; the engine never starts a
/// second sample of the provider.
/// </summary>
public interface IDataProvider
{
    /// <summary>The catalog: every reading the provider offers. Called once; must not need <see cref="Start"/>.</summary>
    IReadOnlyList<ReadingDescriptor> Describe();

    /// <summary>Begins work. Keep <paramref name="sink"/> to publish through.</summary>
    void Start(IReadingSink sink);

    /// <summary>The paths currently subscribed to; called after <see cref="Start"/> and whenever the set changes.</summary>
    void SetDemand(IReadOnlyCollection<string> paths);

    /// <summary>
    /// Collects values and publishes them. Everything published during one call becomes visible together when the call
    /// returns. A pushed provider may leave this empty and publish from its own callbacks.
    /// </summary>
    Task SampleAsync(CancellationToken cancellationToken);

    /// <summary>Ends work and releases everything. No call follows.</summary>
    void Shutdown();
}

/// <summary>Where a provider publishes. Thread-safe.</summary>
public interface IReadingSink
{
    void Publish(string path, ReadingValue value);

    /// <summary>Reports that the reading cannot be supplied.</summary>
    void Unavailable(string path, string reason);

    /// <summary>Writes one line to the host's log, for the facts that make a report useful (which device, which source).</summary>
    void Log(string message)
    {
    }
}
