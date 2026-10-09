// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Input;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Rendering;

namespace UrDeck.Host;

/// <summary>What the pager needs to build its pages for the current window size, theme and configuration.</summary>
/// <param name="Screen">The window size in DIPs.</param>
/// <param name="Scale">DIPs to physical pixels.</param>
/// <param name="Chrome">Where the indicator goes (in DIPs) and the resolved mode.</param>
/// <param name="Style">The indicator's theme values, in physical pixels.</param>
/// <param name="BuildPage">Builds the widgets of the page at an index.</param>
internal sealed record PagerSetup(
    System.Drawing.Size Screen,
    double Scale,
    ChromeLayoutResult Chrome,
    IndicatorStyle Style,
    Func<int, PageHost> BuildPage);

/// <summary>
/// Shows the current page and slides to a neighbour. At rest one page is alive; while a swipe is in progress the
/// neighbour in its direction is built too, and the page that left is disposed when the slide settles. All decisions
/// (swipe or tap, commit, rubber band) are the engine's; this class translates pointer events into samples, moves the
/// pages' composition offsets and runs the settle animation.
/// </summary>
internal sealed class PagerController
{
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(250);

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
    private IndicatorView? _indicator;
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

        input.PointerPressed += OnPressed;
        input.PointerMoved += OnMoved;
        input.PointerReleased += OnReleased;
        input.PointerCanceled += OnCanceled;
        input.PointerCaptureLost += OnCaptureLost;
    }

    private double Width => _setup?.Screen.Width ?? 0;

    /// <summary>The widgets of every live page, for the window's reading-changed lookup.</summary>
    public IEnumerable<PageHost> LivePages
    {
        get
        {
            if (_current != null)
                yield return _current;
            if (_neighbor != null)
                yield return _neighbor;
        }
    }

    /// <summary>
    /// Builds the current page for a new setup. A swipe in progress ends at once: both pages are released and the
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

    /// <summary>Releases every page and the indicator (a rebuild or the window closing).</summary>
    public void Release()
    {
        _epoch++;
        _animating = false;
        _gestures?.Abort();
        _gestures = null;
        _neighbor?.Dispose();
        _neighbor = null;
        _neighborIndex = -1;
        _current?.Dispose();
        _current = null;
        _pages.Children.Clear();
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
            case GestureKind.Tap:
                Tap(g.X, g.Y);
                break;
        }
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
        if (_setup == null || _indicator == null || !_indicator.IsVisible)
            return;

        var rect = _setup.Chrome.Indicator;
        var local = new System.Drawing.PointF((float)((x - rect.X) * _setup.Scale), (float)((y - rect.Y) * _setup.Scale));
        int page = _indicator.HitTest(local, (float)(_setup.Screen.Width * _setup.Scale / 4));
        if (page >= 0 && page != _navigator.Index)
            GoTo(page);
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
