// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Diagnostics;

/// <summary>Minimal file + debug-output logger. Writes to urdeck.log next to the executable.</summary>
public static class UrDeckLog
{
    private static readonly object Gate = new();

    private static readonly string DefaultPath = Path.Combine(AppContext.BaseDirectory, "urdeck.log");
    private static readonly AsyncLocal<string?> Redirect = new();

    /// <summary>
    /// Where the log is written. Setting it redirects only the code running in the current execution context (and what it
    /// starts), so parallel tests that redirect the log, and background work of other tests, cannot disturb each other.
    /// The application never sets it.
    /// </summary>
    public static string LogPath
    {
        get => Redirect.Value ?? DefaultPath;
        set => Redirect.Value = value;
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex == null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {level} {message}";
        System.Diagnostics.Debug.WriteLine(line);
        try
        {
            lock (Gate)
                File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
