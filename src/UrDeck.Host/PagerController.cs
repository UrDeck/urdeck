// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using UrDeck.Engine.Deck;
using UrDeck.Engine.Input;

namespace UrDeck.Host;

/// <summary>
/// The window's half of the pager: what the session's <see cref="PagerState"/> says is alive becomes a <see cref="PageHost"/>
/// in the page canvas (the dock and the indicator in the overlay), a page's offset is its composition offset, and a settle
/// is a compositor animation. Pointer events on the window are turned into the engine's samples; every decision (swipe or
/// tap, commit, rubber band, which page) is the pager state's.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The pages and the dock are disposed when the pager state clears them (Cleared, PageRemoved), which the session does when it ends.")]
internal sealed class PagerController : IPagerView
{
    private readonly Grid _input;
    private readonly Canvas _pages;
    private readonly Canvas _overlay;
    private readonly FrameClock _clock;
    private readonly Compositor _compositor;
    private readonly Dictionary<PageSlots, PageHost> _hosts = [];
    private PageHost? _dock;
    private IndicatorView? _indicator;
    private PagerState? _state;

    public PagerController(Grid input, Canvas pages, Canvas overlay, FrameClock clock)
    {
        _input = input;
        _pages = pages;
        _overlay = overlay;
        _clock = clock;
        _compositor = ElementCompositionPreview.GetElementVisual(pages).Compositor;

        input.PointerPressed += OnPressed;
        input.PointerMoved += OnMoved;
        input.PointerReleased += OnReleased;
        input.PointerCanceled += OnCanceled;
        input.PointerCaptureLost += OnCaptureLost;
    }

    /// <summary>The pager state the pointer samples go to (the session's, made after this view).</summary>
    public void Attach(PagerState state) => _state = state;

    public void PageAdded(PageSlots page, double offset)
    {
        var host = new PageHost(page);
        _hosts[page] = host;
        SetOffset(host, offset);
        _pages.Children.Add(host);
    }

    public void PageRemoved(PageSlots page)
    {
        if (!_hosts.Remove(page, out var host))
            return;
        host.Dispose();
        _pages.Children.Remove(host);
    }

    public void DockAdded(PageSlots dock)
    {
        // The dock goes into the overlay first, so the indicator is above it. Page changes never touch it.
        _dock = new PageHost(dock);
        _overlay.Children.Add(_dock);
    }

    public void IndicatorAdded(IndicatorSetup indicator)
    {
        _indicator = new IndicatorView(indicator.Count, indicator.Index, indicator.Style, indicator.Floating, _clock)
        {
            Width = indicator.Bounds.Width,
            Height = indicator.Bounds.Height,
        };
        Canvas.SetLeft(_indicator, indicator.Bounds.X);
        Canvas.SetTop(_indicator, indicator.Bounds.Y);
        _overlay.Children.Add(_indicator);
    }

    public void Cleared()
    {
        foreach (var host in _hosts.Values)
            host.Dispose();
        _hosts.Clear();
        _pages.Children.Clear();
        _dock?.Dispose();
        _dock = null;
        _indicator?.Stop();
        _indicator = null;
        _overlay.Children.Clear();
    }

    public void SetOffset(PageSlots page, double offset)
    {
        if (_hosts.TryGetValue(page, out var host))
            SetOffset(host, offset);
    }

    public void Slide(IReadOnlyList<PageMove> moves, TimeSpan duration, Action settled)
    {
        var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        foreach (var move in moves)
        {
            if (_hosts.TryGetValue(move.Page, out var host))
                Animate(host, move.To, duration);
        }

        batch.End();
        batch.Completed += (_, _) => _pages.DispatcherQueue.TryEnqueue(() => settled());
    }

    public void IndicatorFollow(double position) => _indicator?.Follow(position);

    public void IndicatorSettle(double position) => _indicator?.Settle(position);

    public int IndicatorHitTest(System.Drawing.PointF local, float cellPx) =>
        _indicator == null || !_indicator.IsVisible ? -1 : _indicator.HitTest(local, cellPx);

    public void Shutdown()
    {
        _input.PointerPressed -= OnPressed;
        _input.PointerMoved -= OnMoved;
        _input.PointerReleased -= OnReleased;
        _input.PointerCanceled -= OnCanceled;
        _input.PointerCaptureLost -= OnCaptureLost;
    }

    // Pointer events -> engine samples. Mouse, touch and pen take the same path; views handle none of them.

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_state is not { AcceptsInput: true })
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
        if (_state is not { AcceptsInput: true } state)
            return;
        var point = e.GetCurrentPoint(_input);
        state.Process(new PointerSample(
            (int)e.Pointer.PointerId, phase, point.Position.X, point.Position.Y, (long)(point.Timestamp / 1000)));
    }

    private void Animate(PageHost page, double target, TimeSpan duration)
    {
        var easing = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f));
        var animation = _compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(1f, new Vector3((float)target, 0, 0), easing);
        animation.Duration = duration;
        ElementCompositionPreview.GetElementVisual(page).StartAnimation("Offset", animation);
    }

    private static void SetOffset(PageHost page, double x)
    {
        var visual = ElementCompositionPreview.GetElementVisual(page);
        visual.StopAnimation("Offset");
        visual.Offset = new Vector3((float)x, 0, 0);
    }
}
