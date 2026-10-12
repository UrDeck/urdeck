// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;

namespace UrDeck.Engine.Display;

/// <summary>Where the settings window opens: away from the deck, so the user is not working on the screen they are changing.</summary>
public static class SettingsWindowPlacement
{
    /// <summary>
    /// The display under <paramref name="cursor"/>, or the primary display when the cursor is on none. When that is the
    /// deck's display and another display is connected, the primary display instead, or the first other one when the deck
    /// is on the primary. The deck's own display only when it is the only one. Null without a connected display.
    /// </summary>
    public static DisplayInfo? Choose(DisplayList list, Point cursor)
    {
        var connected = list.Rows.Where(r => r.Display != null).ToList();
        if (connected.Count == 0)
            return null;

        var under = connected.FirstOrDefault(r => r.Display!.Bounds.Contains(cursor))
            ?? connected.FirstOrDefault(r => r.IsPrimary)
            ?? connected[0];
        if (!under.IsDeck || connected.Count == 1)
            return under.Display;

        var others = connected.Where(r => !r.IsDeck).ToList();
        return (others.FirstOrDefault(r => r.IsPrimary) ?? others[0]).Display;
    }
}
