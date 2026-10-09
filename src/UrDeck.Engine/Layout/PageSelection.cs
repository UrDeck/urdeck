// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Layout;

/// <summary>Chooses which page a snapshot renders.</summary>
public static class PageSelection
{
    /// <summary>
    /// The page to render: <paramref name="requested"/> when given (it must be inside the list), otherwise the
    /// page at <paramref name="activePage"/>, clamped like the window does at startup.
    /// </summary>
    /// <returns>False with a reason in <paramref name="error"/> when the requested page does not exist.</returns>
    public static bool TryChoose(int pageCount, int activePage, int? requested, out int index, out string? error)
    {
        error = null;
        if (requested is { } page)
        {
            index = page;
            if (page >= 0 && page < pageCount)
                return true;
            error = $"Page {page} does not exist; the configuration has {pageCount} page(s), numbered from 0.";
            return false;
        }

        index = pageCount == 0 ? 0 : Math.Clamp(activePage, 0, pageCount - 1);
        return true;
    }
}
