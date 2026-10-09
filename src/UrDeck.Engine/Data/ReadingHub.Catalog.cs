// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>A catalog entry whose path has parameter segments written <c>{name}</c>.</summary>
/// <param name="Descriptor">The entry as the provider wrote it.</param>
/// <param name="Segments">The path's segments; a parameter segment is kept with its braces.</param>
/// <param name="Literals">How many segments are not parameters; the pattern with more of them wins.</param>
internal sealed record CatalogPattern(ReadingDescriptor Descriptor, string[] Segments, int Literals);

public sealed partial class ReadingHub
{
    /// <summary>
    /// The one lookup from a subscribed path to its description: an exact catalog entry first, then an entry already made
    /// from a pattern, then the pattern with the most literal segments that the path fits. A path that fits nothing is
    /// null. Runs under the gate, and the description made from a pattern is kept so it is never made twice.
    /// </summary>
    private ReadingDescriptor? Resolve(ProviderRuntime rt, string path)
    {
        if (rt.Catalog == null)
            return null;
        if (rt.Catalog.TryGetValue(path, out var exact))
            return exact;
        if (rt.Instances.TryGetValue(path, out var made))
            return made;
        if (rt.Patterns.Count == 0)
            return null;

        string[] segments = path.Split('/');
        CatalogPattern? best = null;
        bool tie = false;
        foreach (var pattern in rt.Patterns)
        {
            if (!Fits(pattern, segments))
                continue;
            if (best == null || pattern.Literals > best.Literals)
            {
                best = pattern;
                tie = false;
            }
            else if (pattern.Literals == best.Literals)
            {
                tie = true;
            }
        }

        if (best == null)
            return null;
        if (tie && _loggedBad.Add($"{rt.Descriptor.Id}:tie:{path}"))
            UrDeckLog.Warn($"Provider '{rt.Descriptor.Id}': several catalog patterns fit '{path}'; using '{best.Descriptor.Path}', the first listed");

        var instance = Instantiate(best, segments, path);
        rt.Instances[path] = instance;
        _descriptors[$"{rt.Descriptor.Id}:{path}"] = instance;
        return instance;
    }

    private static bool Fits(CatalogPattern pattern, string[] segments)
    {
        if (pattern.Segments.Length != segments.Length)
            return false;
        for (int i = 0; i < segments.Length; i++)
        {
            if (!IsParameter(pattern.Segments[i]) && !string.Equals(pattern.Segments[i], segments[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    /// <summary>The pattern's description for one path: every <c>{name}</c> replaced by the segment that matched it.</summary>
    private static ReadingDescriptor Instantiate(CatalogPattern pattern, string[] segments, string path)
    {
        string label = pattern.Descriptor.Label;
        string name = pattern.Descriptor.Name;
        string? device = pattern.Descriptor.Device;
        for (int i = 0; i < segments.Length; i++)
        {
            if (!IsParameter(pattern.Segments[i]))
                continue;
            string parameter = pattern.Segments[i];
            label = label.Replace(parameter, segments[i], StringComparison.Ordinal);
            name = name.Replace(parameter, segments[i], StringComparison.Ordinal);
            device = device?.Replace(parameter, segments[i], StringComparison.Ordinal);
        }

        return pattern.Descriptor with { Path = path, Label = label, Name = name, Device = device };
    }

    private static bool IsParameter(string segment) => segment.Length > 2 && segment[0] == '{' && segment[^1] == '}';

    /// <summary>
    /// Whether the path is an exact path (no braces), a well-formed pattern (whole segments written <c>{name}</c>, no name
    /// twice), or malformed (an unclosed brace, a brace inside a segment, an empty name).
    /// </summary>
    private static bool TryParsePattern(string path, out string[]? segments, out bool isPattern)
    {
        segments = null;
        isPattern = false;
        if (path.IndexOfAny(['{', '}']) < 0)
            return true;

        string[] parts = path.Split('/');
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in parts)
        {
            if (part.IndexOfAny(['{', '}']) < 0)
                continue;
            if (!IsParameter(part) || part.AsSpan(1, part.Length - 2).IndexOfAny(['{', '}']) >= 0 || !names.Add(part))
                return false;
        }

        segments = parts;
        isPattern = true;
        return true;
    }
}
