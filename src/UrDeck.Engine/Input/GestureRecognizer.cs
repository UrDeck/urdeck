// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Input;

public enum PointerPhase
{
    Down,
    Move,
    Up,
    Cancel,
}

/// <summary>One pointer sample in the pager's pixels. The timestamp is any monotonic clock, in milliseconds.</summary>
public readonly record struct PointerSample(int Id, PointerPhase Phase, double X, double Y, long TimestampMs);

public enum GestureKind
{
    /// <summary>The pointer has moved past the slop and the gesture is a horizontal swipe.</summary>
    SwipeStarted,

    /// <summary><see cref="GestureEvent.Dx"/> is the distance from where the pointer went down.</summary>
    SwipeMoved,

    /// <summary>The pointer was released; <see cref="GestureEvent.Velocity"/> is in pixels per second.</summary>
    SwipeEnded,

    /// <summary>A short press and release that did not become a swipe.</summary>
    Tap,

    /// <summary>The swipe was interrupted (the pointer was cancelled or lost) and should return.</summary>
    Cancelled,

    /// <summary>The pointer went down; <see cref="GestureEvent.X"/> and <see cref="GestureEvent.Y"/> say where.</summary>
    Pressed,

    /// <summary>
    /// The press ended without a tap: the gesture became a swipe (raised just before <see cref="SwipeStarted"/>) or a
    /// vertical drag, the pointer was cancelled, or it was released after too long for a tap.
    /// </summary>
    PressCancelled,
}

/// <param name="Kind">What happened.</param>
/// <param name="Dx">Horizontal distance from the press, in pixels (swipe events).</param>
/// <param name="Velocity">Horizontal velocity at release, in pixels per second (<see cref="GestureKind.SwipeEnded"/>).</param>
/// <param name="X">Position of the pointer (press and tap).</param>
/// <param name="Y">Position of the pointer (press and tap).</param>
public readonly record struct GestureEvent(GestureKind Kind, double Dx = 0, double Velocity = 0, double X = 0, double Y = 0);

/// <summary>
/// Turns pointer samples into presses, swipes and taps. It has no timers and reads no clock: durations come from the
/// samples' timestamps, so a long hold stays pressed until the pointer lifts. The first pointer to go down owns the gesture; other pointers are ignored until it ends. Thresholds are
/// fractions of the column width, so nothing depends on the resolution.
/// </summary>
public sealed class GestureRecognizer
{
    /// <summary>Movement before a gesture is decided, as a fraction of a column.</summary>
    public const double SlopFraction = 0.04;

    /// <summary>A press and release within the slop and this long is a tap.</summary>
    public const long TapMaxMs = 500;

    /// <summary>The window over which release velocity is measured.</summary>
    public const long VelocityWindowMs = 100;

    private enum State
    {
        Idle,
        Pending,
        Swiping,
        Ignored,
    }

    private readonly double _slop;
    private readonly List<(long T, double X)> _recent = new();
    private State _state;
    private int _owner;
    private double _startX;
    private double _startY;
    private long _startT;

    public GestureRecognizer(double columnWidth) => _slop = Math.Max(1, SlopFraction * columnWidth);

    /// <summary>Raised synchronously from <see cref="Process"/>.</summary>
    public event Action<GestureEvent>? Raised;

    /// <summary>True while a swipe is in progress.</summary>
    public bool IsSwiping => _state == State.Swiping;

    public void Process(PointerSample s)
    {
        if (_state == State.Idle)
        {
            if (s.Phase == PointerPhase.Down)
            {
                _state = State.Pending;
                _owner = s.Id;
                _startX = s.X;
                _startY = s.Y;
                _startT = s.TimestampMs;
                _recent.Clear();
                _recent.Add((s.TimestampMs, s.X));
                Raise(new GestureEvent(GestureKind.Pressed, X: s.X, Y: s.Y));
            }
            return;
        }

        if (s.Id != _owner)
            return;

        switch (s.Phase)
        {
            case PointerPhase.Move:
                OnMove(s);
                break;
            case PointerPhase.Up:
                OnUp(s);
                break;
            case PointerPhase.Cancel:
                if (_state == State.Swiping)
                    Raise(new GestureEvent(GestureKind.Cancelled, s.X - _startX));
                else if (_state == State.Pending)
                    Raise(new GestureEvent(GestureKind.PressCancelled));
                Reset();
                break;
        }
    }

    private void OnMove(PointerSample s)
    {
        double dx = s.X - _startX;
        double dy = s.Y - _startY;
        if (_state == State.Pending && Math.Sqrt(dx * dx + dy * dy) >= _slop)
        {
            Raise(new GestureEvent(GestureKind.PressCancelled));
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                _state = State.Swiping;
                Raise(new GestureEvent(GestureKind.SwipeStarted, dx));
            }
            else
            {
                _state = State.Ignored;
            }
        }

        if (_state != State.Swiping)
            return;

        Track(s);
        Raise(new GestureEvent(GestureKind.SwipeMoved, dx));
    }

    private void OnUp(PointerSample s)
    {
        double dx = s.X - _startX;
        switch (_state)
        {
            case State.Swiping:
                Track(s);
                Raise(new GestureEvent(GestureKind.SwipeEnded, dx, Velocity()));
                break;
            case State.Pending when s.TimestampMs - _startT <= TapMaxMs:
                Raise(new GestureEvent(GestureKind.Tap, X: s.X, Y: s.Y));
                break;
            case State.Pending:
                Raise(new GestureEvent(GestureKind.PressCancelled));
                break;
        }
        Reset();
    }

    private void Track(PointerSample s)
    {
        _recent.Add((s.TimestampMs, s.X));
        _recent.RemoveAll(r => s.TimestampMs - r.T > VelocityWindowMs);
    }

    /// <summary>Pixels per second over the last <see cref="VelocityWindowMs"/>; 0 with too little to go on.</summary>
    private double Velocity()
    {
        if (_recent.Count < 2)
            return 0;
        var (t0, x0) = _recent[0];
        var (t1, x1) = _recent[^1];
        return t1 <= t0 ? 0 : (x1 - x0) * 1000.0 / (t1 - t0);
    }

    /// <summary>Forgets a gesture in progress without raising anything (the surface is being rebuilt).</summary>
    public void Abort() => Reset();

    private void Reset()
    {
        _state = State.Idle;
        _recent.Clear();
    }

    private void Raise(GestureEvent e) => Raised?.Invoke(e);
}

/// <summary>The pure decisions about a swipe: whether it commits, and how far a page moves past the ends.</summary>
public static class SwipeMath
{
    /// <summary>A swipe commits at this fraction of the page width...</summary>
    public const double CommitDistance = 0.5;

    /// <summary>...or at this many page widths per second, in the swipe's direction.</summary>
    public const double CommitVelocity = 0.6;

    /// <summary>The rubber band's stiffness: how much of the drag shows up as movement near zero.</summary>
    public const double Stiffness = 0.55;

    public static bool ShouldCommit(double dx, double velocity, double pageWidth)
    {
        if (pageWidth <= 0 || dx == 0)
            return false;
        if (Math.Abs(dx) >= pageWidth * CommitDistance)
            return true;
        bool sameDirection = Math.Sign(velocity) == Math.Sign(dx);
        return sameDirection && Math.Abs(velocity) >= pageWidth * CommitVelocity;
    }

    /// <summary>
    /// The distance the page moves for a drag of <paramref name="dx"/> past the last page: grows with the drag but
    /// never reaches <paramref name="pageWidth"/>.
    /// </summary>
    public static double RubberBand(double dx, double pageWidth)
    {
        if (pageWidth <= 0 || dx == 0)
            return 0;
        double d = Math.Abs(dx);
        double moved = (1 - 1 / (d * Stiffness / pageWidth + 1)) * pageWidth;
        return Math.Sign(dx) * moved;
    }
}
