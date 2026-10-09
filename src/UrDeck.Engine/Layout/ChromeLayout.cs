// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;

namespace UrDeck.Engine.Layout;

/// <summary>Where the page indicator and the widget grid go on a screen.</summary>
/// <param name="Mode">The resolved mode: <see cref="IndicatorMode.Always"/>, <see cref="IndicatorMode.Fade"/> or <see cref="IndicatorMode.Off"/>.</param>
/// <param name="Indicator">The indicator's rectangle (empty when off). With <c>Fade</c> it overlaps the grid.</param>
/// <param name="Grid">The area widgets may occupy.</param>
/// <param name="Rows">Whole grid rows inside <paramref name="Grid"/>.</param>
public readonly record struct ChromeLayoutResult(IndicatorMode Mode, Rectangle Indicator, Rectangle Grid, int Rows)
{
    public bool ReservesBand => Mode == IndicatorMode.Always;
}

/// <summary>Decides the indicator mode for a screen and splits the screen into indicator and grid.</summary>
public static class ChromeLayout
{
    /// <summary>The largest first-party widget is four rows tall; a band that leaves fewer is not worth reserving.</summary>
    public const int MinRowsForBand = 4;

    /// <summary>Resolves <paramref name="configured"/> for this screen and page count and splits the screen.</summary>
    /// <param name="screen">The screen in pixels.</param>
    /// <param name="configured">The configured mode.</param>
    /// <param name="pageCount">How many pages the configuration has.</param>
    /// <param name="bandHeight">The theme's band height as a fraction of the column width.</param>
    public static ChromeLayoutResult Compute(Size screen, IndicatorMode configured, int pageCount, double bandHeight)
    {
        double cell = screen.Width / 4.0;
        int band = Math.Min(screen.Height, (int)Math.Round(bandHeight * cell, MidpointRounding.AwayFromZero));
        var whole = new Rectangle(0, 0, screen.Width, screen.Height);
        var bandRect = new Rectangle(0, screen.Height - band, screen.Width, band);
        var reserved = new Rectangle(0, 0, screen.Width, screen.Height - band);

        var mode = configured switch
        {
            IndicatorMode.Off => IndicatorMode.Off,
            IndicatorMode.Always => IndicatorMode.Always,
            IndicatorMode.Fade => IndicatorMode.Fade,
            _ => pageCount <= 1
                ? IndicatorMode.Off
                : Rows(reserved, cell) >= MinRowsForBand ? IndicatorMode.Always : IndicatorMode.Fade,
        };

        return mode switch
        {
            IndicatorMode.Always => new ChromeLayoutResult(mode, bandRect, reserved, Rows(reserved, cell)),
            IndicatorMode.Fade => new ChromeLayoutResult(mode, bandRect, whole, Rows(whole, cell)),
            _ => new ChromeLayoutResult(IndicatorMode.Off, Rectangle.Empty, whole, Rows(whole, cell)),
        };
    }

    private static int Rows(Rectangle grid, double cell) => cell <= 0 ? 0 : (int)Math.Floor(grid.Height / cell);
}
