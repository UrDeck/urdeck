// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// The vertical bar under a theme that sets gauge values: the rectangle is the track, filled from the bottom, with the readout
/// over it. It takes the theme's colours (ramp, gradient, track colour, value tint) and nothing that needs a line or an arc.
/// </summary>
internal static class VerticalBarGauge
{
    private const float ReadoutShare = 0.45f;

    public static SKRect ReadoutRect(Theme theme, SKRect rect)
    {
        float inset = BarGauge.Thickness(theme, rect);
        var inside = SKRect.Inflate(rect, -inset, -inset);
        float height = inside.Height * ReadoutShare;
        return new SKRect(inside.Left, inside.Bottom - height, inside.Right, inside.Bottom);
    }

    public static SKRect Draw(GaugeContext c)
    {
        var rect = c.Rect;
        var readout = ReadoutRect(c.Theme, rect);
        float radius = Math.Min(c.Theme.CardRadius, Math.Min(rect.Width, rect.Height) / 2);
        using var shape = new SKRoundRect(rect, radius);
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = c.Track };
        c.Canvas.DrawRoundRect(shape, paint);

        if (c.Fraction is > 0f)
        {
            c.Canvas.Save();
            c.Canvas.ClipRoundRect(shape, SKClipOperation.Intersect, true);
            paint.Color = c.Fill;
            if (c.GradientColors is { } colors)
                paint.Shader = c.G.Cache.Get(("vertical", rect, c.G.Gradient!.Ramp), () =>
                    SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Bottom), new SKPoint(rect.Left, rect.Top), [.. colors], null, SKShaderTileMode.Clamp));
            c.Canvas.DrawRect(new SKRect(rect.Left, rect.Bottom - rect.Height * c.Fraction.Value, rect.Right, rect.Bottom), paint);
            c.Canvas.Restore();
        }

        float? target = c.Options.TargetFraction ?? c.Options.Fraction;
        if (target is { } t && rect.Bottom - rect.Height * Math.Clamp(t, 0f, 1f) <= readout.MidY)
        {
            // Over the fill the text takes whichever of the theme's text and background colours contrasts more with it.
            var ink = ColorMath.MoreContrasting(c.Fill, c.Theme.Text, c.Theme.Background);
            return Readout.Draw(c.Canvas, c.Theme, readout, c.Reading.Value, GaugePaint.ReadoutOptionsFor(c.Reading, c.Options, ink));
        }

        return GaugePaint.DrawReadout(c, readout);
    }
}
