// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// The moment of one frame in a run of frames: the seconds since the first frame, and the motion level the deck would run
/// at (the effective one, with the theme's ceiling). <see cref="PageRenderer"/> draws the background and the animated card
/// border at it; without one it draws them still.
/// </summary>
public readonly record struct FrameTime(double ElapsedSeconds, MotionLevel Level);

/// <summary>
/// An animated card border at one moment, as the host's compositor shows it (<c>BorderMotion</c>): a <c>spin</c> turns the
/// conic source about the card's centre at <see cref="ConicScale"/>, a <c>pulse</c> fades the border, both in steps of
/// <see cref="StepsPerSecond"/>, and the ring is the mask. The host's sprite and this drawing share these numbers, the conic
/// source and the ring's shape, so the two look alike; they are not equal byte for byte.
/// </summary>
public static class BorderFrame
{
    /// <summary>The steps per second of the border's motion; the owner watched 15 and 30 on the panel and kept 15 (docs/perf/theme-look.md).</summary>
    public const int StepsPerSecond = 15;

    /// <summary>The border pulses between full strength and this.</summary>
    public const float PulseLow = 0.55f;

    /// <summary>The turning picture is scaled up until its square always covers the card, even a long one (width x 1.5 covers the diagonal).</summary>
    public const float ConicScale = 1.5f;

    /// <summary>
    /// The border of <paramref name="style"/> at <paramref name="motion"/>: the angle of a spin and the opacity of a pulse. Null
    /// when the border is drawn still: no animated border, no frame time, a level below <c>full</c>, or the first step of the
    /// cycle, which is the still border itself (so a run's first frame equals the still picture).
    /// </summary>
    public static BorderStep? StepAt(ResolvedCardStyle style, FrameTime? motion)
    {
        if (motion is not { Level: MotionLevel.Full } frame || style.BorderAnimation is not { } animation)
            return null;

        int steps = Math.Max(1, (int)Math.Round(animation.PeriodSeconds * StepsPerSecond));
        // A frame time is a sum of fractions: a hair under a whole step must not lose it.
        long step = (long)Math.Floor((frame.ElapsedSeconds * steps / animation.PeriodSeconds) + 1e-6);
        int inCycle = (int)(((step % steps) + steps) % steps);
        if (inCycle == 0)
            return null;

        if (animation.Mode == BorderAnimationMode.Spin)
            return new BorderStep(360f * inCycle / steps, 1f);

        // Down and back up, each half of the period in its own steps, as the host's step easing does.
        int half = Math.Max(1, steps / 2);
        double progress = inCycle / (double)steps;
        float opacity = progress < 0.5
            ? 1f - ((1f - PulseLow) * (float)(Math.Floor(progress * 2 * half) / half))
            : PulseLow + ((1f - PulseLow) * (float)(Math.Floor((progress - 0.5) * 2 * half) / half));
        return opacity >= 1f ? null : new BorderStep(0, opacity);
    }

    /// <summary>
    /// Draws the moving border of <paramref name="style"/> over a card of <paramref name="size"/> whose back was drawn without
    /// it (<see cref="ResolvedCardStyle.WithoutBorder"/>); the canvas's origin is the card's top left.
    /// </summary>
    public static void Draw(SKCanvas canvas, ResolvedCardStyle style, System.Drawing.Size size, BorderStep step)
    {
        if (style.FrameRingImage(size) is not { } ring)
            return;

        var bounds = SKRect.Create(size.Width, size.Height);
        using var layer = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * Math.Clamp(step.Opacity, 0, 1))) };
        int save = canvas.SaveLayer(bounds, layer);
        if (style.FrameConicImage() is { } conic)
        {
            // UniformToFill puts the square over the card's longer side; the compositor then scales and turns it about the centre.
            float side = Math.Max(size.Width, size.Height) * ConicScale;
            int turn = canvas.Save();
            canvas.Translate(size.Width / 2f, size.Height / 2f);
            canvas.RotateDegrees(step.Angle);
            canvas.DrawImage(conic, SKRect.Create(-side / 2, -side / 2, side, side), new SKSamplingOptions(SKFilterMode.Linear));
            canvas.RestoreToCount(turn);
        }
        else if (style.BorderBrush(size) is { } brush)
        {
            using var paint = new SKPaint { Color = brush.Colors[0] };
            if (!brush.IsSolid)
            {
                paint.Color = SKColors.Black;
                paint.Shader = SKShader.CreateLinearGradient(brush.Start, brush.End, brush.Colors, SKShaderTileMode.Clamp);
            }

            canvas.DrawRect(bounds, paint);
            paint.Shader?.Dispose();
        }

        using var mask = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.DrawImage(ring, 0, 0, SKSamplingOptions.Default, mask);
        canvas.RestoreToCount(save);
    }
}

/// <summary>A moving border at one step: the conic source's angle in degrees (clockwise) and the border's opacity.</summary>
public readonly record struct BorderStep(float Angle, float Opacity);
