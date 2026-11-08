// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Config;

/// <summary>
/// Page identities: <c>p-</c> and six base-36 characters, unique in the file. A page without one, or with one another page
/// already has, gets a new one; the first page holding an id keeps it.
/// </summary>
public static class PageIds
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>A new id that is not in <paramref name="taken"/>.</summary>
    public static string New(IReadOnlySet<string> taken)
    {
        Span<char> chars = stackalloc char[8];
        while (true)
        {
            chars[0] = 'p';
            chars[1] = '-';
            for (int i = 2; i < chars.Length; i++)
                chars[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
            string id = new(chars);
            if (!taken.Contains(id))
                return id;
        }
    }

    /// <summary>The ids of the pages of <paramref name="root"/>, in order; null for a page without a usable one.</summary>
    public static IReadOnlyList<string?> Read(JsonObject root)
    {
        if (root["pages"] is not JsonArray pages)
            return [];
        return pages.Select(p => p is JsonObject page && page["id"] is JsonValue value && value.TryGetValue<string>(out string? id) && !string.IsNullOrWhiteSpace(id) ? id : null).ToList();
    }

    /// <summary>
    /// The pages of <paramref name="root"/> that need an id (none, or a duplicate of an earlier page's) and the new id of each,
    /// by index. Nothing is changed.
    /// </summary>
    public static IReadOnlyList<(int Index, string Id)> Missing(JsonObject root)
    {
        var ids = Read(root);
        var taken = new HashSet<string>(ids.OfType<string>(), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(int, string)>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] is { } id && seen.Add(id))
                continue;
            string fresh = New(taken);
            taken.Add(fresh);
            result.Add((i, fresh));
        }

        return result;
    }

    /// <summary>Gives the pages of <paramref name="root"/> that need one a new id, in place; returns how many changed.</summary>
    internal static int AssignInPlace(JsonObject root)
    {
        var missing = Missing(root);
        if (missing.Count == 0)
            return 0;
        var pages = (JsonArray)root["pages"]!;
        foreach (var (index, id) in missing)
        {
            if (pages[index] is JsonObject page)
                page["id"] = id;
        }

        return missing.Count;
    }

    /// <summary>
    /// Gives every page that needs one an id as a host transaction of <paramref name="document"/>: written once, never an
    /// undo step. Does nothing in a read-only document.
    /// </summary>
    public static void Assign(ConfigDocument document)
    {
        var missing = Missing(document.Root);
        if (missing.Count == 0 || document.IsReadOnly)
            return;
        document.Edit("Give the pages ids", e =>
        {
            foreach (var (index, id) in missing)
            {
                if (e.Get($"/pages/{index}") is JsonObject)
                    e.Set($"/pages/{index}/id", id);
            }
        }, EditOrigin.Host);
        UrDeckLog.Info($"Pages: gave {missing.Count} page(s) an id");
    }
}

/// <summary>Which page the deck starts on: the page whose id is <c>startPage</c>, else the first.</summary>
public static class StartPageResolver
{
    /// <summary>
    /// The index of the start page in <paramref name="config"/>'s pages. With <paramref name="log"/> it says once when the
    /// start page is not set or not found.
    /// </summary>
    public static int Resolve(UrDeckConfig config, bool log = false)
    {
        string? start = config.StartPage;
        if (!string.IsNullOrWhiteSpace(start))
        {
            int index = config.Pages.FindIndex(p => string.Equals(p.Id, start, StringComparison.Ordinal));
            if (index >= 0)
                return index;
            if (log)
                UrDeckLog.Info($"Start page '{start}' not found; starting on the first page");
            return 0;
        }

        if (log && config.Pages.Count > 1)
            UrDeckLog.Info("No start page set; starting on the first page");
        return 0;
    }

    /// <summary>Whether <paramref name="config"/> names a start page that exists.</summary>
    public static bool IsSet(UrDeckConfig config) =>
        !string.IsNullOrWhiteSpace(config.StartPage) && config.Pages.Exists(p => string.Equals(p.Id, config.StartPage, StringComparison.Ordinal));
}
