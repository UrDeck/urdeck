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

    /// <summary>
    /// From this value on (canonical unit) the reading deserves attention. Null when a high value is not a fault, which
    /// is the case for every reading that has no levels of its own.
    /// </summary>
    public double? Warning { get; init; }

    /// <summary>From this value on (canonical unit) the reading is a fault. Null when it has none.</summary>
    public double? Critical { get; init; }

    /// <summary>The unit text of a plain <see cref="ReadingKind.Number"/>.</summary>
    public string? Unit { get; init; }

    /// <summary>The unit to show a kind in when the widget does not choose one.</summary>
    public DisplayUnit? DisplayUnit { get; init; }

    /// <summary>The number of decimals to show when the widget does not choose any.</summary>
    public int? Decimals { get; init; }

    /// <summary>The credit the reading's source requires wherever its data is shown; null when it requires none.</summary>
    public ReadingAttribution? Attribution { get; init; }
}

/// <summary>A source's credit: the text to draw, an optional shorter text for a narrow place and an optional link.</summary>
/// <param name="Text">The credit, for example <c>Weather data by Open-Meteo.com</c>.</param>
/// <param name="ShortText">Drawn instead of <paramref name="Text"/> when that does not fit.</param>
/// <param name="Url">The source's page. A component draws text only; the link is for a tap or an about screen.</param>
public sealed record ReadingAttribution(string Text, string? ShortText = null, string? Url = null);
