// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Config;

/// <summary>
/// The configuration's <c>updates</c> key (app-updates spec, "Update Setting"): <c>auto</c> checks for a newer release,
/// <c>off</c> makes no request. Read leniently, like <c>motion</c>: absent or anything else is <c>auto</c>.
/// </summary>
public static class UpdatesSetting
{
    /// <summary>
    /// Whether the update check is on. Absent, empty and <c>auto</c> are on; <c>off</c> is off (trimmed, any case); another
    /// value is on, with a warning when <paramref name="warn"/> is set.
    /// </summary>
    public static bool IsEnabled(string? value, bool warn = false)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or "auto":
                return true;
            case "off":
                return false;
            default:
                if (warn)
                    UrDeckLog.Warn($"updates '{value}' must be 'auto' or 'off'; checking for updates.");
                return true;
        }
    }
}
