// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;
using SkiaSharp.Skottie;

namespace UrDeck.Sdk.Components;

/// <summary>
/// A Lottie animation a widget owns: create it from a stream, draw it at a time, dispose it. It is the one component that
/// holds state, so it is used on the UI thread only and released by the widget that created it. No type of the library it
/// is built on is public.
/// </summary>
public sealed class LottieAnimation : IDisposable
{
    private Animation? _animation;

    private LottieAnimation(Animation animation, TimeSpan duration, SKSize size)
    {
        _animation = animation;
        Duration = duration;
        Size = size;
    }

    /// <summary>The length of one loop.</summary>
    public TimeSpan Duration { get; }

    /// <summary>The animation's own size; its aspect ratio decides how it fits a rectangle.</summary>
    private SKSize Size { get; }

    /// <summary>Reads an animation (Lottie JSON). Returns false, and never throws, when the stream is not one.</summary>
    public static bool TryCreate(Stream stream, out LottieAnimation? animation)
    {
        animation = null;
        try
        {
            using var data = SKData.Create(stream);
            if (data == null || !Animation.TryCreate(data, out var parsed) || parsed == null)
                return false;

            // A file with no duration or size cannot be drawn into a rectangle.
            if (parsed.Duration <= TimeSpan.Zero || parsed.Size.Width <= 0 || parsed.Size.Height <= 0)
            {
                parsed.Dispose();
                return false;
            }

            animation = new LottieAnimation(parsed, parsed.Duration, parsed.Size);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Draws the frame at <paramref name="seconds"/> from the start into <paramref name="rect"/>, scaled to fit with its
    /// aspect ratio kept and centred. A time before the start or after the end is clamped; every call stands alone.
    /// </summary>
    public void Draw(SKCanvas canvas, SKRect rect, double seconds)
    {
        ObjectDisposedException.ThrowIf(_animation == null, this);
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        double clamped = double.IsNaN(seconds) ? 0 : Math.Clamp(seconds, 0, Duration.TotalSeconds);
        float scale = Math.Min(rect.Width / Size.Width, rect.Height / Size.Height);
        float width = Size.Width * scale;
        float height = Size.Height * scale;
        var fit = SKRect.Create(rect.MidX - width / 2, rect.MidY - height / 2, width, height);

        _animation.SeekFrameTime(clamped);
        canvas.Save();
        canvas.ClipRect(rect);
        _animation.Render(canvas, fit);
        canvas.Restore();
    }

    public void Dispose()
    {
        _animation?.Dispose();
        _animation = null;
    }
}
