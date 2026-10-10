// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using System.Text.Json;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Sdk;

namespace UrDeck.Engine.Layout;

/// <summary>One dock slot: the configuration its widget is created from, and whether the slot shows a widget.</summary>
/// <param name="Config">A copy of the entry at the size 1x1; the loaded configuration is not touched.</param>
/// <param name="Shown">False when the slot stays empty: no usable widget type, or the entry is not visible.</param>
public readonly record struct DockEntry(WidgetConfig Config, bool Shown);

/// <summary>
/// Lays out the dock: up to four square slots, shown as one group centred in the dock's band. A slot is a grid cell at
/// a smaller size, so the result is the layout items the grid produces and the same hit test works on them.
/// </summary>
public static class DockLayout
{
    public const int MaxSlots = 4;

    /// <summary>A slot's side: the band's height, never more than a quarter of its width.</summary>
    public static int SlotSide(Rectangle dock) => Math.Max(0, Math.Min(dock.Height, dock.Width / MaxSlots));

    /// <summary>
    /// One item per entry, at most <see cref="MaxSlots"/>, the first at the left. Each card is its slot inset by half of
    /// <paramref name="gapFraction"/> of the slot's side, so neighbouring cards are one gap apart; edges are rounded as
    /// the grid rounds them, and every card has the same size.
    /// </summary>
    /// <param name="dock">The dock's band (see <see cref="ChromeLayoutResult.Dock"/>).</param>
    /// <param name="entryCount">How many entries the dock has.</param>
    /// <param name="gapFraction">The theme's gap between cards, as a fraction of a cell.</param>
    public static List<WidgetLayoutItem> Compute(Rectangle dock, int entryCount, double gapFraction)
    {
        var result = new List<WidgetLayoutItem>();
        int side = SlotSide(dock);
        int count = side > 0 ? Math.Clamp(entryCount, 0, MaxSlots) : 0;

        // The side and the group's origin are whole numbers, so rounding the edges gives the same card in every slot.
        double groupLeft = dock.X + RoundPx((dock.Width - count * side) / 2.0);
        double slotTop = dock.Y + RoundPx((dock.Height - side) / 2.0);
        double half = gapFraction * side / 2;
        for (int i = 0; i < count; i++)
        {
            double slotLeft = groupLeft + i * side;
            int left = RoundPx(slotLeft + half);
            int top = RoundPx(slotTop + half);
            int right = RoundPx(slotLeft + side - half);
            int bottom = RoundPx(slotTop + side - half);
            result.Add(new WidgetLayoutItem
            {
                Position = new System.Drawing.Point(left, top),
                Size = new Size(Math.Max(1, right - left), Math.Max(1, bottom - top)),
                CellPosition = new System.Drawing.Point((int)slotLeft, (int)slotTop),
                CellSize = new Size(side, side),
                GridSize = new Size(1, 1),
            });
        }

        return result;
    }

    /// <summary>
    /// As <see cref="Compute(Rectangle, int, double)"/>, with each item carrying its entry's type and whether its slot
    /// shows a widget: an empty slot is not placed, so it is not drawn and takes no input.
    /// </summary>
    public static List<WidgetLayoutItem> Compute(Rectangle dock, IReadOnlyList<DockEntry> entries, double gapFraction)
    {
        var result = Compute(dock, entries.Count, gapFraction);
        for (int i = 0; i < result.Count; i++)
        {
            result[i].WidgetTypeId = entries[i].Config.WidgetTypeId;
            result[i].Visible = entries[i].Config.IsVisible;
            result[i].Placed = entries[i].Shown;
        }

        return result;
    }

    /// <summary>
    /// Decides what each slot shows. Entries beyond the fourth are dropped with one warning. An entry without a type,
    /// with a type that is not registered or with one that does not support the size 1x1 leaves its slot empty, with a
    /// warning; so does an entry that is not visible, without one.
    /// </summary>
    /// <param name="dock">The configuration's <c>dock</c> list.</param>
    /// <param name="describe">Looks up a widget type by id; null when it is not registered.</param>
    public static List<DockEntry> Select(IReadOnlyList<WidgetConfig> dock, Func<string, WidgetDescriptor?> describe)
    {
        if (dock.Count > MaxSlots)
            UrDeckLog.Warn($"The dock has {dock.Count} entries and {MaxSlots} slots; the entries after the first {MaxSlots} are ignored.");

        var result = new List<DockEntry>();
        for (int i = 0; i < dock.Count && i < MaxSlots; i++)
            result.Add(new DockEntry(AsSlot(dock[i], i), IsShown(dock[i], i, describe)));
        return result;
    }

    private static bool IsShown(WidgetConfig entry, int index, Func<string, WidgetDescriptor?> describe)
    {
        if (string.IsNullOrWhiteSpace(entry.WidgetTypeId))
        {
            UrDeckLog.Warn($"Dock entry {index + 1} has no typeId; leaving its slot empty.");
            return false;
        }

        var descriptor = describe(entry.WidgetTypeId);
        if (descriptor == null)
        {
            UrDeckLog.Warn($"Widget type '{entry.WidgetTypeId}' of dock entry {index + 1} is not registered; leaving its slot empty.");
            return false;
        }

        if (!descriptor.SupportedSizes.Contains(new Size(1, 1)))
        {
            UrDeckLog.Warn($"Widget type '{entry.WidgetTypeId}' does not support the size 1x1 and cannot be shown in the dock; leaving its slot empty.");
            return false;
        }

        return entry.IsVisible;
    }

    /// <summary>The entry as a 1x1 widget in the slot at <paramref name="index"/>, whatever position and size it carries.</summary>
    private static WidgetConfig AsSlot(WidgetConfig entry, int index) => new()
    {
        WidgetTypeId = entry.WidgetTypeId,
        Col = index,
        Row = 0,
        Width = 1,
        Height = 1,
        Parameters = entry.Parameters is { } parameters ? new Dictionary<string, string>(parameters) : [],
        IsVisible = entry.IsVisible,
        ExtensionData = entry.ExtensionData == null ? null : new Dictionary<string, JsonElement>(entry.ExtensionData),
    };

    private static int RoundPx(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
