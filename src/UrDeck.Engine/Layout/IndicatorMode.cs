// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Layout;

/// <summary>The page indicator's setting (<c>pager.indicator</c>) and, once resolved, what it comes to.</summary>
public enum IndicatorMode
{
    /// <summary>Hidden with one page; otherwise a reserved band when enough rows remain, else floating.</summary>
    Auto,

    /// <summary>A band at the bottom is reserved and the indicator is always visible in it.</summary>
    Always,

    /// <summary>Floats over the page and fades out after a page change.</summary>
    Fade,

    /// <summary>Never shown, no band.</summary>
    Off,
}
