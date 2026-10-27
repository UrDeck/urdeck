// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>
/// A data provider's sample file (<c>sample-data</c> spec): the readings it offers, each described as its catalog entry is,
/// with a fixed value, keyframes over the seconds of a drawing, or a reason it is unavailable. Read strictly: every problem
/// is a finding naming the reading, and a file with any finding gives no sample.
/// </summary>
public sealed class SampleFile
{
    public const int CurrentVersion = 1;

    // The descriptor fields a reading may set, and the ones the file's defaults may set for every reading.
    private static readonly HashSet<string> DescriptorKeys = new(StringComparer.Ordinal)
    {
        "device", "unit", "displayUnit", "depthUnit", "min", "max", "warning", "critical", "decimals", "attribution",
    };

    private static readonly HashSet<string> ReadingKeys = new(StringComparer.Ordinal)
    {
        "kind", "label", "name", "value", "keys", "period", "unavailable", "action", "icon",
    };

    // A reading gives its value in exactly one of these.
    private static readonly string[] ValueKeys = ["value", "keys", "unavailable"];

    private readonly Dictionary<string, SampleReading> _exact;
    private readonly List<SampleReading> _patterns;

    private SampleFile(List<SampleReading> readings)
    {
        Readings = readings;
        _exact = readings.Where(r => r.Segments == null).ToDictionary(r => r.Descriptor.Path, StringComparer.OrdinalIgnoreCase);
        _patterns = readings.Where(r => r.Segments != null).ToList();
    }

    /// <summary>The readings in the order the file lists them.</summary>
    public IReadOnlyList<SampleReading> Readings { get; }

    /// <summary>The catalog the sample offers: each reading's description, placeholders kept.</summary>
    public IReadOnlyList<ReadingDescriptor> Describe() => Readings.Select(r => r.Descriptor).ToList();

    /// <summary>
    /// Reads a sample file. Returns null, with at least one finding, when anything in it is wrong; a file without findings
    /// is returned with an empty list.
    /// </summary>
    public static SampleFile? Parse(string json, out IReadOnlyList<string> findings)
    {
        var found = new List<string>();
        findings = found;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            found.Add($"the file is not valid JSON: {ex.Message}");
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                found.Add("the file must be a JSON object");
                return null;
            }

            JsonElement? readings = null;
            JsonElement? defaults = null;
            bool version = false;
            foreach (var property in Distinct(root, "the file", found))
            {
                switch (property.Name)
                {
                    case "version":
                        version = true;
                        if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out int v) || v != CurrentVersion)
                            found.Add($"version: must be {CurrentVersion}");
                        break;
                    case "readings":
                        readings = property.Value;
                        break;
                    case "defaults":
                        defaults = property.Value;
                        break;
                    default:
                        found.Add($"'{property.Name}' is not a key of a sample file (version, defaults, readings)");
                        break;
                }
            }

            if (!version)
                found.Add($"version: missing (it must be {CurrentVersion})");
            if (defaults is { } d && d.ValueKind != JsonValueKind.Object)
            {
                found.Add("defaults: must be an object");
                defaults = null;
            }
            else if (defaults is { } given)
            {
                foreach (var property in Distinct(given, "defaults", found))
                {
                    if (!DescriptorKeys.Contains(property.Name))
                        found.Add($"defaults: '{property.Name}' cannot be a default (one of {string.Join(", ", DescriptorKeys.Order(StringComparer.Ordinal))})");
                }
            }

            if (readings is not { ValueKind: JsonValueKind.Object } list)
            {
                found.Add(readings == null ? "readings: missing" : "readings: must be an object of reading paths");
                return null;
            }

            var result = new List<SampleReading>();
            foreach (var property in Distinct(list, "readings", found))
            {
                if (ReadEntry(property.Name, property.Value, defaults, found) is { } reading)
                    result.Add(reading);
            }

            return found.Count == 0 ? new SampleFile(result) : null;
        }
    }

    /// <summary>
    /// The sample that answers <paramref name="path"/>: the exact path first, else the placeholder path with the most literal
    /// segments that fits it, with what each placeholder matched. Null when nothing answers it.
    /// </summary>
    public SampleReading? Match(string path, out IReadOnlyDictionary<string, string> bindings)
    {
        bindings = EmptyBindings;
        if (_exact.TryGetValue(path, out var exact))
            return exact;

        string[] segments = path.Split('/');
        SampleReading? best = null;
        int bestLiterals = -1;
        foreach (var pattern in _patterns)
        {
            string[] parts = pattern.Segments!;
            if (parts.Length != segments.Length)
                continue;
            int literals = 0;
            bool fits = true;
            for (int i = 0; i < parts.Length && fits; i++)
            {
                if (IsPlaceholder(parts[i]))
                    continue;
                fits = string.Equals(parts[i], segments[i], StringComparison.OrdinalIgnoreCase);
                literals++;
            }

            if (fits && literals > bestLiterals)
            {
                best = pattern;
                bestLiterals = literals;
            }
        }

        if (best == null)
            return null;
        var bound = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < segments.Length; i++)
        {
            if (IsPlaceholder(best.Segments![i]))
                bound[best.Segments[i]] = segments[i];
        }

        bindings = bound;
        return best;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyBindings = new Dictionary<string, string>();

    internal static bool IsPlaceholder(string segment) => segment.Length > 2 && segment[0] == '{' && segment[^1] == '}';

    /// <summary>The object's properties, each name once: a second one is a finding and is skipped.</summary>
    private static List<JsonProperty> Distinct(JsonElement element, string where, List<string> found)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<JsonProperty>();
        foreach (var property in element.EnumerateObject())
        {
            if (seen.Add(property.Name))
                list.Add(property);
            else
                found.Add($"{where}: '{property.Name}' is given twice");
        }

        return list;
    }

    private static SampleReading? ReadEntry(string path, JsonElement entry, JsonElement? defaults, List<string> found)
    {
        string at = $"'{path}'";
        int before = found.Count;
        string[]? segments = CheckPath(path, at, found);
        if (entry.ValueKind != JsonValueKind.Object)
        {
            found.Add($"{at}: must be an object");
            return null;
        }

        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (defaults is { } d)
        {
            foreach (var property in d.EnumerateObject())
                fields[property.Name] = property.Value;
        }

        foreach (var property in Distinct(entry, at, found))
        {
            if (!ReadingKeys.Contains(property.Name) && !DescriptorKeys.Contains(property.Name))
                found.Add($"{at}: '{property.Name}' is not a key of a sample reading");
            else
                fields[property.Name] = property.Value;
        }

        ReadingKind? kind = null;
        if (!fields.TryGetValue("kind", out var kindElement))
            found.Add($"{at}: kind is missing");
        else if (EnumValue<ReadingKind>(kindElement) is { } k)
            kind = k;
        else
            found.Add($"{at}: kind must be one of {EnumNames<ReadingKind>()}");

        string? label = Text(fields, "label", at, found, required: true);
        string? name = Text(fields, "name", at, found, required: true);
        if (kind is not { } readingKind || label == null || name == null)
            return null;

        var descriptor = new ReadingDescriptor(path, readingKind, label, name)
        {
            Device = Text(fields, "device", at, found, required: false),
            Unit = Text(fields, "unit", at, found, required: false),
            Min = Number(fields, "min", at, found),
            Max = Number(fields, "max", at, found),
            Warning = Number(fields, "warning", at, found),
            Critical = Number(fields, "critical", at, found),
            Decimals = WholeNumber(fields, "decimals", at, found),
            DisplayUnit = Enum<DisplayUnit>(fields, "displayUnit", at, found),
            DepthUnit = Enum<DepthUnit>(fields, "depthUnit", at, found),
            Attribution = ReadAttribution(fields, at, found),
            Action = NonEmptyText(fields, "action", at, found),
            Icon = NonEmptyText(fields, "icon", at, found),
        };

        int given = ValueKeys.Count(fields.ContainsKey);
        if (given != 1)
        {
            found.Add($"{at}: give exactly one of value, keys and unavailable");
            return null;
        }

        if (fields.ContainsKey("period") && !fields.ContainsKey("keys"))
            found.Add($"{at}: period needs keys");

        if (fields.TryGetValue("unavailable", out var reasonElement))
        {
            if (reasonElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(reasonElement.GetString()))
                found.Add($"{at}: unavailable must be the reason, a text");
            return found.Count > before ? null : new SampleReading(descriptor, segments, [], null, reasonElement.GetString());
        }

        var keys = new List<SampleKey>();
        double? period = null;
        if (fields.TryGetValue("value", out var valueElement))
        {
            if (Value(readingKind, valueElement, $"{at}: value", found) is { } value)
                keys.Add(new SampleKey(0, value));
        }
        else
        {
            var keysElement = fields["keys"];
            if (keysElement.ValueKind != JsonValueKind.Array || keysElement.GetArrayLength() == 0)
            {
                found.Add($"{at}: keys must be a list of at least one {{ \"at\": seconds, \"value\": ... }}");
                return null;
            }

            int index = 0;
            foreach (var key in keysElement.EnumerateArray())
            {
                string where = $"{at}: keys[{index}]";
                if (ReadKey(readingKind, key, where, found) is { } read)
                {
                    if (index == 0 && read.At != 0)
                        found.Add($"{where}.at: the first key must be at 0, so a still picture and the first frame agree");
                    else if (keys.Count > 0 && read.At <= keys[^1].At)
                        found.Add($"{where}.at: the keys must be in ascending order of time");
                    keys.Add(read);
                }

                index++;
            }

            if (fields.TryGetValue("period", out var periodElement))
            {
                if (periodElement.ValueKind != JsonValueKind.Number || periodElement.GetDouble() <= 0)
                    found.Add($"{at}: period must be a number of seconds above zero");
                else if (keys.Count > 0 && periodElement.GetDouble() <= keys[^1].At)
                    found.Add($"{at}: period must be longer than the time of the last key ({keys[^1].At.ToString(CultureInfo.InvariantCulture)} s)");
                else
                    period = periodElement.GetDouble();
            }
        }

        return found.Count > before ? null : new SampleReading(descriptor, segments, keys, period, null);
    }

    /// <summary>The path's segments when it holds placeholders, null for an exact path; a malformed path is a finding.</summary>
    private static string[]? CheckPath(string path, string at, List<string> found)
    {
        if (path.Length == 0 || path.StartsWith('/') || path.EndsWith('/') || path.Contains("//", StringComparison.Ordinal))
        {
            found.Add($"{at}: not a reading path (segments separated by '/')");
            return null;
        }

        if (path.IndexOfAny(['{', '}']) < 0)
            return null;
        string[] segments = path.Split('/');
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string segment in segments)
        {
            if (segment.IndexOfAny(['{', '}']) < 0)
                continue;
            if (!IsPlaceholder(segment) || segment.AsSpan(1, segment.Length - 2).IndexOfAny(['{', '}']) >= 0 || !names.Add(segment))
            {
                found.Add($"{at}: a placeholder must be a whole segment written {{name}}, each name once");
                return null;
            }
        }

        return segments;
    }

    private static SampleKey? ReadKey(ReadingKind kind, JsonElement key, string where, List<string> found)
    {
        if (key.ValueKind != JsonValueKind.Object)
        {
            found.Add($"{where}: must be {{ \"at\": seconds, \"value\": ... }}");
            return null;
        }

        double? time = null;
        SampleValue? value = null;
        bool hasValue = false;
        foreach (var property in Distinct(key, where, found))
        {
            switch (property.Name)
            {
                case "at":
                    if (property.Value.ValueKind == JsonValueKind.Number && property.Value.GetDouble() >= 0)
                        time = property.Value.GetDouble();
                    else
                        found.Add($"{where}.at: must be a number of seconds, 0 or more");
                    break;
                case "value":
                    hasValue = true;
                    value = Value(kind, property.Value, $"{where}.value", found);
                    break;
                default:
                    found.Add($"{where}: '{property.Name}' is not a key of a keyframe (at, value)");
                    break;
            }
        }

        if (time == null && !key.TryGetProperty("at", out _))
            found.Add($"{where}.at: missing");
        if (!hasValue)
            found.Add($"{where}.value: missing");
        return time is { } t && value is { } v ? new SampleKey(t, v) : null;
    }

    /// <summary>A value of the reading's kind: a number, a text, true or false, a time of day <c>HH:mm</c> or a number of days.</summary>
    private static SampleValue? Value(ReadingKind kind, JsonElement element, string where, List<string> found)
    {
        switch (kind)
        {
            case ReadingKind.Text when element.ValueKind == JsonValueKind.String:
                return new SampleValue(0, element.GetString());
            case ReadingKind.OnOff when element.ValueKind is JsonValueKind.True or JsonValueKind.False:
                return new SampleValue(element.GetBoolean() ? 1 : 0, null);
            case ReadingKind.Time when element.ValueKind == JsonValueKind.String
                && TimeSpan.TryParseExact(element.GetString(), ["hh\\:mm", "hh\\:mm\\:ss"], CultureInfo.InvariantCulture, out var time)
                && time < TimeSpan.FromDays(1):
                return new SampleValue(time.TotalSeconds, null);
            case ReadingKind.Date when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int days):
                return new SampleValue(days, null);
            case ReadingKind.Number or ReadingKind.Percent or ReadingKind.Temperature or ReadingKind.Speed or ReadingKind.Depth
                when element.ValueKind == JsonValueKind.Number:
                return new SampleValue(element.GetDouble(), null);
        }

        string expected = kind switch
        {
            ReadingKind.Text => "a text",
            ReadingKind.OnOff => "true or false",
            ReadingKind.Time => "a time of day, \"HH:mm\" or \"HH:mm:ss\"",
            ReadingKind.Date => "a whole number of days from the day drawn (0 is that day)",
            _ => "a number",
        };
        found.Add($"{where}: a {JsonName(kind.ToString())} reading's value must be {expected}");
        return null;
    }

    private static string? Text(Dictionary<string, JsonElement> fields, string key, string at, List<string> found, bool required)
    {
        if (!fields.TryGetValue(key, out var element))
        {
            if (required)
                found.Add($"{at}: {key} is missing");
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
            return element.GetString();
        found.Add($"{at}: {key} must be a text");
        return null;
    }

    /// <summary>An optional text that, when given, must say something (an action path, an icon key).</summary>
    private static string? NonEmptyText(Dictionary<string, JsonElement> fields, string key, string at, List<string> found)
    {
        string? text = Text(fields, key, at, found, required: false);
        if (text != null && string.IsNullOrWhiteSpace(text))
        {
            found.Add($"{at}: {key} must not be empty");
            return null;
        }

        return text;
    }

    private static double? Number(Dictionary<string, JsonElement> fields, string key, string at, List<string> found)
    {
        if (!fields.TryGetValue(key, out var element))
            return null;
        if (element.ValueKind == JsonValueKind.Number)
            return element.GetDouble();
        found.Add($"{at}: {key} must be a number");
        return null;
    }

    private static int? WholeNumber(Dictionary<string, JsonElement> fields, string key, string at, List<string> found)
    {
        if (!fields.TryGetValue(key, out var element))
            return null;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int value) && value >= 0)
            return value;
        found.Add($"{at}: {key} must be a whole number, 0 or more");
        return null;
    }

    private static TEnum? Enum<TEnum>(Dictionary<string, JsonElement> fields, string key, string at, List<string> found)
        where TEnum : struct, System.Enum
    {
        if (!fields.TryGetValue(key, out var element))
            return null;
        if (EnumValue<TEnum>(element) is { } value)
            return value;
        found.Add($"{at}: {key} must be one of {EnumNames<TEnum>()}");
        return null;
    }

    private static ReadingAttribution? ReadAttribution(Dictionary<string, JsonElement> fields, string at, List<string> found)
    {
        if (!fields.TryGetValue("attribution", out var element))
            return null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            found.Add($"{at}: attribution must be {{ \"text\": ..., \"shortText\": ..., \"url\": ... }}");
            return null;
        }

        string? text = null, shortText = null, url = null;
        foreach (var property in Distinct(element, $"{at}: attribution", found))
        {
            string? value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            switch (property.Name)
            {
                case "text":
                    text = value;
                    break;
                case "shortText":
                    shortText = value;
                    break;
                case "url":
                    url = value;
                    break;
                default:
                    found.Add($"{at}: attribution: '{property.Name}' is not a key of an attribution (text, shortText, url)");
                    continue;
            }

            if (value == null)
                found.Add($"{at}: attribution.{property.Name} must be a text");
        }

        if (string.IsNullOrEmpty(text))
        {
            found.Add($"{at}: attribution.text is missing");
            return null;
        }

        return new ReadingAttribution(text, shortText, url);
    }

    private static TEnum? EnumValue<TEnum>(JsonElement element)
        where TEnum : struct, System.Enum
    {
        if (element.ValueKind != JsonValueKind.String)
            return null;
        string? text = element.GetString();
        foreach (var value in System.Enum.GetValues<TEnum>())
        {
            if (string.Equals(JsonName(value.ToString()), text, StringComparison.Ordinal))
                return value;
        }

        return null;
    }

    private static string EnumNames<TEnum>()
        where TEnum : struct, System.Enum =>
        string.Join(", ", System.Enum.GetNames<TEnum>().Select(JsonName));

    /// <summary>An enum member as the file writes it: camelCase (<c>OnOff</c> is <c>onOff</c>).</summary>
    internal static string JsonName(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}

/// <summary>A value as the sample file gives it: a number (also on/off, a time of day in seconds, a number of days) or a text.</summary>
internal readonly record struct SampleValue(double Number, string? Text);

/// <summary>One keyframe: the seconds from the start of a drawing and the value from then on.</summary>
internal readonly record struct SampleKey(double At, SampleValue Value);

/// <summary>One reading of a sample file: its description and how its value changes over a drawing.</summary>
public sealed class SampleReading
{
    private readonly IReadOnlyList<SampleKey> _keys;

    internal SampleReading(ReadingDescriptor descriptor, string[]? segments, IReadOnlyList<SampleKey> keys, double? period, string? unavailable)
    {
        Descriptor = descriptor;
        Segments = segments;
        _keys = keys;
        Period = period;
        Unavailable = unavailable;
    }

    /// <summary>The reading's description; its path may hold placeholders.</summary>
    public ReadingDescriptor Descriptor { get; }

    /// <summary>The seconds after which the keys start again; null when they do not repeat.</summary>
    public double? Period { get; }

    /// <summary>Why the reading is unavailable, for a sample given as unavailable; null otherwise.</summary>
    public string? Unavailable { get; }

    /// <summary>The path's segments when it holds a placeholder; null for an exact path.</summary>
    internal string[]? Segments { get; }

    /// <summary>
    /// The value <paramref name="seconds"/> after the start of a drawing, at the moment <paramref name="moment"/> (whose local
    /// date times and dates are relative to). <paramref name="bindings"/> replaces placeholders in a text. Null for a reading
    /// given as unavailable.
    /// </summary>
    public ReadingValue? ValueAt(double seconds, DateTime moment, IReadOnlyDictionary<string, string>? bindings = null)
    {
        if (Unavailable != null || _keys.Count == 0)
            return null;

        var value = Evaluate(seconds, out SampleValue? next, out double blend);
        var kind = Descriptor.Kind;
        DateTime day = moment.Date;
        switch (kind)
        {
            case ReadingKind.Text:
                string text = value.Text ?? "";
                if (bindings != null)
                {
                    foreach (var (placeholder, bound) in bindings)
                        text = text.Replace(placeholder, bound, StringComparison.Ordinal);
                }

                return ReadingValue.FromText(text);
            case ReadingKind.OnOff:
                return ReadingValue.FromOnOff(value.Number != 0);
            case ReadingKind.Time:
                return ReadingValue.FromTime(Local(day + TimeSpan.FromSeconds(value.Number)));
            case ReadingKind.Date:
                return ReadingValue.FromTime(Local(day.AddDays(value.Number)));
            default:
                double number = next is { } to ? value.Number + ((to.Number - value.Number) * blend) : value.Number;
                return ReadingValue.FromNumber(number);
        }
    }

    /// <summary>The key in effect at <paramref name="seconds"/> and, for a number between two keys, the next one and how far along.</summary>
    private SampleValue Evaluate(double seconds, out SampleValue? next, out double blend)
    {
        next = null;
        blend = 0;
        double t = seconds;
        if (Period is { } period)
        {
            t %= period;
            if (t < 0)
                t += period;
        }
        else if (t < 0 || !double.IsFinite(t))
        {
            t = 0;
        }

        int i = _keys.Count - 1;
        while (i > 0 && _keys[i].At > t)
            i--;
        var key = _keys[i];

        bool numeric = Descriptor.Kind is ReadingKind.Number or ReadingKind.Percent or ReadingKind.Temperature
            or ReadingKind.Speed or ReadingKind.Depth;
        if (!numeric)
            return key.Value;

        if (i + 1 < _keys.Count)
        {
            var after = _keys[i + 1];
            next = after.Value;
            blend = (t - key.At) / (after.At - key.At);
        }
        else if (Period is { } length && _keys.Count > 1)
        {
            // A repeating list closes its loop: from the last key back to the first at the end of the period.
            next = _keys[0].Value;
            blend = (t - key.At) / (length - key.At);
        }

        return key.Value;
    }

    /// <summary>A local date and time as an instant with the offset the machine's time zone has at it.</summary>
    private static DateTimeOffset Local(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }
}
