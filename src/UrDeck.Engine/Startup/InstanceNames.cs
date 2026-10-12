// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Security.Cryptography;
using System.Text;

namespace UrDeck.Engine.Startup;

/// <summary>
/// The names of the operating-system objects that make one host per application folder: a mutex the host holds for its
/// lifetime, and an event that <c>--quit</c> sets. Both are session-local and keyed on the folder.
/// </summary>
public static class InstanceNames
{
    /// <summary>The first 16 hex digits of the SHA-256 of the upper-cased full folder path, without a trailing separator.</summary>
    public static string Key(string applicationFolder)
    {
        string full = Path.GetFullPath(applicationFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(full.ToUpperInvariant()));
        return Convert.ToHexString(hash, 0, 8);
    }

    public static string Mutex(string applicationFolder) => $@"Local\UrDeck.{Key(applicationFolder)}.instance";

    public static string QuitEvent(string applicationFolder) => $@"Local\UrDeck.{Key(applicationFolder)}.quit";
}
