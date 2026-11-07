// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Deck;

/// <summary>Where a deck is drawn: its size in surface units (DIPs) and how many physical pixels one unit is.</summary>
public readonly record struct DeckSurface(System.Drawing.Size Size, double Scale)
{
    /// <summary>Whether a page can be laid out on it (both sides at least 16 units).</summary>
    public bool IsUsable => Size.Width >= 16 && Size.Height >= 16;
}

/// <summary>
/// How a <see cref="DeckSession"/> is shown. The session asks for the surface when it builds a page and tells the presenter
/// what to draw: the pages, dock and indicator through <see cref="Pages"/>, the background through
/// <see cref="ShowBackground"/>, each widget through its <see cref="WidgetSlot"/>. The presenter tells the session when the
/// deck is shown or hidden (<see cref="DeckSession.SetShown"/>), when its surface changed and a page is wanted
/// (<see cref="DeckSession.RequestPage"/>), and feeds input samples (<see cref="DeckSession.Process"/>) if it has input.
/// Every call is on the deck's thread.
/// </summary>
public interface IDeckPresenter
{
    /// <summary>The surface now; read when a page is built.</summary>
    DeckSurface Surface { get; }

    /// <summary>How the pages, the dock and the indicator are shown.</summary>
    IPagerView Pages { get; }

    /// <summary>
    /// The background to show under the pages, on a usable surface: the plan of the configuration and the theme, and the theme
    /// (its colour and its pattern). Called on every rebuild; the presenter keeps what it shows when the plan's key is the same.
    /// </summary>
    void ShowBackground(BackgroundPlan plan, LoadedTheme theme, DeckSurface surface);
}
