// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using System.Globalization;
using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// The theme's page pattern (<c>pattern</c>) resolved to whole pixels and drawn once as a block of whole repeats. The host lays
/// the block out as compositor tiles under the cards; <see cref="PageRenderer"/> shades the canvas with the same image, so a
/// snapshot shows what the panel shows. Made by <see cref="Create"/>, kept by the host while its <see cref="Key"/> is the same.
/// </summary>
public sealed class PatternTile : IDisposable
{
    /// <summary>The block is at least this many pixels along an axis (when the window is that big).</summary>
    internal const int MinBlock = 512;

    /// <summary>A pattern with no colour takes the muted text colour with its alpha divided by this.</summary>
    internal const int DefaultAlphaDivisor = 16;

    private const double DefaultSpacing = 0.1;
    private const double DefaultThickness = 0.01;
    private const double AxisSnapDegrees = 1;

    private byte[]? _png;

    private PatternTile(string kind, SKImage image, int repeatX, int repeatY, string key, TimeSpan buildTime)
    {
        Kind = kind;
        Image = image;
        RepeatX = repeatX;
        RepeatY = repeatY;
        Key = key;
        BuildTime = buildTime;
    }

    /// <summary><c>dots</c>, <c>grid</c>, <c>hatch</c> or <c>scanlines</c>.</summary>
    public string Kind { get; }

    /// <summary>The block, premultiplied, in the platform colour type.</summary>
    public SKImage Image { get; }

    public int PixelWidth => Image.Width;

    public int PixelHeight => Image.Height;

    /// <summary>The pattern's period in x and y, in whole pixels.</summary>
    public int RepeatX { get; }

    public int RepeatY { get; }

    /// <summary>Every resolved value and both sizes: the same key means the same pixels.</summary>
    public string Key { get; }

    /// <summary>How long the drawing took.</summary>
    public TimeSpan BuildTime { get; }

    /// <summary>The host's decoded copy of <see cref="Png"/>, released with the tile.</summary>
    public IDisposable? HostSurface { get; set; }

    /// <summary>The block, encoded on first use (the host decodes it into a <c>LoadedImageSurface</c>).</summary>
    public byte[] Png => _png ??= EncodePng();

    /// <summary>The tiles that cover a window of <paramref name="windowPx"/>, columns and rows.</summary>
    public (int Columns, int Rows) TileCount(System.Drawing.Size windowPx) =>
        ((windowPx.Width + PixelWidth - 1) / PixelWidth, (windowPx.Height + PixelHeight - 1) / PixelHeight);

    /// <summary>
    /// Resolves the theme's pattern for a grid cell of <paramref name="cellPx"/> physical pixels and a window of
    /// <paramref name="windowPx"/>, and draws its block; null when the theme has no pattern or its kind is <c>none</c>.
    /// </summary>
    public static PatternTile? Create(LoadedTheme loaded, float cellPx, System.Drawing.Size windowPx)
    {
        var p = loaded.Definition.Pattern;
        string? kind = p?.Kind?.ToLowerInvariant();
        if (p == null || kind is null or "none" || windowPx.Width < 1 || windowPx.Height < 1)
            return null;

        long started = Stopwatch.GetTimestamp();
        int spacing = Math.Max(2, (int)Math.Round((p.Spacing ?? DefaultSpacing) * cellPx));
        double thickness = (p.Thickness ?? DefaultThickness) * cellPx;
        int lineWidth = Math.Max(1, (int)Math.Round(thickness));
        float dotRadius = (float)Math.Max(0.5, thickness);
        var color = p.Color != null ? SKColor.Parse(p.Color) : Muted(loaded);
        int every = kind == "grid" ? p.MajorEvery ?? 0 : 0;
        var majorColor = every >= 2 && p.Major?.Color != null ? SKColor.Parse(p.Major.Color) : color;
        int majorWidth = every >= 2 && p.Major?.Thickness != null ? Math.Max(1, (int)Math.Round(p.Major.Thickness.Value * cellPx)) : lineWidth;

        // Where the pattern repeats: whole pixels in each direction. A hatch near an axis is that axis's line pattern.
        double angle = kind == "hatch" ? p.Angle ?? 45 : 0;
        HatchGeometry? hatch = null;
        if (kind == "hatch")
        {
            double a = ((angle % 180) + 180) % 180;
            if (a < AxisSnapDegrees || a > 180 - AxisSnapDegrees)
                kind = "scanlines";
            else if (Math.Abs(a - 90) < AxisSnapDegrees)
                kind = "verticals";
            else
                hatch = HatchGeometry.Resolve((p.Spacing ?? DefaultSpacing) * cellPx, angle, windowPx); // the periods are rounded, not the spacing
        }

        (int repeatX, int repeatY) = kind switch
        {
            "dots" => (spacing, spacing),
            "grid" => every >= 2 ? (spacing * every, spacing * every) : (spacing, spacing),
            "scanlines" => (1, spacing),
            "verticals" => (spacing, 1),
            _ => (hatch!.Value.PeriodX, hatch.Value.PeriodY),
        };

        int width = BlockSize(repeatX, windowPx.Width), height = BlockSize(repeatY, windowPx.Height);
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = color, IsAntialias = false, Style = SKPaintStyle.Fill };
        switch (kind)
        {
            case "dots":
                paint.IsAntialias = true;
                for (int y = 0; y * spacing < height; y++)
                    for (int x = 0; x * spacing < width; x++)
                        canvas.DrawCircle((x + 0.5f) * spacing, (y + 0.5f) * spacing, dotRadius, paint);
                break;
            case "grid":
                DrawGrid(canvas, paint, width, height, spacing, every, lineWidth, majorWidth, color, majorColor);
                break;
            case "scanlines":
                for (int y = 0; y * spacing < height; y++)
                    canvas.DrawRect(0, Line(y, spacing, lineWidth), width, lineWidth, paint);
                break;
            case "verticals":
                for (int x = 0; x * spacing < width; x++)
                    canvas.DrawRect(Line(x, spacing, lineWidth), 0, lineWidth, height, paint);
                break;
            default:
                DrawHatch(canvas, paint, width, height, hatch!.Value, lineWidth);
                break;
        }

        canvas.Flush();
        string key = string.Create(CultureInfo.InvariantCulture,
            $"{kind}|s{spacing}|w{lineWidth}|r{dotRadius}|c{color}|e{every}|mw{majorWidth}|mc{majorColor}|a{hatch?.PeriodX}x{hatch?.PeriodY}|block{width}x{height}|win{windowPx.Width}x{windowPx.Height}");
        return new PatternTile(kind is "verticals" ? "hatch" : kind, surface.Snapshot(), repeatX, repeatY, key, Stopwatch.GetElapsedTime(started));
    }

    /// <summary>The muted text colour at a sixteenth of its alpha.</summary>
    internal static SKColor Muted(LoadedTheme loaded)
    {
        var muted = SKColor.Parse(loaded.Definition.Colors!.TextMuted);
        return muted.WithAlpha((byte)Math.Max(1, muted.Alpha / DefaultAlphaDivisor));
    }

    /// <summary>The smallest whole multiple of <paramref name="repeat"/> of at least <see cref="MinBlock"/>, never past the window.</summary>
    internal static int BlockSize(int repeat, int window)
    {
        long block = (long)(MinBlock + repeat - 1) / repeat * repeat;
        return (int)Math.Min(block, window);
    }

    /// <summary>The start of the line of width <paramref name="width"/> centred half a period past mark <paramref name="index"/>.</summary>
    private static float Line(int index, int period, int width) => (float)Math.Round((index + 0.5) * period - width / 2.0);

    private static void DrawGrid(SKCanvas canvas, SKPaint paint, int width, int height, int spacing, int every, int minor, int major, SKColor minorColor, SKColor majorColor)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            // Minor lines first so that a major line lies over a crossing.
            bool majors = pass == 1;
            int lineWidth = majors ? major : minor;
            paint.Color = majors ? majorColor : minorColor;
            for (int i = 0; i * spacing < height; i++)
                if (majors == (every >= 2 && i % every == 0))
                    canvas.DrawRect(0, Line(i, spacing, lineWidth), width, lineWidth, paint);
            for (int i = 0; i * spacing < width; i++)
                if (majors == (every >= 2 && i % every == 0))
                    canvas.DrawRect(Line(i, spacing, lineWidth), 0, lineWidth, height, paint);
        }
    }

    private static void DrawHatch(SKCanvas canvas, SKPaint paint, int width, int height, HatchGeometry hatch, int lineWidth)
    {
        paint.IsAntialias = true;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = lineWidth;
        // One line passes through the block's centre; the others are a horizontal step apart. Each runs past both edges.
        float cx = width / 2f, cy = height / 2f;
        double length = Math.Sqrt(hatch.DirX * hatch.DirX + hatch.DirY * hatch.DirY);
        double ux = hatch.DirX / length, uy = -hatch.DirY / length;
        double reach = width + height;
        int count = (int)Math.Ceiling((width / 2.0 + height / 2.0 * Math.Abs(hatch.DirX / hatch.DirY)) / hatch.StepX) + 1;
        for (int k = -count; k <= count; k++)
        {
            double ox = cx + k * hatch.StepX;
            canvas.DrawLine((float)(ox - ux * reach), (float)(cy - uy * reach), (float)(ox + ux * reach), (float)(cy + uy * reach), paint);
        }
    }

    private byte[] EncodePng()
    {
        using var data = Image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public void Dispose()
    {
        HostSurface?.Dispose();
        HostSurface = null;
        Image.Dispose();
    }

    /// <summary>
    /// A hatch's periods and line direction (<see cref="DirX"/> right, <see cref="DirY"/> up). The periods are whole in each
    /// direction that repeats and the angle follows from them; in a direction the window is too small to repeat, the
    /// other period is kept whole and the angle is exact.
    /// </summary>
    internal readonly record struct HatchGeometry(int PeriodX, int PeriodY, double StepX, double DirX, double DirY)
    {
        public static HatchGeometry Resolve(double spacing, double angleDegrees, System.Drawing.Size window)
        {
            double theta = angleDegrees * Math.PI / 180;
            double sin = Math.Abs(Math.Sin(theta)), cos = Math.Abs(Math.Cos(theta));
            double tan = sin / cos;
            double sign = Math.Tan(theta) < 0 ? -1 : 1;
            int px = Math.Max(1, (int)Math.Round(spacing / sin)), py = Math.Max(1, (int)Math.Round(spacing / cos));
            if (px > window.Width && py <= window.Height)
            {
                double step = py / tan;
                return new HatchGeometry(px, py, step, step, sign * py);
            }

            if (py > window.Height && px <= window.Width)
                return new HatchGeometry(px, py, px, px, sign * px * tan);
            return new HatchGeometry(px, py, px, px, sign * py);
        }
    }
}
