// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;

namespace UrDeck.Engine.Layout;

public class WidgetLayoutItem
{
    public string WidgetTypeId { get; set; } = "";
    /// <summary>Top-left of the widget's card (its surface): the cell inset by half the gap.</summary>
    public System.Drawing.Point Position { get; set; }
    /// <summary>Size of the widget's card (its surface).</summary>
    public Size Size { get; set; }
    /// <summary>Top-left of the block of grid cells the widget spans.</summary>
    public System.Drawing.Point CellPosition { get; set; }
    public Size CellSize { get; set; }
    public Size GridSize { get; set; }
    /// <summary>False when the widget's cells do not lie inside the grid area; it is then not drawn.</summary>
    public bool Placed { get; set; } = true;
}
