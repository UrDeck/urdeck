// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Input;

namespace UrDeck.Engine.Deck;

/// <summary>
/// One placed widget of a deck session, without its surface: the widget instance, its host services, its reading
/// subscriptions (held while the slot is shown), its refresh timer and its animation wake-up, its frame-source entry, and
/// its taps and pressed state. Whoever shows it calls <see cref="Show"/> and <see cref="Hide"/>, paints it with
/// <see cref="Paint"/> when it raises <see cref="PaintRequested"/>, and shows <see cref="PressedChanged"/>.
/// Widgets without a timer-based policy (OnData) render once and then only when one of their readings changes.
/// Deck thread only.
/// </summary>
public sealed class WidgetSlot : IFrameClient, IDisposable
{
    // Cleared on Dispose: a presenter's surface can stay alive for a while, and it must not keep the plugin alive with it.
    private IWidget? _widget;
    private readonly Theme _theme;
    private readonly CardLook _look;
    private readonly Func<UrDeck.Sdk.MotionLevel> _motion;
    private readonly IDeckScheduler _scheduler;
    private readonly IFrameSource _frames;
    private readonly IDeckTimer? _timer;
    private readonly TimeSpan _interval;
    private IDeckTimer? _wake;
    private readonly CancellationTokenSource _cts = new();
    private readonly IDeckReadings _readings;
    private readonly string[] _subscriptions;
    private bool _subscribed;
    private bool _updating;
    private bool _hasPainted;
    private bool _animating;
    private bool _disposed;

    // Null for a widget that does not take taps, and cleared on Dispose like the widget itself.
    private ITapTarget? _tap;
    private WidgetServices? _services;
    private bool _pressed;

    public WidgetSlot(
        IWidget widget,
        WidgetServices? services,
        WidgetDescriptor descriptor,
        Theme theme,
        CardLook look,
        PressStyle press,
        IDeckScheduler scheduler,
        IFrameSource frames,
        IDeckReadings readings,
        Func<UrDeck.Sdk.MotionLevel>? motion = null)
    {
        _motion = motion ?? (() => UrDeck.Sdk.MotionLevel.Full);
        _widget = widget;
        _tap = widget as ITapTarget;
        _services = services;
        Press = press;
        _scheduler = scheduler;
        _frames = frames;
        _theme = theme;
        _look = look;
        _readings = readings;
        if (services != null)
            services.RepaintRequested += OnRepaintRequested;

        // The widget is already configured, so its readings are known. A widget that declares none costs nothing.
        _subscriptions = ReadSubscriptions(widget);

        if (descriptor.RefreshInterval is { } interval && interval > TimeSpan.Zero)
        {
            _interval = interval;
            _timer = scheduler.CreateTimer(() => _ = RefreshAsync());
        }
    }

    /// <summary>Raised when the slot must be painted again (a reading, a refresh, a frame, a tap, an icon that arrived).</summary>
    public event Action? PaintRequested;

    /// <summary>Raised when the card is pressed (true) or returns to rest (false); the presenter shows it with <see cref="Press"/>.</summary>
    public event Action<bool>? PressedChanged;

    /// <summary>The theme's press values: how a pressed card looks.</summary>
    public PressStyle Press { get; }

    /// <summary>The readings the widget declared, for the page's reading-to-slot map.</summary>
    public IReadOnlyList<string> Subscriptions => _subscriptions;

    /// <summary>False once the slot is disposed: it holds no widget then.</summary>
    public bool HasWidget => _widget != null;

    /// <summary>Whether its readings are subscribed (between <see cref="Show"/> and <see cref="Hide"/> for a widget that declares some).</summary>
    public bool IsSubscribed => _subscribed;

    /// <summary>Whether it is registered with the frame source (the widget said it animates at its last paint).</summary>
    public bool IsAnimating => _animating;

    public bool IsPressed => _pressed;

    /// <summary>Whether the refresh timer runs (a widget with a refresh interval, while shown).</summary>
    public bool IsRefreshing => _timer?.IsRunning == true;

    /// <summary>Whether a wake-up is scheduled for a widget that animates now and then.</summary>
    public bool IsWakeScheduled => _wake?.IsRunning == true;

    /// <summary>The widget's name, for log lines; empty once disposed.</summary>
    public string Name => _widget?.Name ?? "";

    /// <summary>The slot is on screen: its readings are subscribed, the widget refreshes once and its timer runs.</summary>
    public void Show()
    {
        if (_disposed)
            return;
        Subscribe();
        _ = RefreshAsync();
        _timer?.Start(_interval, repeat: true);
    }

    /// <summary>The slot left the screen: the timer stops and the readings are unsubscribed.</summary>
    public void Hide()
    {
        if (_disposed)
            return;
        _timer?.Cancel();
        Unsubscribe();
    }

    /// <summary>Called on the deck thread when a reading this widget declared changed: repaints if the widget says it must.</summary>
    public void OnReadingsChanged()
    {
        if (_widget != null && (!_hasPainted || SafeNeedsRender()))
            RequestPaint();
    }

    /// <summary>
    /// Raised by the engine on a worker thread when something the widget asked for (an icon) changed. The widget is
    /// repainted without asking it: the engine only says so when there is something new to draw.
    /// </summary>
    private void OnRepaintRequested() => _scheduler.Post(() =>
    {
        if (_widget != null)
            RequestPaint();
    });

    private void RequestPaint() => PaintRequested?.Invoke();

    void IFrameClient.Invalidate() => RequestPaint();

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
            RequestPaint();
    }

    /// <summary>Shows the card as pressed, when the theme's press is visible at all. Nothing is painted.</summary>
    public void PressDown()
    {
        if (_pressed || !Press.IsVisible)
            return;
        _pressed = true;
        PressedChanged?.Invoke(true);
    }

    /// <summary>Returns a pressed card to rest.</summary>
    public void ReleasePress()
    {
        if (!_pressed)
            return;
        _pressed = false;
        PressedChanged?.Invoke(false);
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
            RequestPaint();
    }

    private bool SafeNeedsRender()
    {
        var widget = _widget;
        if (widget == null)
            return false;
        try
        {
            return widget.NeedsRender(_scheduler.Now);
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Widget '{widget.Name}' NeedsRender failed; repainting", ex);
            return true;
        }
    }

    /// <summary>
    /// Paints the card on <paramref name="canvas"/>, whose origin is the card's top-left corner and whose size is
    /// <paramref name="size"/> (physical pixels), with the theme's card drawing (<see cref="WidgetPainter"/>). The presenter
    /// clears its surface first if it needs to. Afterwards the slot is registered with the frame source if the widget
    /// animates, or its next wake-up is scheduled.
    /// </summary>
    public void Paint(SKCanvas canvas, System.Drawing.Size size)
    {
        var widget = _widget;
        if (widget == null)
            return;
        WidgetPainter.Paint(widget, canvas, size, _theme, _look, _scheduler.Now, _cts.Token, _motion());
        _hasPainted = true;

        // Membership only changes here; the frame source's next tick asks for the repaint.
        if (SafeIsAnimating(widget))
        {
            _animating = true;
            _frames.Add(this);
            _wake?.Cancel();
        }
        else
        {
            _animating = false;
            _frames.Remove(this);
            ScheduleWake(widget);
        }
    }

    /// <summary>The time between frames the widget asks for while it animates; zero is the frame source's own rate.</summary>
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
            _wake?.Cancel();
            return;
        }

        _wake ??= _scheduler.CreateTimer(RequestPaint);
        _wake.Start(TimeSpan.FromMilliseconds(Math.Max(50, (at - _scheduler.Now).TotalMilliseconds)));
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
        if (_disposed)
            return;
        _disposed = true;
        _timer?.Cancel();
        _wake?.Cancel();
        Unsubscribe();
        _frames.Remove(this);
        _animating = false;
        _cts.Cancel();
        _cts.Dispose();
        if (_services != null)
        {
            // Releases the widget's icons and drops the engine's reference to this slot.
            _services.RepaintRequested -= OnRepaintRequested;
            _services.Dispose();
            _services = null;
        }

        (_widget as IDisposable)?.Dispose();
        _widget = null;
        _tap = null;
        _pressed = false;
        PaintRequested = null;
        PressedChanged = null;
    }
}
