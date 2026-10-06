// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Data;

/// <summary>Splits a reading id of the form <c>provider:path</c>.</summary>
public static class ReadingId
{
    public static bool TryParse(string? id, out string providerId, out string path, out string? error)
    {
        providerId = "";
        path = "";
        error = null;

        int colon = id?.IndexOf(':', StringComparison.Ordinal) ?? -1;
        if (id == null || colon <= 0)
        {
            error = $"'{id}' is not a reading id (expected provider:path)";
            return false;
        }

        providerId = id[..colon];
        path = id[(colon + 1)..];
        if (path.Length == 0 || path.StartsWith('/') || path.EndsWith('/') || path.Contains("//", StringComparison.Ordinal))
        {
            error = $"'{id}' has no valid path after the provider id";
            return false;
        }

        return true;
    }
}
