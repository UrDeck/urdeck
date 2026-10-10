// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;

namespace UrDeck.Engine.Layout;

/// <summary>Where the dock, the page indicator and the widget grid go on a screen.</summary>
/// <param name="Mode">The resolved mode: <see cref="IndicatorMode.Always"/>, <see cref="IndicatorMode.Fade"/> or <see cref="IndicatorMode.Off"/>.</param>
/// <param name="Indicator">The indicator's rectangle (empty when off). With <c>Fade</c> it overlaps the grid.</param>
/// <param name="Grid">The area widgets may occupy.</param>
/// <param name="Rows">Whole grid rows inside <paramref name="Grid"/>.</param>
/// <param name="Dock">The dock's band at the very bottom of the screen (empty without a dock).</param>
public readonly record struct ChromeLayoutResult(IndicatorMode Mode, Rectangle Indicator, Rectangle Grid, int Rows, Rectangle Dock = default)
{
    public bool ReservesBand => Mode == IndicatorMode.Always;
}

/// <summary>Decides the indicator mode for a screen and splits the screen into dock, indicator and grid.</summary>
public static class ChromeLayout
{
    /// <summary>The largest first-party widget is four rows tall; a band that leaves fewer is not worth reserving.</summary>
    public const int MinRowsForBand = 4;

    /// <summary>
    /// Resolves <paramref name="configured"/> for this screen and page count and splits the screen. From the bottom:
    /// the dock, the indicator's band, the grid. The dock is reserved whenever it has a height, however few rows that
    /// leaves; the indicator is the element that gives way.
    /// </summary>
    /// <param name="screen">The screen in pixels.</param>
    /// <param name="configured">The configured mode.</param>
    /// <param name="pageCount">How many pages the configuration has.</param>
    /// <param name="bandHeight">The theme's band height as a fraction of the column width.</param>
    /// <param name="dockHeight">The theme's dock height as a fraction of the column width; 0 when there is no dock.</param>
    public static ChromeLayoutResult Compute(Size screen, IndicatorMode configured, int pageCount, double bandHeight, double dockHeight = 0)
    {
        double cell = screen.Width / 4.0;
        int dock = dockHeight > 0 ? Math.Clamp(RoundPx(dockHeight * cell), 0, screen.Height) : 0;
        int above = screen.Height - dock;
        int band = Math.Min(above, RoundPx(bandHeight * cell));
        var dockRect = dock > 0 ? new Rectangle(0, above, screen.Width, dock) : Rectangle.Empty;
        var whole = new Rectangle(0, 0, screen.Width, above);
        var bandRect = new Rectangle(0, above - band, screen.Width, band);
        var reserved = new Rectangle(0, 0, screen.Width, above - band);

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
            IndicatorMode.Always => new ChromeLayoutResult(mode, bandRect, reserved, Rows(reserved, cell), dockRect),
            IndicatorMode.Fade => new ChromeLayoutResult(mode, bandRect, whole, Rows(whole, cell), dockRect),
            _ => new ChromeLayoutResult(IndicatorMode.Off, Rectangle.Empty, whole, Rows(whole, cell), dockRect),
        };
    }

    private static int RoundPx(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static int Rows(Rectangle grid, double cell) => cell <= 0 ? 0 : (int)Math.Floor(grid.Height / cell);
}
