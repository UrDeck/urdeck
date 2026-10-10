// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Input;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;

namespace UrDeck.Host;

/// <summary>What the pager needs to build its pages for the current window size, theme and configuration.</summary>
/// <param name="Screen">The window size in DIPs.</param>
/// <param name="Scale">DIPs to physical pixels.</param>
/// <param name="Chrome">Where the indicator and the dock go (in DIPs) and the resolved mode.</param>
/// <param name="Style">The indicator's theme values, in physical pixels.</param>
/// <param name="BuildPage">Builds the widgets of the page at an index.</param>
/// <param name="BuildDock">Builds the dock's widgets; null when there is no dock.</param>
internal sealed record PagerSetup(
    System.Drawing.Size Screen,
    double Scale,
    ChromeLayoutResult Chrome,
    IndicatorStyle Style,
    Func<int, PageHost> BuildPage,
    Func<PageHost?> BuildDock);

/// <summary>
/// Shows the current page and slides to a neighbour. At rest one page is alive; while a swipe is in progress the
/// neighbour in its direction is built too, and the page that left is disposed when the slide settles. The dock is not
/// a page: it is built once per setup, sits in the overlay and stays where it is while pages slide. All decisions
/// (swipe or tap, commit, rubber band) are the engine's; this class translates pointer events into samples, moves the
/// pages' composition offsets and runs the settle animation.
/// </summary>
internal sealed class PagerController
{
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(250);

    // A page's surfaces are native memory the garbage collector cannot see, so a released page can sit around for a
    // long time. One collection shortly after the pages have settled (and nothing is moving) frees them.
    private static readonly TimeSpan ReclaimDelay = TimeSpan.FromSeconds(1.5);

    private readonly Grid _input;
    private readonly Canvas _pages;
    private readonly Canvas _overlay;
    private readonly FrameClock _clock;
    private readonly PageNavigator _navigator;
    private readonly Compositor _compositor;

    private PagerSetup? _setup;
    private GestureRecognizer? _gestures;
    private PageHost? _current;
    private PageHost? _neighbor;
    private int _neighborIndex = -1;
    private PageHost? _dock;
    private IndicatorView? _indicator;
    private WidgetView? _pressed;
    private readonly DispatcherQueueTimer _reclaim;
    private bool _animating;
    private int _epoch;

    public PagerController(Grid input, Canvas pages, Canvas overlay, FrameClock clock, PageNavigator navigator)
    {
        _input = input;
        _pages = pages;
        _overlay = overlay;
        _clock = clock;
        _navigator = navigator;
        _compositor = ElementCompositionPreview.GetElementVisual(pages).Compositor;
        _reclaim = pages.DispatcherQueue.CreateTimer();
        _reclaim.Interval = ReclaimDelay;
        _reclaim.IsRepeating = false;
        _reclaim.Tick += (_, _) => GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);

        input.PointerPressed += OnPressed;
        input.PointerMoved += OnMoved;
        input.PointerReleased += OnReleased;
        input.PointerCanceled += OnCanceled;
        input.PointerCaptureLost += OnCaptureLost;
    }

    private double Width => _setup?.Screen.Width ?? 0;

    /// <summary>The widgets of every live page and of the dock, for the window's reading-changed lookup.</summary>
    public IEnumerable<PageHost> LivePages
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
        SetOffset(_current, 0);
        _pages.Children.Add(_current);

        // The dock goes into the overlay first, so the indicator is above it. Page changes never touch it.
        _dock = setup.BuildDock();
        if (_dock != null)
        {
            _overlay.Children.Add(_dock);
            UrDeckLog.Info($"Dock: {_dock.ViewCount} slot(s) shown");
        }

        var chrome = setup.Chrome;
        if (chrome.Mode != IndicatorMode.Off && _navigator.Count > 0)
        {
            _indicator = new IndicatorView(_navigator.Count, _navigator.Index, setup.Style, chrome.Mode == IndicatorMode.Fade, _clock)
            {
                Width = chrome.Indicator.Width,
                Height = chrome.Indicator.Height,
            };
            Canvas.SetLeft(_indicator, chrome.Indicator.X);
            Canvas.SetTop(_indicator, chrome.Indicator.Y);
            _overlay.Children.Add(_indicator);
        }
    }

    /// <summary>Releases every page, the dock and the indicator (a rebuild or the window closing).</summary>
    public void Release()
    {
        _epoch++;
        _animating = false;
        // The recognizer says nothing when it is aborted, and the views are about to go: just forget the press.
        _pressed = null;
        _gestures?.Abort();
        _gestures = null;
        _neighbor?.Dispose();
        _neighbor = null;
        _neighborIndex = -1;
        _current?.Dispose();
        _current = null;
        _pages.Children.Clear();
        _dock?.Dispose();
        _dock = null;
        _indicator?.Stop();
        _indicator = null;
        _overlay.Children.Clear();
    }

    public void Shutdown()
    {
        _input.PointerPressed -= OnPressed;
        _input.PointerMoved -= OnMoved;
        _input.PointerReleased -= OnReleased;
        _input.PointerCanceled -= OnCanceled;
        _input.PointerCaptureLost -= OnCaptureLost;
        Release();
    }

    // Pointer events -> engine samples. Mouse, touch and pen take the same path; views handle none of them.

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_gestures == null || _animating)
            return;
        _input.CapturePointer(e.Pointer);
        Feed(PointerPhase.Down, e);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e) => Feed(PointerPhase.Move, e);

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        Feed(PointerPhase.Up, e);
        _input.ReleasePointerCapture(e.Pointer);
    }

    private void OnCanceled(object sender, PointerRoutedEventArgs e) => Feed(PointerPhase.Cancel, e);

    private void OnCaptureLost(object sender, PointerRoutedEventArgs e) => Feed(PointerPhase.Cancel, e);

    private void Feed(PointerPhase phase, PointerRoutedEventArgs e)
    {
        if (_gestures == null || _animating)
            return;
        var point = e.GetCurrentPoint(_input);
        _gestures.Process(new PointerSample(
            (int)e.Pointer.PointerId, phase, point.Position.X, point.Position.Y, (long)(point.Timestamp / 1000)));
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
        if (FindView(x, y, out var local) is not { } view || !view.CanTap(ToCardPixels(local)))
            return;
        view.Press();
        _pressed = view;
    }

    /// <summary>
    /// The view under the point. A point in the dock's band is looked up in the dock and never in the page: nothing of
    /// the page is there, so a point beside the dock's cards belongs to nobody.
    /// </summary>
    private WidgetView? FindView(double x, double y, out System.Drawing.PointF local)
    {
        local = default;
        var dock = _setup!.Chrome.Dock;
        if (dock.Height > 0 && y >= dock.Top && y < dock.Bottom)
            return _dock?.FindView(x, y, out local);
        return _current?.FindView(x, y, out local);
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
        if (_setup == null || _indicator == null || !_indicator.IsVisible)
            return -1;

        var rect = _setup.Chrome.Indicator;
        var local = new System.Drawing.PointF((float)((x - rect.X) * _setup.Scale), (float)((y - rect.Y) * _setup.Scale));
        return _indicator.HitTest(local, (float)(_setup.Screen.Width * _setup.Scale / 4));
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

        _indicator?.Follow(Math.Clamp(_navigator.Index - dx / Width, 0, _navigator.Count - 1));
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

        if (FindView(x, y, out var local) is { } view)
            view.Tap(ToCardPixels(local));
    }

    /// <summary>Slides to <paramref name="page"/> (a tap on a dot).</summary>
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

        _neighbor?.Dispose();
        _pages.Children.Remove(_neighbor);
        _neighbor = null;
        _neighborIndex = -1;
        if (index < 0 || index >= _navigator.Count)
            return;

        // Built now, when the direction is decided, and not at pointer-down: a tap builds nothing.
        _neighbor = _setup.BuildPage(index);
        _neighborIndex = index;
        SetOffset(_neighbor, Width * (index > _navigator.Index ? 1 : -1));
        _pages.Children.Add(_neighbor);
    }

    // Settle: both pages animate to their resting place on the compositor, then the page that left is released.

    private void Slide(bool toNeighbor, int side)
    {
        var current = _current!;
        var neighbor = _neighbor;
        _animating = true;
        int epoch = _epoch;

        var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        Animate(current, toNeighbor ? -side * Width : 0);
        if (neighbor != null)
            Animate(neighbor, toNeighbor ? 0 : side * Width);
        batch.End();
        batch.Completed += (_, _) => current.DispatcherQueue.TryEnqueue(() =>
        {
            if (epoch == _epoch)
                Settled(toNeighbor);
        });

        if (toNeighbor && neighbor != null)
            _indicator?.Settle(_neighborIndex);
        else
            _indicator?.Settle(_navigator.Index);
    }

    private void Settled(bool toNeighbor)
    {
        if (toNeighbor && _neighbor != null)
        {
            _current?.Dispose();
            _pages.Children.Remove(_current);
            _current = _neighbor;
            _neighbor = null;
            _navigator.Settle(_neighborIndex);
            UrDeckLog.Info($"Page '{_navigator.Name}' ({_navigator.Index + 1}/{_navigator.Count}) shown");
        }
        else
        {
            _neighbor?.Dispose();
            _pages.Children.Remove(_neighbor);
            _neighbor = null;
        }

        _neighborIndex = -1;
        if (_current != null)
            SetOffset(_current, 0);
        _animating = false;
        _reclaim.Stop();
        _reclaim.Start();
    }

    private void Animate(PageHost page, double target)
    {
        var easing = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f));
        var animation = _compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(1f, new Vector3((float)target, 0, 0), easing);
        animation.Duration = SettleTime;
        ElementCompositionPreview.GetElementVisual(page).StartAnimation("Offset", animation);
    }

    private static void SetOffset(PageHost page, double x)
    {
        var visual = ElementCompositionPreview.GetElementVisual(page);
        visual.StopAnimation("Offset");
        visual.Offset = new Vector3((float)x, 0, 0);
    }
}
