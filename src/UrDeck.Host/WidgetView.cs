// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Numerics;
using Microsoft.UI.Xaml.Hosting;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using UrDeck.Engine.Deck;

namespace UrDeck.Host;

/// <summary>
/// The surface of one <see cref="WidgetSlot"/>: its own Skia canvas (composited by the GPU, drawn by Skia on the CPU). The
/// slot decides when it is painted and what it does; the view shows it while it is in the tree and shows a press by scaling
/// and dimming its layer on the compositor.
/// </summary>
internal sealed class WidgetView : SKXamlCanvas, IDisposable
{
    private static readonly TimeSpan PressTime = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan ReleaseTime = TimeSpan.FromMilliseconds(160);

    // Cleared on Dispose: WinUI can keep a removed canvas alive for a while, and it must not keep the slot (and its plugin) alive with it.
    private WidgetSlot? _slot;

    public WidgetView(WidgetSlot slot)
    {
        _slot = slot;
        PaintSurface += OnPaintSurface;
        slot.PaintRequested += Invalidate;
        slot.PressedChanged += OnPressedChanged;
        Loaded += (_, _) => _slot?.Show();
        Unloaded += (_, _) => _slot?.Hide();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_slot is not { HasWidget: true } slot)
            return;
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        slot.Paint(canvas, new System.Drawing.Size(e.Info.Width, e.Info.Height));
    }

    /// <summary>
    /// Shows the card as pressed, or back at rest: the compositor scales and dims this view's layer to the theme's press
    /// values. Nothing is painted and the frame clock is not involved.
    /// </summary>
    private void OnPressedChanged(bool pressed)
    {
        if (_slot is not { } slot)
            return;
        if (pressed)
            AnimatePress(slot.Press.Scale, slot.Press.Opacity, PressTime);
        else
            AnimatePress(1f, 1f, ReleaseTime);
    }

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

    /// <summary>Lets go of the slot (which its page disposes right after) and of the border sprite, if the page gave this card one.</summary>
    public void Dispose()
    {
        PaintSurface -= OnPaintSurface;
        if (_slot != null)
        {
            _slot.PaintRequested -= Invalidate;
            _slot.PressedChanged -= OnPressedChanged;
            _slot = null;
        }

        // A removed canvas can stay alive for a while.
        ElementCompositionPreview.SetElementChildVisual(this, null);
    }
}
