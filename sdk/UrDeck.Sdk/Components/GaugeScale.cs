// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Sdk.Components;

/// <summary>Turns a reading into the fraction and level a <see cref="Gauge"/> shows, so that all widgets scale alike.</summary>
public static class GaugeScale
{
    public static GaugeValue Resolve(Reading reading, ReadingDescriptor? descriptor, GaugeScaleOptions? options = null)
    {
        if (reading.State == ReadingState.Unavailable || reading.Value is not { Type: ReadingValueType.Number } value)
            return new GaugeValue(null, GaugeLevel.Normal);

        // Rounded to what the text shows, so a change the text does not show moves neither the fill nor the level.
        int decimals = Math.Max(0, options?.Decimals ?? descriptor?.Decimals ?? 0);
        double number = Math.Round(value.Number, decimals, MidpointRounding.AwayFromZero);
        if (!double.IsFinite(number))
            return new GaugeValue(null, GaugeLevel.Normal);

        double? warning = options?.Warning ?? descriptor?.Warning;
        double? critical = options?.Critical ?? descriptor?.Critical;
        var level = critical is { } c && number >= c ? GaugeLevel.Critical
            : warning is { } w && number >= w ? GaugeLevel.Warning
            : GaugeLevel.Normal;

        bool percent = descriptor?.Kind == ReadingKind.Percent;
        double? min = options?.Min ?? descriptor?.Min ?? (percent ? 0 : null);
        double? max = options?.Max ?? descriptor?.Max ?? (percent ? 100 : null);
        if (min is not { } low || max is not { } high || !(high > low))
            return new GaugeValue(null, level);

        return new GaugeValue((float)Math.Clamp((number - low) / (high - low), 0, 1), level);
    }
}
