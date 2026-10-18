// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Themes;

namespace UrDeck.Engine.Rendering;

/// <summary>
/// What a card needs beyond its size: the page's or the dock's card style, and the card's index for <c>tint</c> (its place
/// from <see cref="Layout.CardOrder"/>, or a dock slot's position; -1 for none).
/// </summary>
public readonly record struct CardLook(ResolvedCardStyle Style, int Index);
