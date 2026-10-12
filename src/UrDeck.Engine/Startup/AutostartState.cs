// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Startup;

/// <summary>What Windows will do with UrDeck at the next sign-in.</summary>
public enum AutostartState
{
    /// <summary>Nothing is registered.</summary>
    Off,

    /// <summary>This copy is registered and Windows has it enabled.</summary>
    On,

    /// <summary>An executable in a different folder is registered.</summary>
    AnotherCopy,

    /// <summary>This copy is registered, and the user disabled it in Task Manager or Settings.</summary>
    SwitchedOffInWindows,
}

/// <summary>
/// The autostart state as a pure function of what the registry holds, so that the interesting cases are tested without
/// Windows. The Run value is the command line Windows runs; the approval value is the binary value Task Manager and
/// Settings write under <c>StartupApproved\Run</c> (first byte odd means disabled; absent or unrecognised means enabled).
/// </summary>
public static class AutostartStates
{
    /// <summary>Computes the state from the Run value's data, the approval bytes (both null when absent) and the running executable.</summary>
    public static AutostartState From(string? runData, byte[]? approval, string runningExecutable)
    {
        string? registered = ExecutableOf(runData);
        if (registered is null)
            return AutostartState.Off;
        if (!SamePath(registered, runningExecutable))
            return AutostartState.AnotherCopy;
        return IsDisabled(approval) ? AutostartState.SwitchedOffInWindows : AutostartState.On;
    }

    /// <summary>
    /// The executable of a Run command line: the quoted path when it starts with a quote, otherwise everything up to
    /// and including the first <c>.exe</c> (an unquoted path may hold spaces), otherwise the first word. Arguments are
    /// ignored. Null when there is nothing to read.
    /// </summary>
    public static string? ExecutableOf(string? runData)
    {
        if (string.IsNullOrWhiteSpace(runData))
            return null;

        string text = runData.Trim();
        if (text[0] == '"')
        {
            int close = text.IndexOf('"', 1);
            string quoted = close < 0 ? text[1..] : text[1..close];
            return quoted.Length == 0 ? null : quoted;
        }

        int exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe >= 0)
            return text[..(exe + 4)];

        int space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }

    private static bool IsDisabled(byte[]? approval) => approval is { Length: > 0 } && (approval[0] & 1) == 1;

    private static bool SamePath(string a, string b) =>
        string.Equals(Normalise(a), Normalise(b), StringComparison.OrdinalIgnoreCase);

    private static string Normalise(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
