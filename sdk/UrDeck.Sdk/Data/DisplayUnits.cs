// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>
/// Converts a reading's value between its canonical unit (what providers report and the configuration stores) and the display unit
/// a card shows it in. <see cref="ReadingFormatter"/> uses the same methods, so a form that shows a threshold in °F and the card
/// that draws it agree. <see cref="DisplayUnit.Fahrenheit"/> names the imperial system for a speed and a depth too.
/// </summary>
public static class DisplayUnits
{
    private const double KilometresPerMile = 1.609344;

    /// <summary>The value in the display unit. A kind with no conversion returns the value as it is.</summary>
    /// <param name="kind">The reading's kind.</param>
    /// <param name="canonical">The value in the canonical unit (°C, km/h, and the descriptor's depth unit).</param>
    /// <param name="unit">The display unit.</param>
    /// <param name="descriptor">The reading's catalog entry; gives a depth's canonical unit.</param>
    public static double ToDisplay(ReadingKind kind, double canonical, DisplayUnit unit, ReadingDescriptor? descriptor)
    {
        if (unit != DisplayUnit.Fahrenheit)
            return canonical;

        return kind switch
        {
            ReadingKind.Temperature => canonical * 9 / 5 + 32,
            ReadingKind.Speed => canonical / KilometresPerMile,
            ReadingKind.Depth => canonical / InchDivisor(descriptor),
            _ => canonical,
        };
    }

    /// <summary>The value in the canonical unit; the inverse of <see cref="ToDisplay"/>.</summary>
    /// <param name="kind">The reading's kind.</param>
    /// <param name="display">The value in the display unit.</param>
    /// <param name="unit">The display unit.</param>
    /// <param name="descriptor">The reading's catalog entry; gives a depth's canonical unit.</param>
    public static double FromDisplay(ReadingKind kind, double display, DisplayUnit unit, ReadingDescriptor? descriptor)
    {
        if (unit != DisplayUnit.Fahrenheit)
            return display;

        return kind switch
        {
            ReadingKind.Temperature => (display - 32) * 5 / 9,
            ReadingKind.Speed => display * KilometresPerMile,
            ReadingKind.Depth => display * InchDivisor(descriptor),
            _ => display,
        };
    }

    /// <summary>
    /// The text that follows a value shown in the display unit (<c>°</c>, <c>%</c>, <c>km/h</c>, <c>mph</c>, <c>cm</c>, <c>mm</c>,
    /// <c>"</c>, or a number reading's own unit); null for a kind with none.
    /// </summary>
    /// <param name="kind">The reading's kind.</param>
    /// <param name="unit">The display unit.</param>
    /// <param name="descriptor">The reading's catalog entry.</param>
    public static string? UnitText(ReadingKind kind, DisplayUnit unit, ReadingDescriptor? descriptor) => kind switch
    {
        ReadingKind.Percent => "%",
        ReadingKind.Temperature => "°",
        ReadingKind.Number or ReadingKind.List when !string.IsNullOrEmpty(descriptor?.Unit) => descriptor!.Unit,
        ReadingKind.Speed => unit == DisplayUnit.Fahrenheit ? "mph" : "km/h",
        ReadingKind.Depth when unit == DisplayUnit.Fahrenheit => "\"",
        ReadingKind.Depth => CanonicalDepth(descriptor) == DepthUnit.Centimetre ? "cm" : "mm",
        _ => null,
    };

    internal static DepthUnit CanonicalDepth(ReadingDescriptor? descriptor) => descriptor?.DepthUnit ?? DepthUnit.Millimetre;

    private static double InchDivisor(ReadingDescriptor? descriptor) => CanonicalDepth(descriptor) == DepthUnit.Centimetre ? 2.54 : 25.4;
}
