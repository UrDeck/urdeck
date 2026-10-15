// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkiaSharp;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Backgrounds;

public enum BackgroundKind
{
    None,
    Shader,
    Image,

    /// <summary>A theme's still gradient; never from the configuration.</summary>
    Gradient,

    /// <summary>A theme's still colour; never from the configuration.</summary>
    Color,
}

/// <summary>
/// The configuration's <c>background</c> value, read leniently: every problem is one warning that starts with
/// <c>Background:</c> and is handled by using a default or by having no background. Nothing here throws.
/// </summary>
public sealed partial class BackgroundSettings
{
    public const double DefaultSpeed = 1;
    public const double MaxSpeed = 10;
    public const double DefaultScale = 0.5;
    public const double MinScale = 0.1;
    public const double MaxScale = 1;
    public const int MaxColors = 4;

    /// <summary>The setting's own keys; any other key of a shader background is a parameter of the shader.</summary>
    private static readonly string[] OwnKeys = ["type", "name", "path", "speed", "scale", "colors"];

    /// <summary>No choice: the configuration has no <c>background</c> (or <c>null</c>), so the theme chooses.</summary>
    public static BackgroundSettings None { get; } = new();

    /// <summary><c>{ "type": "none" }</c>: the user chose no background, so neither the theme's default nor its still background shows.</summary>
    public static BackgroundSettings Disabled { get; } = new() { IsExplicit = true };

    /// <summary>A <c>background</c> that could not be read (a warning was logged): the user chose something that failed.</summary>
    public static BackgroundSettings Invalid { get; } = new() { IsExplicit = true, IsInvalid = true };

    /// <summary>A shader named by a theme's <c>background.default</c>: the shader's own defaults for everything else.</summary>
    internal static BackgroundSettings ForThemeShader(string name) => new() { Kind = BackgroundKind.Shader, Name = name };

    /// <summary>True for one of the setting's own keys (<c>type</c>, <c>name</c>, <c>path</c>, <c>speed</c>, <c>scale</c>, <c>colors</c>).</summary>
    internal static bool IsOwnKey(string key) => OwnKeys.Contains(key, StringComparer.OrdinalIgnoreCase);

    private BackgroundSettings()
    {
    }

    public BackgroundKind Kind { get; private init; }

    /// <summary>True when the configuration said something (a shader, an image, <c>none</c>, or a value that failed); false for no choice.</summary>
    public bool IsExplicit { get; private init; }

    /// <summary>True for a <c>background</c> value that could not be used.</summary>
    public bool IsInvalid { get; private init; }

    /// <summary>True for the user's <c>none</c>.</summary>
    public bool IsDisabled => IsExplicit && !IsInvalid && Kind == BackgroundKind.None;

    /// <summary>The shader's name; null for an image.</summary>
    public string? Name { get; private init; }

    /// <summary>The picture's path as written; null for a shader.</summary>
    public string? Path { get; private init; }

    public double Speed { get; private init; } = DefaultSpeed;
    public double Scale { get; private init; } = DefaultScale;

    /// <summary>The configuration's palette (one to four colour strings), or null to use the theme's.</summary>
    public IReadOnlyList<string>? Colors { get; private init; }

    /// <summary>The keys that are not the setting's own, to be matched with the shader's uniforms.</summary>
    public IReadOnlyDictionary<string, JsonElement> Parameters { get; private init; } = new Dictionary<string, JsonElement>();

    /// <summary>
    /// Reads the <c>background</c> value. Null, an absent value and JSON <c>null</c> are no choice (the theme's) without a
    /// warning; <c>none</c> is <see cref="Disabled"/>; anything unusable is <see cref="Invalid"/> with a warning.
    /// </summary>
    public static BackgroundSettings Parse(JsonElement? element)
    {
        if (element is not { } value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return None;
        if (value.ValueKind != JsonValueKind.Object)
        {
            Warn($"'background' must be an object, not {value.ValueKind}; the theme's background colour or still background is shown.");
            return Invalid;
        }

        var members = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            members[property.Name] = property.Value.Clone();

        string? type = Own(members, "type") is { ValueKind: JsonValueKind.String } t ? t.GetString() : null;
        if (type == null)
        {
            Warn("'type' is missing; it must be 'shader', 'image' or 'none'. No background.");
            return Invalid;
        }

        if (type.Equals("none", StringComparison.OrdinalIgnoreCase))
            return Disabled;
        if (type.Equals("shader", StringComparison.OrdinalIgnoreCase))
            return ParseShader(members);
        if (type.Equals("image", StringComparison.OrdinalIgnoreCase))
            return ParseImage(members);

        Warn($"'type' '{type}' must be 'shader', 'image' or 'none'. No background.");
        return Invalid;
    }

    /// <summary>
    /// Logs one info line when a page has a <c>background</c> of its own: only the top-level setting is supported.
    /// </summary>
    public static void NotePageBackgrounds(UrDeckConfig config)
    {
        if (config.Pages.Any(p => p.Background is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) }))
            UrDeckLog.Info("Background: a page's own 'background' is not supported; set the top-level 'background' instead.");
    }

    /// <summary>A shader name is letters, digits, '-' and '_' only, so it cannot name a file outside the shader folder.</summary>
    public static bool IsValidName(string name) => NamePattern().IsMatch(name);

    /// <summary>Parses <c>#RRGGBB</c> or <c>#AARRGGBB</c>; the palette ignores the alpha later.</summary>
    internal static bool TryParseColor(string? text, out SKColor color)
    {
        color = default;
        return text != null && ColorPattern().IsMatch(text) && SKColor.TryParse(text, out color);
    }

    private static BackgroundSettings ParseShader(Dictionary<string, JsonElement> members)
    {
        string? name = Own(members, "name") is { ValueKind: JsonValueKind.String } n ? n.GetString() : null;
        if (string.IsNullOrEmpty(name))
        {
            Warn("a shader background needs a 'name'. No background.");
            return Invalid;
        }

        if (!IsValidName(name))
        {
            Warn($"shader name '{name}' may only contain letters, digits, '-' and '_'. No background.");
            return Invalid;
        }

        var parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, member) in members)
            if (!OwnKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                parameters[key] = member;

        return new BackgroundSettings
        {
            Kind = BackgroundKind.Shader,
            IsExplicit = true,
            Name = name,
            Speed = Number(members, "speed", DefaultSpeed, 0, MaxSpeed),
            Scale = Number(members, "scale", DefaultScale, MinScale, MaxScale),
            Colors = ParseColors(Own(members, "colors")),
            Parameters = parameters,
        };
    }

    private static BackgroundSettings ParseImage(Dictionary<string, JsonElement> members)
    {
        string? path = Own(members, "path") is { ValueKind: JsonValueKind.String } p ? p.GetString() : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            Warn("an image background needs a 'path'. No background.");
            return Invalid;
        }

        foreach (string key in members.Keys)
            if (!key.Equals("type", StringComparison.OrdinalIgnoreCase) && !key.Equals("path", StringComparison.OrdinalIgnoreCase))
                Warn($"'{key}' has no meaning for an image background and is ignored.");

        return new BackgroundSettings { Kind = BackgroundKind.Image, IsExplicit = true, Path = path };
    }

    private static JsonElement? Own(Dictionary<string, JsonElement> members, string key)
    {
        foreach (var (name, value) in members)
            if (name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return value;
        return null;
    }

    private static double Number(Dictionary<string, JsonElement> members, string key, double fallback, double min, double max)
    {
        if (Own(members, key) is not { } element)
            return fallback;

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double number)
            && double.IsFinite(number) && number >= min && number <= max)
            return number;

        Warn($"'{key}' {element.GetRawText()} must be a number from {Format(min)} to {Format(max)}; using {Format(fallback)}.");
        return fallback;
    }

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static List<string>? ParseColors(JsonElement? element)
    {
        if (element is not { } value || value.ValueKind is JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Array)
        {
            Warn("'colors' must be an array of colours; using the theme's palette.");
            return null;
        }

        var colors = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            string? text = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (!TryParseColor(text, out _))
            {
                Warn($"'colors' entry {item.GetRawText()} is not #RRGGBB or #AARRGGBB; using the theme's palette.");
                return null;
            }
            colors.Add(text!);
        }

        if (colors.Count == 0)
        {
            Warn("'colors' is empty; using the theme's palette.");
            return null;
        }

        if (colors.Count > MaxColors)
        {
            Warn($"'colors' has {colors.Count} entries; only the first {MaxColors} are used.");
            colors.RemoveRange(MaxColors, colors.Count - MaxColors);
        }

        return colors;
    }

    private static void Warn(string message) => UrDeckLog.Warn($"Background: {message}");

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex ColorPattern();
}
