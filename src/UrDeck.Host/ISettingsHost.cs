// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Display;

namespace UrDeck.Host;

/// <summary>
/// Everything the settings window asks of the host, and nothing else: it never sees the deck window. The seam is what
/// lets the window move to a process of its own, with an implementation that reads and writes the configuration file.
/// All members are used on the UI thread.
/// </summary>
internal interface ISettingsHost
{
    /// <summary>The displays now connected as picker rows, with the deck's display marked.</summary>
    DisplayList GetDisplayList();

    /// <summary>Moves the deck to <paramref name="display"/> and remembers the choice (see display-targeting, "Explicit Display Choice").</summary>
    void ChooseDisplay(DisplayInfo display);

    /// <summary>Hands the choice back to the <c>monitorName</c> and <c>monitor</c> selectors.</summary>
    void ChooseAutomatically();

    /// <summary>The displays, the arrangement or the placement changed. May arrive in bursts and when nothing visible changed.</summary>
    event Action? Changed;
}
