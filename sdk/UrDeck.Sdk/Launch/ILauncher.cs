// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Launch;

/// <summary>
/// The host's launch service: opens a file, a folder, an application, a <c>shell:</c> item or an address the way a
/// desktop shortcut does. See <see cref="LaunchTarget"/> for what a target may be.
/// </summary>
public interface ILauncher
{
    /// <summary>
    /// Asks the host to open <paramref name="target"/>. Call it from <see cref="Input.ITapTarget.OnTap"/>, so that Windows
    /// lets the launched application come to the front. Never throws: an invalid or refused target starts nothing, is
    /// logged by the host and returns <c>false</c>. <paramref name="arguments"/> apply to file targets only.
    /// </summary>
    bool Launch(string target, string? arguments = null);
}

/// <summary>The launcher of a widget that was never attached, and of a host that offers none: it starts nothing.</summary>
internal sealed class NullLauncher : ILauncher
{
    public static readonly NullLauncher Instance = new();

    public bool Launch(string target, string? arguments = null) => false;
}
