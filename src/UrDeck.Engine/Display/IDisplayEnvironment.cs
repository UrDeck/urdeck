// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;

namespace UrDeck.Engine.Display;

/// <summary>What <see cref="PlacementController"/> sees of the displays and of the deck's window. The host implements it.</summary>
public interface IDisplayEnvironment
{
    /// <summary>The connected displays, in enumeration order.</summary>
    IReadOnlyList<DisplayInfo> GetDisplays();

    /// <summary>The window's content area in screen coordinates, physical pixels.</summary>
    Rectangle ContentBounds { get; }

    /// <summary>The window's scaling (1 is 100%).</summary>
    double WindowScaling { get; }

    /// <summary>The size the page is laid out at, in device-independent pixels.</summary>
    SizeF LayoutSize { get; }

    /// <summary>
    /// Moves the window so that its content area covers <paramref name="bounds"/>. With <paramref name="nudge"/> it
    /// is resized by a pixel first, which makes a layout that kept the size of another scaling lay out again.
    /// </summary>
    void Cover(Rectangle bounds, bool nudge);

    /// <summary>Shows the window without giving it focus.</summary>
    void Show();

    void Hide();
}

/// <summary>What <see cref="PlacementController"/> asks of whoever owns the page and the configuration.</summary>
public interface IPlacementOwner
{
    /// <summary>
    /// Placement has settled and the page may not fit it any more. With <paramref name="force"/> it is rebuilt even
    /// when its size and scaling are the ones it was built for (after a resume, and after the deck was hidden).
    /// </summary>
    void RebuildPage(bool force);

    /// <summary>The deck was hidden: release the page, so its widgets stop and drop their reading subscriptions.</summary>
    void ReleasePage();

    /// <summary>The configuration's <c>display</c> was set to <paramref name="display"/>: write the file.</summary>
    void SaveRememberedDisplay(RememberedDisplay display);

    /// <summary>The state changed. <paramref name="displayName"/> is the remembered display's name, if one is remembered.</summary>
    void StateChanged(PlacementState state, string? displayName);
}

/// <summary>Why placement is looked at again.</summary>
public enum PlacementSignal
{
    Startup,

    /// <summary>A display was connected or disconnected, or a resolution, the arrangement or a scaling changed.</summary>
    DisplaysChanged,

    /// <summary>The computer woke from sleep. The page is rebuilt once placement has settled.</summary>
    Resumed,

    /// <summary>The window's own size or scaling changed. Often the echo of the controller's own placement.</summary>
    WindowChanged,

    ConfigReloaded,
}

public enum PlacementState
{
    /// <summary>No signal yet: the window has not been shown.</summary>
    Starting,

    /// <summary>Waiting for display changes to stop arriving.</summary>
    Settling,

    /// <summary>Covering the display and checking the result, with a growing delay between attempts.</summary>
    Placing,

    /// <summary>The placement was checked and is correct.</summary>
    Placed,

    /// <summary>The deck's display is not connected: the window is hidden.</summary>
    Absent,

    /// <summary>No correct placement within the time limit. The next signal starts over.</summary>
    GaveUp,
}
