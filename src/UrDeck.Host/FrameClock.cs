// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Host;

/// <summary>
/// The one shared frame source. It repaints the views whose widgets report <c>IsAnimating</c> about 30 times a second,
/// and exists only while there is at least one: the timer starts with the first view and stops when the last one leaves.
/// A frame only invalidates; it never refreshes widget data.
/// </summary>
internal sealed class FrameClock
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 30);

    private const double TickSlackMs = 10;

    private readonly HashSet<WidgetView> _views = new();
    private readonly Dictionary<WidgetView, long> _last = new();
    private readonly DispatcherQueueTimer _timer;

    public FrameClock(DispatcherQueue queue)
    {
        _timer = queue.CreateTimer();
        _timer.Interval = FrameInterval;
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) => OnTick();
    }

    public bool IsRunning => _timer.IsRunning;

    public void Add(WidgetView view)
    {
        if (!_views.Add(view) || _timer.IsRunning)
            return;
        _timer.Start();
        UrDeckLog.Info("Frame clock started");
    }

    public void Remove(WidgetView view)
    {
        _last.Remove(view);
        if (_views.Remove(view) && _views.Count == 0)
            Stop();
    }

    /// <summary>Stops the clock and forgets every view (the window is closing).</summary>
    public void Shutdown()
    {
        _views.Clear();
        _last.Clear();
        Stop();
    }

    private void Stop()
    {
        if (!_timer.IsRunning)
            return;
        _timer.Stop();
        UrDeckLog.Info("Frame clock stopped");
    }

    private void OnTick()
    {
        // A paint can change membership, so walk a copy. A widget that asks for a slower rate is invalidated on every
        // n-th tick; the tick itself is cheap.
        long now = Environment.TickCount64;
        foreach (var view in _views.ToArray())
        {
            double interval = view.FrameInterval.TotalMilliseconds;
            if (interval > 0 && _last.TryGetValue(view, out long last) && now - last < interval - TickSlackMs)
                continue;
            _last[view] = now;
            view.Invalidate();
        }
    }
}
