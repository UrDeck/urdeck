// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Globalization;
using UrDeck.Sdk.Components;

namespace UrDeck.Sdk.Data;

/// <summary>What a widget asks of the formatter for one reading. Everything is optional.</summary>
public sealed class ReadingFormatOptions
{
    /// <summary>Decimals to show; defaults to the descriptor's, then 0.</summary>
    public int? Decimals { get; init; }

    /// <summary>The unit to show a temperature in; defaults to the descriptor's, then the user's region.</summary>
    public DisplayUnit? DisplayUnit { get; init; }
}

/// <summary>A reading as a widget draws it.</summary>
/// <param name="Value">The value text, or <see cref="ReadingFormatter.Dash"/> when there is no value.</param>
/// <param name="Unit">The unit text, kept when the value is a dash.</param>
/// <param name="UnitPlacement">Raised for <c>%</c> and <c>°</c>, on the baseline for a plain number's unit.</param>
/// <param name="WidestValue">The widest value text to expect, which keeps a readout's size stable.</param>
/// <param name="IsCurrent">False for a stale, pending or unavailable reading.</param>
public readonly record struct ReadingText(string Value, string? Unit, UnitPlacement UnitPlacement, string WidestValue, bool IsCurrent);

/// <summary>Turns a reading and its catalog entry into the text a widget draws, so that all widgets format alike.</summary>
public static class ReadingFormatter
{
    /// <summary>Drawn in place of a value that is missing (an en dash).</summary>
    public const string Dash = "–";

    /// <summary>The widest value a dash is sized for when the catalog entry gives no range.</summary>
    private const string DashWidth = "88";

    public static ReadingText Format(Reading reading, ReadingDescriptor? descriptor, ReadingFormatOptions? options, IReadingSource source)
    {
        options ??= new ReadingFormatOptions();
        int decimals = Math.Max(0, options.Decimals ?? descriptor?.Decimals ?? 0);
        var unit = ResolveDisplayUnit(options, descriptor, source);

        // Without a catalog entry the kind follows the value.
        var kind = descriptor?.Kind ?? reading.Value?.Type switch
        {
            ReadingValueType.Text => ReadingKind.Text,
            ReadingValueType.OnOff => ReadingKind.OnOff,
            _ => ReadingKind.Number,
        };
        (string? unitText, UnitPlacement placement) = kind switch
        {
            ReadingKind.Percent => ("%", UnitPlacement.Raised),
            ReadingKind.Temperature => ("°", UnitPlacement.Raised),
            ReadingKind.Number when !string.IsNullOrEmpty(descriptor?.Unit) => (descriptor!.Unit, UnitPlacement.Baseline),
            _ => (null, UnitPlacement.Baseline),
        };

        bool hasValue = reading.State != ReadingState.Unavailable && reading.Value.HasValue;
        string text = hasValue ? Text(reading.Value!.Value, kind, decimals, unit) : Dash;
        string widest = Widest(hasValue ? reading.Value : null, text, descriptor, kind, decimals, unit);
        return new ReadingText(text, unitText, placement, widest, reading.State == ReadingState.Ok);
    }

    private static DisplayUnit ResolveDisplayUnit(ReadingFormatOptions options, ReadingDescriptor? descriptor, IReadingSource source) =>
        options.DisplayUnit
        ?? descriptor?.DisplayUnit
        ?? (source.RegionUsesFahrenheit ? DisplayUnit.Fahrenheit : DisplayUnit.Celsius);

    private static string Text(ReadingValue value, ReadingKind kind, int decimals, DisplayUnit unit) => value.Type switch
    {
        ReadingValueType.Text => value.Text,
        ReadingValueType.OnOff => value.IsOn ? "On" : "Off",
        _ => Number(value.Number, kind, decimals, unit),
    };

    private static string Number(double number, ReadingKind kind, int decimals, DisplayUnit unit)
    {
        if (kind == ReadingKind.Temperature && unit == DisplayUnit.Fahrenheit)
            number = number * 9 / 5 + 32;
        number = Math.Round(number, decimals, MidpointRounding.AwayFromZero);
        return number.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>The longer of the range's two ends as text; the value itself when the entry has no range.</summary>
    private static string Widest(ReadingValue? value, string text, ReadingDescriptor? descriptor, ReadingKind kind, int decimals, DisplayUnit unit)
    {
        // A dash with nothing to size it against would fill the readout; draw it like a two-digit number instead.
        if (value == null && (descriptor == null || (descriptor.Min == null && descriptor.Max == null)))
            return DashWidth;
        if (descriptor == null || (descriptor.Min == null && descriptor.Max == null) || value is { Type: not ReadingValueType.Number })
            return text;

        string widest = text;
        foreach (double? end in new[] { descriptor.Min, descriptor.Max })
        {
            if (end is { } number)
            {
                string candidate = Number(number, kind, decimals, unit);
                if (candidate.Length > widest.Length || widest == Dash)
                    widest = candidate;
            }
        }

        return widest;
    }
}
