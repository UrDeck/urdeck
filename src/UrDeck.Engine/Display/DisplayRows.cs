// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;

namespace UrDeck.Engine.Display;

public enum DisplayOrientation
{
    Landscape,
    Portrait,
}

/// <summary>
/// One line of the monitor picker. A connected display has a <paramref name="Number"/> (its place in the list, the number
/// Identify shows); the row of a remembered display that is not connected has none and is only <paramref name="IsWaiting"/>.
/// </summary>
/// <param name="Number">The place in the list, 1-based, or null for the waiting row.</param>
/// <param name="Name">What the user reads: the display's own name, or "Display N" when it reports none.</param>
/// <param name="Width">Physical pixels; 0 for the waiting row.</param>
/// <param name="Height">Physical pixels; 0 for the waiting row.</param>
/// <param name="Orientation">Portrait when the display is taller than it is wide.</param>
/// <param name="IsPrimary">Windows' primary display.</param>
/// <param name="RelativePosition">Where the display is compared with its nearest neighbour ("Right of Display 1"), or null.</param>
/// <param name="IsDeck">The deck is shown on this display.</param>
/// <param name="IsWaiting">The remembered display, which is not connected: it can not be chosen.</param>
/// <param name="Display">The connected display, or null for the waiting row.</param>
public sealed record DisplayRow(
    int? Number,
    string Name,
    int Width,
    int Height,
    DisplayOrientation Orientation,
    bool IsPrimary,
    string? RelativePosition,
    bool IsDeck,
    bool IsWaiting,
    DisplayInfo? Display);

/// <summary>What the monitor picker shows, decided away from the window so that it can be tested.</summary>
/// <param name="Rows">The connected displays by position, then the waiting row if there is one.</param>
/// <param name="CanChooseAutomatically">A display was chosen explicitly, so there is something to hand back to the selectors.</param>
public sealed record DisplayList(IReadOnlyList<DisplayRow> Rows, bool CanChooseAutomatically);

/// <summary>
/// Turns the connected displays and the configuration into the rows of the monitor picker. The deck's display is found
/// with <see cref="DisplaySelector.Select"/>, so the picker and the placement can not disagree about it.
/// </summary>
public static class DisplayRows
{
    public static DisplayList Build(UrDeckConfig config, IReadOnlyList<DisplayInfo> displays)
    {
        var ordered = displays.OrderBy(d => d.Bounds.X).ThenBy(d => d.Bounds.Y).ToList();
        var deck = DisplaySelector.Select(config, displays).Display;

        var rows = new List<DisplayRow>(ordered.Count + 1);
        for (int i = 0; i < ordered.Count; i++)
        {
            var d = ordered[i];
            rows.Add(new DisplayRow(
                Number: i + 1,
                Name: ShownName(d, i + 1),
                Width: d.Bounds.Width,
                Height: d.Bounds.Height,
                Orientation: d.Bounds.Height > d.Bounds.Width ? DisplayOrientation.Portrait : DisplayOrientation.Landscape,
                IsPrimary: d.IsPrimary,
                RelativePosition: Relative(ordered, i),
                IsDeck: deck != null && ReferenceEquals(deck, d),
                IsWaiting: false,
                Display: d));
        }

        var remembered = config.Display;
        bool identifies = remembered != null && (!string.IsNullOrEmpty(remembered.Path) || !string.IsNullOrEmpty(remembered.Model));
        if (deck == null && identifies)
        {
            string name = !string.IsNullOrWhiteSpace(remembered!.Name) ? remembered.Name
                : !string.IsNullOrWhiteSpace(remembered.Model) ? remembered.Model
                : "Display";
            rows.Add(new DisplayRow(null, name, 0, 0, DisplayOrientation.Landscape, false, null, false, true, null));
        }

        return new DisplayList(rows, identifies && remembered!.Selector == null);
    }

    /// <summary>The display's own name; a device name such as <c>DISPLAY2</c> is never shown, "Display 2" is.</summary>
    private static string ShownName(DisplayInfo display, int number)
    {
        string device = display.DeviceName.TrimStart('\\', '.');
        return string.IsNullOrWhiteSpace(display.Name) || string.Equals(display.Name, device, StringComparison.OrdinalIgnoreCase)
            ? $"Display {number}"
            : display.Name;
    }

    /// <summary>
    /// The display compared with the nearest other one (the distance between the centres; the lower number on a tie), along
    /// the axis where they differ most. Two displays describe each other: "Right of Display 1" and "Left of Display 2".
    /// </summary>
    private static string? Relative(List<DisplayInfo> ordered, int index)
    {
        if (ordered.Count < 2)
            return null;

        var (cx, cy) = Center(ordered[index]);
        int nearest = -1;
        double best = double.MaxValue;
        for (int j = 0; j < ordered.Count; j++)
        {
            if (j == index)
                continue;
            var (ox, oy) = Center(ordered[j]);
            double distance = ((cx - ox) * (cx - ox)) + ((cy - oy) * (cy - oy));
            if (distance < best)
            {
                best = distance;
                nearest = j;
            }
        }

        var (nx, ny) = Center(ordered[nearest]);
        double dx = cx - nx;
        double dy = cy - ny;
        string side = Math.Abs(dx) >= Math.Abs(dy)
            ? (dx >= 0 ? "Right of" : "Left of")
            : (dy >= 0 ? "Below" : "Above");
        return $"{side} Display {nearest + 1}";
    }

    private static (double X, double Y) Center(DisplayInfo d) =>
        (d.Bounds.X + (d.Bounds.Width / 2.0), d.Bounds.Y + (d.Bounds.Height / 2.0));
}
