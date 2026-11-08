// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;

namespace UrDeck.Engine.Deck;

/// <summary>A configuration that never changes: what a session shows when it is not the deck (a theme's picture in the editor).</summary>
public sealed class StaticConfigSource(UrDeckConfig config) : IDeckConfigSource
{
    public UrDeckConfig Config => config;

    // Never raised.
    public event Action<UrDeckConfig>? ConfigChanged
    {
        add { }
        remove { }
    }
}
