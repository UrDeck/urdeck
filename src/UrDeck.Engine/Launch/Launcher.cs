// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Launch;

namespace UrDeck.Engine.Launch;

/// <summary>The one door to starting something, so that tests record what would be started and start nothing.</summary>
internal interface IProcessStarter
{
    /// <summary>Starts what <paramref name="info"/> describes. Throws when Windows refuses it.</summary>
    void Start(ProcessStartInfo info);
}

internal sealed class ShellProcessStarter : IProcessStarter
{
    public void Start(ProcessStartInfo info)
    {
        // The process object is not kept: the launcher neither waits for nor moves what it started.
        using var process = Process.Start(info);
    }
}

/// <summary>
/// The launch service every widget shares: a target is handed to the Windows shell with the default action, as a
/// double click on a desktop shortcut would. Never through a command interpreter and never elevated. It runs on the
/// calling (UI) thread, inside the handling of the tap, because that is what lets the launched window come to the front.
/// </summary>
public sealed class Launcher : ILauncher
{
    /// <summary>A second request for the same target and arguments within this time is not started again (a double tap).</summary>
    public static readonly TimeSpan RepeatGuard = TimeSpan.FromSeconds(1);

    private readonly IProcessStarter _starter;
    private readonly TimeProvider _time;
    private string? _lastKey;
    private long _lastStarted;

    public Launcher(TimeProvider? time = null)
        : this(new ShellProcessStarter(), time)
    {
    }

    internal Launcher(IProcessStarter starter, TimeProvider? time = null)
    {
        _starter = starter;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Called on the launching thread just before a target is handed to the shell. Empty by default. It is the one place
    /// for a foreground step, should a launched window ever stay behind the application in front.
    /// </summary>
    public Action? BeforeLaunch { get; set; }

    public bool Launch(string target, string? arguments = null)
    {
        var parsed = LaunchTarget.Parse(target);
        if (!parsed.IsValid)
        {
            UrDeckLog.Warn($"Launch refused: '{target}' is not a valid target (a rooted path, a bare name, a shell: item or an address).");
            return false;
        }

        if (string.IsNullOrWhiteSpace(arguments))
        {
            arguments = null;
        }
        else if (parsed.Kind != LaunchTargetKind.File)
        {
            UrDeckLog.Warn($"Launch of '{parsed.Text}': arguments apply to file targets only and are ignored for an address.");
            arguments = null;
        }

        string key = parsed.Text + "\n" + arguments;
        if (key == _lastKey && _time.GetElapsedTime(_lastStarted) < RepeatGuard)
            return true;

        var info = new ProcessStartInfo(parsed.Text)
        {
            UseShellExecute = true,
            Arguments = arguments ?? "",
        };
        if (WorkingDirectoryOf(parsed) is { } folder)
            info.WorkingDirectory = folder;

        // The shell call holds the calling (UI) thread; how long is worth a place in the log.
        long before = Stopwatch.GetTimestamp();
        try
        {
            BeforeLaunch?.Invoke();
            _starter.Start(info);
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Launch of '{parsed.Text}' failed: {ex.Message}");
            return false;
        }

        _lastKey = key;
        _lastStarted = _time.GetTimestamp();
        // Arguments can carry secrets (a token, a profile name); the target is enough to follow what happened.
        UrDeckLog.Info($"Launched '{parsed.Text}' in {Stopwatch.GetElapsedTime(before).TotalMilliseconds:0} ms");
        return true;
    }

    /// <summary>The folder of an executable given by its path; null for everything else, which starts where the host runs.</summary>
    private static string? WorkingDirectoryOf(LaunchTarget target)
    {
        if (target.Kind != LaunchTargetKind.File || !target.Text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return null;
        int cut = target.Text.LastIndexOfAny(['\\', '/']);
        return cut > 0 ? target.Text[..cut] : null;
    }
}
