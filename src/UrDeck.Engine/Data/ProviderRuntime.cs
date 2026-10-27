// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>One thing a provider published: a value or a statement that the reading is unavailable.</summary>
internal readonly record struct Publication(ReadingValue? Value, string? Reason);

/// <summary>An action asked of a provider and not yet finished.</summary>
internal sealed record PendingAction(string Id, string Path, ReadingValue? Argument, long Asked, TaskCompletionSource<ActionResult> Completion);

/// <summary>
/// The hub's state for one provider. Every field is guarded by the hub's gate except <see cref="DemandLock"/>, which
/// only serializes calls to <see cref="IDataProvider.SetDemand"/>.
/// </summary>
internal sealed class ProviderRuntime(ProviderDescriptor descriptor)
{
    public ProviderDescriptor Descriptor { get; } = descriptor;

    /// <summary>The current instance; null before the first need and after a stop.</summary>
    public IDataProvider? Instance { get; set; }

    /// <summary>Whether <see cref="IDataProvider.Start"/> ran on <see cref="Instance"/>.</summary>
    public bool Started { get; set; }

    /// <summary>The catalog's exact entries by path; kept across stops, dropped on release.</summary>
    public Dictionary<string, ReadingDescriptor>? Catalog { get; set; }

    /// <summary>The catalog's entries with parameter segments, in the order the provider listed them.</summary>
    public List<CatalogPattern> Patterns { get; } = [];

    /// <summary>The descriptions already made from a pattern, by the path that asked for them.</summary>
    public Dictionary<string, ReadingDescriptor> Instances { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Paths with at least one subscriber (including paths the catalog turns out not to contain).</summary>
    public HashSet<string> Wanted { get; } = new(StringComparer.OrdinalIgnoreCase);

    public CancellationTokenSource? Cts { get; set; }

    public Task? Loop { get; set; }

    public ITimer? LingerTimer { get; set; }

    /// <summary>Bumped on every stop, so a loop or a sink of an earlier instance can tell it is out of date.</summary>
    public int Generation { get; set; }

    public bool DemandDirty { get; set; }

    public bool DemandPumpRunning { get; set; }

    public object DemandLock { get; } = new();

    public int IntervalMs { get; set; }

    /// <summary>The fingerprint of the settings this run was prepared with (see <c>BoundSettings</c>); null before the first preparation.</summary>
    public string? Fingerprint { get; set; }

    /// <summary>Why the provider is not started while a required setting is missing; null when it is not waiting for settings.</summary>
    public string? MissingReason { get; set; }

    /// <summary>Completed when the run starts (null) or cannot (the reason); actions wait on it. Replaced after a failure and on a stop.</summary>
    public TaskCompletionSource<string?>? StartedSignal { get; set; }

    /// <summary>The configured interval last reported as raised, so a reload does not repeat the log line.</summary>
    public int? LoggedClampedFrom { get; set; }

    /// <summary>Completed to cut a wait short (the interval changed); replaced after use.</summary>
    public TaskCompletionSource Signal { get; set; } = NewSignal();

    public bool InSample { get; set; }

    public Dictionary<string, Publication> Buffer { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Samples in a row that published something but not this path.</summary>
    public Dictionary<string, int> Misses { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int Failures { get; set; }

    /// <summary>The actions waiting for this run of the provider; emptied (as failed) when the run ends.</summary>
    public Queue<PendingAction> Actions { get; } = new();

    /// <summary>Whether a task is working through <see cref="Actions"/>; belongs to the run it started in.</summary>
    public bool ActionPumpRunning { get; set; }

    /// <summary>Cancelled when the run ends, so an action in flight sees its token cancelled; null until the run's first action.</summary>
    public CancellationTokenSource? ActionCts { get; set; }

    /// <summary>The action catalog; kept across stops, dropped on release. Null until read.</summary>
    public List<ActionDescriptor>? ActionCatalog { get; set; }

    public static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static TaskCompletionSource<string?> NewStartSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
