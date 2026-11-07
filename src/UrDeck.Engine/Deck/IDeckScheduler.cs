// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Deck;

/// <summary>How urgently posted work runs: <see cref="Low"/> waits until the thread has nothing else to do (layout rebuilds).</summary>
public enum DeckPriority
{
    Normal,
    Low,
}

/// <summary>
/// The deck's thread, as a deck session sees it: everything a session and its widget slots do runs there, so widgets keep
/// their threading contract (every call on one thread). The host implements it over its dispatcher; tests over a manual clock.
/// </summary>
public interface IDeckScheduler
{
    /// <summary>The local wall time widgets are given (their render time, <c>NeedsRender</c>, <c>NextAnimationAt</c>).</summary>
    DateTime Now { get; }

    /// <summary>A monotonic clock, for durations (the pager's settle).</summary>
    TimeSpan Elapsed { get; }

    /// <summary>Runs <paramref name="action"/> on the deck's thread later. Safe to call from any thread.</summary>
    void Post(Action action, DeckPriority priority = DeckPriority.Normal);

    /// <summary>A stopped timer that runs <paramref name="tick"/> on the deck's thread. Deck thread only.</summary>
    IDeckTimer CreateTimer(Action tick);
}

/// <summary>A timer of an <see cref="IDeckScheduler"/>. Deck thread only.</summary>
public interface IDeckTimer
{
    bool IsRunning { get; }

    /// <summary>(Re)starts the timer: it fires after <paramref name="interval"/>, and again every interval when <paramref name="repeat"/>.</summary>
    void Start(TimeSpan interval, bool repeat = false);

    /// <summary>Stops the timer; it does not fire until started again.</summary>
    void Cancel();
}

/// <summary>Something a <see cref="IFrameSource"/> repaints while it animates.</summary>
public interface IFrameClient
{
    /// <summary>The time between frames it asks for; zero is the source's own rate.</summary>
    TimeSpan FrameInterval { get; }

    /// <summary>Asks for a repaint; called on the deck's thread at the frame rate while the client is registered.</summary>
    void Invalidate();
}

/// <summary>
/// The frames of a deck: every registered client is invalidated at the frame rate (or at its own slower one) until it is
/// removed. It costs nothing while nobody is registered. Deck thread only.
/// </summary>
public interface IFrameSource
{
    void Add(IFrameClient client);

    void Remove(IFrameClient client);
}
