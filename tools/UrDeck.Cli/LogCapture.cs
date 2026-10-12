// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Cli;

/// <summary>
/// Takes what the engine would write to <c>urdeck.log</c> while it is alive: the tool never creates a log file. The warnings
/// (a parameter value the configuration refuses, say) are kept for the command to show.
/// </summary>
internal sealed class LogCapture : IDisposable
{
    private readonly Action<string, string>? _before = UrDeckLog.Listener;
    private readonly List<string> _warnings = [];

    public LogCapture() => UrDeckLog.Listener = (level, message) =>
    {
        if (level is "WARN" or "ERROR")
            _warnings.Add(message);
    };

    public IReadOnlyList<string> Warnings => _warnings;

    public void Dispose() => UrDeckLog.Listener = _before;
}
