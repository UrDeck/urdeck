// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Layout;

/// <summary>
/// Each card's index for the theme's <c>tint</c>: the live window and a snapshot both take it from here, so they agree.
/// </summary>
public static class CardOrder
{
    /// <summary>
    /// One index per item of a page's <paramref name="layout"/>: its place among the placed and visible items, ordered by
    /// grid row and then by grid column, from 0. A hidden or unplaced item gets -1 and takes no place.
    /// </summary>
    public static int[] Assign(IReadOnlyList<WidgetLayoutItem> layout)
    {
        int[] result = new int[layout.Count];
        Array.Fill(result, -1);
        IEnumerable<int> order = Enumerable.Range(0, layout.Count)
            .Where(i => layout[i].Placed && layout[i].Visible)
            .OrderBy(i => layout[i].CellPosition.Y)
            .ThenBy(i => layout[i].CellPosition.X)
            .ThenBy(i => i);
        int next = 0;
        foreach (int i in order)
            result[i] = next++;
        return result;
    }

    /// <summary>The dock's indices: each slot's position, whether or not the slots before it show a widget.</summary>
    public static int[] Dock(int slotCount) => Enumerable.Range(0, slotCount).ToArray();
}
