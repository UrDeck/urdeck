// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using UrDeck.Engine.Tests.Support;
using UrDeck.Providers.Steam;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The connected half of the Steam provider (provider-steam spec) over a scripted Steam: no network, no account.</summary>
public sealed class SteamConnectedProviderTests : IDisposable
{
    private const long Base = LocalReadings.IdBase;
    private const string Key = "0123456789ABCDEF0123456789ABCDEF";
    private const string Sha1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Sha2 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static readonly string[] AllConnected = ["me/state", "me/avatar", "friends/online", "friends/playing"];

    private readonly string _cache = Path.Combine(Path.GetTempPath(), "urdeck-steam-cache-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTime _time = new();
    private readonly FakeSteamWeb _web;
    private readonly SteamLocalProviderTests.FakeLocal _local = new();
    private readonly SteamLocalProviderTests.Sink _sink = new();
    private readonly SteamProvider _provider;

    public SteamConnectedProviderTests()
    {
        _web = new FakeSteamWeb(_time);
        _sink.CacheFolder = _cache;
        _provider = new SteamProvider(_local, _web, _time);
    }

    public void Dispose()
    {
        _provider.Dispose();
        if (Directory.Exists(_cache))
            Directory.Delete(_cache, recursive: true);
    }

    [Fact]
    public async Task Not_connected_says_so_without_calling_steam()
    {
        _provider.Start(_sink, new SteamSettings());

        _provider.SetDemand(AllConnected);
        await _provider.SampleAsync(CancellationToken.None);

        foreach (string path in AllConnected)
            Assert.Equal("Connect Steam in Settings", _sink.Latest[path].Reason);
        Assert.Empty(_web.Calls);
    }

    [Fact]
    public async Task Nothing_calls_steam_while_no_connected_reading_is_subscribed()
    {
        StartConnected();

        _provider.SetDemand(["me/game"]);
        await _provider.SampleAsync(CancellationToken.None);

        Assert.Empty(_web.Calls);
    }

    [Fact]
    public async Task Friends_online_and_playing_are_listed_playing_first_then_by_name()
    {
        _local.Manifests.Add(Manifest(1001, "Sample Quest"));
        AddFriend(1, "zed", 1, sha: Sha1);
        AddFriend(2, "Amy", 1);
        AddFriend(3, "Bob", 3, game: 1001);
        AddFriend(4, "Cy", 0);
        AddFriend(5, "Dee", 4, game: 77, sha: Sha2);
        _web.StoreNames[77] = "Store Game";
        _web.Avatars[Sha1] = [1, 2, 3];
        _web.States[SteamTestIds.Me] = new FriendState(SteamTestIds.Me, "Pat", 3, 0, Sha2);
        _web.Avatars[Sha2] = [4, 5, 6];
        StartConnected();

        _provider.SetDemand(AllConnected);
        await _provider.SampleAsync(CancellationToken.None);

        var online = _sink.List("friends/online");
        Assert.Equal([$"{Base + 3}", $"{Base + 5}", $"{Base + 2}", $"{Base + 1}"], online.Rows.Select(r => r.Key).ToArray());
        Assert.Equal("Sample Quest", online[0].Get("game")!.Value.Text);
        Assert.Equal("Away", online[0].Get("state")!.Value.Text);
        Assert.Equal("Snooze", online[1].Get("state")!.Value.Text);
        Assert.Equal("Store Game", online[1].Get("game")!.Value.Text);
        Assert.Null(online[2].Get("game"));
        Assert.Equal($"steam://url/SteamIDPage/{Base + 3}", online[0].Get("link")!.Value.Text);
        Assert.Equal([$"{Base + 3}", $"{Base + 5}"], _sink.List("friends/playing").Rows.Select(r => r.Key).ToArray());
        Assert.Equal("Away", _sink.Text("me/state"));
        Assert.True(File.Exists(_sink.Text("me/avatar")));

        string avatar = online[3].Get("avatar")!.Value.Text;
        Assert.True(File.Exists(avatar));
        Assert.StartsWith(Path.Combine(_cache, "steam", "avatars"), avatar, StringComparison.Ordinal);
        Assert.Equal("", online[0].Get("avatar")!.Value.Text);
    }

    [Fact]
    public async Task Friends_steam_leaves_out_count_as_offline_and_the_count_is_logged_once_per_change()
    {
        AddFriend(1, "Amy", 1);
        _web.Friends.Add(Base + 2); // Steam reports nothing on this one
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);
        await _provider.SampleAsync(CancellationToken.None);

        Assert.Equal(1, _sink.List("friends/online").Count);
        Assert.Single(_sink.Logged, l => l.Contains("did not report", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Me_without_an_entry_is_offline()
    {
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);

        Assert.Equal("Offline", _sink.Text("me/state"));
        Assert.Equal(0, _sink.List("friends/online").Count);
    }


    [Fact]
    public async Task The_key_and_the_account_are_what_steam_is_asked_with()
    {
        AddFriend(1, "Amy", 1);
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);

        Assert.All(_web.Keys, k => Assert.Equal(Key, k));
        Assert.Equal([SteamTestIds.Me], _web.FriendsOf);
    }

    [Fact]
    public async Task Without_a_steam_id_the_account_steam_is_signed_in_to_on_this_pc_is_used()
    {
        AddFriend(1, "Amy", 1);
        _provider.Start(_sink, new SteamSettings { ApiKey = new SecretValue(Key) });
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);

        Assert.Equal([SteamTestIds.Me], _web.FriendsOf);
        Assert.Equal(1, _sink.List("friends/online").Count);
    }

    [Fact]
    public async Task Without_a_steam_id_and_without_steam_on_this_pc_the_readings_ask_for_one()
    {
        _local.Installed = false;
        _provider.Start(_sink, new SteamSettings { ApiKey = new SecretValue(Key) });
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);

        Assert.Equal("Enter your SteamID in Settings", _sink.Latest["friends/online"].Reason);
        Assert.Empty(_web.Calls);
    }

    [Fact]
    public async Task A_key_steam_refuses_makes_the_readings_unavailable_and_steam_is_not_called_again()
    {
        StartConnected();
        _provider.SetDemand(AllConnected);
        _web.RefuseKey = true;

        await _provider.SampleAsync(CancellationToken.None);

        foreach (string path in AllConnected)
            Assert.Equal("Steam rejected the API key; check it in Settings", _sink.Latest[path].Reason);
        int calls = _web.Calls.Length;
        await _provider.SampleAsync(CancellationToken.None);
        Assert.Equal(calls, _web.Calls.Length);
    }

    [Fact]
    public async Task A_private_friends_list_leaves_my_state_and_says_why_the_friends_are_missing()
    {
        _web.FriendsPrivate = true;
        _web.States[SteamTestIds.Me] = new FriendState(SteamTestIds.Me, "Pat", 1, 0, "");
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);

        Assert.Equal("Online", _sink.Text("me/state"));
        Assert.Equal("Your Steam friends list is private; make it public to show friends", _sink.Latest["friends/online"].Reason);
        Assert.Equal("Your Steam friends list is private; make it public to show friends", _sink.Latest["friends/playing"].Reason);
    }

    [Fact]
    public async Task A_new_key_is_tried_after_the_old_one_was_refused()
    {
        StartConnected();
        _provider.SetDemand(AllConnected);
        _web.RefuseKey = true;
        await _provider.SampleAsync(CancellationToken.None);

        _web.RefuseKey = false;
        _provider.Shutdown();
        using var next = new SteamProvider(_local, _web, _time);
        next.Start(_sink, new SteamSettings { ApiKey = new SecretValue("another-key"), SteamId = SteamTestIds.Me.ToString(CultureInfo.InvariantCulture) });
        next.SetDemand(AllConnected);
        await next.SampleAsync(CancellationToken.None);

        Assert.Equal("Offline", _sink.Text("me/state"));
    }
    [Fact]
    public async Task The_friend_list_is_fetched_every_ten_minutes_and_states_every_sample_in_batches_of_a_hundred()
    {
        for (int i = 1; i <= 250; i++)
            AddFriend(i, "F" + i.ToString(CultureInfo.InvariantCulture), 1);
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);
        Assert.Equal(1, _web.Count("friends"));
        Assert.Equal(3, _web.Count("states")); // 251 accounts with me
        Assert.Equal(250, _sink.List("friends/online").Count);
    }

    [Fact]
    public async Task The_friend_list_is_not_asked_again_before_ten_minutes()
    {
        AddFriend(1, "Amy", 1);
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(9));
        await _provider.SampleAsync(CancellationToken.None);
        Assert.Equal(1, _web.Count("friends"));
        Assert.Equal(2, _web.Count("states"));

        _time.Advance(TimeSpan.FromMinutes(2));
        await _provider.SampleAsync(CancellationToken.None);
        Assert.Equal(2, _web.Count("friends"));
    }

    [Fact]
    public async Task A_network_failure_is_an_interruption_and_the_next_good_sample_recovers()
    {
        AddFriend(1, "Amy", 1);
        StartConnected();
        _provider.SetDemand(AllConnected);
        await _provider.SampleAsync(CancellationToken.None);
        int published = _sink.Batches.Count;

        _web.NetworkDown = true;
        await _provider.SampleAsync(CancellationToken.None);
        Assert.Single(_sink.Interruptions);
        Assert.Equal(published, _sink.Batches.Count);

        _web.NetworkDown = false;
        await _provider.SampleAsync(CancellationToken.None);
        Assert.True(_sink.Batches.Count > published);
        Assert.Single(_sink.Interruptions);
    }

    [Fact]
    public async Task Game_names_come_from_this_pc_then_the_cache_then_the_store_at_most_five_a_sample()
    {
        _local.Manifests.Add(Manifest(1, "Installed Game"));
        AddFriend(1, "A", 1, game: 1);
        for (uint g = 100; g < 108; g++)
        {
            AddFriend((int)g, "P" + g.ToString(CultureInfo.InvariantCulture), 1, game: g);
            _web.StoreNames[g] = "Game " + g.ToString(CultureInfo.InvariantCulture);
        }

        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);
        Assert.Equal(5, _web.Count("store"));
        await _provider.SampleAsync(CancellationToken.None);
        Assert.Equal(8, _web.Count("store")); // the five are cached; the other three are asked once
        await _provider.SampleAsync(CancellationToken.None);
        Assert.Equal(8, _web.Count("store"));
        Assert.All(_sink.List("friends/playing").Rows, r => Assert.NotNull(r.Get("game")));
        Assert.True(File.Exists(Path.Combine(_cache, "steam", "apps.json")));
    }

    [Fact]
    public async Task A_game_the_store_does_not_know_is_asked_once_and_the_row_has_no_game()
    {
        AddFriend(1, "A", 1, game: 555);
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);
        await _provider.SampleAsync(CancellationToken.None);

        Assert.Equal(1, _web.Count("store"));
        Assert.Null(_sink.List("friends/playing")[0].Get("game"));
    }

    [Fact]
    public async Task A_different_connected_account_is_logged_once_without_naming_either()
    {
        _local.Client = new ClientState(true, 9999, 0);
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);
        await _provider.SampleAsync(CancellationToken.None);

        string line = Assert.Single(_sink.Logged, l => l.Contains("not the one", StringComparison.Ordinal));
        Assert.DoesNotContain("9999", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_log_of_a_busy_evening_names_no_person_game_id_or_token()
    {
        AddFriend(1, "Secretname", 1, game: 4444);
        _web.StoreNames[4444] = "Secretgame";
        _web.Friends.Add(Base + 99);
        _local.Client = new ClientState(true, 9999, 0);
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);
        _web.RefuseKey = true;
        _time.Advance(TimeSpan.FromMinutes(11));
        await _provider.SampleAsync(CancellationToken.None);

        Assert.NotEmpty(_sink.Logged);
        foreach (string line in _sink.Logged)
        {
            Assert.DoesNotContain("Secret", line, StringComparison.Ordinal);
            Assert.DoesNotContain("4444", line, StringComparison.Ordinal);
            Assert.DoesNotContain(Base.ToString(CultureInfo.InvariantCulture)[..8], line, StringComparison.Ordinal);
            Assert.DoesNotContain(Key[..12], line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Avatars_are_kept_at_most_three_hundred_and_the_least_recently_used_go_first()
    {
        var caches = new SteamCaches(Path.Combine(_cache, "steam"));
        for (int i = 0; i < SteamCaches.MaxAvatars; i++)
        {
            string path = caches.SaveAvatar(i.ToString("x8", CultureInfo.InvariantCulture), [1]);
            File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i));
        }

        string oldest = Path.Combine(caches.AvatarFolder, "00000000_medium.jpg");
        Assert.NotEqual("", caches.AvatarPath("00000000")); // used now: no longer the oldest
        caches.SaveAvatar("ffffffff", [1]);

        Assert.Equal(SteamCaches.MaxAvatars, Directory.GetFiles(caches.AvatarFolder).Length);
        Assert.True(File.Exists(oldest));
        Assert.False(File.Exists(Path.Combine(caches.AvatarFolder, "00000001_medium.jpg")));
        Assert.Equal("", caches.SaveAvatar("../evil", [1]));
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData("lVbCCgAAAAAAAAAAAAAAAAAAAAA=", "9556c20a00000000000000000000000000000000")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAA=", "")]
    [InlineData("9556c20a00000000000000000000000000000000", "9556c20a00000000000000000000000000000000")]
    [InlineData("0000000000000000000000000000000000000000", "")]
    [InlineData("not base64 or hex!!!!!!!!!!!!", "")]
    [InlineData("", "")]
    public void Avatar_hashes_are_turned_into_the_hosts_hex_form(string reported, string expected)
    {
        Assert.Equal(expected, SteamCaches.NormalizeAvatarHash(reported));
    }


    private static string Manifest(int id, string name) =>
        $"\"AppState\"\n{{\n\t\"appid\"\t\"{id}\"\n\t\"name\"\t\"{name}\"\n\t\"StateFlags\"\t\"4\"\n}}\n";

    private void StartConnected() =>
        _provider.Start(_sink, new SteamSettings { ApiKey = new SecretValue(Key), SteamId = SteamTestIds.Me.ToString(CultureInfo.InvariantCulture) });

    private void AddFriend(int n, string name, int state, ulong game = 0, string sha = "")
    {
        long id = Base + n;
        _web.Friends.Add(id);
        _web.States[id] = new FriendState(id, name, state, game, sha);
    }
}
