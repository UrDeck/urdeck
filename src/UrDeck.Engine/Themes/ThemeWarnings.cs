// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Themes;

/// <summary>
/// The findings already logged for one loaded theme. A theme is resolved several times (page, dock, each display change),
/// and each finding is written once; loading the theme again starts afresh, which is wanted after the user edits it.
/// </summary>
internal sealed class ThemeWarnings
{
    private readonly HashSet<string> _reported = [];

    /// <summary>Logs <paramref name="message"/> unless a finding with this key was logged before.</summary>
    public void Once(string key, string message)
    {
        lock (_reported)
            if (!_reported.Add(key))
                return;
        UrDeckLog.Warn(message);
    }
}
