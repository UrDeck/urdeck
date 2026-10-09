// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Layout;

/// <summary>
/// Which page is shown. The host holds one; a swipe never writes the configuration. Pages have names but no id, so a
/// reload keeps the page by name (the first one if names repeat) and otherwise the same index.
/// </summary>
public sealed class PageNavigator
{
    private IReadOnlyList<string> _names;

    public PageNavigator(IReadOnlyList<string> pageNames, int activePage)
    {
        _names = pageNames;
        Index = Clamp(activePage);
    }

    public int Count => _names.Count;

    /// <summary>The index of the page shown, always inside the page list (0 for an empty list).</summary>
    public int Index { get; private set; }

    public string Name => Index < _names.Count ? _names[Index] : "";

    /// <summary>Takes a new page list and keeps the user on the same page: by name, else by index.</summary>
    public void Reload(IReadOnlyList<string> pageNames)
    {
        string name = Name;
        int previous = Index;
        _names = pageNames;
        int byName = -1;
        for (int i = 0; i < pageNames.Count && byName < 0; i++)
        {
            if (pageNames[i] == name)
                byName = i;
        }
        Index = byName >= 0 ? byName : Clamp(previous);
    }

    /// <summary>A page change has settled on <paramref name="index"/> (clamped).</summary>
    public void Settle(int index) => Index = Clamp(index);

    private int Clamp(int index) => _names.Count == 0 ? 0 : Math.Clamp(index, 0, _names.Count - 1);
}
