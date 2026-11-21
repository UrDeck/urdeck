// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Providers.Steam;

namespace UrDeck.Engine.Tests.Support;

/// <summary>Ids the Steam tests share.</summary>
internal static class SteamTestIds
{
    /// <summary>The account the fake Steam PC is signed in to (account 4242).</summary>
    public const long Me = 76561197960265728 + 4242;
}

/// <summary>A scripted <see cref="ISteamWeb"/>: answers from what a test sets, counts calls, and fails on demand.</summary>
internal sealed class FakeSteamWeb : ISteamWeb
{
    private readonly object _gate = new();
    private readonly List<string> _calls = [];
    private readonly List<string> _keys = [];
    private readonly List<long> _friendsOf = [];

    public FakeSteamWeb(TimeProvider? time = null) => Time = time ?? TimeProvider.System;

    public TimeProvider Time { get; }

    public List<long> Friends { get; } = [];

    public Dictionary<long, FriendState> States { get; } = [];

    public Dictionary<uint, string> StoreNames { get; } = [];

    public Dictionary<string, byte[]> Avatars { get; } = [];

    /// <summary>Steam refuses the API key.</summary>
    public bool RefuseKey { get; set; }

    /// <summary>The account keeps its friends list private.</summary>
    public bool FriendsPrivate { get; set; }

    /// <summary>The network is down: every call throws.</summary>
    public bool NetworkDown { get; set; }

    public string[] Calls
    {
        get
        {
            lock (_gate)
                return [.. _calls];
        }
    }

    /// <summary>The API key of every keyed call.</summary>
    public string[] Keys
    {
        get
        {
            lock (_gate)
                return [.. _keys];
        }
    }

    /// <summary>The account of every friend-list call.</summary>
    public long[] FriendsOf
    {
        get
        {
            lock (_gate)
                return [.. _friendsOf];
        }
    }

    public int Count(string call) => Calls.Count(c => c == call);

    public Task<IReadOnlyList<long>> GetFriendsAsync(string apiKey, long steamId, CancellationToken cancellationToken)
    {
        Enter("friends", apiKey);
        lock (_gate)
            _friendsOf.Add(steamId);
        if (FriendsPrivate)
            throw new SteamFriendsPrivateException("private");
        return Task.FromResult<IReadOnlyList<long>>([.. Friends]);
    }

    public Task<IReadOnlyList<FriendState>> GetStatesAsync(string apiKey, IReadOnlyList<long> steamIds, CancellationToken cancellationToken)
    {
        Enter("states", apiKey);
        if (steamIds.Count > 100)
            throw new InvalidOperationException("a batch is at most 100 accounts");
        return Task.FromResult<IReadOnlyList<FriendState>>([.. steamIds.Where(States.ContainsKey).Select(id => States[id])]);
    }

    public Task<string?> GetAppNameAsync(uint appId, CancellationToken cancellationToken)
    {
        Enter("store", null);
        return Task.FromResult(StoreNames.GetValueOrDefault(appId));
    }

    public Task<byte[]?> GetAvatarAsync(string sha, CancellationToken cancellationToken)
    {
        Enter("avatar", null);
        return Task.FromResult(Avatars.TryGetValue(sha, out byte[]? bytes) ? bytes : null);
    }

    private void Enter(string call, string? apiKey)
    {
        lock (_gate)
        {
            _calls.Add(call);
            if (apiKey != null)
                _keys.Add(apiKey);
        }

        if (NetworkDown)
            throw new HttpRequestException("down");
        if (apiKey != null && RefuseKey)
            throw new SteamRejectedException("refused");
    }
}
