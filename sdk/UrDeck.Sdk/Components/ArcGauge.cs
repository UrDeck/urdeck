// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// The ring, the tach and the marker gauge: an arc with the readout inside it. They differ in what is drawn on the arc (a
/// filled arc for the ring and the tach, a mark at the value for the marker; a ring of ticks, a redline and a needle for
/// the tach), so they share the geometry and the track.
/// </summary>
internal static class ArcGauge
{
    private const float DefaultStart = 135f;
    private const float DefaultSpan = 270f;

    /// <summary>The space between the track and its ticks, in thicknesses of the stroke.</summary>
    private const float TickGapRatio = 0.5f;

    /// <summary>A major tick is this many times as long as a minor one.</summary>
    private const float MajorTickScale = 1.5f;

    /// <summary>Strength of the redline while it is not lit.</summary>
    private const float DimRedline = 0.35f;

    /// <summary>The band for a label outside the arc, in sizes of the label step.</summary>
    private const float LabelBandRatio = 1.6f;

    /// <summary>The room for the numbers beside the ticks, in sizes of the label step.</summary>
    private const float TickNumberRatio = 1.3f;

    private const float TickNumberCentre = 0.75f;

    /// <summary>A tach with no ticks of the theme's own: 28 marks, a longer one every fourth, a twentieth of the diameter long.</summary>
    private const int TachTickCount = 28;
    private const int TachMajorEvery = 4;
    private const float TachTickLength = 0.05f;
    private const float TachTickThickness = 0.008f;
    private const float TachRedlineFrom = 0.85f;
    private const float CrosshairRatio = 0.12f;
    private const float MarkerMinimum = 8f;

    internal readonly record struct ArcLayout(
        SKPoint Center,
        float Radius,
        float Stroke,
        float TrackStroke,
        float Start,
        float Span,
        SKRect Readout,
        SKRect LabelBand,
        bool LabelOutside,
        GaugeTicks? Ticks);

    public static ArcLayout Measure(Theme theme, SKRect rect, bool hasLabel, GaugeStyle style)
    {
        var g = theme.Gauge;
        bool outside = hasLabel && g.LabelPosition is GaugeLabelPosition.Below or GaugeLabelPosition.Above;
        float band = outside ? theme.LabelSize * LabelBandRatio : 0f;
        var arc = rect;
        var bandRect = SKRect.Empty;
        if (outside && g.LabelPosition == GaugeLabelPosition.Below)
        {
            arc = new SKRect(rect.Left, rect.Top, rect.Right, Math.Max(rect.Top, rect.Bottom - band));
            bandRect = new SKRect(rect.Left, arc.Bottom, rect.Right, rect.Bottom);
        }
        else if (outside)
        {
            arc = new SKRect(rect.Left, Math.Min(rect.Bottom, rect.Top + band), rect.Right, rect.Bottom);
            bandRect = new SKRect(rect.Left, rect.Top, rect.Right, arc.Top);
        }

        float diameter = Math.Min(arc.Width, arc.Height);
        float stroke = diameter * theme.StrokeRatio;
        float trackStroke = stroke * (g.Track?.WidthRatio ?? 1f);
        float widest = Math.Max(stroke, trackStroke);
        var ticks = g.Ticks ?? (style == GaugeStyle.Tach
            ? new GaugeTicks(TachTickCount, TachMajorEvery, diameter * TachTickLength, Math.Max(1.5f, diameter * TachTickThickness), null, false)
            : null);
        float reserve = ticks != null
            ? ticks.Length * MajorTickScale + stroke * TickGapRatio + (ticks.Labels ? theme.LabelSize * TickNumberRatio : 0f)
            : 0f;

        float radius = diameter / 2 - reserve - widest / 2;
        float inner = Math.Max(0f, diameter / 2 - reserve - widest);
        // With ticks the circle is small; a label inside it takes the room it needs below the middle, where a ring is open.
        float labelRoom = hasLabel && !outside && reserve > 0f && (g.Sweep?.Span ?? DefaultSpan) < 360f ? theme.LabelSize * 1.2f : 0f;
        var readout = SKRect.Create(arc.MidX - inner * 0.7f, arc.MidY - inner * 0.6f, inner * 1.4f, inner * 1.2f + labelRoom);
        return new ArcLayout(
            new SKPoint(arc.MidX, arc.MidY),
            Math.Max(0f, radius),
            stroke,
            trackStroke,
            g.Sweep?.Start ?? DefaultStart,
            g.Sweep?.Span ?? DefaultSpan,
            readout,
            bandRect,
            outside,
            ticks);
    }

    public static SKRect Draw(GaugeContext c)
    {
        var layout = Measure(c.Theme, c.Rect, c.HasLabel, c.Style);
        var g = c.G;
        var oval = SKRect.Create(layout.Center.X - layout.Radius, layout.Center.Y - layout.Radius, layout.Radius * 2, layout.Radius * 2);

        DrawTrack(c, layout, oval);
        var redline = g.Redline ?? (c.Style == GaugeStyle.Tach ? new GaugeRedline(TachRedlineFrom, null, null, false) : null);
        if (redline != null)
            DrawRedline(c, layout, oval, redline, lit: false);
        if (c.Style != GaugeStyle.Marker && c.Fraction is > 0f)
            DrawFill(c, layout, oval);
        if (redline is { LightsAbove: true } && c.Current && c.Fraction is { } fraction && fraction >= redline.From)
            DrawRedline(c, layout, oval, redline, lit: true);
        if (layout.Ticks != null)
            DrawTicks(c, layout, layout.Ticks);
        DrawMarker(c, layout);
        if (g.Crosshair)
            DrawCrosshair(c, layout);

        var readout = GaugePaint.DrawReadout(c, layout.Readout, withLabel: !layout.LabelOutside);
        if (layout.LabelOutside)
            TextLine.Draw(c.Canvas, c.Theme, layout.LabelBand, c.Options.Label!, TextStep.Label, TextEmphasis.Muted);
        return readout;
    }

    private static SKStrokeCap CapOf(GaugeContext c) => c.G.Segments != null ? SKStrokeCap.Butt : c.Theme.StrokeCap;

    private static void DrawTrack(GaugeContext c, ArcLayout layout, SKRect oval)
    {
        using var paint = GaugePaint.Stroke(layout.TrackStroke, CapOf(c), c.Track);
        StrokeArc(c, layout, oval, paint, layout.Span);
    }

    /// <summary>Strokes the arc from its start for <paramref name="span"/> degrees, cut into blocks when the theme asks.</summary>
    private static void StrokeArc(GaugeContext c, ArcLayout layout, SKRect oval, SKPaint paint, float span)
    {
        if (c.G.Segments is not { } segments || layout.Radius <= 0)
        {
            c.Canvas.DrawArc(oval, layout.Start, span, false, paint);
            return;
        }

        float perPixel = 180f / (MathF.PI * layout.Radius);
        foreach (var (start, length) in GaugePaint.Blocks(layout.Span, segments.Length * perPixel, segments.Gap * perPixel, span))
            c.Canvas.DrawArc(oval, layout.Start + start, length, false, paint);
    }

    private static void DrawFill(GaugeContext c, ArcLayout layout, SKRect oval)
    {
        float span = layout.Span * c.Fraction!.Value;
        if (c.Current && c.G.Glow is { Blur: > 0 } glow)
        {
            using var halo = GaugePaint.Glow(c.Theme, glow, c.Fill, layout.Stroke, CapOf(c));
            StrokeArc(c, layout, oval, halo, span);
        }

        using var paint = GaugePaint.Stroke(layout.Stroke, CapOf(c), c.Fill);
        if (c.GradientColors is { } colors)
            paint.Shader = GradientShader(c, layout, colors);
        StrokeArc(c, layout, oval, paint, span);
    }

    /// <summary>The gradient laid over the whole sweep, so the fill's end shows the colour at its fraction.</summary>
    private static SKShader GradientShader(GaugeContext c, ArcLayout layout, IReadOnlyList<SKColor> colors) =>
        c.G.Cache.Get(("arc", c.Rect, layout.Start, layout.Span, c.G.Gradient!.Ramp), () =>
        {
            var rotation = SKMatrix.CreateRotationDegrees(layout.Start, layout.Center.X, layout.Center.Y);
            if (layout.Span >= 360f)
            {
                using var closed = SKShader.CreateSweepGradient(layout.Center, [.. colors], null, SKShaderTileMode.Clamp, 0, 360);
                return closed.WithLocalMatrix(rotation);
            }

            // Over the sweep the colours run evenly; in the gap the last colour holds to its middle and the first takes over, so the
            // round cap at the start of the arc is not tinted with the end's colour.
            float share = layout.Span / 360f;
            float middle = share + (1 - share) / 2;
            var stops = new List<SKColor>(colors);
            var positions = new List<float>();
            for (int i = 0; i < colors.Count; i++)
                positions.Add(share * i / (colors.Count - 1));
            stops.Add(colors[^1]);
            positions.Add(middle - 0.0005f);
            stops.Add(colors[0]);
            positions.Add(middle);
            stops.Add(colors[0]);
            positions.Add(1f);
            using var sweep = SKShader.CreateSweepGradient(layout.Center, [.. stops], [.. positions], SKShaderTileMode.Clamp, 0, 360);
            return sweep.WithLocalMatrix(rotation);
        });

    private static void DrawRedline(GaugeContext c, ArcLayout layout, SKRect oval, GaugeRedline redline, bool lit)
    {
        var color = redline.Color ?? c.Theme.Critical;
        float start = layout.Start + layout.Span * redline.From;
        float span = layout.Span * (1 - redline.From);
        float width = Math.Max(layout.Stroke, layout.TrackStroke);
        var cap = c.Theme.StrokeCap == SKStrokeCap.Round ? SKStrokeCap.Butt : c.Theme.StrokeCap;

        if (lit && redline.Glow is { Blur: > 0 } glow)
        {
            using var halo = GaugePaint.Glow(c.Theme, glow, color, width, cap);
            c.Canvas.DrawArc(oval, start, span, false, halo);
        }

        using var paint = GaugePaint.Stroke(width, cap, lit ? color : color.WithAlpha((byte)Math.Round(color.Alpha * DimRedline)));
        c.Canvas.DrawArc(oval, start, span, false, paint);
    }

    private static void DrawTicks(GaugeContext c, ArcLayout layout, GaugeTicks ticks)
    {
        float gap = layout.Stroke * TickGapRatio;
        float inner = layout.Radius + Math.Max(layout.Stroke, layout.TrackStroke) / 2 + gap;
        float majorLength = ticks.Length * MajorTickScale;
        var color = ticks.Color ?? c.Theme.TextMuted;
        float numberSize = c.Theme.LabelSize;
        float numberHeight = ticks.Labels ? GaugePaint.TextHeight(c.Theme, numberSize) : 0f;

        using var paint = GaugePaint.Stroke(ticks.Thickness, SKStrokeCap.Butt, color);
        for (int i = 0; i < ticks.Count; i++)
        {
            float along = layout.Span >= 360f ? i / (float)ticks.Count : i / (float)(ticks.Count - 1);
            double angle = (layout.Start + layout.Span * along) * Math.PI / 180;
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            bool major = i % ticks.MajorEvery == 0;
            float length = major ? majorLength : ticks.Length;
            c.Canvas.DrawLine(
                layout.Center.X + cos * inner, layout.Center.Y + sin * inner,
                layout.Center.X + cos * (inner + length), layout.Center.Y + sin * (inner + length), paint);

            if (ticks.Labels && major)
            {
                float at = inner + majorLength + numberSize * TickNumberCentre;
                GaugePaint.DrawText(
                    c.Canvas, c.Theme, ((int)Math.Round(100 * along)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    layout.Center.X + cos * at, layout.Center.Y + sin * at - numberHeight / 2, numberSize, color, HorizontalAlign.Center);
            }
        }
    }

    /// <summary>The marker gauge's mark at the value, and the tach's needle when the theme asks for one.</summary>
    private static void DrawMarker(GaugeContext c, ArcLayout layout)
    {
        if (c.Fraction is not { } fraction || c.Style is not (GaugeStyle.Marker or GaugeStyle.Tach))
            return;

        var marker = c.G.Marker;
        var shape = marker?.Shape ?? GaugeMarkerShape.Diamond;
        if (c.Style == GaugeStyle.Tach && shape != GaugeMarkerShape.Needle)
            return;

        float size = marker?.Size ?? Math.Max(layout.Stroke * 2.5f, MarkerMinimum);
        var color = c.Current ? marker?.Color ?? c.Fill : c.Fill;
        double angle = (layout.Start + layout.Span * fraction) * Math.PI / 180;
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);
        var tip = new SKPoint(layout.Center.X + cos * layout.Radius, layout.Center.Y + sin * layout.Radius);

        if (shape == GaugeMarkerShape.Needle)
        {
            using var line = GaugePaint.Stroke(Math.Max(2f, size * 0.25f), SKStrokeCap.Round, color);
            c.Canvas.DrawLine(layout.Center, tip, line);
            using var hub = new SKPaint { IsAntialias = true, Color = color };
            c.Canvas.DrawCircle(layout.Center, size * 0.4f, hub);
            return;
        }

        float half = size / 2;
        using var builder = new SKPathBuilder();
        builder.MoveTo(tip.X, tip.Y - half);
        builder.LineTo(tip.X + half, tip.Y);
        builder.LineTo(tip.X, tip.Y + half);
        builder.LineTo(tip.X - half, tip.Y);
        builder.Close();
        using var diamond = builder.Detach();

        if (marker?.Halo == true)
        {
            var ring = c.Theme.Background.WithAlpha(0xFF);
            using var haloFill = new SKPaint { IsAntialias = true, Color = ring };
            using var haloEdge = GaugePaint.Stroke(Math.Max(2f, half * 0.6f), SKStrokeCap.Butt, ring);
            haloEdge.StrokeJoin = SKStrokeJoin.Miter;
            c.Canvas.DrawPath(diamond, haloEdge);
            c.Canvas.DrawPath(diamond, haloFill);
        }

        using var paint = new SKPaint { IsAntialias = true, Color = color };
        c.Canvas.DrawPath(diamond, paint);
    }

    private static void DrawCrosshair(GaugeContext c, ArcLayout layout)
    {
        float arm = layout.Radius * CrosshairRatio;
        using var paint = GaugePaint.Stroke(Math.Max(1f, layout.Stroke * 0.25f), SKStrokeCap.Butt, c.Theme.TextMuted);
        c.Canvas.DrawLine(layout.Center.X - arm, layout.Center.Y, layout.Center.X + arm, layout.Center.Y, paint);
        c.Canvas.DrawLine(layout.Center.X, layout.Center.Y - arm, layout.Center.X, layout.Center.Y + arm, paint);
    }
}
