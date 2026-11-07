// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Rendering;

namespace UrDeck.Engine.Deck;

/// <summary>One page's part of a settle: it moves from <see cref="From"/> to <see cref="To"/> (horizontal offsets, surface units).</summary>
public readonly record struct PageMove(PageSlots Page, double From, double To);

/// <summary>The page indicator a presenter shows.</summary>
/// <param name="Count">How many pages (marks).</param>
/// <param name="Index">The page shown at the start.</param>
/// <param name="Style">The indicator's theme values, in physical pixels.</param>
/// <param name="Floating">True for the fading indicator over the page (<c>fade</c>), false for the reserved band.</param>
/// <param name="Bounds">Its rectangle, in surface units.</param>
public sealed record IndicatorSetup(int Count, int Index, IndicatorStyle Style, bool Floating, System.Drawing.Rectangle Bounds);

/// <summary>
/// How a <see cref="PagerState"/> shows its pages. It says what is alive and where; the view makes and drops the surfaces
/// and moves them. Every call is on the deck's thread. A view drops what it made for a page in <see cref="PageRemoved"/>
/// and <see cref="Cleared"/>, before the page's slots are disposed.
/// </summary>
public interface IPagerView
{
    /// <summary>A page became alive at the horizontal <paramref name="offset"/> (surface units; 0 is at rest).</summary>
    void PageAdded(PageSlots page, double offset);

    /// <summary>A page is no longer alive; its slots are disposed right after.</summary>
    void PageRemoved(PageSlots page);

    /// <summary>The dock's slots, above the pages; they never move.</summary>
    void DockAdded(PageSlots dock);

    /// <summary>The page indicator, above the dock.</summary>
    void IndicatorAdded(IndicatorSetup indicator);

    /// <summary>Every page, the dock and the indicator are gone (a rebuild or a release); their slots are disposed right after.</summary>
    void Cleared();

    /// <summary>Puts a page at an offset at once, stopping a settle it was in.</summary>
    void SetOffset(PageSlots page, double offset);

    /// <summary>
    /// Settles the pages to their targets over <paramref name="duration"/> with the pager's easing
    /// (<see cref="PagerState.Ease"/>), then calls <paramref name="settled"/> on the deck's thread. A call that comes after
    /// the pager was rebuilt is ignored by the pager.
    /// </summary>
    void Slide(IReadOnlyList<PageMove> moves, TimeSpan duration, Action settled);

    /// <summary>The indicator's pill follows the finger: <paramref name="position"/> is the fractional page.</summary>
    void IndicatorFollow(double position);

    /// <summary>The indicator settles on <paramref name="position"/>.</summary>
    void IndicatorSettle(double position);

    /// <summary>
    /// The page whose mark is at <paramref name="local"/> (physical pixels from the indicator's corner), or -1, also while
    /// a floating indicator is faded out. <paramref name="cellPx"/> is one grid cell in physical pixels.
    /// </summary>
    int IndicatorHitTest(System.Drawing.PointF local, float cellPx);
}
