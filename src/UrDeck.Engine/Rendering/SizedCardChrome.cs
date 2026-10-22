// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// What a card of one size, tint and strip colour needs in a <see cref="ResolvedCardStyle"/> that is not plain: its shape,
/// the paints and shaders, the fitted dash, and, when the back has a gradient, a sheen or an accent strip, the back drawn once
/// into an image. Made by <see cref="ResolvedCardStyle.For"/> and released with the style.
/// </summary>
internal sealed class SizedCardChrome : IDisposable
{
    // Owned here: a paint does not dispose what is set on it.
    private readonly List<IDisposable> _owned = [];

    // A dashed border is stroked along this path (from the middle of the top edge); null for a solid one.
    private readonly SKPath? _dashOutline;

    // The sheen, the strip and its glow, drawn into the back image; null when the style has none.
    private readonly SKPaint? _sheen;
    private readonly SKPaint? _strip;
    private readonly SKPaint? _stripGlow;
    private readonly SKRect _stripRect;

    // The highlight: each line's outline, already clipped to the shape, and its paint with the fading shader.
    private readonly (SKPath Line, SKPaint Paint)? _highlightTop;
    private readonly (SKPath Line, SKPaint Paint)? _highlightBottom;

    public SizedCardChrome(ResolvedCardStyle style, int tintSlot, int stripSlot, System.Drawing.Size size)
    {
        Shape = CardShape.For(style, size);
        Fill = new SKPaint { Color = style.FillFor(tintSlot), IsAntialias = true };
        BorderPaint = MakeBorder(style, Shape, _owned, out _dashOutline);
        var bounds = Shape.Bounds;

        if (style.FillGradient is { } gradient)
        {
            // A paint's alpha multiplies its shader: the gradient's own colours must come through.
            Fill.Color = SKColors.Black;
            Fill.Shader = Own(Linear(bounds, gradient, style.FillAngle), _owned);
        }

        if (style.Sheen is { } sheen)
        {
            _sheen = Own(new SKPaint { Color = SKColors.Black, IsAntialias = true }, _owned);
            _sheen.Shader = Own(SKShader.CreateRadialGradient(
                new SKPoint(sheen.X * bounds.Width, sheen.Y * bounds.Height),
                MathF.Max(1, sheen.Size * bounds.Width),
                [sheen.Color, sheen.Color.WithAlpha(0)],
                SKShaderTileMode.Clamp), _owned);
        }

        if (style.Strip is { } strip)
        {
            var color = strip.Colors[stripSlot];
            _stripRect = strip.Rect(bounds.Width, bounds.Height);
            _strip = Own(new SKPaint { Color = color, IsAntialias = true }, _owned);
            if (strip.Fade)
            {
                // From the start of its length (the left, or the top on a vertical side) to transparent at its end.
                var end = strip.Side is CardSide.Top or CardSide.Bottom
                    ? new SKPoint(_stripRect.Right, _stripRect.Top)
                    : new SKPoint(_stripRect.Left, _stripRect.Bottom);
                _strip.Color = SKColors.Black;
                _strip.Shader = Own(SKShader.CreateLinearGradient(
                    new SKPoint(_stripRect.Left, _stripRect.Top), end, [color, color.WithAlpha(0)], SKShaderTileMode.Clamp), _owned);
            }
            if (strip.GlowBlur > 0)
            {
                var glow = strip.GlowColor ?? color;
                _stripGlow = Own(new SKPaint
                {
                    Color = glow.WithAlpha((byte)Math.Round(glow.Alpha * Math.Clamp(strip.GlowAlpha, 0, 1))),
                    IsAntialias = true,
                    MaskFilter = Own(SKMaskFilter.CreateBlur(SKBlurStyle.Normal, strip.GlowBlur / 2), _owned),
                }, _owned);
            }
        }

        if (style.Highlight is { } highlight)
        {
            float corner = Shape.Round?.Radii[0].Y ?? Math.Min(style.Cut, Math.Min(bounds.Width, bounds.Height) / 2);
            float band = corner + highlight.Thickness;
            _highlightTop = MakeHighlight(Shape, highlight.Top, highlight.Thickness,
                SKRect.Create(bounds.Left, bounds.Top, bounds.Width, band), _owned);
            _highlightBottom = MakeHighlight(Shape, highlight.Bottom, highlight.Thickness,
                SKRect.Create(bounds.Left, bounds.Bottom - band, bounds.Width, band), _owned);
        }

        if (style.Marks is { ReachesTheCard: true } marks)
        {
            Marks = marks;
            MarkPaint = Own(CardChrome.MarkPaint(marks), _owned);
        }

        // A gradient, a sheen or a strip's glow over the card costs several times a repaint in software; drawn once, it is a
        // blit (docs/perf/card-chrome.md). A solid back without them is cheaper drawn than blitted.
        if (style.FillGradient != null || _sheen != null || _strip != null)
            BackImage = DrawBackImage(size);
    }

    public CardShape Shape { get; }

    /// <summary>The card's fill: a colour, or the gradient the back image was drawn with.</summary>
    public SKPaint Fill { get; }

    /// <summary>The border, stroked twice its width under the shape's clip; null when there is none.</summary>
    public SKPaint? BorderPaint { get; }

    /// <summary>The fill and the border, drawn once; null for a solid fill.</summary>
    public SKImage? BackImage { get; }

    public bool HasBackImage => BackImage != null;

    /// <summary>The marks the card's surface draws (some of each lies on the card); null for none.</summary>
    public CardMarks? Marks { get; }

    /// <summary>The paint of <see cref="Marks"/>.</summary>
    public SKPaint? MarkPaint { get; }

    public bool HasHighlight => _highlightTop != null || _highlightBottom != null;

    /// <summary>Fill, sheen, strip (its glow first) and border, under the shape's clip, which the caller has set.</summary>
    public void DrawBackPieces(SKCanvas canvas)
    {
        canvas.DrawPaint(Fill);
        if (_sheen != null)
            canvas.DrawPaint(_sheen);
        if (_stripGlow != null)
            canvas.DrawRect(_stripRect, _stripGlow);
        if (_strip != null)
            canvas.DrawRect(_stripRect, _strip);
        if (BorderPaint == null)
            return;
        if (_dashOutline != null)
            canvas.DrawPath(_dashOutline, BorderPaint);
        else
            Shape.Draw(canvas, BorderPaint);
    }

    /// <summary>The top and bottom highlights; they lie within the shape, so they need no clip.</summary>
    public void DrawHighlights(SKCanvas canvas)
    {
        if (_highlightTop is { } top)
            canvas.DrawPath(top.Line, top.Paint);
        if (_highlightBottom is { } bottom)
            canvas.DrawPath(bottom.Line, bottom.Paint);
    }

    /// <summary>
    /// One highlight: the shape's outline stroked twice the line's thickness, cut to the shape (so the inner half is left)
    /// and to <paramref name="band"/>, as tall as the corner plus the line, so it follows the corner. Made once per size:
    /// stroked under the shape's clip on every paint, Prism's two cost twice as much (docs/perf/card-chrome.md).
    /// </summary>
    private static (SKPath, SKPaint)? MakeHighlight(CardShape shape, SKColor? color, float thickness, SKRect band, List<IDisposable> owned)
    {
        if (color is not { } c || thickness <= 0)
            return null;
        using var outline = shape.ToPath(out _);
        using var stroke = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = thickness * 2 };
        using var stroked = stroke.GetFillPath(outline);
        using var inside = stroked.Op(outline, SKPathOp.Intersect);
        using var bandBuilder = new SKPathBuilder();
        bandBuilder.AddRect(band);
        using var bandPath = bandBuilder.Detach();
        var line = Own(inside.Op(bandPath, SKPathOp.Intersect), owned);

        // Transparent at both ends, full over all but the first and the last eighth of the width.
        var paint = Own(new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Shader = Own(SKShader.CreateLinearGradient(
                new SKPoint(band.Left, 0), new SKPoint(band.Right, 0),
                [c.WithAlpha(0), c, c, c.WithAlpha(0)], [0, 0.125f, 0.875f, 1], SKShaderTileMode.Clamp), owned),
        }, owned);
        return (line, paint);
    }

    private SKImage DrawBackImage(System.Drawing.Size size)
    {
        var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        Shape.Clip(canvas);
        DrawBackPieces(canvas);
        return surface.Snapshot();
    }

    private static T Own<T>(T item, List<IDisposable> owned)
        where T : IDisposable
    {
        owned.Add(item);
        return item;
    }

    private static SKPaint? MakeBorder(ResolvedCardStyle style, CardShape shape, List<IDisposable> owned, out SKPath? dashOutline)
    {
        dashOutline = null;
        if (!style.HasBorderStyle || style.BorderWidth <= 0 || (style.BorderGradient == null && style.Border.Alpha == 0))
            return null;

        // Twice as wide, half of it clipped away by the shape: the border lies inside the edge for any shape.
        var paint = new SKPaint
        {
            Color = style.Border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = style.BorderWidth * 2,
        };
        var bounds = shape.Bounds;
        if (style.BorderGradient is { } colors)
        {
            paint.Color = SKColors.Black;
            paint.Shader = Own(style.BorderConic
                ? SKShader.CreateSweepGradient(new SKPoint(bounds.MidX, bounds.MidY), colors)
                : Linear(bounds, colors, style.BorderAngle), owned);
        }

        if (style.Dashed && style.DashOn > 0 && style.DashOff > 0)
        {
            // Stroked along this outline, not the shape: the dash pattern is laid out from the outline's own start.
            var outline = Own(shape.ToPath(out float[] corners), owned);
            using var measure = new SKPathMeasure(outline, forceClosed: true);
            var (intervals, phase) = FitDash(measure.Length, corners, style.DashOn, style.DashOff);
            paint.PathEffect = Own(SKPathEffect.CreateDash(intervals, phase), owned);
            dashOutline = outline;
        }
        return paint;
    }

    /// <summary>
    /// The dash pattern for an outline of <paramref name="length"/> with <paramref name="corners"/> at those distances along
    /// it: on each side between two corners the dash and gap are scaled by one factor so that a whole number of periods,
    /// at least one, fills the side, and a dash is centred on every corner, so all corners look alike and none shows a
    /// short dash or a long gap. Returns the intervals for <see cref="SKPathEffect.CreateDash"/> and the phase that puts
    /// the pattern's start where the outline starts.
    /// </summary>
    internal static (float[] Intervals, float Phase) FitDash(float length, IReadOnlyList<float> corners, float on, float off)
    {
        float period = on + off;
        if (length <= 0 || period <= 0)
            return ([on, off], 0);
        if (corners.Count == 0)
        {
            float k = length / (MathF.Max(1, MathF.Round(length / period)) * period);
            return ([on * k, off * k], 0);
        }

        int count = corners.Count;
        float[] ons = new float[count];
        float[] offs = new float[count];
        int[] periods = new int[count];
        for (int i = 0; i < count; i++)
        {
            float side = i < count - 1 ? corners[i + 1] - corners[i] : length - corners[i] + corners[0];
            periods[i] = (int)MathF.Max(1, MathF.Round(side / period));
            float k = side / (periods[i] * period);
            ons[i] = on * k;
            offs[i] = off * k;
        }

        // The pattern starts half a dash before the first corner: each corner's dash is the end of one side's last dash
        // and the start of the next side's first.
        var intervals = new List<float>();
        for (int i = 0; i < count; i++)
        {
            intervals.Add(ons[(i + count - 1) % count] / 2 + ons[i] / 2);
            intervals.Add(offs[i]);
            for (int n = 1; n < periods[i]; n++)
            {
                intervals.Add(ons[i]);
                intervals.Add(offs[i]);
            }
        }

        float start = corners[0] - ons[count - 1] / 2;
        float phase = ((-start % length) + length) % length;
        return ([.. intervals], phase);
    }

    /// <summary>A linear gradient across <paramref name="r"/> at an angle (0 left to right, 90 top to bottom), evenly spaced.</summary>
    internal static SKShader Linear(SKRect r, SKColor[] colors, float angleDegrees)
    {
        var (start, end) = LinearPoints(r, angleDegrees);
        return SKShader.CreateLinearGradient(start, end, colors, SKShaderTileMode.Clamp);
    }

    /// <summary>The end points <see cref="Linear"/> draws between; the host's moving border places its gradient with them too.</summary>
    internal static (SKPoint Start, SKPoint End) LinearPoints(SKRect r, float angleDegrees)
    {
        float a = angleDegrees * MathF.PI / 180;
        float dx = MathF.Cos(a), dy = MathF.Sin(a);
        float half = (MathF.Abs(dx) * r.Width + MathF.Abs(dy) * r.Height) / 2;
        var centre = new SKPoint(r.MidX, r.MidY);
        return (new SKPoint(centre.X - dx * half, centre.Y - dy * half), new SKPoint(centre.X + dx * half, centre.Y + dy * half));
    }

    public void Dispose()
    {
        BackImage?.Dispose();
        Fill.Dispose();
        BorderPaint?.Dispose();
        foreach (var item in _owned)
            item.Dispose();
        Shape.Dispose();
    }
}
