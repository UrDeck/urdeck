// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Input;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;

namespace UrDeck.Engine.Deck;

/// <summary>What the pager needs to build its pages for the current surface, theme and configuration.</summary>
/// <param name="Screen">The surface size in surface units (DIPs).</param>
/// <param name="Scale">Surface units to physical pixels.</param>
/// <param name="Chrome">Where the indicator and the dock go (surface units) and the resolved mode.</param>
/// <param name="Style">The indicator's theme values, in physical pixels.</param>
/// <param name="BuildPage">Builds the widgets of the page at an index.</param>
/// <param name="BuildDock">Builds the dock's widgets; null when there is no dock.</param>
public sealed record PagerSetup(
    System.Drawing.Size Screen,
    double Scale,
    ChromeLayoutResult Chrome,
    IndicatorStyle Style,
    Func<int, PageSlots> BuildPage,
    Func<PageSlots?> BuildDock);

/// <summary>
/// Which pages are alive and where they are. At rest one page is alive; while a swipe is in progress the neighbour in its
/// direction is built too, and the page that left is disposed when the slide settles. The dock is not a page: it is built
/// once per setup and stays where it is while pages slide. All decisions (swipe or tap, commit, rubber band) are the
/// engine's <see cref="GestureRecognizer"/> and <see cref="SwipeMath"/>; a presenter feeds input samples with
/// <see cref="Process"/> and shows what this says through its <see cref="IPagerView"/>. Deck thread only.
/// </summary>
public sealed class PagerState
{
    /// <summary>How long a page takes to settle after a swipe or a page change.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(250);

    // A page's surfaces are native memory the garbage collector cannot see, so a released page can sit around for a
    // long time. One collection shortly after the pages have settled (and nothing is moving) frees them.
    private static readonly TimeSpan ReclaimDelay = TimeSpan.FromSeconds(1.5);

    private readonly PageNavigator _navigator;
    private readonly IPagerView _view;
    private readonly IDeckScheduler _scheduler;
    private readonly IDeckTimer _reclaim;
    private readonly Dictionary<PageSlots, double> _offsets = [];

    private PagerSetup? _setup;
    private GestureRecognizer? _gestures;
    private PageSlots? _current;
    private PageSlots? _neighbor;
    private int _neighborIndex = -1;
    private PageSlots? _dock;
    private bool _hasIndicator;
    private WidgetSlot? _pressed;
    private bool _animating;
    private int _epoch;
    private TimeSpan _slideStart;
    private IReadOnlyList<PageMove> _slide = [];

    public PagerState(PageNavigator navigator, IPagerView view, IDeckScheduler scheduler)
    {
        _navigator = navigator;
        _view = view;
        _scheduler = scheduler;
        _reclaim = scheduler.CreateTimer(() => GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false));
    }

    /// <summary>A page change settled on the page at the index (a swipe, a dot, <see cref="ShowPage"/>).</summary>
    public event Action<int>? PageChanged;

    private double Width => _setup?.Screen.Width ?? 0;

    /// <summary>The setup of the current layout; null while nothing is built.</summary>
    public PagerSetup? Setup => _setup;

    /// <summary>The page shown (at rest, or the one a swipe started from).</summary>
    public PageSlots? Current => _current;

    /// <summary>The page a swipe is bringing in; null at rest.</summary>
    public PageSlots? Neighbor => _neighbor;

    public PageSlots? Dock => _dock;

    /// <summary>True while a settle runs (input is ignored until it ends).</summary>
    public bool IsSettling => _animating;

    /// <summary>Whether a pointer that goes down now would be followed: something is built and nothing settles.</summary>
    public bool AcceptsInput => _gestures != null && !_animating;

    /// <summary>The widgets of every live page and of the dock.</summary>
    public IEnumerable<PageSlots> LivePages
    {
        get
        {
            if (_current != null)
                yield return _current;
            if (_neighbor != null)
                yield return _neighbor;
            if (_dock != null)
                yield return _dock;
        }
    }

    /// <summary>
    /// Builds the current page and the dock for a new setup. A swipe in progress ends at once: both pages are released and the
    /// navigator keeps the page the user was on.
    /// </summary>
    public void Rebuild(PagerSetup setup)
    {
        Release();
        _setup = setup;
        _gestures = new GestureRecognizer(setup.Screen.Width / 4.0);
        _gestures.Raised += OnGesture;

        _current = setup.BuildPage(_navigator.Index);
        _offsets[_current] = 0;
        _view.PageAdded(_current, 0);

        // The dock goes above the pages, and the indicator above it. Page changes never touch it.
        _dock = setup.BuildDock();
        if (_dock != null)
        {
            _view.DockAdded(_dock);
            UrDeckLog.Info($"Dock: {_dock.Count} slot(s) shown");
        }

        var chrome = setup.Chrome;
        if (chrome.Mode != IndicatorMode.Off && _navigator.Count > 0)
        {
            _hasIndicator = true;
            _view.IndicatorAdded(new IndicatorSetup(_navigator.Count, _navigator.Index, setup.Style, chrome.Mode == IndicatorMode.Fade, chrome.Indicator));
        }
    }

    /// <summary>Releases every page, the dock and the indicator (a rebuild, a hidden deck or the end of the session).</summary>
    public void Release()
    {
        _epoch++;
        _animating = false;
        _slide = [];
        // The recognizer says nothing when it is aborted, and the slots are about to go: just forget the press.
        _pressed = null;
        _gestures?.Abort();
        _gestures = null;
        bool any = _current != null || _neighbor != null || _dock != null || _hasIndicator;
        _hasIndicator = false;
        if (any)
            _view.Cleared();
        _neighbor?.Dispose();
        _neighbor = null;
        _neighborIndex = -1;
        _current?.Dispose();
        _current = null;
        _dock?.Dispose();
        _dock = null;
        _offsets.Clear();
        _setup = null;
    }

    /// <summary>Feeds one pointer sample (surface units, from the surface's corner). Ignored while nothing is built or a settle runs.</summary>
    public void Process(PointerSample sample)
    {
        if (_gestures == null || _animating)
            return;
        _gestures.Process(sample);
    }

    /// <summary>
    /// Slides to the page at <paramref name="index"/> with the normal settle, as a tap on its dot does. While nothing is
    /// built the navigator moves at once and the next build shows the page. False when the index is outside the pages or
    /// the page is already shown.
    /// </summary>
    public bool ShowPage(int index)
    {
        if (index < 0 || index >= _navigator.Count || index == _navigator.Index)
            return false;
        if (_current == null)
        {
            _navigator.Settle(index);
            PageChanged?.Invoke(index);
            return true;
        }

        if (_animating)
            return false;
        // A gesture in progress is the user's; a page chosen from outside ends it.
        _gestures?.Abort();
        ReleasePress();
        GoTo(index);
        return true;
    }

    /// <summary>
    /// A page's horizontal offset at <paramref name="elapsed"/> (the scheduler's clock), for a presenter that moves its
    /// pages itself: the eased settle while one runs, else where the page was put.
    /// </summary>
    public double OffsetAt(PageSlots page, TimeSpan elapsed)
    {
        if (_animating)
        {
            foreach (var move in _slide)
            {
                if (move.Page == page)
                {
                    double t = Math.Clamp((elapsed - _slideStart).TotalMilliseconds / SettleTime.TotalMilliseconds, 0, 1);
                    return move.From + (move.To - move.From) * Ease(t);
                }
            }
        }

        return _offsets.TryGetValue(page, out double offset) ? offset : 0;
    }

    /// <summary>
    /// The settle's easing, the cubic Bézier (0.2, 0) (0, 1) the window's compositor animates with: progress at the
    /// fraction <paramref name="t"/> of the time.
    /// </summary>
    public static double Ease(double t)
    {
        if (t <= 0)
            return 0;
        if (t >= 1)
            return 1;

        // Solve x(u) = t for the curve parameter u by bisection (x is monotonic), then return y(u).
        const double x1 = 0.2, y1 = 0, x2 = 0, y2 = 1;
        double lo = 0, hi = 1, u = t;
        for (int i = 0; i < 40; i++)
        {
            u = (lo + hi) / 2;
            double x = Bezier(u, x1, x2);
            if (Math.Abs(x - t) < 1e-7)
                break;
            if (x < t)
                lo = u;
            else
                hi = u;
        }

        return Bezier(u, y1, y2);
    }

    private static double Bezier(double u, double p1, double p2)
    {
        double v = 1 - u;
        return 3 * v * v * u * p1 + 3 * v * u * u * p2 + u * u * u;
    }

    private void OnGesture(GestureEvent g)
    {
        switch (g.Kind)
        {
            case GestureKind.SwipeStarted:
            case GestureKind.SwipeMoved:
                Drag(g.Dx);
                break;
            case GestureKind.SwipeEnded:
                EndDrag(g.Dx, g.Velocity, allowCommit: true);
                break;
            case GestureKind.Cancelled:
                EndDrag(g.Dx, 0, allowCommit: false);
                break;
            case GestureKind.Pressed:
                Press(g.X, g.Y);
                break;
            case GestureKind.PressCancelled:
                ReleasePress();
                break;
            case GestureKind.Tap:
                ReleasePress();
                Tap(g.X, g.Y);
                break;
        }
    }

    // Press and tap go to the widget under the pointer: the indicator is asked first, then the dock, then the page at
    // rest. The press is shown from pointer-down, before anybody knows whether it becomes a tap; a swipe, a vertical
    // drag or a long hold takes it back.

    private void Press(double x, double y)
    {
        ReleasePress();
        if (_setup == null || _current == null || IndicatorPageAt(x, y) >= 0)
            return;
        if (FindCard(x, y, out var local) is not { } card || !card.Slot.CanTap(ToCardPixels(local)))
            return;
        card.Slot.PressDown();
        _pressed = card.Slot;
    }

    /// <summary>
    /// The card under the point. A point in the dock's band is looked up in the dock and never in the page: nothing of
    /// the page is there, so a point beside the dock's cards belongs to nobody.
    /// </summary>
    private DeckCard? FindCard(double x, double y, out System.Drawing.PointF local)
    {
        local = default;
        var dock = _setup!.Chrome.Dock;
        if (dock.Height > 0 && y >= dock.Top && y < dock.Bottom)
            return _dock?.Find(x, y, out local);
        return _current?.Find(x, y, out local);
    }

    private void ReleasePress()
    {
        _pressed?.ReleasePress();
        _pressed = null;
    }

    private SKPoint ToCardPixels(System.Drawing.PointF local) =>
        new((float)(local.X * _setup!.Scale), (float)(local.Y * _setup.Scale));

    /// <summary>The page whose mark of a visible indicator the point is on, or -1.</summary>
    private int IndicatorPageAt(double x, double y)
    {
        if (_setup == null || !_hasIndicator)
            return -1;

        var rect = _setup.Chrome.Indicator;
        var local = new System.Drawing.PointF((float)((x - rect.X) * _setup.Scale), (float)((y - rect.Y) * _setup.Scale));
        return _view.IndicatorHitTest(local, (float)(_setup.Screen.Width * _setup.Scale / 4));
    }

    // The page follows the finger. A swipe to the left (dx < 0) brings the next page in from the right.

    private void Drag(double dx)
    {
        if (_current == null || dx == 0)
            return;
        int side = dx < 0 ? 1 : -1;
        EnsureNeighbor(_navigator.Index + side);

        if (_neighbor == null)
        {
            // Past the first or last page: resistance, and nothing else is built.
            SetOffset(_current, SwipeMath.RubberBand(dx, Width));
        }
        else
        {
            SetOffset(_current, dx);
            SetOffset(_neighbor, dx + side * Width);
        }

        if (_hasIndicator)
            _view.IndicatorFollow(Math.Clamp(_navigator.Index - dx / Width, 0, _navigator.Count - 1));
    }

    private void EndDrag(double dx, double velocity, bool allowCommit)
    {
        if (_current == null)
            return;
        int side = dx < 0 ? 1 : -1;
        bool commit = allowCommit && _neighbor != null && SwipeMath.ShouldCommit(dx, velocity, Width);
        Slide(commit, side);
    }

    private void Tap(double x, double y)
    {
        if (_setup == null)
            return;

        // A visible indicator takes a tap on one of its marks before any widget does.
        int page = IndicatorPageAt(x, y);
        if (page >= 0)
        {
            if (page != _navigator.Index)
                GoTo(page);
            return;
        }

        if (FindCard(x, y, out var local) is { } card)
            card.Slot.Tap(ToCardPixels(local));
    }

    /// <summary>Slides to <paramref name="page"/> (a tap on a dot, or a page chosen from outside).</summary>
    private void GoTo(int page)
    {
        if (_current == null || _animating)
            return;
        int side = page > _navigator.Index ? 1 : -1;
        EnsureNeighbor(page);
        if (_neighbor == null)
            return;
        SetOffset(_current, 0);
        SetOffset(_neighbor, side * Width);
        Slide(true, side);
    }

    private void EnsureNeighbor(int index)
    {
        if (_setup == null || index == _neighborIndex)
            return;

        if (_neighbor != null)
        {
            _view.PageRemoved(_neighbor);
            _offsets.Remove(_neighbor);
            _neighbor.Dispose();
        }

        _neighbor = null;
        _neighborIndex = -1;
        if (index < 0 || index >= _navigator.Count)
            return;

        // Built now, when the direction is decided, and not at pointer-down: a tap builds nothing.
        _neighbor = _setup.BuildPage(index);
        _neighborIndex = index;
        double offset = Width * (index > _navigator.Index ? 1 : -1);
        _offsets[_neighbor] = offset;
        _view.PageAdded(_neighbor, offset);
    }

    // Settle: both pages move to their resting place, then the page that left is released.

    private void Slide(bool toNeighbor, int side)
    {
        var current = _current!;
        var neighbor = _neighbor;
        _animating = true;
        int epoch = _epoch;

        var moves = new List<PageMove> { new(current, Offset(current), toNeighbor ? -side * Width : 0) };
        if (neighbor != null)
            moves.Add(new PageMove(neighbor, Offset(neighbor), toNeighbor ? 0 : side * Width));
        _slide = moves;
        _slideStart = _scheduler.Elapsed;
        _view.Slide(moves, SettleTime, () =>
        {
            if (epoch == _epoch)
                Settled(toNeighbor);
        });

        if (_hasIndicator)
            _view.IndicatorSettle(toNeighbor && neighbor != null ? _neighborIndex : _navigator.Index);
    }

    private void Settled(bool toNeighbor)
    {
        bool changed = false;
        if (toNeighbor && _neighbor != null)
        {
            if (_current != null)
            {
                _view.PageRemoved(_current);
                _offsets.Remove(_current);
                _current.Dispose();
            }

            _current = _neighbor;
            _neighbor = null;
            _navigator.Settle(_neighborIndex);
            changed = true;
            UrDeckLog.Info($"Page '{_navigator.Name}' ({_navigator.Index + 1}/{_navigator.Count}) shown");
        }
        else if (_neighbor != null)
        {
            _view.PageRemoved(_neighbor);
            _offsets.Remove(_neighbor);
            _neighbor.Dispose();
            _neighbor = null;
        }

        _neighborIndex = -1;
        _animating = false;
        _slide = [];
        if (_current != null)
            SetOffset(_current, 0);
        _reclaim.Cancel();
        _reclaim.Start(ReclaimDelay);
        if (changed)
            PageChanged?.Invoke(_navigator.Index);
    }

    private double Offset(PageSlots page) => _offsets.TryGetValue(page, out double offset) ? offset : 0;

    private void SetOffset(PageSlots page, double offset)
    {
        _offsets[page] = offset;
        _view.SetOffset(page, offset);
    }
}
