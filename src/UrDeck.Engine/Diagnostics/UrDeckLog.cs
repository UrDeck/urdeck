// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Diagnostics;

/// <summary>
/// Minimal file + debug-output logger. Writes to urdeck.log next to the executable, every line dated. When the file
/// passes <see cref="DefaultMaxBytes"/> it is renamed to <c>urdeck.log.1</c> (replacing an earlier one) and a new file
/// starts; a rename that fails is retried on a later write.
/// </summary>
public static class UrDeckLog
{
    /// <summary>The size at which the log is rotated.</summary>
    public const long DefaultMaxBytes = 5 * 1024 * 1024;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, long> Sizes = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string DefaultPath = Path.Combine(AppContext.BaseDirectory, "urdeck.log");
    private static readonly AsyncLocal<string?> Redirect = new();
    private static readonly AsyncLocal<long?> CapRedirect = new();

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

    /// <summary>The rotation size, redirected per execution context like <see cref="LogPath"/>. The application never sets it.</summary>
    public static long MaxBytes
    {
        get => CapRedirect.Value ?? DefaultMaxBytes;
        set => CapRedirect.Value = value;
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex == null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {level} {message}";
        System.Diagnostics.Debug.WriteLine(line);
        try
        {
            string path = LogPath;
            string text = line + Environment.NewLine;
            lock (Gate)
            {
                long size = SizeOf(path);
                if (size > MaxBytes && TryRotate(path))
                    size = 0;
                File.AppendAllText(path, text);
                Sizes[path] = size + System.Text.Encoding.UTF8.GetByteCount(text);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // Read once per path (which also covers a file that is already over the cap at startup), then kept by Write.
    private static long SizeOf(string path)
    {
        if (Sizes.TryGetValue(path, out long size))
            return size;
        try
        {
            size = File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (IOException)
        {
            size = 0;
        }

        Sizes[path] = size;
        return size;
    }

    private static bool TryRotate(string path)
    {
        try
        {
            File.Move(path, path + ".1", overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
