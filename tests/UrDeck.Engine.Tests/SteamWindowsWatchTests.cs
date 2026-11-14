// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.Versioning;
using Microsoft.Win32;
using UrDeck.Engine.Tests.Support;
using UrDeck.Providers.Steam;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The Windows implementation of the Steam seam: file watching and reading in a made-up Steam folder.</summary>
[SupportedOSPlatform("windows")]
public sealed class SteamWindowsWatchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-steam-tests-" + Guid.NewGuid().ToString("N"));

    public SteamWindowsWatchTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "config"));
        Directory.CreateDirectory(Path.Combine(_root, "steamapps"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [WindowsFact]
    public void A_manifest_written_while_watched_is_reported_and_stops_when_the_watch_is_disposed()
    {
        using var local = new WindowsSteamLocal(_root);
        using var changed = new ManualResetEventSlim();
        var watch = local.Watch(LocalChange.Files, c => changed.Set());

        File.WriteAllText(Path.Combine(_root, "steamapps", "appmanifest_9.acf"), "\"AppState\" { \"appid\" \"9\" }");

        Assert.True(changed.Wait(TimeSpan.FromSeconds(5)));
        watch.Dispose();
        changed.Reset();
        File.WriteAllText(Path.Combine(_root, "steamapps", "appmanifest_9.acf"), "\"AppState\" { \"appid\" \"9\" \"x\" \"y\" }");
        Assert.False(changed.Wait(TimeSpan.FromMilliseconds(700)));
    }

    [WindowsFact]
    public void Manifests_are_read_while_another_process_holds_them_open()
    {
        string file = Path.Combine(_root, "steamapps", "appmanifest_9.acf");
        using var writer = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        writer.Write("\"AppState\" { \"appid\" \"9\" }"u8);
        writer.Flush();
        using var local = new WindowsSteamLocal(_root);

        var manifests = local.ReadManifests(_root);

        Assert.Equal("\"AppState\" { \"appid\" \"9\" }", Assert.Single(manifests).Text);
    }

    [WindowsFact]
    public void A_registry_change_under_steams_key_is_reported_and_stops_when_the_watch_is_disposed()
    {
        using var local = new WindowsSteamLocal(_root);
        using var changed = new ManualResetEventSlim();
        const string Probe = @"Software\Valve\Steam\UrDeckTestProbe";
        try
        {
            var watch = local.Watch(LocalChange.Client, c => changed.Set());
            using (var key = Registry.CurrentUser.CreateSubKey(Probe))
                key.SetValue("n", 1);

            Assert.True(changed.Wait(TimeSpan.FromSeconds(5)));
            watch.Dispose();
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(Probe, throwOnMissingSubKey: false);
        }
    }
}
