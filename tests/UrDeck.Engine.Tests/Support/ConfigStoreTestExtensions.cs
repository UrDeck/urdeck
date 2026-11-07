// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Config;

internal static class ConfigStoreTestExtensions
{
    /// <summary>Writes the document now, as a change would half a second from now: for tests of what survives a write.</summary>
    public static void Save(this ConfigStore store)
    {
        store.Document.RequestWrite();
        store.Document.Flush();
    }
}
