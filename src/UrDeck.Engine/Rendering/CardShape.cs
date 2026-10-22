// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// The outline of one card: a rounded rectangle (<see cref="Round"/>), or for a chamfer a path (<see cref="Path"/>) with
/// straight cuts at the chosen corners. Its bounds are the card's surface.
/// </summary>
internal sealed class CardShape : IDisposable
{
    private CardShape(SKRect bounds, SKRoundRect? round, SKPath? path)
    {
        Bounds = bounds;
        Round = round;
        Path = path;
    }

    public SKRect Bounds { get; }

    public SKRoundRect? Round { get; }

    public SKPath? Path { get; }

    /// <summary>The shape of a card of <paramref name="size"/> in <paramref name="style"/>.</summary>
    public static CardShape For(ResolvedCardStyle style, System.Drawing.Size size)
    {
        var bounds = SKRect.Create(size.Width, size.Height);
        float half = Math.Min(size.Width, size.Height) / 2f;
        if (style.Chamfer)
            return new CardShape(bounds, null, Chamfer(bounds, Math.Min(style.Cut, half), style.CutCorners));

        float radius = Math.Min(style.Radius, half);
        return new CardShape(bounds, new SKRoundRect(bounds, radius, radius), null);
    }

    /// <summary>
    /// The shape of <paramref name="style"/> on a card of <paramref name="size"/> whose corners reach <paramref name="reach"/>
    /// pixels (the radius, or the chamfer's cut, already limited to half of the real card's smaller side), as the ring
    /// picture of a moving border draws it.
    /// </summary>
    public static CardShape ForReach(ResolvedCardStyle style, float reach, System.Drawing.Size size)
    {
        var bounds = SKRect.Create(size.Width, size.Height);
        if (style.Chamfer)
            return new CardShape(bounds, null, Chamfer(bounds, reach, style.CutCorners));
        return new CardShape(bounds, new SKRoundRect(bounds, reach, reach), null);
    }

    /// <summary>
    /// A rectangle with the <paramref name="corners"/> cut by <paramref name="cut"/> along each side. The path starts in the
    /// middle of the top edge, so a dash's joint falls on an edge and not on a corner.
    /// </summary>
    public static SKPath Chamfer(SKRect r, float cut, CardCorners corners)
    {
        float tl = corners.HasFlag(CardCorners.TopLeft) ? cut : 0;
        float tr = corners.HasFlag(CardCorners.TopRight) ? cut : 0;
        float br = corners.HasFlag(CardCorners.BottomRight) ? cut : 0;
        float bl = corners.HasFlag(CardCorners.BottomLeft) ? cut : 0;

        using var path = new SKPathBuilder();
        path.MoveTo(r.MidX, r.Top);
        path.LineTo(r.Right - tr, r.Top);
        if (tr > 0)
            path.LineTo(r.Right, r.Top + tr);
        path.LineTo(r.Right, r.Bottom - br);
        if (br > 0)
            path.LineTo(r.Right - br, r.Bottom);
        path.LineTo(r.Left + bl, r.Bottom);
        if (bl > 0)
            path.LineTo(r.Left, r.Bottom - bl);
        path.LineTo(r.Left, r.Top + tl);
        if (tl > 0)
            path.LineTo(r.Left + tl, r.Top);
        path.Close();
        return path.Detach();
    }

    public void Clip(SKCanvas canvas)
    {
        if (Path != null)
            canvas.ClipPath(Path, antialias: true);
        else
            canvas.ClipRoundRect(Round!, antialias: true);
    }

    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        if (Path != null)
            canvas.DrawPath(Path, paint);
        else
            canvas.DrawRoundRect(Round!, paint);
    }

    /// <summary>
    /// The outline as a new path from the middle of the top edge, clockwise, which the caller disposes, and the distance
    /// along it of each corner: a chamfer's or a square's vertices, a rounded corner's middle.
    /// </summary>
    public SKPath ToPath(out float[] corners)
    {
        var b = Bounds;
        float r = Round?.Radii[0].X ?? 0;
        if (Path != null || r <= 0)
        {
            var polygon = Path != null ? new SKPath(Path) : Chamfer(b, 0, CardCorners.None);
            corners = VertexDistances(polygon.Points);
            return polygon;
        }

        float d = 2 * r;
        using var path = new SKPathBuilder();
        path.MoveTo(b.MidX, b.Top);
        path.LineTo(b.Right - r, b.Top);
        path.ArcTo(SKRect.Create(b.Right - d, b.Top, d, d), -90, 90, false);
        path.LineTo(b.Right, b.Bottom - r);
        path.ArcTo(SKRect.Create(b.Right - d, b.Bottom - d, d, d), 0, 90, false);
        path.LineTo(b.Left + r, b.Bottom);
        path.ArcTo(SKRect.Create(b.Left, b.Bottom - d, d, d), 90, 90, false);
        path.LineTo(b.Left, b.Top + r);
        path.ArcTo(SKRect.Create(b.Left, b.Top, d, d), 180, 90, false);
        path.Close();

        float quarter = MathF.PI * r / 2;
        float across = b.Width - d + quarter, down = b.Height - d + quarter;
        float first = b.Width / 2 - r + quarter / 2;
        corners = [first, first + down, first + down + across, first + 2 * down + across];
        return path.Detach();
    }

    /// <summary>The distance along a closed polygon of each vertex after the first (the start, mid-edge, is no corner).</summary>
    private static float[] VertexDistances(SKPoint[] points)
    {
        var result = new List<float>();
        float along = 0;
        for (int i = 1; i < points.Length; i++)
        {
            along += SKPoint.Distance(points[i - 1], points[i]);
            if (result.Count == 0 || along - result[^1] > 0.5f)
                result.Add(along);
        }
        return [.. result];
    }

    public void Dispose() => Path?.Dispose();
}
