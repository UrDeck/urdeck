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
    private const string Sha1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Sha2 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static readonly string[] AllConnected = ["me/state", "me/avatar", "friends/online", "friends/playing"];

    private readonly string _cache = Path.Combine(Path.GetTempPath(), "urdeck-steam-cache-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTime _time = new();
    private readonly FakeSteamWeb _web;
    private readonly SteamLocalProviderTests.FakeLocal _local = new();
    private readonly SteamLocalProviderTests.Sink _sink = new();
    private readonly SteamProvider _provider;
    private readonly string _refresh;

    public SteamConnectedProviderTests()
    {
        _web = new FakeSteamWeb(_time);
        _sink.CacheFolder = _cache;
        _refresh = SteamTestTokens.Make(SteamTestTokens.Me, _time.GetUtcNow().AddDays(200));
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
        _web.States[SteamTestTokens.Me] = new FriendState(SteamTestTokens.Me, "Pat", 3, 0, Sha2);
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
    public async Task Access_is_renewed_before_it_expires_and_not_every_sample()
    {
        AddFriend(1, "Amy", 1);
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromHours(12));
        await _provider.SampleAsync(CancellationToken.None);
        Assert.Equal(1, _web.Renewals);

        _time.Advance(TimeSpan.FromHours(11.7)); // inside the last half hour of the 24
        await _provider.SampleAsync(CancellationToken.None);
        Assert.Equal(2, _web.Renewals);
    }

    [Fact]
    public async Task An_access_token_steam_refuses_is_renewed_once_and_the_call_repeated()
    {
        AddFriend(1, "Amy", 1);
        StartConnected();
        _provider.SetDemand(AllConnected);
        await _provider.SampleAsync(CancellationToken.None);

        // Steam refuses the access token the provider holds (revoked elsewhere, say) at the next call.
        _time.Advance(TimeSpan.FromMinutes(11));
        _web.RefuseNextAccess = true;
        await _provider.SampleAsync(CancellationToken.None);

        Assert.Equal(2, _web.Renewals);
        Assert.Equal(1, _sink.List("friends/online").Count);
    }

    [Fact]
    public async Task A_rotated_refresh_token_is_stored_and_used_for_the_next_renewal()
    {
        _web.RotateRefreshTokens = true;
        AddFriend(1, "Amy", 1);
        StartConnected();
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);

        var (setting, value) = Assert.Single(_sink.StoredSecrets);
        Assert.Equal("refreshToken", setting);
        Assert.NotEqual(_refresh, value);
        Assert.Equal(1, _sink.List("friends/online").Count);
    }

    [Fact]
    public async Task A_refresh_token_steam_refuses_makes_the_readings_unavailable_and_steam_is_not_called_again()
    {
        AddFriend(1, "Amy", 1);
        StartConnected();
        _provider.SetDemand(AllConnected);
        await _provider.SampleAsync(CancellationToken.None);
        _web.RefuseRefreshToken = true;
        _time.Advance(TimeSpan.FromHours(25));

        await _provider.SampleAsync(CancellationToken.None);

        foreach (string path in AllConnected)
            Assert.Equal("Steam sign-in expired; connect Steam again in Settings", _sink.Latest[path].Reason);
        int calls = _web.Calls.Length;
        _time.Advance(TimeSpan.FromHours(1));
        await _provider.SampleAsync(CancellationToken.None);
        Assert.Equal(calls, _web.Calls.Length);
    }

    [Fact]
    public async Task A_token_that_is_not_steams_is_expired_without_a_call()
    {
        _provider.Start(_sink, new SteamSettings { RefreshToken = new SecretValue("not-a-token") });
        _provider.SetDemand(AllConnected);

        await _provider.SampleAsync(CancellationToken.None);

        Assert.Equal("Steam sign-in expired; connect Steam again in Settings", _sink.Latest["friends/online"].Reason);
        Assert.Empty(_web.Calls);
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
        _web.RefuseRefreshToken = true;
        _time.Advance(TimeSpan.FromHours(25));
        await _provider.SampleAsync(CancellationToken.None);

        Assert.NotEmpty(_sink.Logged);
        foreach (string line in _sink.Logged)
        {
            Assert.DoesNotContain("Secret", line, StringComparison.Ordinal);
            Assert.DoesNotContain("4444", line, StringComparison.Ordinal);
            Assert.DoesNotContain(Base.ToString(CultureInfo.InvariantCulture)[..8], line, StringComparison.Ordinal);
            Assert.DoesNotContain(_refresh[..20], line, StringComparison.Ordinal);
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

    [Fact]
    public async Task Sign_in_shows_the_qr_redraws_a_rotated_one_reports_the_scan_and_returns_the_token()
    {
        var progress = new Progress();
        _web.Polls.Enqueue(new QrPoll(null, null, false, null));
        _web.Polls.Enqueue(new QrPoll("client-2", "https://s.team/q/2/rotated", false, null));
        _web.Polls.Enqueue(new QrPoll(null, null, true, null));
        _web.Polls.Enqueue(new QrPoll(null, null, true, new QrApproval(_refresh, "sample_account")));
        _provider.Start(_sink, new SteamSettings());

        var result = await Advancing(_provider.SignInAsync(new SteamSettings(), progress, CancellationToken.None));

        Assert.True(result.Approved);
        Assert.Equal("sample_account", result.Account);
        Assert.Equal(_refresh, result.Secrets["refreshToken"].Reveal());
        Assert.Equal(["https://s.team/q/1/secret-challenge", "https://s.team/q/2/rotated"], progress.Challenges.Select(c => c.QrText).ToArray());
        Assert.Contains("Approve the sign-in in the Steam app", progress.Statuses);
        Assert.DoesNotContain(_sink.Logged, l => l.Contains("s.team", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sign_in_declined_fails_with_a_reason()
    {
        _web.PollFault = new SteamSignInFailedException("eresult 3");

        var result = await Advancing(_provider.SignInAsync(new SteamSettings(), new Progress(), CancellationToken.None));

        Assert.False(result.Approved);
        Assert.Equal("Steam did not approve the sign-in.", result.Reason);
    }

    [Fact]
    public async Task Sign_in_nobody_scans_expires()
    {
        var result = await Advancing(_provider.SignInAsync(new SteamSettings(), new Progress(), CancellationToken.None), TimeSpan.FromSeconds(10));

        Assert.False(result.Approved);
        Assert.Equal("The sign-in expired. Connect again.", result.Reason);
    }

    [Fact]
    public async Task Sign_in_can_be_cancelled()
    {
        using var cancel = new CancellationTokenSource();
        var task = _provider.SignInAsync(new SteamSettings(), new Progress(), cancel.Token);
        await cancel.CancelAsync();

        var result = await Advancing(task);

        Assert.False(result.Approved);
    }

    [Fact]
    public async Task Sign_in_that_cannot_reach_steam_fails_with_a_reason()
    {
        _web.NetworkDown = true;
        var result = await Advancing(_provider.SignInAsync(new SteamSettings(), new Progress(), CancellationToken.None));
        Assert.False(result.Approved);
        Assert.Equal("Steam could not be reached. Check the connection and try again.", result.Reason);
    }

    [Fact]
    public async Task Sign_out_renews_then_revokes_the_session()
    {
        await _provider.SignOutAsync(new SteamSettings { RefreshToken = new SecretValue(_refresh) }, CancellationToken.None);

        Assert.Equal(["renew", "revoke"], _web.Calls);
        Assert.Equal([_refresh], _web.Revoked);
    }

    [Fact]
    public async Task Sign_out_of_a_session_steam_already_ended_is_not_an_error()
    {
        _web.RefuseRefreshToken = true;

        await _provider.SignOutAsync(new SteamSettings { RefreshToken = new SecretValue(_refresh) }, CancellationToken.None);

        Assert.Empty(_web.Revoked);
    }

    [Fact]
    public async Task Sign_out_when_steam_cannot_be_reached_throws_so_the_host_can_say_so()
    {
        _web.NetworkDown = true;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.SignOutAsync(new SteamSettings { RefreshToken = new SecretValue(_refresh) }, CancellationToken.None));
    }

    [Fact]
    public void The_provider_offers_a_sign_in_and_declares_one_secret_set_by_it()
    {
        Assert.True(_provider.OffersSignIn);
        var property = typeof(SteamSettings).GetProperty(nameof(SteamSettings.RefreshToken))!;
        var secret = (UrDeck.Sdk.Settings.SecretAttribute)Attribute.GetCustomAttribute(property, typeof(UrDeck.Sdk.Settings.SecretAttribute))!;
        Assert.True(secret.SetBySignIn);
    }

    private static string Manifest(int id, string name) =>
        $"\"AppState\"\n{{\n\t\"appid\"\t\"{id}\"\n\t\"name\"\t\"{name}\"\n\t\"StateFlags\"\t\"4\"\n}}\n";

    private void StartConnected() =>
        _provider.Start(_sink, new SteamSettings { RefreshToken = new SecretValue(_refresh) });

    private void AddFriend(int n, string name, int state, ulong game = 0, string sha = "")
    {
        long id = Base + n;
        _web.Friends.Add(id);
        _web.States[id] = new FriendState(id, name, state, game, sha);
    }

    /// <summary>Lets a task that waits on the fake clock finish: moves the clock on in steps until it does.</summary>
    private async Task<T> Advancing<T>(Task<T> task, TimeSpan? step = null)
    {
        for (int i = 0; i < 400 && !task.IsCompleted; i++)
        {
            _time.Advance(step ?? TimeSpan.FromSeconds(5));
            await Task.Delay(2);
        }

        return await task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class Progress : ISignInProgress
    {
        public List<SignInChallenge> Challenges { get; } = [];

        public List<string> Statuses { get; } = [];

        public void Show(SignInChallenge challenge) => Challenges.Add(challenge);

        public void Status(string text) => Statuses.Add(text);
    }
}
