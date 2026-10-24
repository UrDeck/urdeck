// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UrDeck.Engine.Themes;

/// <summary>
/// Reads a theme file so that a value of the wrong JSON kind (a text where a number goes) costs only that value:
/// it is removed with one warning before the file is deserialized, where it would fail the whole theme.
/// </summary>
internal static class ThemeLenientJson
{
    /// <summary>Reads <paramref name="json"/>; <paramref name="reported"/> receives the keys already reported as the wrong kind.</summary>
    public static ThemeDefinition Read(string json, IThemeFindings sink, JsonSerializerOptions options, out HashSet<string> reported)
    {
        reported = [];
        var node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        if (node is not JsonObject root)
            throw new JsonException("the file is empty");

        Clean(root, typeof(ThemeDefinition), "", new ThemeValueCheck(sink, reported));
        return root.Deserialize<ThemeDefinition>(options) ?? throw new JsonException("the file is empty");
    }

    private static void Clean(JsonObject obj, Type type, string path, ThemeValueCheck check)
    {
        foreach (var (key, value) in obj.ToList())
        {
            if (value == null || type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) is not { } property)
                continue;

            var memberType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            string memberPath = path.Length == 0 ? property.Name.ToLowerFirst() : $"{path}.{property.Name.ToLowerFirst()}";
            if (Expected(memberType, value) is { } expected)
            {
                check.WrongKind(memberPath, Shorten(value.ToJsonString()), expected);
                obj.Remove(key);
                continue;
            }

            if (memberType.IsDefined(typeof(JsonConverterAttribute)))
            {
                if (value is JsonObject stops && memberType == typeof(ThemeGaugeGradientDefinition))
                    Clean(stops, typeof(ThemeGradientDefinition), memberPath, check);
                continue;
            }
            if (value is JsonObject child && IsGroup(memberType))
                Clean(child, memberType, memberPath, check);
            else if (value is JsonArray array && ItemType(memberType) is { } item && IsGroup(item))
                foreach (var element in array.OfType<JsonObject>())
                    Clean(element, item, memberPath, check);
        }
    }

    /// <summary>What the member needs when the JSON is not that, or null when it is.</summary>
    private static string? Expected(Type type, JsonNode value)
    {
        if (type.IsDefined(typeof(JsonConverterAttribute)))
            return null;
        var kind = value.GetValueKind();
        if (type == typeof(double))
            return kind == JsonValueKind.Number ? null : "a number";
        if (type == typeof(int))
            return kind == JsonValueKind.Number && value.AsValue().TryGetValue<int>(out _) ? null : "a whole number";
        if (type == typeof(bool))
            return kind is JsonValueKind.True or JsonValueKind.False ? null : "true or false";
        if (type == typeof(string))
            return kind == JsonValueKind.String ? null : "text";
        if (ItemType(type) is { } item)
        {
            if (value is not JsonArray array)
                return "a list";
            return array.All(e => e != null && Expected(item, e) == null) ? null : "a list of " + Plural(item);
        }
        return value is JsonObject ? null : "an object";
    }

    private static string Plural(Type item) =>
        item == typeof(double) ? "numbers" : item == typeof(string) ? "text" : "objects";

    internal static Type? ItemType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : null;

    internal static bool IsGroup(Type type) =>
        type.IsClass && type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(type);

    private static string Shorten(string raw) => raw.Length <= 40 ? raw : raw[..37] + "...";

    private static string ToLowerFirst(this string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
