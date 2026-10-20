// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UrDeck.Engine.Themes;

/// <summary>
/// Finds the keys of a theme file that the deck does not read. The model is the truth, as it is for the lenient reader: a key
/// is read when a public member of the model has its name in any case. Only the command line tool runs this; the deck stays
/// silent about unknown keys on purpose.
/// </summary>
internal static class ThemeLintKeys
{
    private const int MaxEdits = 2;

    /// <summary>One key the deck does not read: where it is, and the closest key that is read (or null).</summary>
    public sealed record Unknown(string Path, string? Suggestion);

    public static List<Unknown> Find(JsonObject root)
    {
        var found = new List<Unknown>();
        Walk(root, typeof(ThemeDefinition), "", topLevel: true, found);
        return found;
    }

    private static void Walk(JsonObject obj, Type type, string path, bool topLevel, List<Unknown> found)
    {
        var known = Members(type);
        foreach (var (key, value) in obj)
        {
            if (topLevel && key == "$schema")
                continue;

            var property = known.FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
            if (property == null)
            {
                found.Add(new Unknown(Join(path, key), Closest(key, known.Select(p => ToCamel(p.Name))) is { } near ? Join(path, near) : null));
                continue;
            }

            if (value == null)
                continue;

            var memberType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            string memberPath = Join(path, ToCamel(property.Name));
            if (memberType.IsDefined(typeof(JsonConverterAttribute)))
            {
                // The gauge gradient is the text "ramp" or a gradient written out; the second is walked as a gradient.
                if (value is JsonObject stops && memberType == typeof(ThemeGaugeGradientDefinition))
                    Walk(stops, typeof(ThemeGradientDefinition), memberPath, topLevel: false, found);
                continue;
            }

            if (value is JsonObject child && ThemeLenientJson.IsGroup(memberType))
            {
                Walk(child, memberType, memberPath, topLevel: false, found);
            }
            else if (value is JsonArray array && ThemeLenientJson.ItemType(memberType) is { } item && ThemeLenientJson.IsGroup(item))
            {
                for (int i = 0; i < array.Count; i++)
                    if (array[i] is JsonObject element)
                        Walk(element, item, $"{memberPath}[{i}]", topLevel: false, found);
            }
        }
    }

    /// <summary>The members a file can set: public, settable, and not left out of the serializer.</summary>
    private static List<PropertyInfo> Members(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true } && !p.IsDefined(typeof(JsonIgnoreAttribute)))
            .ToList();

    private static string Join(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";

    private static string ToCamel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>The name within <see cref="MaxEdits"/> edits of <paramref name="key"/>, the nearest first (then the first in order).</summary>
    private static string? Closest(string key, IEnumerable<string> names)
    {
        string? best = null;
        int bestDistance = MaxEdits + 1;
        foreach (string name in names.Order(StringComparer.Ordinal))
        {
            int distance = Distance(key.ToLowerInvariant(), name.ToLowerInvariant());
            if (distance < bestDistance)
            {
                best = name;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static int Distance(string a, string b)
    {
        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
