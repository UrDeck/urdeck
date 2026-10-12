// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Backgrounds;

/// <summary>The time a shader sees. A <c>float</c> holds seconds precisely only for hours, so it is wrapped.</summary>
public static class BackgroundClock
{
    /// <summary>Scaled seconds after which time returns to 0; the step of a float there is about 1 ms.</summary>
    public const double WrapSeconds = 14400;

    /// <summary><c>iTime</c> of a snapshot: fixed, so the same configuration gives the same picture.</summary>
    public const float SnapshotTime = 10;

    /// <summary><c>(elapsed * speed) mod 14400</c>; negative or non-finite input gives 0.</summary>
    public static float Wrap(double elapsedSeconds, double speed)
    {
        double scaled = elapsedSeconds * speed;
        if (!double.IsFinite(scaled) || scaled < 0)
            return 0;
        return (float)(scaled % WrapSeconds);
    }
}
