// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Sdk;

namespace UrDeck.Host;

/// <summary>
/// Hosts one widget instance in its own Skia surface (composited by the GPU, drawn by Skia on the CPU), driven by the widget's declared refresh policy.
/// Widgets without a timer-based policy (OnData) render once and then only when one of their readings changes.
/// </summary>
internal sealed class WidgetView : SKXamlCanvas, IDisposable
{
    // Cleared on Dispose: WinUI can keep a removed canvas alive for a while, and it must not keep the plugin alive with it.
    private IWidget? _widget;
    private readonly Theme _theme;
    private readonly FrameClock _frameClock;
    private readonly DispatcherQueueTimer? _timer;
    private DispatcherQueueTimer? _wake;
    private readonly CancellationTokenSource _cts = new();
    private readonly ReadingHub _readings;
    private readonly string[] _subscriptions;
    private bool _subscribed;
    private bool _updating;
    private bool _hasPainted;

    public WidgetView(IWidget widget, WidgetDescriptor descriptor, Theme theme, FrameClock frameClock, ReadingHub readings)
    {
        _widget = widget;
        _frameClock = frameClock;
        _theme = theme;
        _readings = readings;
        PaintSurface += OnPaintSurface;

        // The widget is already configured, so its readings are known. A widget that declares none costs nothing.
        _subscriptions = ReadSubscriptions(widget);

        if (descriptor.RefreshInterval is { } interval && interval > TimeSpan.Zero)
        {
            _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _timer.Interval = interval;
            _timer.IsRepeating = true;
            _timer.Tick += (_, _) => _ = RefreshAsync();
        }

        Loaded += (_, _) =>
        {
            Subscribe();
            _ = RefreshAsync();
            _timer?.Start();
        };
        Unloaded += (_, _) =>
        {
            _timer?.Stop();
            Unsubscribe();
        };
    }

    /// <summary>The readings the widget declared, for the window id-to-view map.</summary>
    public IReadOnlyList<string> Subscriptions => _subscriptions;

    /// <summary>Called on the UI thread when a reading this widget declared changed: repaints if the widget says it must.</summary>
    public void OnReadingsChanged()
    {
        if (_widget != null && (!_hasPainted || SafeNeedsRender()))
            Invalidate();
    }

    private static string[] ReadSubscriptions(IWidget widget)
    {
        try
        {
            return widget.Subscriptions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{widget.Name}' Subscriptions failed; it gets no readings", ex);
            return [];
        }
    }

    private void Subscribe()
    {
        if (_subscribed || _subscriptions.Length == 0)
            return;
        _subscribed = true;
        _readings.Subscribe(_subscriptions);
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;
        _subscribed = false;
        _readings.Unsubscribe(_subscriptions);
    }

    private async Task RefreshAsync()
    {
        if (_updating)
            return;
        var widget = _widget;
        if (widget == null)
            return;
        _updating = true;
        try
        {
            await widget.UpdateAsync(_cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{widget.Name}' update failed", ex);
        }
        finally
        {
            _updating = false;
        }

        // The first refresh always paints; later ones only when the widget says something changed.
        if (_widget != null && (!_hasPainted || SafeNeedsRender()))
            Invalidate();
    }

    private bool SafeNeedsRender()
    {
        var widget = _widget;
        if (widget == null)
            return false;
        try
        {
            return widget.NeedsRender(DateTime.Now);
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{widget.Name}' NeedsRender failed; repainting", ex);
            return true;
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var widget = _widget;
        if (widget == null)
            return;
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        WidgetPainter.Paint(
            widget,
            canvas,
            new System.Drawing.Size(e.Info.Width, e.Info.Height),
            _theme,
            DateTime.Now,
            _cts.Token);
        _hasPainted = true;

        // Membership only changes here; the frame clock's next tick does the invalidating.
        if (SafeIsAnimating(widget))
        {
            _frameClock.Add(this);
            _wake?.Stop();
        }
        else
        {
            _frameClock.Remove(this);
            ScheduleWake(widget);
        }
    }

    /// <summary>The time between frames the widget asks for while it animates; zero is the clock's own rate.</summary>
    public TimeSpan FrameInterval
    {
        get
        {
            try
            {
                return _widget?.AnimationFrameInterval ?? TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                UrDeckLog.Error("AnimationFrameInterval failed; using the clock's rate", ex);
                return TimeSpan.Zero;
            }
        }
    }

    /// <summary>A widget that animates now and then says when; one timer, restarted after every paint, paints it then.</summary>
    private void ScheduleWake(IWidget widget)
    {
        DateTime? next;
        try
        {
            next = widget.NextAnimationAt;
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{widget.Name}' NextAnimationAt failed; no wake is scheduled", ex);
            next = null;
        }

        if (next is not { } at)
        {
            _wake?.Stop();
            return;
        }

        if (_wake == null)
        {
            _wake = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _wake.IsRepeating = false;
            _wake.Tick += (_, _) => Invalidate();
        }

        _wake.Stop();
        _wake.Interval = TimeSpan.FromMilliseconds(Math.Max(50, (at - DateTime.Now).TotalMilliseconds));
        _wake.Start();
    }

    private static bool SafeIsAnimating(IWidget widget)
    {
        try
        {
            return widget.IsAnimating;
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{widget.Name}' IsAnimating failed; treating it as not animating", ex);
            return false;
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _wake?.Stop();
        Unsubscribe();
        _frameClock.Remove(this);
        _cts.Cancel();
        _cts.Dispose();
        PaintSurface -= OnPaintSurface;
        (_widget as IDisposable)?.Dispose();
        _widget = null;
    }
}
