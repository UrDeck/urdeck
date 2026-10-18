// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using SkiaSharp;

namespace UrDeck.Sdk.Components;

/// <summary>
/// The meter: the readout at the top, left-aligned, a thin track below it, and under the track the theme's ticks and printed
/// scale. It is a stacked bar with a scale, so the track code is the bar's.
/// </summary>
internal static class MeterGauge
{
    public static SKRect ReadoutRect(Theme theme, SKRect rect) => BarGauge.Measure(theme, rect, stacked: true, meter: true).Readout;

    public static SKRect Draw(GaugeContext c) => BarGauge.DrawBar(c, stacked: true, meter: true);
}
