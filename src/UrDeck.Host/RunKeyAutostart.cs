// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.Win32;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Startup;

namespace UrDeck.Host;

/// <summary>
/// Whether Windows starts UrDeck at sign-in, and how to change it. Windows holds the setting; there is no copy of it in
/// the configuration. A packaged build would add a second implementation (the <c>StartupTask</c> API) and nothing else
/// changes. None of the members throws: a failure is logged and the state read afterwards says what is in effect.
/// </summary>
internal interface IAutostart
{
    /// <summary>What Windows will do at the next sign-in, read from Windows now.</summary>
    AutostartState State { get; }

    /// <summary>The executable the Run value names, whichever copy it is; null when nothing is registered or it cannot be read.</summary>
    string? RegisteredExecutable { get; }

    /// <summary>Registers the running copy and makes Windows enable it.</summary>
    void SwitchOn();

    /// <summary>Removes the registration.</summary>
    void SwitchOff();
}

/// <summary>
/// The per-user Run key: the value <c>UrDeck</c> holds <c>"&lt;exe&gt;" --autostart</c>. Task Manager and Settings do not
/// remove a Run value when the user disables an app; they write a binary value of the same name under
/// <c>StartupApproved\Run</c> whose first byte is odd, which <see cref="AutostartStates"/> interprets.
/// </summary>
internal sealed class RunKeyAutostart : IAutostart
{
    private const string ValueName = "UrDeck";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovalKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private readonly string _executable;
    private bool _readFailureLogged;

    public RunKeyAutostart(string? executable = null)
    {
        _executable = executable ?? Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "UrDeck.Host.exe");
    }

    public string? RegisteredExecutable => AutostartStates.ExecutableOf(ReadRunData());

    public AutostartState State => AutostartStates.From(ReadRunData(), ReadApproval(), _executable);

    public void SwitchOn()
    {
        try
        {
            using (var run = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true))
                run.SetValue(ValueName, $"\"{_executable}\" --autostart", RegistryValueKind.String);

            // Overriding a Task Manager switch is deliberate: the user has just asked for it in the app's own menu.
            using var approval = Registry.CurrentUser.OpenSubKey(ApprovalKeyPath, writable: true);
            approval?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            UrDeckLog.Warn($"Start with Windows: could not register {_executable}: {ex.Message}");
        }
    }

    public void SwitchOff()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            run?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            UrDeckLog.Warn($"Start with Windows: could not remove the registration: {ex.Message}");
        }
    }

    private string? ReadRunData()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return run?.GetValue(ValueName) as string;
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            LogReadFailureOnce(ex);
            return null;
        }
    }

    private byte[]? ReadApproval()
    {
        try
        {
            using var approval = Registry.CurrentUser.OpenSubKey(ApprovalKeyPath);
            return approval?.GetValue(ValueName) as byte[];
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            LogReadFailureOnce(ex);
            return null;
        }
    }

    private void LogReadFailureOnce(Exception ex)
    {
        if (_readFailureLogged)
            return;
        _readFailureLogged = true;
        UrDeckLog.Warn($"Start with Windows: could not read the registration, treating it as off: {ex.Message}");
    }

    private static bool IsRegistryFailure(Exception ex) =>
        ex is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException;
}
