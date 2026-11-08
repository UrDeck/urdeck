// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UrDeck.Engine.Settings;

/// <summary>
/// Reads a setting's value out of the configuration the way a widget reads it, and writes an edited value back as the widget expects
/// it (editor-forms D12). Reading is tolerant: a number written as a string reads as the number, an unknown option reads as its
/// raw text. Absent is <c>null</c> everywhere; the document never holds JSON null.
/// </summary>
public static class SettingValues
{
    /// <summary>
    /// The value as the descriptor's kind types it: <see cref="bool"/> (toggle), <see cref="double"/> (number, range),
    /// <see cref="string"/> (choice, text, reading, action, unit, launch target, icon, unknown kinds) or <see cref="JsonArray"/>
    /// (positions). Null when the key is absent or the value cannot be read as the kind. A text that is present and empty is the
    /// empty string, which differs from absent.
    /// </summary>
    public static object? Read(JsonNode? node, SettingDescriptor descriptor)
    {
        if (node == null)
            return null;

        switch (descriptor.Kind)
        {
            case SettingKinds.Toggle:
                return ReadBool(node);
            case SettingKinds.Number:
            case SettingKinds.Range:
                return ReadNumber(node);
            case SettingKinds.Positions:
                return node as JsonArray;
            default:
                return ReadText(node);
        }
    }

    /// <summary>
    /// The node to store for an edited value; null removes the key. A text is removed when empty, except that a setting that allows
    /// none keeps an empty text (the "no label" state) and removes the key for null. A number is whole for a whole-number setting.
    /// </summary>
    public static JsonNode? Write(object? value, SettingDescriptor descriptor)
    {
        if (value == null)
            return null;

        switch (descriptor.Kind)
        {
            case SettingKinds.Toggle:
                return value is bool b ? JsonValue.Create(b) : null;
            case SettingKinds.Number:
            case SettingKinds.Range:
                return value is double d && double.IsFinite(d) ? NumberNode(d, descriptor) : null;
            case SettingKinds.Positions:
                return value as JsonArray;
            case SettingKinds.Choice:
                return value is string option && option.Length > 0 ? ChoiceNode(option, descriptor) : null;
            default:
                return value is string text && (text.Length > 0 || (descriptor.Kind == SettingKinds.Text && descriptor.AllowsNone))
                    ? JsonValue.Create(text)
                    : null;
        }
    }

    /// <summary>Why <paramref name="value"/> is not valid for a number or range setting ("Between 0 and 3."), else null.</summary>
    public static string? Invalid(double value, SettingDescriptor descriptor)
    {
        var spec = descriptor.Number;
        if (spec == null)
            return null;
        if (spec.WholeNumber && value != Math.Round(value))
            return "A whole number.";
        if (!(spec.Min is { } min && value < min) && !(spec.Max is { } max && value > max))
            return null;
        if (spec.Min is { } lo && spec.Max is { } hi)
            return $"Between {Format(lo)} and {Format(hi)}.";
        return spec.Min is { } only ? $"At least {Format(only)}." : $"At most {Format(spec.Max!.Value)}.";
    }

    private static bool? ReadBool(JsonNode node)
    {
        if (node is not JsonValue v)
            return null;
        if (v.TryGetValue<bool>(out bool b))
            return b;
        if (v.TryGetValue<string>(out string? s) && bool.TryParse(s, out bool parsed))
            return parsed;
        return null;
    }

    private static double? ReadNumber(JsonNode node)
    {
        if (node is not JsonValue v)
            return null;
        if (v.TryGetValue<double>(out double d))
            return d;
        if (v.TryGetValue<JsonElement>(out var element))
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double e))
                return e;
            if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double fromElement))
                return fromElement;
        }

        if (v.TryGetValue<string>(out string? s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            return parsed;
        return null;
    }

    private static string? ReadText(JsonNode node)
    {
        if (node is not JsonValue v)
            return node.ToJsonString();
        if (v.TryGetValue<string>(out string? s))
            return s;
        if (v.TryGetValue<bool>(out bool b))
            return b ? "true" : "false";
        if (v.TryGetValue<double>(out double d))
            return d.ToString("R", CultureInfo.InvariantCulture);
        if (v.TryGetValue<JsonElement>(out var element))
            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        return node.ToJsonString();
    }

    private static JsonValue NumberNode(double value, SettingDescriptor descriptor)
    {
        if (descriptor.Number is { WholeNumber: true })
            return JsonValue.Create((long)Math.Round(value));
        return JsonValue.Create(Math.Round(value, 4));
    }

    /// <summary>A choice whose property is not text (a number or a <see cref="JsonElement"/>) and whose option reads as a number is stored as one.</summary>
    private static JsonValue ChoiceNode(string option, SettingDescriptor descriptor)
    {
        var type = Nullable.GetUnderlyingType(descriptor.Property.PropertyType) ?? descriptor.Property.PropertyType;
        if (type != typeof(string) && double.TryParse(option, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            return number == Math.Round(number) ? JsonValue.Create((long)number) : JsonValue.Create(number);
        return JsonValue.Create(option);
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
