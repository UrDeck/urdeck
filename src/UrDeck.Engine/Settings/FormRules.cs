// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;

namespace UrDeck.Engine.Settings;

/// <summary>The display unit a form offers for a reading's kind: the value stored in the file and the words the user reads.</summary>
/// <param name="Value">The text stored in the unit setting (<c>celsius</c>, <c>fahrenheit</c>).</param>
/// <param name="Label">What the list shows.</param>
public sealed record UnitChoice(string Value, string Label);

/// <summary>
/// The placeholder of an empty field and the unit a number is shown in (editor-forms D4, D5): the rules of the form that need no
/// window, so they are tested on their own.
/// </summary>
public static class FormRules
{
    private const string FromTheReading = "From the reading";

    /// <summary>
    /// The words an empty field shows. The declared words, or what the widget takes instead: the reading's label, unit, range or
    /// levels (by the setting's key), or the unit of the user's region.
    /// </summary>
    /// <param name="setting">The setting whose field is empty.</param>
    /// <param name="reading">The reading chosen in the setting it follows; null when none is chosen or it is unknown.</param>
    /// <param name="display">The unit the card shows the reading in.</param>
    /// <param name="regionUsesFahrenheit">Whether the user's region uses Fahrenheit.</param>
    public static string Placeholder(SettingDescriptor setting, ReadingDescriptor? reading, DisplayUnit display, bool regionUsesFahrenheit)
    {
        string words = setting.Fallback.Text ?? "";
        switch (setting.Fallback.From)
        {
            case FallbackSource.Region:
                return regionUsesFahrenheit ? "°F (your region)" : "°C (your region)";
            case FallbackSource.Reading:
                if (reading == null)
                    return words;
                return setting.Key switch
                {
                    "label" => reading.Label,
                    "unit" => UnitSuffix(reading, display) is { Length: > 0 } unit ? unit : words,
                    "min" => Level(reading.Min, reading, display, words: FromTheReading),
                    "max" => Level(reading.Max, reading, display, words: FromTheReading),
                    "warning" => Level(reading.Warning, reading, display, words),
                    "critical" => Level(reading.Critical, reading, display, words),
                    _ => words,
                };
            default:
                return words;
        }
    }

    /// <summary>The display unit a number with a unit from a reading is shown in: the item's unit setting, else the reading's, else the region's.</summary>
    public static DisplayUnit ResolveUnit(string? unitSetting, ReadingDescriptor? reading, bool regionUsesFahrenheit) =>
        ParseUnit(unitSetting) ?? reading?.DisplayUnit ?? (regionUsesFahrenheit ? DisplayUnit.Fahrenheit : DisplayUnit.Celsius);

    /// <summary>Reads the text of a unit setting; null for anything else, which the card treats as automatic.</summary>
    public static DisplayUnit? ParseUnit(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "celsius" => DisplayUnit.Celsius,
        "fahrenheit" => DisplayUnit.Fahrenheit,
        _ => null,
    };

    /// <summary>
    /// The units a unit setting offers for the reading: two for a temperature, a speed and a depth (Celsius or Fahrenheit as the
    /// stored words; the labels say metric or imperial where that is what they mean), none for the other kinds, whose unit setting
    /// the form hides.
    /// </summary>
    public static IReadOnlyList<UnitChoice> UnitChoices(ReadingKind kind) => kind switch
    {
        ReadingKind.Temperature => [new("celsius", "Celsius (°C)"), new("fahrenheit", "Fahrenheit (°F)")],
        ReadingKind.Speed => [new("celsius", "Metric (km/h)"), new("fahrenheit", "Imperial (mph)")],
        ReadingKind.Depth => [new("celsius", "Metric"), new("fahrenheit", "Imperial")],
        _ => [],
    };

    /// <summary>The stored value as the field shows it: converted to the display unit and rounded to the step's decimals.</summary>
    public static double ToForm(double canonical, ReadingDescriptor? reading, DisplayUnit display, double step) =>
        Math.Round(DisplayUnits.ToDisplay(reading?.Kind ?? ReadingKind.Number, canonical, display, reading), Decimals(step));

    /// <summary>The typed value in the canonical unit the file stores, to four decimals.</summary>
    public static double FromForm(double typed, ReadingDescriptor? reading, DisplayUnit display) =>
        Math.Round(DisplayUnits.FromDisplay(reading?.Kind ?? ReadingKind.Number, typed, display, reading), 4);

    /// <summary>The words after a number shown in the display unit (<c>°F</c>, <c>%</c>, <c>mph</c>); empty for none.</summary>
    public static string UnitSuffix(ReadingDescriptor? reading, DisplayUnit display)
    {
        if (reading == null)
            return "";
        string? text = DisplayUnits.UnitText(reading.Kind, display, reading);
        if (text == null)
            return "";
        return reading.Kind == ReadingKind.Temperature ? text + (display == DisplayUnit.Fahrenheit ? "F" : "C") : text;
    }

    /// <summary>The decimals a step shows: 0 for a whole step (or none), 1 for 0.5, 2 for 0.05.</summary>
    public static int Decimals(double step)
    {
        if (step <= 0 || step % 1 == 0)
            return 0;
        string text = step.ToString("0.########", CultureInfo.InvariantCulture);
        int point = text.IndexOf('.', StringComparison.Ordinal);
        return point < 0 ? 0 : Math.Min(text.Length - point - 1, 4);
    }

    private static string Level(double? canonical, ReadingDescriptor reading, DisplayUnit display, string words)
    {
        if (canonical is not { } value)
            return words;
        double shown = Math.Round(DisplayUnits.ToDisplay(reading.Kind, value, display, reading), 1);
        string number = shown.ToString("0.#", CultureInfo.InvariantCulture);
        string suffix = UnitSuffix(reading, display);
        return suffix.Length == 0 ? number : suffix is "%" or "\"" || suffix.StartsWith('°') ? number + suffix : $"{number} {suffix}";
    }
}
