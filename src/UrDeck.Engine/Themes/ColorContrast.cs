// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Engine.Themes;

/// <summary>WCAG contrast between colours, for the defaults a theme leaves out and for the checks on one.</summary>
internal static class ColorContrast
{
    /// <summary>The first colour or the second, whichever contrasts more with <paramref name="against"/>, opaque.</summary>
    public static SKColor MoreContrasting(SKColor against, SKColor a, SKColor b)
    {
        // Colours are compared as opaque: a translucent background still stands for its hue.
        double luminance = Luminance(against);
        return Ratio(luminance, Luminance(a)) >= Ratio(luminance, Luminance(b)) ? a.WithAlpha(0xFF) : b.WithAlpha(0xFF);
    }

    private static double Ratio(double a, double b) => (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);

    /// <summary>The WCAG contrast ratio of two colours, from 1 to 21, both taken as opaque.</summary>
    public static double Ratio(SKColor a, SKColor b) => Ratio(Luminance(a), Luminance(b));

    /// <summary>
    /// <paramref name="top"/> drawn over <paramref name="under"/>, opaque: what a translucent colour looks like on the colour
    /// beneath it.
    /// </summary>
    public static SKColor Over(SKColor top, SKColor under)
    {
        double alpha = top.Alpha / 255.0;
        byte Mix(byte over, byte below) => (byte)Math.Round((over * alpha) + (below * (1 - alpha)));
        return new SKColor(Mix(top.Red, under.Red), Mix(top.Green, under.Green), Mix(top.Blue, under.Blue));
    }

    public static double Luminance(SKColor c) =>
        0.2126 * Linear(c.Red) + 0.7152 * Linear(c.Green) + 0.0722 * Linear(c.Blue);

    private static double Linear(byte channel)
    {
        double s = channel / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
