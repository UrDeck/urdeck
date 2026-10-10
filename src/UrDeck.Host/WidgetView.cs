// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Numerics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Hosting;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Input;

namespace UrDeck.Host;

/// <summary>
/// Hosts one widget instance in its own Skia surface (composited by the GPU, drawn by Skia on the CPU), driven by the widget's declared refresh policy.
/// Widgets without a timer-based policy (OnData) render once and then only when one of their readings changes.
/// </summary>
internal sealed class WidgetView : SKXamlCanvas, IFrameClient, IDisposable
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

    // Null for a widget that does not take taps, and cleared on Dispose like the widget itself.
    private ITapTarget? _tap;
    private WidgetServices? _services;
    private readonly PressStyle _press;
    private readonly DispatcherQueue _dispatcher;
    private bool _pressed;

    public WidgetView(
        IWidget widget,
        WidgetServices? services,
        WidgetDescriptor descriptor,
        Theme theme,
        PressStyle press,
        FrameClock frameClock,
        ReadingHub readings)
    {
        _widget = widget;
        _tap = widget as ITapTarget;
        _services = services;
        _press = press;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _frameClock = frameClock;
        _theme = theme;
        _readings = readings;
        PaintSurface += OnPaintSurface;
        if (services != null)
            services.RepaintRequested += OnRepaintRequested;

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

    /// <summary>
    /// Raised by the engine on a worker thread when something the widget asked for (an icon) changed. The widget is
    /// repainted without asking it: the engine only says so when there is something new to draw.
    /// </summary>
    private void OnRepaintRequested() => _dispatcher.TryEnqueue(() =>
    {
        if (_widget != null)
            Invalidate();
    });

    /// <summary>
    /// Whether a tap at <paramref name="point"/> (card pixels) would do something. False for a widget that does not take
    /// taps, without running any of its code.
    /// </summary>
    public bool CanTap(SKPoint point)
    {
        var tap = _tap;
        if (tap == null)
            return false;
        try
        {
            return tap.CanTap(point);
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{_widget?.Name}' CanTap failed; the tap is ignored", ex);
            return false;
        }
    }

    /// <summary>Delivers a tap at <paramref name="point"/> (card pixels), then repaints the widget if it says it changed.</summary>
    public void Tap(SKPoint point)
    {
        var tap = _tap;
        if (tap == null || !CanTap(point))
            return;
        try
        {
            tap.OnTap(point);
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{_widget?.Name}' OnTap failed", ex);
        }

        if (_widget != null && SafeNeedsRender())
            Invalidate();
    }

    /// <summary>
    /// Shows the card as pressed: the compositor scales and dims this view's layer to the theme's press values. Nothing
    /// is painted and the frame clock is not involved.
    /// </summary>
    public void Press()
    {
        if (_pressed || !_press.IsVisible)
            return;
        _pressed = true;
        AnimatePress(_press.Scale, _press.Opacity, PressTime);
    }

    /// <summary>Returns a pressed card to rest.</summary>
    public void ReleasePress()
    {
        if (!_pressed)
            return;
        _pressed = false;
        AnimatePress(1f, 1f, ReleaseTime);
    }

    private static readonly TimeSpan PressTime = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan ReleaseTime = TimeSpan.FromMilliseconds(160);

    private void AnimatePress(float scale, float opacity, TimeSpan duration)
    {
        var visual = ElementCompositionPreview.GetElementVisual(this);
        var compositor = visual.Compositor;
        visual.CenterPoint = new Vector3((float)(ActualWidth / 2), (float)(ActualHeight / 2), 0);
        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f));

        var scaling = compositor.CreateVector3KeyFrameAnimation();
        scaling.InsertKeyFrame(1f, new Vector3(scale, scale, 1f), easing);
        scaling.Duration = duration;
        visual.StartAnimation("Scale", scaling);

        var fading = compositor.CreateScalarKeyFrameAnimation();
        fading.InsertKeyFrame(1f, opacity, easing);
        fading.Duration = duration;
        visual.StartAnimation("Opacity", fading);
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
        if (_services != null)
        {
            // Releases the widget's icons and drops the engine's reference to this view.
            _services.RepaintRequested -= OnRepaintRequested;
            _services.Dispose();
            _services = null;
        }

        (_widget as IDisposable)?.Dispose();
        _widget = null;
        _tap = null;
    }
}
