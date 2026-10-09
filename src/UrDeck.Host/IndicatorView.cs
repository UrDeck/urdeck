// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using SkiaSharp.Views.Windows;
using UrDeck.Engine.Rendering;

namespace UrDeck.Host;

/// <summary>
/// Draws the page indicator (the engine's <see cref="PageIndicator"/>) in its rectangle. It repaints only when the pill's
/// position or the opacity changes, and takes no input itself: the pager sees taps and asks it where they landed.
/// </summary>
internal sealed class IndicatorView : SKXamlCanvas, IFrameClient
{
    private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan MoveTime = TimeSpan.FromMilliseconds(250);

    private readonly int _count;
    private readonly IndicatorStyle _style;
    private readonly bool _floating;
    private readonly FrameClock _clock;
    private readonly DispatcherQueueTimer _hold;

    private double _position;
    private double _moveFrom;
    private double _moveTo;
    private long _moveStart;
    private bool _moving;
    private double _opacity;
    private long _fadeStart;
    private bool _fading;

    public IndicatorView(int count, double position, IndicatorStyle style, bool floating, FrameClock clock)
    {
        _count = count;
        _style = style;
        _floating = floating;
        _clock = clock;
        _position = position;
        _opacity = floating ? 0 : 1;
        IsHitTestVisible = false;

        _hold = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _hold.Interval = HoldTime;
        _hold.IsRepeating = false;
        _hold.Tick += (_, _) =>
        {
            _fading = true;
            _fadeStart = Environment.TickCount64;
            _clock.Add(this);
            Invalidate();
        };
        PaintSurface += OnPaintSurface;
    }

    public TimeSpan FrameInterval => TimeSpan.Zero;

    /// <summary>False while a floating indicator has faded out; taps then go to what is underneath.</summary>
    public bool IsVisible => !_floating || CurrentOpacity(Environment.TickCount64) > 0;

    public int HitTest(System.Drawing.PointF local, float cellPx) =>
        PageIndicator.HitTest(new System.Drawing.RectangleF(0, 0, CanvasSize.Width, CanvasSize.Height), _count, _style, cellPx, local);

    /// <summary>The pill follows the finger: <paramref name="position"/> is the fractional page. Nothing fades meanwhile.</summary>
    public void Follow(double position)
    {
        _position = position;
        _moving = false;
        Reveal();
        Invalidate();
    }

    /// <summary>Settles on <paramref name="position"/>, sliding there from where the pill is, then starts the fade.</summary>
    public void Settle(double position)
    {
        _moveFrom = _position;
        _moveTo = position;
        _moveStart = Environment.TickCount64;
        _moving = _moveFrom != _moveTo;
        Reveal();
        if (_floating)
            _hold.Start();
        if (_moving)
            _clock.Add(this);
        Invalidate();
    }

    /// <summary>A floating indicator appears and stays while something is happening.</summary>
    private void Reveal()
    {
        _hold.Stop();
        _fading = false;
        _opacity = 1;
    }

    private double CurrentOpacity(long now)
    {
        if (!_fading)
            return _opacity;
        double t = (now - _fadeStart) / FadeTime.TotalMilliseconds;
        return t >= 1 ? 0 : 1 - t;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        long now = Environment.TickCount64;
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);

        if (_moving)
        {
            double t = (now - _moveStart) / MoveTime.TotalMilliseconds;
            if (t >= 1)
            {
                _position = _moveTo;
                _moving = false;
            }
            else
            {
                double eased = 1 - Math.Pow(1 - t, 3);
                _position = _moveFrom + (_moveTo - _moveFrom) * eased;
            }
        }

        double opacity = CurrentOpacity(now);
        if (_fading && opacity <= 0)
        {
            _fading = false;
            _opacity = 0;
        }

        PageIndicator.Paint(canvas, new System.Drawing.RectangleF(0, 0, e.Info.Width, e.Info.Height), _count, _position, (float)opacity, _floating, _style);

        // Membership only changes here, as in WidgetView; the frame clock's next tick does the invalidating.
        if (_moving || _fading)
            _clock.Add(this);
        else
            _clock.Remove(this);
    }

    public void Stop()
    {
        _hold.Stop();
        _clock.Remove(this);
        PaintSurface -= OnPaintSurface;
    }
}
