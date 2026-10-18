// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>Contrast and interpolation of colours, shared by the theme's defaults and the gauge.</summary>
internal static class ColorMath
{
    /// <summary>The first colour, or the second, whichever contrasts more with <paramref name="against"/> (WCAG), opaque.</summary>
    public static SKColor MoreContrasting(SKColor against, SKColor a, SKColor b)
    {
        // Colours are compared as opaque: a translucent background still stands for its hue.
        double la = Luminance(against);
        return Ratio(la, Luminance(a)) >= Ratio(la, Luminance(b)) ? a.WithAlpha(0xFF) : b.WithAlpha(0xFF);
    }

    /// <summary>The colour at <paramref name="t"/> (0 to 1) along colours evenly spaced, linear in sRGB between them.</summary>
    public static SKColor Along(IReadOnlyList<SKColor> colors, float t)
    {
        if (colors.Count == 0)
            return SKColors.Transparent;
        if (colors.Count == 1)
            return colors[0];
        float position = Math.Clamp(t, 0f, 1f) * (colors.Count - 1);
        int index = Math.Min((int)position, colors.Count - 2);
        return Lerp(colors[index], colors[index + 1], position - index);
    }

    public static SKColor Lerp(SKColor a, SKColor b, float t) => new(
        Mix(a.Red, b.Red, t), Mix(a.Green, b.Green, t), Mix(a.Blue, b.Blue, t), Mix(a.Alpha, b.Alpha, t));

    private static byte Mix(byte a, byte b, float t) => (byte)Math.Round(a + (b - a) * t);

    private static double Ratio(double a, double b) => (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);

    private static double Luminance(SKColor c) =>
        0.2126 * Linear(c.Red) + 0.7152 * Linear(c.Green) + 0.0722 * Linear(c.Blue);

    private static double Linear(byte channel)
    {
        double s = channel / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
