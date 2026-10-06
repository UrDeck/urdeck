// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>
/// What a widget reads: a state, the value that goes with it (a pending or stale reading keeps its last value) and, for
/// an unavailable reading, why. Immutable, so a paint reads a consistent reading without a lock.
/// </summary>
public readonly record struct Reading(ReadingState State, ReadingValue? Value = null, string? Reason = null)
{
    public static Reading Ok(ReadingValue value) => new(ReadingState.Ok, value);

    public static Reading Pending(ReadingValue? lastValue = null) => new(ReadingState.Pending, lastValue);

    public static Reading Stale(ReadingValue lastValue) => new(ReadingState.Stale, lastValue);

    public static Reading Unavailable(string reason) => new(ReadingState.Unavailable, null, reason);
}
