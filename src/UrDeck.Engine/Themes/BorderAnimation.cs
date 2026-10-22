// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Themes;

/// <summary>How a card's border moves: <c>spin</c> turns a conic gradient, <c>pulse</c> breathes the border's opacity.</summary>
public enum BorderAnimationMode
{
    Spin,
    Pulse,
}

/// <summary>A resolved <c>card.borderAnimate</c>: the mode and the seconds one cycle takes.</summary>
public sealed record BorderAnimation(BorderAnimationMode Mode, double PeriodSeconds);
