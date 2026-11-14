// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace UrDeck.Providers.Steam;

/// <summary>
/// <see cref="ISteamLocal"/> on Windows: <c>HKCU\Software\Valve\Steam</c> and the files under Steam's folder. Read only. Faults carry
/// only the exception's type name, because a file's name holds an app id and nothing that names a game may reach the log.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsSteamLocal : ISteamLocal
{
    private const string SteamKey = @"Software\Valve\Steam";

    private static readonly string[] ArtFiles =
    [
        "header.jpg",
        "library_header.jpg",
        "capsule_616x353.jpg",
        "library_hero.jpg",
        "library_600x900.jpg",
    ];

    private readonly string _steamPath;

    public WindowsSteamLocal()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SteamKey);
        string path = (key?.GetValue("SteamPath") as string ?? "").Replace('/', Path.DirectorySeparatorChar);
        _steamPath = Directory.Exists(path) ? path : "";
    }

    /// <summary>Reads the files of the Steam folder <paramref name="steamPath"/> (the registry is still Steam's own); for tests.</summary>
    internal WindowsSteamLocal(string steamPath) => _steamPath = steamPath;

    public bool Installed => _steamPath.Length > 0;

    public string SteamPath => _steamPath;

    public ClientState ReadClient()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SteamKey);
        if (key is null)
            return default;

        using var active = key.OpenSubKey("ActiveProcess");
        uint pid = Dword(active?.GetValue("pid"));
        uint user = Dword(active?.GetValue("ActiveUser"));
        uint app = Dword(key.GetValue("RunningAppID"));
        return new ClientState(pid != 0 && ProcessExists(pid), user, app);
    }

    public RegistryApp? ReadApp(uint appId)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{SteamKey}\Apps\{appId.ToString(CultureInfo.InvariantCulture)}");
        if (key is null)
            return null;
        return new RegistryApp(key.GetValue("Name") as string, Dword(key.GetValue("Updating")) != 0);
    }

    public string ReadLoginUsers() => ReadText(Path.Combine(_steamPath, "config", "loginusers.vdf"));

    public string ReadLibraryFolders() => ReadText(Path.Combine(_steamPath, "config", "libraryfolders.vdf"));

    public string ReadLocalConfig(uint accountId) =>
        ReadText(Path.Combine(_steamPath, "userdata", accountId.ToString(CultureInfo.InvariantCulture), "config", "localconfig.vdf"));

    public string? ReadManifest(string libraryPath, uint appId)
    {
        string file = Path.Combine(libraryPath, "steamapps", $"appmanifest_{appId.ToString(CultureInfo.InvariantCulture)}.acf");
        return File.Exists(file) ? ReadText(file) : null;
    }

    public IReadOnlyList<ManifestFile> ReadManifests(string libraryPath)
    {
        string folder = Path.Combine(libraryPath, "steamapps");
        if (!Directory.Exists(folder))
            return [];

        var files = new List<ManifestFile>();
        foreach (string file in Directory.EnumerateFiles(folder, "appmanifest_*.acf"))
        {
            try
            {
                files.Add(new ManifestFile(file, ReadText(file), null));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                files.Add(new ManifestFile(file, null, ex.GetType().Name));
            }
        }

        return files;
    }

    public string FindArt(uint appId)
    {
        string id = appId.ToString(CultureInfo.InvariantCulture);
        string cache = Path.Combine(_steamPath, "appcache", "librarycache");
        try
        {
            string folder = Path.Combine(cache, id);
            if (Directory.Exists(folder))
            {
                // Steam keeps the art in the app's folder, and in newer clients one level deeper in a folder named by a hash.
                var folders = new List<string> { folder };
                folders.AddRange(Directory.EnumerateDirectories(folder));
                foreach (string name in ArtFiles)
                {
                    foreach (string candidate in folders)
                    {
                        string path = Path.Combine(candidate, name);
                        if (File.Exists(path))
                            return path;
                    }
                }
            }

            foreach (string name in ArtFiles)
            {
                string flat = Path.Combine(cache, id + "_" + name);
                if (File.Exists(flat))
                    return flat;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return "";
    }

    public IDisposable Watch(LocalChange what, Action<LocalChange> changed) =>
        what == LocalChange.Client ? new RegistryWatch(changed) : new FileWatch(this, changed);

    public void Dispose()
    {
    }

    private static uint Dword(object? value) => value switch
    {
        int i => unchecked((uint)i),
        uint u => u,
        long l => unchecked((uint)l),
        _ => 0,
    };

    private static bool ProcessExists(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>Reads a file Steam may be writing: shared, so that Steam's own write handle does not make it fail.</summary>
    private static string ReadText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>One thread that waits for a change under Steam's registry key, as long as it is open.</summary>
    private sealed class RegistryWatch : IDisposable
    {
        private const int NotifyName = 0x1;
        private const int NotifyLastSet = 0x4;

        private readonly ManualResetEventSlim _stop = new();
        private readonly ManualResetEventSlim _armed = new();
        private readonly Thread _thread;

        public RegistryWatch(Action<LocalChange> changed)
        {
            _thread = new Thread(() => Run(changed)) { IsBackground = true, Name = "Steam registry watch" };
            _thread.Start();
            _armed.Wait(TimeSpan.FromSeconds(2)); // a change made once this returns is reported
        }

        public void Dispose()
        {
            _stop.Set();
            _thread.Join(TimeSpan.FromSeconds(2));
            _stop.Dispose();
            _armed.Dispose();
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegNotifyChangeKeyValue(
            Microsoft.Win32.SafeHandles.SafeRegistryHandle key,
            [MarshalAs(UnmanagedType.Bool)] bool watchSubtree,
            int filter,
            Microsoft.Win32.SafeHandles.SafeWaitHandle eventHandle,
            [MarshalAs(UnmanagedType.Bool)] bool asynchronous);

        private void Run(Action<LocalChange> changed)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SteamKey);
                if (key is null)
                    return;
                using var signal = new ManualResetEvent(false);
                if (!Arm(key, signal))
                    return;
                _armed.Set();
                while (WaitHandle.WaitAny([signal, _stop.WaitHandle]) == 0)
                {
                    // Armed again before the callback runs, so a change made meanwhile is not lost.
                    signal.Reset();
                    if (!Arm(key, signal))
                        return;
                    changed(LocalChange.Client);
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or UnauthorizedAccessException)
            {
                // Stopped while waiting, or the key is gone: there is nothing left to watch.
            }
            finally
            {
                _armed.Set();
            }
        }

        private static bool Arm(RegistryKey key, ManualResetEvent signal) =>
            RegNotifyChangeKeyValue(key.Handle, true, NotifyName | NotifyLastSet, signal.SafeWaitHandle, true) == 0;
    }

    /// <summary>File system watchers on Steam's configuration, the user's local configuration and each library's manifests.</summary>
    private sealed class FileWatch : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = [];

        public FileWatch(WindowsSteamLocal owner, Action<LocalChange> changed)
        {
            string root = owner._steamPath;
            Add(Path.Combine(root, "config"), "*.vdf", false, changed);
            Add(Path.Combine(root, "userdata"), "localconfig.vdf", true, changed);
            try
            {
                var libraries = new List<string> { root };
                if (KeyValues.TryParse(owner.ReadLibraryFolders(), out var folders))
                {
                    foreach (var (_, entry) in folders.Find("libraryfolders")?.Children ?? [])
                    {
                        if (entry.GetText("path") is { Length: > 0 } path && !libraries.Contains(path, StringComparer.OrdinalIgnoreCase))
                            libraries.Add(path);
                    }
                }

                foreach (string library in libraries)
                    Add(Path.Combine(library, "steamapps"), "appmanifest_*.acf", false, changed);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The libraries are not known: Steam's own folder is watched, which is the common case.
                Add(Path.Combine(root, "steamapps"), "appmanifest_*.acf", false, changed);
            }
        }

        public void Dispose()
        {
            foreach (var watcher in _watchers)
                watcher.Dispose();
            _watchers.Clear();
        }

        private void Add(string folder, string filter, bool subdirectories, Action<LocalChange> changed)
        {
            if (!Directory.Exists(folder))
                return;
            var watcher = new FileSystemWatcher(folder, filter)
            {
                IncludeSubdirectories = subdirectories,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            FileSystemEventHandler handler = (_, _) => changed(LocalChange.Files);
            watcher.Changed += handler;
            watcher.Created += handler;
            watcher.Deleted += handler;
            watcher.Renamed += (_, _) => changed(LocalChange.Files);
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }
}
