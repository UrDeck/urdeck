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

    /// <summary>
    /// Publishes several readings so that they become visible together, in one notification: a widget never sees a track's new
    /// title with the previous track's cover. A provider that publishes from its own callbacks uses it for the readings of one
    /// change; values published during <see cref="IDataProvider.SampleAsync"/> already arrive together. The default publishes
    /// them one by one.
    /// </summary>
    void PublishBatch(IReadOnlyList<ReadingUpdate> updates)
    {
        foreach (var update in updates)
        {
            if (update.Value is { } value)
                Publish(update.Path, value);
            else
                Unavailable(update.Path, update.Reason ?? "unavailable");
        }
    }

    /// <summary>Writes one line to the host's log, for the facts that make a report useful (which device, which source).</summary>
    void Log(string message)
    {
    }
}

/// <summary>One entry of <see cref="IReadingSink.PublishBatch"/>: a value, or the reason the reading cannot be supplied.</summary>
/// <param name="Path">The reading's path under the provider id.</param>
/// <param name="Value">The value; null when the reading is unavailable.</param>
/// <param name="Reason">Why it is unavailable; used when <paramref name="Value"/> is null.</param>
public readonly record struct ReadingUpdate(string Path, ReadingValue? Value, string? Reason = null)
{
    /// <summary>An update that publishes <paramref name="value"/>.</summary>
    public static ReadingUpdate Of(string path, ReadingValue value) => new(path, value);

    /// <summary>An update that marks the reading unavailable for <paramref name="reason"/>.</summary>
    public static ReadingUpdate Missing(string path, string reason) => new(path, null, reason);
}
