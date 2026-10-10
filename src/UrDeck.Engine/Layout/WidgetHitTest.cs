// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Layout;

/// <summary>The widget a point falls on: its index in the page's widget list and the point relative to its card.</summary>
public readonly record struct WidgetHit(int Index, double X, double Y);

/// <summary>Finds the widget under a point, from the page's layout alone.</summary>
public static class WidgetHitTest
{
    /// <summary>
    /// The widget whose card (not its cell: the gap around a card belongs to nobody) contains the point, or null. The
    /// point and the result use the layout's units. A card's left and top edges are inside, its right and bottom edges
    /// outside. Widgets that are not placed or not visible are skipped; where cards overlap, the later one wins, as it
    /// is drawn on top.
    /// </summary>
    public static WidgetHit? Find(IReadOnlyList<WidgetLayoutItem> layout, double x, double y)
    {
        for (int i = layout.Count - 1; i >= 0; i--)
        {
            var item = layout[i];
            if (!item.Placed || !item.Visible)
                continue;

            double left = item.Position.X;
            double top = item.Position.Y;
            if (x >= left && x < left + item.Size.Width && y >= top && y < top + item.Size.Height)
                return new WidgetHit(i, x - left, y - top);
        }

        return null;
    }
}
