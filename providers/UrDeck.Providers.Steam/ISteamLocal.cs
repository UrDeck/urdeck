// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Providers.Steam;

/// <summary>What the registry says about the Steam client right now.</summary>
/// <param name="Running">Steam's process is running.</param>
/// <param name="ActiveUser">The 32-bit account id signed in to the running client; 0 when nobody is.</param>
/// <param name="RunningAppId">The app id of the game being played; 0 when none.</param>
internal readonly record struct ClientState(bool Running, uint ActiveUser, uint RunningAppId);

/// <summary>What the registry keeps under <c>Apps\&lt;appid&gt;</c> for one app.</summary>
internal readonly record struct RegistryApp(string? Name, bool Updating);

/// <summary>One <c>appmanifest_&lt;appid&gt;.acf</c> file: its text, or null when it could not be read.</summary>
internal readonly record struct ManifestFile(string File, string? Text, string? Fault);

[Flags]
internal enum LocalChange
{
    None = 0,

    /// <summary>The registry values of the client (the account, the game playing, an app's flags).</summary>
    Client = 1,

    /// <summary>One of the files: logins, libraries, a manifest, the user's local configuration.</summary>
    Files = 2,
}

/// <summary>
/// Everything the provider reads from this PC: Steam's registry values and text files. Never the network. The Windows
/// implementation is <see cref="WindowsSteamLocal"/>; tests use a fake. The read methods throw
/// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> when a file is missing, locked or cut off.
/// </summary>
internal interface ISteamLocal : IDisposable
{
    /// <summary>Whether Steam is installed for this Windows user (false on every other system).</summary>
    bool Installed { get; }

    /// <summary>The Steam folder, or "" when it is not installed.</summary>
    string SteamPath { get; }

    ClientState ReadClient();

    /// <summary>The registry's record of one app, or null when it has none.</summary>
    RegistryApp? ReadApp(uint appId);

    /// <summary>The text of <c>config\loginusers.vdf</c>.</summary>
    string ReadLoginUsers();

    /// <summary>The text of <c>config\libraryfolders.vdf</c>.</summary>
    string ReadLibraryFolders();

    /// <summary>The text of <c>userdata\&lt;accountId&gt;\config\localconfig.vdf</c>.</summary>
    string ReadLocalConfig(uint accountId);

    /// <summary>The manifest of one app in a library, or null when that library does not hold it.</summary>
    string? ReadManifest(string libraryPath, uint appId);

    /// <summary>Every manifest in a library; one that cannot be read comes back with its fault.</summary>
    IReadOnlyList<ManifestFile> ReadManifests(string libraryPath);

    /// <summary>The path of the app's art in the library cache, or "" when there is none.</summary>
    string FindArt(uint appId);

    /// <summary>
    /// Starts watching for what <paramref name="what"/> names and calls <paramref name="changed"/> (on any thread) for each change.
    /// Disposing stops all of it; nothing is watched while no watch is open.
    /// </summary>
    IDisposable Watch(LocalChange what, Action<LocalChange> changed);
}

/// <summary>The seam on a system that has no Steam to read.</summary>
internal sealed class NoSteamLocal : ISteamLocal
{
    public bool Installed => false;

    public string SteamPath => "";

    public ClientState ReadClient() => default;

    public RegistryApp? ReadApp(uint appId) => null;

    public string ReadLoginUsers() => throw new FileNotFoundException();

    public string ReadLibraryFolders() => throw new FileNotFoundException();

    public string ReadLocalConfig(uint accountId) => throw new FileNotFoundException();

    public string? ReadManifest(string libraryPath, uint appId) => null;

    public IReadOnlyList<ManifestFile> ReadManifests(string libraryPath) => [];

    public string FindArt(uint appId) => "";

    public IDisposable Watch(LocalChange what, Action<LocalChange> changed) => new Nothing();

    public void Dispose()
    {
    }

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
