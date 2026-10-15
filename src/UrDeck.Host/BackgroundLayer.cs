// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Host;

/// <summary>
/// The deck's background under the pages: one element in the window's background layer, and the frames that move it.
/// A moving shader is an <see cref="SKSwapChainPanel"/> (Skia on the GPU) driven at 30 frames per second from the
/// compositor's own frame callback; a picture or a still shader is an <see cref="SKXamlCanvas"/> painted when something
/// changes. No background means no element and no frame source. Frames run only while the background moves, the deck
/// is shown and the display is on.
/// </summary>
/// <remarks>
/// The element is the panel's size times the shader's scale, and its composition visual is scaled back up. A XAML
/// RenderTransform must not be used for that: it feeds the panel's composition scale and the panel then renders at full
/// size again. Pacing is not <see cref="FrameClock"/>: a dispatcher timer at 1/30 s delivered about 22 frames a second.
/// </remarks>
internal sealed class BackgroundLayer(Grid host) : IDisposable
{
    private static readonly long FrameInterval = Stopwatch.Frequency / 30;

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Stopwatch _clock = new();
    private readonly Stopwatch _run = new();

    private DeckBackground? _background;
    private string? _requestKey;
    private Func<int, DeckBackground?>? _create;
    private FrameworkElement? _element;
    private Action? _invalidate;
    private SKColor _themeColor;
    private System.Drawing.Size _screen;
    private double _scaling = 1;
    private bool _visible;
    private bool _displayOn = true;
    private bool _running;
    private bool _preparing;
    private bool _surfaceLogged;
    private long _next;
    private int _frames;
    private DispatcherQueueTimer? _firstFrameCheck;

    /// <summary>
    /// Shows the background of <paramref name="requestKey"/> (see <see cref="BackgroundPlan.RequestKey"/>; null for none).
    /// With the key already shown, whether it worked or not, nothing is rebuilt and the motion is not interrupted;
    /// otherwise the old one is dropped and <paramref name="create"/> builds the new one, with its time at 0. It takes the
    /// plan's candidate to start from, so a picture that cannot be decoded goes on to the next candidate.
    /// </summary>
    public void Apply(string? requestKey, Func<int, DeckBackground?> create, System.Drawing.Size screenDips, double scaling, SKColor themeColor)
    {
        bool colorChanged = themeColor != _themeColor;
        _themeColor = themeColor;
        bool sizeChanged = screenDips != _screen || scaling != _scaling;
        _screen = screenDips;
        _scaling = scaling;

        if (requestKey != _requestKey)
        {
            bool hadOne = _requestKey != null;
            Discard("background changed");
            _requestKey = requestKey;
            _create = requestKey == null ? null : create;
            if (requestKey == null)
            {
                if (hadOne)
                    UrDeckLog.Info("Background: none");
            }
            else
            {
                Show(create(0));
            }
        }
        else if (_element != null && (sizeChanged || colorChanged))
        {
            Resize(_background!);
            _surfaceLogged = false;
            _invalidate?.Invoke();
        }

        UpdateRunning(_background == null ? "no background" : "background applied");
    }

    /// <summary>Attaches what the plan built, or says that only the theme's colour is left.</summary>
    private void Show(DeckBackground? background)
    {
        _background = background;
        if (background != null)
            Attach(background);
        else
            UrDeckLog.Info($"Background: colour ({BackgroundOrigin.ThemeColour.Label()})");
    }

    /// <summary>The background cannot go on: drop it and show the plan's next candidate, or the colour.</summary>
    private void Fall(DeckBackground failed, string reason)
    {
        int next = failed.CandidateIndex + 1;
        var create = _create;
        Discard(reason);
        if (create != null)
        {
            Show(create(next));
            UpdateRunning(reason);
        }
    }

    public void SetVisible(bool visible)
    {
        if (_visible == visible)
            return;
        _visible = visible;
        UpdateRunning(visible ? "deck shown" : "deck hidden");
    }

    public void SetDisplayOn(bool on)
    {
        if (_displayOn == on)
            return;
        _displayOn = on;
        UpdateRunning(on ? "display on" : "display off");
    }

    /// <summary>The deck's page was dropped: the element and its GPU context go too, and come back with the next <see cref="Apply"/>.</summary>
    public void Release()
    {
        Discard("released");
        _requestKey = null;
        _create = null;
    }

    public void Dispose()
    {
        Discard("window closed");
        _requestKey = null;
        _create = null;
    }

    private void Attach(DeckBackground background)
    {
        try
        {
            FrameworkElement element;
            if (background.IsMoving)
            {
                var panel = new SKSwapChainPanel { EnableRenderLoop = false };
                panel.PaintSurface += (_, e) => OnPaint(e.Surface.Canvas, e.Info);
                _invalidate = panel.Invalidate;
                element = panel;
            }
            else
            {
                var canvas = new SKXamlCanvas();
                canvas.PaintSurface += (_, e) => OnPaint(e.Surface.Canvas, e.Info);
                _invalidate = canvas.Invalidate;
                element = canvas;
            }

            element.IsHitTestVisible = false;
            element.HorizontalAlignment = HorizontalAlignment.Left;
            element.VerticalAlignment = VerticalAlignment.Top;
            element.Loaded += (_, _) => _invalidate?.Invoke();
            _element = element;
            Resize(background);
            host.Children.Insert(0, element);
            _surfaceLogged = false;
            _clock.Reset();
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Background: could not create the surface for {background.Describe}: {ex.Message}");
            Discard("surface failed");
        }
    }

    /// <summary>The element covers the panel at the background's scale; the compositor stretches it back (see the class remarks).</summary>
    private void Resize(DeckBackground background)
    {
        double scale = background.Scale;
        _element!.Width = Math.Max(1, _screen.Width * scale);
        _element.Height = Math.Max(1, _screen.Height * scale);
        float up = (float)(1 / scale);
        ElementCompositionPreview.GetElementVisual(_element).Scale = new Vector3(up, up, 1);
    }

    private void OnPaint(SKCanvas canvas, SKImageInfo info)
    {
        var background = _background;
        canvas.Clear(_themeColor);
        if (background == null)
            return;

        try
        {
            if (!background.IsReadyFor(info.Width, info.Height))
            {
                Prepare(background, info.Width, info.Height);
                return;
            }

            background.Paint(canvas, info.Width, info.Height, _clock.Elapsed.TotalSeconds);
            if (_running)
                _frames++;
            if (!_surfaceLogged)
            {
                _surfaceLogged = true;
                string scale = background.Kind == BackgroundKind.Shader
                    ? string.Create(CultureInfo.InvariantCulture, $", scale {background.Scale}")
                    : "";
                UrDeckLog.Info($"Background: {background.Describe} from {background.Origin.Label()}, surface {info.Width}x{info.Height}{scale}, {(background.IsMoving ? "moving" : "still")}");
            }
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Background: painting {background.Describe} failed, the theme colour is shown instead: {ex.Message}");
            _dispatcher.TryEnqueue(() =>
            {
                if (ReferenceEquals(_background, background))
                    Fall(background, "painting failed");
            });
        }
    }

    /// <summary>Decodes a picture off the UI thread; the theme colour shows until the result arrives.</summary>
    private void Prepare(DeckBackground background, int width, int height)
    {
        if (_preparing)
            return;
        _preparing = true;
        _ = PrepareAsync(background, width, height);
    }

    private async Task PrepareAsync(DeckBackground background, int width, int height)
    {
        bool ok;
        try
        {
            ok = await background.PrepareAsync(width, height);
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Background: {background.Describe} could not be prepared: {ex.Message}");
            ok = false;
        }

        _dispatcher.TryEnqueue(() =>
        {
            _preparing = false;
            if (!ReferenceEquals(_background, background))
                return; // replaced meanwhile: the result is discarded with the old background
            if (ok)
                _invalidate?.Invoke();
            else
                Fall(background, "picture failed");
        });
    }

    private void UpdateRunning(string reason)
    {
        bool want = _background is { IsMoving: true } && _element != null && _visible && _displayOn;
        if (want == _running)
            return;

        if (want)
            Start(reason);
        else
            Stop(reason);
    }

    private void Start(string reason)
    {
        _running = true;
        _frames = 0;
        _next = 0;
        _run.Restart();
        _clock.Start();
        CompositionTarget.Rendering += OnRendering;
        _invalidate?.Invoke();
        UrDeckLog.Info($"Background frames started ({reason})");

        // A GPU surface that cannot be created raises nothing; say so when the first frame does not arrive.
        _firstFrameCheck ??= _dispatcher.CreateTimer();
        _firstFrameCheck.Interval = TimeSpan.FromSeconds(3);
        _firstFrameCheck.IsRepeating = false;
        _firstFrameCheck.Tick -= OnFirstFrameCheck;
        _firstFrameCheck.Tick += OnFirstFrameCheck;
        _firstFrameCheck.Start();
    }

    private void Stop(string reason)
    {
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
        _firstFrameCheck?.Stop();
        _clock.Stop();
        _run.Stop();
        UrDeckLog.Info(string.Create(CultureInfo.InvariantCulture,
            $"Background frames stopped ({reason}): {_frames} frames in {_run.Elapsed.TotalSeconds:F1} s"));
    }

    private void OnFirstFrameCheck(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (_running && _frames == 0)
            UrDeckLog.Warn("Background: no frame was painted in 3 s; the GPU surface may not have started");
    }

    private void OnRendering(object? sender, object e)
    {
        long now = Stopwatch.GetTimestamp();
        if (now < _next - (FrameInterval / 4))
            return;
        _next = Math.Max(_next + FrameInterval, now);
        _invalidate?.Invoke();
    }

    /// <summary>Stops the frames and removes and disposes the element and the background.</summary>
    private void Discard(string reason)
    {
        if (_running)
            Stop(reason);

        if (_element != null)
        {
            host.Children.Remove(_element);
            _element = null;
        }

        _invalidate = null;
        _background?.Dispose();
        _background = null;
        _preparing = false;
    }
}
