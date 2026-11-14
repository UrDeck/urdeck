// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

// List readings (data-providers spec, "List Bounds"): a list is kept to 200 rows and its text fields to 500 characters, and a
// publication that cannot be a list at all (a field holding a list, two rows with one key) is refused. Equal lists are not
// changes: Set compares readings by value, and a list compares by its precomputed hash first.
public sealed partial class ReadingHub
{
    private const string Ellipsis = "…";

    /// <summary>
    /// Under the gate: the value to store for a publication. A list is cut to the bounds (logged once per reading until it is within
    /// them again); an invalid list gives null, so the reading keeps its previous value (logged once until it is valid again).
    /// Every other value is returned as it is.
    /// </summary>
    private ReadingValue? Bounded(string providerId, string id, ReadingValue value)
    {
        if (value.Type != ReadingValueType.List)
            return value;

        var list = value.List;
        string? invalid = Invalid(list);
        if (invalid != null)
        {
            if (_loggedBad.Add($"{id}:invalid"))
                UrDeckLog.Warn($"Provider '{providerId}' published an invalid list for '{id}' ({invalid}); keeping the previous value");
            return null;
        }

        _loggedBad.Remove($"{id}:invalid");

        var (bounded, rowsCut, textsCut) = Cut(list);
        if (rowsCut == 0 && textsCut == 0)
        {
            _loggedBad.Remove($"{id}:cut");
            return value;
        }

        if (_loggedBad.Add($"{id}:cut"))
            UrDeckLog.Warn($"Provider '{providerId}' published a list for '{id}' beyond the bounds ({ReadingList.MaxRows} rows, {ReadingList.MaxTextLength} characters); cut {rowsCut} rows and {textsCut} texts");
        return ReadingValue.FromList(bounded);
    }

    private static string? Invalid(ReadingList list)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in list.Rows)
        {
            if (row.HasNestedList)
                return $"row '{row.Key}' has a field holding a list";
            if (!keys.Add(row.Key))
                return $"two rows have the key '{row.Key}'";
        }

        return null;
    }

    private static (ReadingList List, int RowsCut, int TextsCut) Cut(ReadingList list)
    {
        int rowsCut = Math.Max(0, list.Count - ReadingList.MaxRows);
        int textsCut = 0;
        var rows = new List<ReadingRow>(Math.Min(list.Count, ReadingList.MaxRows));
        foreach (var row in list.Rows.Take(ReadingList.MaxRows))
        {
            int before = textsCut;
            var cut = row.Map((_, v) =>
            {
                if (v.Type != ReadingValueType.Text || v.Text.Length <= ReadingList.MaxTextLength)
                    return v;
                textsCut++;
                return ReadingValue.FromText(v.Text[..(ReadingList.MaxTextLength - Ellipsis.Length)] + Ellipsis);
            });
            rows.Add(textsCut == before ? row : cut);
        }

        return rowsCut == 0 && textsCut == 0 ? (list, 0, 0) : (new ReadingList(rows), rowsCut, textsCut);
    }
}
