// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using UrDeck.Sdk;

namespace UrDeck.Engine.Layout;

public class GridLayoutManager
{
    public double ScreenWidth { get; private set; }
    public double ScreenHeight { get; private set; }
    public double ColumnWidth => ScreenWidth / 4;
    public double RowHeight => ColumnWidth;

    /// <summary>The theme's gap between cards, as a fraction of the column width. Each card is inset by half of it.</summary>
    public double GapFraction { get; }

    private readonly double? _gridHeight;

    /// <summary>The height widgets may occupy: the screen without a reserved bottom band.</summary>
    public double GridHeight => _gridHeight ?? ScreenHeight;

    public GridLayoutManager(double screenWidth, double screenHeight, double gapFraction = 0, double? gridHeight = null)
    {
        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;
        GapFraction = gapFraction;
        _gridHeight = gridHeight;
    }

    public System.Drawing.Point ConvertToPixels(int col, int row, int width, int height)
    {
        return new System.Drawing.Point(
            (int)(col * ColumnWidth),
            (int)(row * RowHeight));
    }

    public Size ConvertSizeToPixels(int width, int height)
    {
        return new Size(
            (int)(width * ColumnWidth),
            (int)(height * RowHeight));
    }

    public static bool ValidatePosition(int col, int width)
    {
        return col >= 0 && col + width <= 4 && width >= 1 && width <= 4;
    }

    public static int ValidateHeight(int height)
    {
        return height >= 1 ? height : 1;
    }

    public List<WidgetLayoutItem> RenderWidgetLayout(List<WidgetConfig> widgets, Size screenDims)
    {
        ScreenWidth = screenDims.Width;
        ScreenHeight = screenDims.Height;
        var result = new List<WidgetLayoutItem>();

        foreach (var widget in widgets)
        {
            // Clamp width first so the column bound (4 - width) is valid.
            int clampedWidth = Clamp(1, widget.Width, 4);
            int clampedCol = Clamp(0, widget.Col, 4 - clampedWidth);
            int clampedRow = Math.Max(0, widget.Row);
            int clampedHeight = Math.Max(1, widget.Height);

            if (clampedCol != widget.Col || clampedWidth != widget.Width
                || clampedRow != widget.Row || clampedHeight != widget.Height)
            {
                UrDeck.Engine.Diagnostics.UrDeckLog.Warn($"Widget {widget.WidgetTypeId} position clamped from ({widget.Col},{widget.Row})[{widget.Width}x{widget.Height}] to ({clampedCol},{clampedRow})[{clampedWidth}x{clampedHeight}]");
            }

            var cellPos = new System.Drawing.Point(
                (int)(clampedCol * ColumnWidth),
                (int)(clampedRow * RowHeight));
            var cellSize = new Size(
                (int)(clampedWidth * ColumnWidth),
                (int)(clampedHeight * RowHeight));

            // The card is the cell inset by half the gap on every side. Edges are rounded, not sizes, so the space
            // between neighbours stays the same for every widget size.
            double half = GapFraction * ColumnWidth / 2;
            int left = RoundPx(clampedCol * ColumnWidth + half);
            int top = RoundPx(clampedRow * RowHeight + half);
            int right = RoundPx((clampedCol + clampedWidth) * ColumnWidth - half);
            int bottom = RoundPx((clampedRow + clampedHeight) * RowHeight - half);

            bool fits = cellPos.Y + cellSize.Height <= GridHeight + 0.5;
            if (!fits)
            {
                UrDeck.Engine.Diagnostics.UrDeckLog.Error($"Widget {widget.WidgetTypeId} at ({clampedCol},{clampedRow})[{clampedWidth}x{clampedHeight}] does not fit in the grid area ({(int)(GridHeight / RowHeight)} whole rows); it is not placed");
            }

            var item = new WidgetLayoutItem
            {
                WidgetTypeId = widget.WidgetTypeId,
                Position = GapFraction > 0 ? new System.Drawing.Point(left, top) : cellPos,
                Size = GapFraction > 0 ? new Size(Math.Max(1, right - left), Math.Max(1, bottom - top)) : cellSize,
                CellPosition = cellPos,
                CellSize = cellSize,
                GridSize = new Size(clampedWidth, clampedHeight),
                Placed = fits,
            };
            result.Add(item);
        }

        return result;
    }

    private static int RoundPx(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static int Clamp(int min, int value, int max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }
}
