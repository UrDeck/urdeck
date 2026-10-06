// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>A catalog entry: what a provider says about one of its readings.</summary>
/// <param name="Path">The path under the provider id, for example <c>cpu/core/2/load</c>.</param>
/// <param name="Kind">What the reading measures.</param>
/// <param name="Label">A short default label, for example <c>Core 2</c>.</param>
/// <param name="Name">The full name, for example <c>CPU core 2 load</c>.</param>
public sealed record ReadingDescriptor(string Path, ReadingKind Kind, string Label, string Name)
{
    /// <summary>The device the reading belongs to, for example the CPU's name.</summary>
    public string? Device { get; init; }

    /// <summary>The smallest value the reading takes (canonical unit).</summary>
    public double? Min { get; init; }

    /// <summary>The largest value the reading takes (canonical unit).</summary>
    public double? Max { get; init; }

    /// <summary>The unit text of a plain <see cref="ReadingKind.Number"/>.</summary>
    public string? Unit { get; init; }

    /// <summary>The unit to show a kind in when the widget does not choose one.</summary>
    public DisplayUnit? DisplayUnit { get; init; }

    /// <summary>The number of decimals to show when the widget does not choose any.</summary>
    public int? Decimals { get; init; }
}
