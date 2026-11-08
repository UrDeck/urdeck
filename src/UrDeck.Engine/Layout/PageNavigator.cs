// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Layout;

/// <summary>A page as the navigator knows it: its id (null when the file gave it none, in a read-only document) and its name.</summary>
public readonly record struct PageKey(string? Id, string Name);

/// <summary>
/// Which page is shown. The host holds one; a swipe never writes the configuration. A reload keeps the page by its id,
/// whatever its name or place, and otherwise the same index.
/// </summary>
public sealed class PageNavigator
{
    private IReadOnlyList<PageKey> _pages;

    public PageNavigator(IReadOnlyList<PageKey> pages, int startIndex)
    {
        _pages = pages;
        Index = Clamp(startIndex);
    }

    public int Count => _pages.Count;

    /// <summary>The index of the page shown, always inside the page list (0 for an empty list).</summary>
    public int Index { get; private set; }

    public string Name => Index < _pages.Count ? _pages[Index].Name : "";

    /// <summary>The id of the page shown; null without pages or for a page without an id.</summary>
    public string? Id => Index < _pages.Count ? _pages[Index].Id : null;

    /// <summary>The index of the page with <paramref name="id"/>, or -1.</summary>
    public int IndexOf(string? id)
    {
        if (id == null)
            return -1;
        for (int i = 0; i < _pages.Count; i++)
        {
            if (string.Equals(_pages[i].Id, id, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    /// <summary>Takes a new page list and keeps the user on the same page: by id, else by index (clamped).</summary>
    public void Reload(IReadOnlyList<PageKey> pages)
    {
        string? id = Id;
        int previous = Index;
        _pages = pages;
        int byId = IndexOf(id);
        Index = byId >= 0 ? byId : Clamp(previous);
    }

    /// <summary>A page change has settled on <paramref name="index"/> (clamped).</summary>
    public void Settle(int index) => Index = Clamp(index);

    private int Clamp(int index) => _pages.Count == 0 ? 0 : Math.Clamp(index, 0, _pages.Count - 1);
}
