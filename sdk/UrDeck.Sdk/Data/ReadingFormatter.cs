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

    /// <summary>
    /// The unit to show a temperature in; defaults to the descriptor's, then the user's region. It also names the system
    /// for a speed and a depth: Celsius is metric, Fahrenheit is imperial.
    /// </summary>
    public DisplayUnit? DisplayUnit { get; init; }

    /// <summary>How to show a date; defaults to the weekday.</summary>
    public DateStyle? DateStyle { get; init; }

    /// <summary>Whether to show a time on a 24 hour clock; defaults to the user's region.</summary>
    public bool? Use24HourClock { get; init; }
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
            ReadingValueType.Time => ReadingKind.Time,
            _ => ReadingKind.Number,
        };
        (string? unitText, UnitPlacement placement) = kind switch
        {
            ReadingKind.Percent => ("%", UnitPlacement.Raised),
            ReadingKind.Temperature => ("°", UnitPlacement.Raised),
            ReadingKind.Number when !string.IsNullOrEmpty(descriptor?.Unit) => (descriptor!.Unit, UnitPlacement.Baseline),
            ReadingKind.Speed => (unit == DisplayUnit.Fahrenheit ? "mph" : "km/h", UnitPlacement.Baseline),
            ReadingKind.Depth when unit == DisplayUnit.Fahrenheit => ("\"", UnitPlacement.Raised),
            ReadingKind.Depth => (CanonicalDepth(descriptor) == DepthUnit.Centimetre ? "cm" : "mm", UnitPlacement.Baseline),
            _ => (null, UnitPlacement.Baseline),
        };

        bool hasValue = reading.State != ReadingState.Unavailable && reading.Value.HasValue;
        if (kind == ReadingKind.Time)
            return FormatTime(reading, hasValue, options.Use24HourClock ?? source.RegionUses24HourClock);

        if (kind == ReadingKind.Date)
            return FormatDate(reading, hasValue, options.DateStyle ?? DateStyle.Weekday, source.RegionCulture);

        int? explicitDecimals = options.Decimals ?? descriptor?.Decimals;
        string text = hasValue ? Text(reading.Value!.Value, kind, decimals, explicitDecimals, unit, descriptor) : Dash;
        string widest = Widest(hasValue ? reading.Value : null, text, descriptor, kind, decimals, explicitDecimals, unit);
        return new ReadingText(text, unitText, placement, widest, reading.State == ReadingState.Ok);
    }

    private static ReadingText FormatTime(Reading reading, bool hasValue, bool use24Hour)
    {
        string widest = use24Hour ? "00:00" : "12:00";
        bool current = reading.State == ReadingState.Ok;
        if (!hasValue || reading.Value!.Value.Type != ReadingValueType.Time)
            return new ReadingText(Dash, null, UnitPlacement.Baseline, widest, current);

        var time = reading.Value.Value.Time;
        if (use24Hour)
            return new ReadingText(time.ToString("HH:mm", CultureInfo.InvariantCulture), null, UnitPlacement.Baseline, widest, current);

        int hour = time.Hour % 12 == 0 ? 12 : time.Hour % 12;
        string text = hour.ToString(CultureInfo.InvariantCulture) + ":" + time.Minute.ToString("00", CultureInfo.InvariantCulture);
        return new ReadingText(text, time.Hour < 12 ? "AM" : "PM", UnitPlacement.Baseline, widest, current);
    }

    private static ReadingText FormatDate(Reading reading, bool hasValue, DateStyle style, System.Globalization.CultureInfo culture)
    {
        string widest = WidestDate(style, culture);
        bool current = reading.State == ReadingState.Ok;
        if (!hasValue || reading.Value!.Value.Type != ReadingValueType.Time)
            return new ReadingText(Dash, null, UnitPlacement.Baseline, widest, current);

        var time = reading.Value.Value.Time;
        string text = style == DateStyle.MonthDay
            ? time.ToString(MonthDayPattern(culture), culture)
            : culture.DateTimeFormat.GetAbbreviatedDayName(time.DayOfWeek);
        return new ReadingText(text, null, UnitPlacement.Baseline, widest, current);
    }

    private static string MonthDayPattern(System.Globalization.CultureInfo culture) =>
        culture.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM", StringComparison.Ordinal);

    private static string WidestDate(DateStyle style, System.Globalization.CultureInfo culture)
    {
        string widest = "";
        if (style == DateStyle.Weekday)
        {
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                string name = culture.DateTimeFormat.GetAbbreviatedDayName(day);
                if (name.Length > widest.Length)
                    widest = name;
            }

            return widest;
        }

        for (int month = 1; month <= 12; month++)
        {
            string name = new DateTime(2001, month, 28).ToString(MonthDayPattern(culture), culture);
            if (name.Length > widest.Length)
                widest = name;
        }

        return widest;
    }

    /// <summary>The compass point (<c>N</c>, <c>NE</c> ... <c>NW</c>) whose 45 degree sector holds the bearing.</summary>
    public static string CompassPoint(double degrees)
    {
        string[] points = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        double normalised = ((degrees % 360) + 360) % 360;
        return points[(int)Math.Floor((normalised + 22.5) / 45) % 8];
    }

    private static DepthUnit CanonicalDepth(ReadingDescriptor? descriptor) => descriptor?.DepthUnit ?? DepthUnit.Millimetre;

    private static DisplayUnit ResolveDisplayUnit(ReadingFormatOptions options, ReadingDescriptor? descriptor, IReadingSource source) =>
        options.DisplayUnit
        ?? descriptor?.DisplayUnit
        ?? (source.RegionUsesFahrenheit ? DisplayUnit.Fahrenheit : DisplayUnit.Celsius);

    private static string Text(ReadingValue value, ReadingKind kind, int decimals, int? explicitDecimals, DisplayUnit unit, ReadingDescriptor? descriptor) => value.Type switch
    {
        ReadingValueType.Text => value.Text,
        ReadingValueType.OnOff => value.IsOn ? "On" : "Off",
        ReadingValueType.Time => value.ToString(),
        _ => Number(value.Number, kind, decimals, explicitDecimals, unit, descriptor),
    };

    private static string Number(double number, ReadingKind kind, int decimals, int? explicitDecimals, DisplayUnit unit, ReadingDescriptor? descriptor)
    {
        bool imperial = unit == DisplayUnit.Fahrenheit;
        switch (kind)
        {
            case ReadingKind.Temperature when imperial:
                number = number * 9 / 5 + 32;
                break;
            case ReadingKind.Speed when imperial:
                number /= KilometresPerMile;
                break;
            case ReadingKind.Depth:
                return Depth(number, imperial, explicitDecimals, descriptor);
        }

        number = Math.Round(number, decimals, MidpointRounding.AwayFromZero);
        return number.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    private const double KilometresPerMile = 1.609344;

    private static string Depth(double number, bool imperial, int? explicitDecimals, ReadingDescriptor? descriptor)
    {
        if (imperial)
            number /= CanonicalDepth(descriptor) == DepthUnit.Centimetre ? 2.54 : 25.4;

        if (number <= 0)
            return "0";

        // One decimal below 10 and none from 10 up (judged after rounding, so 9.96 reads 10), unless asked.
        int decimals = explicitDecimals is { } asked
            ? Math.Max(0, asked)
            : Math.Round(number, 1, MidpointRounding.AwayFromZero) < 10 ? 1 : 0;
        double rounded = Math.Round(number, decimals, MidpointRounding.AwayFromZero);
        if (rounded == 0)
            return "<" + Math.Pow(10, -decimals).ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        return rounded.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>The longer of the range's two ends as text; the value itself when the entry has no range.</summary>
    private static string Widest(ReadingValue? value, string text, ReadingDescriptor? descriptor, ReadingKind kind, int decimals, int? explicitDecimals, DisplayUnit unit)
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
                string candidate = Number(number, kind, decimals, explicitDecimals, unit, descriptor);
                if (candidate.Length > widest.Length || widest == Dash)
                    widest = candidate;
            }
        }

        return widest;
    }
}
