// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using SkiaSharp;
using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Backgrounds;

/// <summary>
/// Draws a theme's still background (<c>background.static</c>): a gradient or a colour. A picture goes through
/// <see cref="BackgroundImage"/>. Every value is a fraction of the deck, so it does not depend on the resolution.
/// </summary>
internal static class StillPainter
{
    public const double DefaultAngle = 90;
    public const double DefaultCx = 0.5;
    public const double DefaultCy = 0;
    public const double DefaultRadius = 0.7;

    public static void Paint(SKCanvas canvas, int width, int height, ThemeBackgroundStaticDefinition still)
    {
        if (still.Gradient is { Colors: { Count: >= 2 } } gradient)
        {
            using var shader = CreateGradient(gradient, width, height);
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(0, 0, width, height, paint);
        }
        else if (BackgroundSettings.TryParseColor(still.Color, out var color))
        {
            using var paint = new SKPaint { Color = color };
            canvas.DrawRect(0, 0, width, height, paint);
        }
    }

    /// <summary>A text that is equal for two gradients that look the same.</summary>
    public static string Describe(ThemeBackgroundStaticGradientDefinition gradient) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{gradient.Kind ?? "linear"}|{gradient.Angle ?? DefaultAngle}|{gradient.Cx ?? DefaultCx}|{gradient.Cy ?? DefaultCy}|{gradient.Radius ?? DefaultRadius}|{string.Join(",", gradient.Colors ?? [])}");

    private static SKShader CreateGradient(ThemeBackgroundStaticGradientDefinition gradient, int width, int height)
    {
        var colors = gradient.Colors!.Select(c => SKColor.Parse(c)).ToArray();

        if (string.Equals(gradient.Kind, "radial", StringComparison.OrdinalIgnoreCase))
        {
            var centre = new SKPoint((float)((gradient.Cx ?? DefaultCx) * width), (float)((gradient.Cy ?? DefaultCy) * height));
            float radius = Math.Max(1f, (float)((gradient.Radius ?? DefaultRadius) * width));
            return SKShader.CreateRadialGradient(centre, radius, colors, null, SKShaderTileMode.Clamp);
        }

        // 0 degrees runs left to right and 90 top to bottom; the line is as long as the deck is along that direction, so
        // the first and last stop touch the corners.
        double radians = (gradient.Angle ?? DefaultAngle) * Math.PI / 180;
        double dx = Math.Cos(radians);
        double dy = Math.Sin(radians);
        double half = (Math.Abs(width * dx) + Math.Abs(height * dy)) / 2;
        double cx = width / 2.0;
        double cy = height / 2.0;
        var start = new SKPoint((float)(cx - dx * half), (float)(cy - dy * half));
        var end = new SKPoint((float)(cx + dx * half), (float)(cy + dy * half));
        return SKShader.CreateLinearGradient(start, end, colors, null, SKShaderTileMode.Clamp);
    }
}
