// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.RegularExpressions;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Themes;

/// <summary>
/// The checks <see cref="ThemeDefinition.Sanitize"/> runs on a theme's values. Each one returns the value when it is
/// valid, and otherwise null (a list may come back shortened) after one warning naming the theme and the key, so that the
/// merge takes the default's.
/// </summary>
internal sealed partial class ThemeValueCheck(string themeName, HashSet<string>? reported = null)
{
    /// <summary>Keys that were already reported before the check ran (a value of the wrong kind), so that nothing reports them twice.</summary>
    private readonly HashSet<string> _reported = reported ?? [];

    private static readonly string[] IconTints = ["accent", "accent2", "text", "ramp", "none"];

    [GeneratedRegex("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex ColorPattern();

    public string? Color(string path, string? value)
    {
        if (value == null || ColorPattern().IsMatch(value))
            return value;
        UrDeckLog.Warn($"Theme '{themeName}': {path} '{value}' is not #RRGGBB or #AARRGGBB; using the default.");
        return null;
    }

    /// <summary>One to four valid colours, or null (with a warning) so the palette is derived from the theme's own colours.</summary>
    public List<string>? Palette(string path, List<string>? value)
    {
        if (value == null)
            return null;
        if (value.Count is >= 1 and <= 4 && value.All(c => c != null && ColorPattern().IsMatch(c)))
            return value;
        Warn($"Theme '{themeName}': {path} must be one to four colours (#RRGGBB or #AARRGGBB); the palette is derived from the theme's colours.");
        return null;
    }

    private static void Warn(string message) => UrDeckLog.Warn(message);

    public double? Number(string path, double? value, double min, double max)
    {
        if (value == null)
            return null;
        if (double.IsFinite(value.Value) && value >= min && value <= max)
            return value;
        UrDeckLog.Warn($"Theme '{themeName}': {path} {value} is outside {min}..{max}; using the default.");
        return null;
    }

    /// <summary>An angle in degrees; the range says which turns a key accepts.</summary>
    public double? Angle(string path, double? value, double min, double max) => Number(path, value, min, max);

    public int? Integer(string path, int? value, int min, int max)
    {
        if (value == null)
            return null;
        if (value >= min && value <= max)
            return value;
        UrDeckLog.Warn($"Theme '{themeName}': {path} {value} is outside {min}..{max}; using the default.");
        return null;
    }

    /// <summary>One of <paramref name="names"/>, in any case, returned in the spelling of the list.</summary>
    public string? Enum(string path, string? value, params string[] names)
    {
        if (value == null)
            return null;
        foreach (string name in names)
            if (name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
                return name;
        UrDeckLog.Warn($"Theme '{themeName}': {path} '{value}' must be one of {string.Join(", ", names.Select(n => $"'{n}'"))}; using the default.");
        return null;
    }

    /// <summary>Between <paramref name="min"/> and <paramref name="max"/> valid colours; anything else drops the list whole.</summary>
    public List<string>? Colors(string path, List<string>? value, int min, int max)
    {
        if (value == null)
            return null;
        if (value.Count >= min && value.Count <= max && value.All(c => c != null && ColorPattern().IsMatch(c)))
            return value;
        UrDeckLog.Warn($"Theme '{themeName}': {path} must be {Range(min, max)} colours (#RRGGBB or #AARRGGBB); using the default.");
        return null;
    }

    /// <summary>Between <paramref name="min"/> and <paramref name="max"/> strings; anything else drops the list whole.</summary>
    public List<string>? Strings(string path, List<string>? value, int min, int max)
    {
        if (value == null)
            return null;
        if (value.Count >= min && value.Count <= max && value.All(s => s != null))
            return value;
        UrDeckLog.Warn($"Theme '{themeName}': {path} must be {Range(min, max)} strings; using the default.");
        return null;
    }

    /// <summary>Exactly <paramref name="count"/> numbers, each between <paramref name="min"/> and <paramref name="max"/>.</summary>
    public List<double>? Numbers(string path, List<double>? value, int count, double min, double max)
    {
        if (value == null)
            return null;
        if (value.Count == count && value.All(n => double.IsFinite(n) && n >= min && n <= max))
            return value;
        UrDeckLog.Warn($"Theme '{themeName}': {path} must be {count} numbers between {min} and {max}; using the default.");
        return null;
    }

    /// <summary>
    /// A list of names from <paramref name="names"/>: an unknown name is dropped with one warning, a repeated one is dropped
    /// silently, and the names come back in the spelling of the list. An empty list stays empty.
    /// </summary>
    public List<string>? NameList(string path, List<string>? value, params string[] names)
    {
        if (value == null)
            return null;
        var kept = new List<string>();
        var unknown = new List<string>();
        foreach (string item in value)
        {
            string? name = item == null ? null : names.FirstOrDefault(n => n.Equals(item.Trim(), StringComparison.OrdinalIgnoreCase));
            if (name == null)
                unknown.Add(item ?? "null");
            else if (!kept.Contains(name))
                kept.Add(name);
        }
        if (unknown.Count > 0)
            UrDeckLog.Warn($"Theme '{themeName}': {path} has {string.Join(", ", unknown.Select(u => $"'{u}'"))}, not one of {string.Join(", ", names.Select(n => $"'{n}'"))}; dropped.");
        return kept;
    }

    /// <summary><c>accent</c>, <c>accent2</c>, <c>text</c>, <c>ramp</c>, <c>none</c> or a colour.</summary>
    public string? IconTint(string path, string? value)
    {
        if (value == null)
            return null;
        string trimmed = value.Trim();
        if (IconTints.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            return trimmed.ToLowerInvariant();
        if (ColorPattern().IsMatch(trimmed))
            return trimmed;
        UrDeckLog.Warn($"Theme '{themeName}': {path} '{value}' must be 'accent', 'accent2', 'text', 'ramp', 'none' or a colour (#RRGGBB or #AARRGGBB); using the default.");
        return null;
    }

    /// <summary>A group that cannot work without one of its members.</summary>
    public void Missing(string path, string member)
    {
        if (!_reported.Contains($"{path}.{member}"))
            UrDeckLog.Warn($"Theme '{themeName}': {path} needs '{member}'; ignoring it.");
    }

    /// <summary>A value that has the wrong kind of JSON, found before it could be read.</summary>
    public void WrongKind(string path, string raw, string expected)
    {
        _reported.Add(path);
        UrDeckLog.Warn($"Theme '{themeName}': {path} {raw} must be {expected}; using the default.");
    }

    /// <summary>A rule between values that the file alone decides.</summary>
    public void Rule(string message) => UrDeckLog.Warn($"Theme '{themeName}': {message}");

    public void Note(string message) => UrDeckLog.Info($"Theme '{themeName}': {message}");

    private static string Range(int min, int max) =>
        max == int.MaxValue ? $"at least {min}" : min == max ? $"exactly {min}" : $"{min} to {max}";
}

/// <summary>Merging of the groups a theme may leave out.</summary>
internal static class ThemeMerge
{
    /// <summary>
    /// The merge of two optional groups: absent when both are, so that a feature that is off never gains a group of
    /// defaults from the merge.
    /// </summary>
    public static T? Group<T>(T? own, T? baseline, Func<T, T?, T> merge)
        where T : class, new() =>
        own == null && baseline == null ? null : merge(own ?? new T(), baseline);
}
