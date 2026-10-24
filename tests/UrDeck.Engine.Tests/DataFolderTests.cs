// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Startup;
using Xunit;

namespace UrDeck.Engine.Tests;

public class DataFolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-datafolder-" + Guid.NewGuid().ToString("N"));
    private string App => Path.Combine(_root, "app");
    private string Roaming => Path.Combine(_root, "roaming");

    public DataFolderTests() => Directory.CreateDirectory(App);

    public void Dispose()
    {
        try
        { Directory.Delete(_root, true); }
        catch { }
    }

    [Fact]
    public void MarkerPresent_IsPortableAndTheRootIsTheApplicationFolder()
    {
        File.WriteAllText(Path.Combine(App, DataFolder.PortableMarker), "");

        var folder = DataFolder.Resolve(App, Roaming);

        Assert.Equal(DataFolderMode.Portable, folder.Mode);
        Assert.Equal(Path.GetFullPath(App), folder.Root);
        Assert.Equal(Path.Combine(folder.Root, "urdeck-config.json"), folder.ConfigPath);
    }

    [Fact]
    public void MarkerAbsent_IsPerUserUnderRoamingAppData()
    {
        var folder = DataFolder.Resolve(App, Roaming);

        Assert.Equal(DataFolderMode.PerUser, folder.Mode);
        Assert.Equal(Path.Combine(Path.GetFullPath(Roaming), "UrDeck"), folder.Root);
        Assert.Equal(Path.Combine(folder.Root, "urdeck.log"), folder.LogPath);
        Assert.Equal(Path.Combine(folder.Root, "themes"), folder.ThemesFolder);
        Assert.Equal(Path.Combine(folder.Root, "backgrounds"), folder.BackgroundsFolder);
        Assert.Equal(Path.Combine(folder.Root, "cache", "icons"), folder.IconCacheFolder);
        Assert.Equal(Path.Combine(Path.GetFullPath(App), "urdeck-config.default.json"), folder.SamplePath);
    }

    [Fact]
    public void MarkerThatIsAFolder_IsNotPortable()
    {
        Directory.CreateDirectory(Path.Combine(App, DataFolder.PortableMarker));

        Assert.Equal(DataFolderMode.PerUser, DataFolder.Resolve(App, Roaming).Mode);
    }

    [Fact]
    public void PortableRelease_IsTheFolderAboveTheApplicationFolder()
    {
        string unpacked = Path.Combine(_root, "unpacked");

        var folder = DataFolder.Resolve(App, Roaming, unpacked);

        Assert.Equal(DataFolderMode.PortableRelease, folder.Mode);
        Assert.Equal(Path.GetFullPath(unpacked), folder.Root);
        Assert.Equal(Path.GetFullPath(App), folder.ApplicationFolder);
        Assert.Equal($"Data folder: {folder.Root} (portable release)", folder.Describe());
        Assert.False(Directory.Exists(Roaming));
    }

    [Fact]
    public void MarkerBeatsPortableRelease_AndPortableReleaseBeatsPerUser()
    {
        string unpacked = Path.Combine(_root, "unpacked");
        Assert.Equal(DataFolderMode.PortableRelease, DataFolder.Resolve(App, Roaming, unpacked).Mode);
        Assert.Equal(DataFolderMode.PerUser, DataFolder.Resolve(App, Roaming, null).Mode);
        Assert.Equal(DataFolderMode.PerUser, DataFolder.Resolve(App, Roaming, "").Mode);

        File.WriteAllText(Path.Combine(App, DataFolder.PortableMarker), "");
        var folder = DataFolder.Resolve(App, Roaming, unpacked);

        Assert.Equal(DataFolderMode.Portable, folder.Mode);
        Assert.Equal(Path.GetFullPath(App), folder.Root);
    }

    [Fact]
    public void Resolving_WritesNothing()
    {
        DataFolder.Resolve(App, Roaming);

        Assert.False(Directory.Exists(Roaming));
    }

    [Fact]
    public void PluginFolders_AreOneInPortableModeAndTwoPerUser()
    {
        var perUser = DataFolder.Resolve(App, Roaming);
        Assert.Equal(new[] { perUser.FirstPartyPluginsFolder, perUser.UserPluginsFolder }, perUser.PluginFolders);
        Assert.NotEqual(perUser.FirstPartyPluginsFolder, perUser.UserPluginsFolder);

        File.WriteAllText(Path.Combine(App, DataFolder.PortableMarker), "");
        var portable = DataFolder.Resolve(App, Roaming);
        Assert.Equal(portable.FirstPartyPluginsFolder, portable.UserPluginsFolder);
        Assert.Single(portable.PluginFolders);
    }

    [Fact]
    public void Describe_NamesTheFolderAndTheMode()
    {
        var perUser = DataFolder.Resolve(App, Roaming);
        Assert.Equal($"Data folder: {perUser.Root} (per-user)", perUser.Describe());

        File.WriteAllText(Path.Combine(App, DataFolder.PortableMarker), "");
        var portable = DataFolder.Resolve(App, Roaming);
        Assert.Equal($"Data folder: {portable.Root} (portable: urdeck.portable next to the executable)", portable.Describe());
    }

    [Fact]
    public void Log_WithNoDefaultPath_WritesNothingNextToTheExecutable()
    {
        // The default path is process-wide and set once by the host, never by tests; here it is unset, so only the redirect counts.
        string beside = Path.Combine(AppContext.BaseDirectory, "urdeck.log");
        bool existed = File.Exists(beside);
        long before = existed ? new FileInfo(beside).Length : 0;

        UrDeckLog.Info("data folder test line");

        Assert.Equal(existed, File.Exists(beside));
        if (existed)
            Assert.Equal(before, new FileInfo(beside).Length);
    }

    [Fact]
    public void Log_Redirect_StillWins()
    {
        string original = UrDeckLog.LogPath;
        string path = Path.Combine(_root, "redirect.log");
        Directory.CreateDirectory(_root);
        UrDeckLog.LogPath = path;
        try
        {
            UrDeckLog.Info("redirected line");
        }
        finally
        {
            UrDeckLog.LogPath = original;
        }

        Assert.Contains("redirected line", File.ReadAllText(path));
    }
}
