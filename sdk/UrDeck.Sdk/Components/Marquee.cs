// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Components;

/// <summary>The marquee's motion: rest, scroll, rest, return, as a pure function of time.</summary>
internal static class Marquee
{
    /// <summary>Seconds the line rests at each end.</summary>
    public const double PauseSeconds = 1.5;

    /// <summary>Seconds the line takes to return to its start.</summary>
    public const double ReturnSeconds = 0.5;

    /// <summary>The scroll speed in text sizes per second, so the motion reads the same at any resolution.</summary>
    public const double SizesPerSecond = 3;

    /// <summary>
    /// How far the line has moved left of its start (0 to <paramref name="overflow"/> pixels) at <paramref name="seconds"/>
    /// for a line <paramref name="overflow"/> pixels wider than its rectangle drawn at <paramref name="size"/>.
    /// </summary>
    public static float Offset(double seconds, float overflow, float size)
    {
        if (overflow <= 0)
            return 0;
        double scroll = overflow / (size * SizesPerSecond);
        double cycle = PauseSeconds + scroll + PauseSeconds + ReturnSeconds;
        double t = seconds % cycle;
        if (t < 0)
            t += cycle;
        if (t < PauseSeconds)
            return 0;
        t -= PauseSeconds;
        if (t < scroll)
            return (float)(overflow * (t / scroll));
        t -= scroll;
        if (t < PauseSeconds)
            return overflow;
        t -= PauseSeconds;
        double u = t / ReturnSeconds;
        double eased = u * u * (3 - 2 * u);
        return (float)(overflow * (1 - eased));
    }
}
