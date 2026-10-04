// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Sdk;

namespace UrDeck.Host;

/// <summary>
/// Hosts one widget instance in its own Skia surface (composited by the GPU, drawn by Skia on the CPU), driven by the widget's declared refresh policy.
/// Widgets without a timer-based policy (OnEvent) render once and then only on explicit invalidation.
/// </summary>
internal sealed class WidgetView : SKXamlCanvas, IDisposable
{
    // Cleared on Dispose: WinUI can keep a removed canvas alive for a while, and it must not keep the plugin alive with it.
    private IWidget? _widget;
    private readonly Theme _theme;
    private readonly DispatcherQueueTimer? _timer;
    private readonly CancellationTokenSource _cts = new();
    private bool _updating;
    private bool _hasPainted;

    public WidgetView(IWidget widget, WidgetDescriptor descriptor, Theme theme)
    {
        _widget = widget;
        _theme = theme;
        PaintSurface += OnPaintSurface;

        if (descriptor.RefreshInterval is { } interval && interval > TimeSpan.Zero)
        {
            _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _timer.Interval = interval;
            _timer.IsRepeating = true;
            _timer.Tick += (_, _) => _ = RefreshAsync();
        }

        Loaded += (_, _) =>
        {
            _ = RefreshAsync();
            _timer?.Start();
        };
        Unloaded += (_, _) => _timer?.Stop();
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
    }

    public void Dispose()
    {
        _timer?.Stop();
        _cts.Cancel();
        _cts.Dispose();
        PaintSurface -= OnPaintSurface;
        (_widget as IDisposable)?.Dispose();
        _widget = null;
    }
}
