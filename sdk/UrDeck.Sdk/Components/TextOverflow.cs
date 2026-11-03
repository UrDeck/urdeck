// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Components;

/// <summary>What <see cref="TextLine"/> does with a line that is wider than its rectangle.</summary>
public enum TextOverflow
{
    /// <summary>The text is drawn smaller until it fits. The default, and what a line did before the choice existed.</summary>
    Shrink,

    /// <summary>The text is drawn at the theme's size, cut at a character boundary and ended with <c>…</c>.</summary>
    Ellipsis,

    /// <summary>The text is drawn at the theme's size and cut at the rectangle's edge.</summary>
    Clip,

    /// <summary>
    /// The text is drawn at the theme's size and scrolls sideways, resting at each end, as a function of the paint's time.
    /// Below <see cref="MotionLevel.Full"/> it stands still and ends in <c>…</c>, as <see cref="Ellipsis"/>.
    /// </summary>
    Marquee,
}
