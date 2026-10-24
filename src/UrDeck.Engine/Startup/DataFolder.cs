// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Startup;

/// <summary>How the data folder was chosen.</summary>
public enum DataFolderMode
{
    /// <summary>The user's roaming application data folder (<c>%APPDATA%\UrDeck</c>).</summary>
    PerUser,

    /// <summary>The application folder, because it holds the <see cref="DataFolder.PortableMarker"/> file.</summary>
    Portable,

    /// <summary>The folder a release's portable zip was unpacked into: it holds the application folder and survives an update.</summary>
    PortableRelease,
}

/// <summary>
/// Where the user's files live, decided once at startup (data-folder spec). Takes both folders as arguments and touches
/// no Windows API and no file other than looking for the marker, so it runs on Linux and in tests.
/// </summary>
public sealed class DataFolder
{
    /// <summary>The marker file whose presence in the application folder selects the portable mode.</summary>
    public const string PortableMarker = "urdeck.portable";

    /// <summary>The name of the per-user folder inside the roaming application data folder.</summary>
    public const string FolderName = "UrDeck";

    private DataFolder(string root, string applicationFolder, DataFolderMode mode)
    {
        Root = root;
        ApplicationFolder = applicationFolder;
        Mode = mode;
    }

    /// <summary>The folder that holds the configuration, the log, <c>themes/</c>, <c>backgrounds/</c>, <c>plugins/</c> and <c>cache/</c>.</summary>
    public string Root { get; }

    /// <summary>The folder of the executable: first-party plugins, the sample page and the icon.</summary>
    public string ApplicationFolder { get; }

    public DataFolderMode Mode { get; }

    public string ConfigPath => Path.Combine(Root, "urdeck-config.json");

    /// <summary>The sample page shipped in the application folder; it seeds the first run.</summary>
    public string SamplePath => Path.Combine(ApplicationFolder, "urdeck-config.default.json");

    public string LogPath => Path.Combine(Root, "urdeck.log");

    public string ThemesFolder => Path.Combine(Root, "themes");

    public string BackgroundsFolder => Path.Combine(Root, "backgrounds");

    /// <summary>Plugins the user adds.</summary>
    public string UserPluginsFolder => Path.Combine(Root, "plugins");

    /// <summary>First-party plugins; they update with the application. The same folder as <see cref="UserPluginsFolder"/> in portable mode.</summary>
    public string FirstPartyPluginsFolder => Path.Combine(ApplicationFolder, "plugins");

    public string IconCacheFolder => Path.Combine(Root, "cache", "icons");

    /// <summary>Where the migration backup of a configuration version is kept.</summary>
    public string BackupPath(int fromVersion) => Path.Combine(Root, $"urdeck-config.v{fromVersion}.json");

    /// <summary>The plugin folders in priority order: the application's first, then the user's (once when they are the same folder).</summary>
    public IReadOnlyList<string> PluginFolders
    {
        get
        {
            string first = FirstPartyPluginsFolder;
            string user = UserPluginsFolder;
            return PathsEqual(first, user) ? new[] { first } : new[] { first, user };
        }
    }

    /// <summary>The log line that names the folder and why it was chosen.</summary>
    public string Describe() => Mode switch
    {
        DataFolderMode.Portable => $"Data folder: {Root} (portable: {PortableMarker} next to the executable)",
        DataFolderMode.PortableRelease => $"Data folder: {Root} (portable release)",
        _ => $"Data folder: {Root} (per-user)",
    };

    /// <summary>
    /// Chooses the data folder: the marker first, then the portable release's root, then the per-user folder.
    /// </summary>
    /// <param name="applicationFolder">The folder of the executable.</param>
    /// <param name="roamingAppData">The user's roaming application data folder.</param>
    /// <param name="portableReleaseRoot">
    /// The folder a release's portable zip was unpacked into, when the copy runs from one (the host asks the installer's
    /// runtime); null otherwise. The engine sees a folder, not the installer.
    /// </param>
    public static DataFolder Resolve(string applicationFolder, string roamingAppData, string? portableReleaseRoot = null)
    {
        string app = Path.GetFullPath(applicationFolder);

        // A marker that is a folder does not count: it must be a file.
        if (File.Exists(Path.Combine(app, PortableMarker)))
            return new DataFolder(app, app, DataFolderMode.Portable);

        if (!string.IsNullOrWhiteSpace(portableReleaseRoot))
            return new DataFolder(Path.GetFullPath(portableReleaseRoot), app, DataFolderMode.PortableRelease);

        return new DataFolder(Path.Combine(Path.GetFullPath(roamingAppData), FolderName), app, DataFolderMode.PerUser);
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
