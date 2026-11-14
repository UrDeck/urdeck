// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;

namespace UrDeck.Engine.Editor;

/// <summary>One reading the editor can offer: a listed reading, a fixed reading of a catalog, or a catalog pattern (a template).</summary>
public sealed class ReadingEntry
{
    internal ReadingEntry(string providerId, ReadingDescriptor descriptor, bool hasAction)
    {
        ProviderId = providerId;
        Descriptor = descriptor;
        Id = $"{providerId}:{descriptor.Path}";
        IsTemplate = descriptor.Path.Contains('{', StringComparison.Ordinal);
        HasRange = descriptor.Kind == ReadingKind.Percent || (descriptor.Min != null && descriptor.Max != null);
        HasAction = hasAction;
        FoldedLabel = ReadingIndex.Fold(descriptor.Label);
        FoldedName = ReadingIndex.Fold(descriptor.Name);
        FoldedId = ReadingIndex.Fold(Id);
        FoldedArea = ReadingIndex.Fold(descriptor.Area ?? "");
        FoldedCategory = ReadingIndex.Fold(descriptor.Category ?? "");
        LabelWords = FoldedLabel.Split([' ', '-', '_', '.', '/', ':'], StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>The provider's id.</summary>
    public string ProviderId { get; }

    /// <summary>The full id, <c>provider:path</c>; for a template the pattern (<c>system:cpu/core/{n}/load</c>).</summary>
    public string Id { get; }

    /// <summary>What the provider says about the reading.</summary>
    public ReadingDescriptor Descriptor { get; }

    /// <summary>Whether the entry is a pattern that stands for many readings.</summary>
    public bool IsTemplate { get; }

    /// <summary>Whether the reading has a range (a minimum and a maximum, or a percentage).</summary>
    public bool HasRange { get; }

    /// <summary>Whether something can be done to the reading (it has an action).</summary>
    public bool HasAction { get; }

    /// <summary>Whether the reading is a list of rows.</summary>
    public bool IsList => Descriptor.Kind == ReadingKind.List;

    internal string[] LabelWords { get; }

    internal string FoldedLabel { get; }

    internal string FoldedName { get; }

    internal string FoldedId { get; }

    internal string FoldedArea { get; }

    internal string FoldedCategory { get; }
}

/// <summary>A span of text to show in bold.</summary>
/// <param name="Start">The first character.</param>
/// <param name="Length">How many characters.</param>
public readonly record struct MatchRange(int Start, int Length);

/// <summary>An entry that matched and where.</summary>
/// <param name="Entry">The reading.</param>
/// <param name="LabelMatches">The spans of the label that matched.</param>
/// <param name="IdMatches">The spans of the id that matched.</param>
public sealed record ReadingHit(ReadingEntry Entry, IReadOnlyList<MatchRange> LabelMatches, IReadOnlyList<MatchRange> IdMatches);

/// <summary>What to look for in the index.</summary>
/// <param name="Text">The words typed; empty for everything.</param>
/// <param name="Provider">One provider's readings only; null for all.</param>
/// <param name="Area">One area only; null for all.</param>
/// <param name="Category">One category only; null for all.</param>
/// <param name="Restrict">What the setting accepts.</param>
public sealed record ReadingQuery(string Text = "", string? Provider = null, string? Area = null, string? Category = null, ReadingFilter Restrict = ReadingFilter.None);

/// <summary>The hits of a query.</summary>
/// <param name="Hits">The matching readings, best match first.</param>
/// <param name="Total">How many readings the provider and the setting's restriction leave, before the words and filters.</param>
public sealed record ReadingSearchResult(IReadOnlyList<ReadingHit> Hits, int Total);

/// <summary>A heading of the displayed list and its hits.</summary>
/// <param name="Header">The heading: <c>Lights · Office</c>, a category, an area, a device, or <c>Other</c>.</param>
/// <param name="Hits">The hits under it, in the order they were found.</param>
public sealed record ReadingGroup(string Header, IReadOnlyList<ReadingHit> Hits);

/// <summary>
/// Every reading the editor can offer, searchable in memory (reading-picker spec, "Search And Filters"): built from the providers'
/// listings and catalogs, then searched as the user types. Entries are folded once (case and accents), so a search over
/// thousands of them takes a few milliseconds.
/// </summary>
public sealed class ReadingIndex
{
    private const string Other = "Other";

    private readonly List<ReadingEntry> _entries;

    /// <summary>Builds the index of the entries.</summary>
    public ReadingIndex(IEnumerable<ReadingEntry> entries) => _entries = [.. entries];

    /// <summary>Every entry.</summary>
    public IReadOnlyList<ReadingEntry> Entries => _entries;

    /// <summary>
    /// The entries of one provider: its listing when it has one, else the catalog's fixed readings and its patterns.
    /// <paramref name="hasAction"/> says whether something can be done to a reading; by default it has a natural action.
    /// </summary>
    public static IEnumerable<ReadingEntry> EntriesOf(string providerId, IEnumerable<ReadingDescriptor> descriptors, Func<ReadingDescriptor, bool>? hasAction = null) =>
        descriptors.Select(d => new ReadingEntry(providerId, d, hasAction?.Invoke(d) ?? d.Action != null));

    /// <summary>The ids of the providers that have entries, in the order they first appear.</summary>
    public IReadOnlyList<string> Providers() => [.. _entries.Select(e => e.ProviderId).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The areas of a provider's entries (all providers' when null), sorted.</summary>
    public IReadOnlyList<string> Areas(string? provider = null) => Distinct(provider, e => e.Descriptor.Area);

    /// <summary>The categories of a provider's entries (all providers' when null), sorted.</summary>
    public IReadOnlyList<string> Categories(string? provider = null) => Distinct(provider, e => e.Descriptor.Category);

    /// <summary>Finds the entry with this id (case-insensitive); null when there is none.</summary>
    public ReadingEntry? Find(string id) => _entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The entries matching the query. Every word must be found in the label, name, id, area or category.</summary>
    public ReadingSearchResult Search(ReadingQuery query)
    {
        var scope = _entries.Where(e => InScope(e, query)).ToList();
        string[] words = Fold(query.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hits = new List<(ReadingEntry Entry, int Rank)>(scope.Count);
        foreach (var entry in scope)
        {
            if (query.Area != null && !string.Equals(entry.Descriptor.Area, query.Area, StringComparison.OrdinalIgnoreCase))
                continue;
            if (query.Category != null && !string.Equals(entry.Descriptor.Category, query.Category, StringComparison.OrdinalIgnoreCase))
                continue;
            if (words.Length == 0)
            {
                hits.Add((entry, 0));
                continue;
            }

            if (words.All(w => Matches(entry, w)))
                hits.Add((entry, RankOf(entry, words)));
        }

        var ordered = hits
            .OrderBy(h => h.Rank)
            .ThenBy(h => h.Entry.Descriptor.Label, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(h => h.Entry.Id, StringComparer.Ordinal)
            .Select(h => new ReadingHit(h.Entry, Ranges(h.Entry.FoldedLabel, words), Ranges(h.Entry.FoldedId, words)))
            .ToList();
        return new ReadingSearchResult(ordered, scope.Count);
    }

    /// <summary>
    /// The hits under headings: category then area (<c>Lights · Office</c>), else the device, else <c>Other</c>. Headings are sorted
    /// with <c>Other</c> last; hits keep the order they have.
    /// </summary>
    public static IReadOnlyList<ReadingGroup> Group(IEnumerable<ReadingHit> hits) =>
    [
        .. hits.GroupBy(h => HeaderOf(h.Entry.Descriptor))
            .OrderBy(g => g.Key == Other ? 1 : 0)
            .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ReadingGroup(g.Key, [.. g])),
    ];

    /// <summary>Lower case without accents, one character for each character of the text so that spans carry over.</summary>
    internal static string Fold(string text)
    {
        if (text.Length == 0)
            return text;
        var folded = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c < 128)
            {
                folded.Append(char.ToLowerInvariant(c));
                continue;
            }

            char first = c.ToString().Normalize(NormalizationForm.FormD)[0];
            folded.Append(CharUnicodeInfo.GetUnicodeCategory(first) == UnicodeCategory.NonSpacingMark ? char.ToLowerInvariant(c) : char.ToLowerInvariant(first));
        }

        return folded.ToString();
    }

    private static string HeaderOf(ReadingDescriptor d)
    {
        bool category = !string.IsNullOrWhiteSpace(d.Category);
        bool area = !string.IsNullOrWhiteSpace(d.Area);
        if (category && area)
            return $"{d.Category} · {d.Area}";
        if (category)
            return d.Category!;
        if (area)
            return d.Area!;
        return string.IsNullOrWhiteSpace(d.Device) ? Other : d.Device;
    }

    private IReadOnlyList<string> Distinct(string? provider, Func<ReadingEntry, string?> pick) =>
    [
        .. _entries
            .Where(e => provider == null || string.Equals(e.ProviderId, provider, StringComparison.OrdinalIgnoreCase))
            .Select(pick)
            .OfType<string>()
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase),
    ];

    private static bool InScope(ReadingEntry entry, ReadingQuery query)
    {
        if (query.Provider != null && !string.Equals(entry.ProviderId, query.Provider, StringComparison.OrdinalIgnoreCase))
            return false;
        var restrict = query.Restrict;
        if (restrict.HasFlag(ReadingFilter.HasRange) && !entry.HasRange)
            return false;
        if (restrict.HasFlag(ReadingFilter.HasAction) && !entry.HasAction)
            return false;
        if (restrict.HasFlag(ReadingFilter.ListsOnly) && !entry.IsList)
            return false;
        return !(restrict.HasFlag(ReadingFilter.NoLists) && entry.IsList);
    }

    private static bool Matches(ReadingEntry e, string word) =>
        e.FoldedLabel.Contains(word, StringComparison.Ordinal)
        || e.FoldedName.Contains(word, StringComparison.Ordinal)
        || e.FoldedId.Contains(word, StringComparison.Ordinal)
        || e.FoldedArea.Contains(word, StringComparison.Ordinal)
        || e.FoldedCategory.Contains(word, StringComparison.Ordinal);

    /// <summary>0: the label starts with the words; 1: each word starts a word of the label; 2: the words are in the id; 3: elsewhere.</summary>
    private static int RankOf(ReadingEntry e, string[] words)
    {
        string whole = string.Join(' ', words);
        if (e.FoldedLabel.StartsWith(whole, StringComparison.Ordinal))
            return 0;
        if (words.All(w => e.LabelWords.Any(l => l.StartsWith(w, StringComparison.Ordinal))))
            return 1;
        return words.All(w => e.FoldedId.Contains(w, StringComparison.Ordinal)) ? 2 : 3;
    }

    private static List<MatchRange> Ranges(string folded, string[] words)
    {
        var spans = new List<MatchRange>();
        foreach (string word in words)
        {
            for (int at = folded.IndexOf(word, StringComparison.Ordinal); at >= 0; at = folded.IndexOf(word, at + word.Length, StringComparison.Ordinal))
                spans.Add(new MatchRange(at, word.Length));
        }

        if (spans.Count < 2)
            return spans;
        spans.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<MatchRange> { spans[0] };
        foreach (var span in spans.Skip(1))
        {
            var last = merged[^1];
            if (span.Start <= last.Start + last.Length)
                merged[^1] = new MatchRange(last.Start, Math.Max(last.Start + last.Length, span.Start + span.Length) - last.Start);
            else
                merged.Add(span);
        }

        return merged;
    }
}
