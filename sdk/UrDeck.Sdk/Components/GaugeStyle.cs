// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Components;

/// <summary>The shape a <see cref="Gauge"/> draws around a readout. New styles may be added.</summary>
public enum GaugeStyle
{
    /// <summary>The readout alone.</summary>
    Plain,
    /// <summary>An arc open at the bottom, with the readout inside it.</summary>
    Ring,
    /// <summary>A horizontal track at the top, with the readout below it.</summary>
    Bar,
    /// <summary>The whole rectangle as the track, filled from the bottom, with the readout inside at the bottom.</summary>
    VerticalBar,
}

/// <summary>How far a value is along its range: normal, or from the warning or critical value on.</summary>
public enum GaugeLevel
{
    Normal,
    Warning,
    Critical,
}

/// <summary>What <see cref="GaugeScale"/> makes of a reading.</summary>
/// <param name="Fraction">The value's position in its range, 0 to 1; null when there is none to show.</param>
/// <param name="Level">The level the value has reached.</param>
public readonly record struct GaugeValue(float? Fraction, GaugeLevel Level);

/// <summary>What a widget passes to <see cref="GaugeScale"/> to replace the reading's catalog entry. Everything is optional.</summary>
public sealed class GaugeScaleOptions
{
    /// <summary>The start of the range, in the reading's canonical unit.</summary>
    public double? Min { get; init; }

    /// <summary>The end of the range, in the reading's canonical unit.</summary>
    public double? Max { get; init; }

    /// <summary>The value from which the level is warning, in the reading's canonical unit.</summary>
    public double? Warning { get; init; }

    /// <summary>The value from which the level is critical, in the reading's canonical unit.</summary>
    public double? Critical { get; init; }

    /// <summary>The decimals the text shows; the value is rounded to them first. Defaults to the catalog's, then 0.</summary>
    public int? Decimals { get; init; }
}
