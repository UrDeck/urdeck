// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Json;
using UrDeck.Providers.Steam;

namespace UrDeck.Engine.Tests.Support;

/// <summary>A token of Steam's shape (a JWT with the claims the provider reads) for tests; it is not signed and means nothing.</summary>
internal static class SteamTestTokens
{
    public const long Me = 76561197960265728 + 4242;

    public static string Make(long steamId, DateTimeOffset expires, string tag = "")
    {
        static string Part(object value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return Part(new { alg = "none" }) + "." +
            Part(new { sub = steamId.ToString(System.Globalization.CultureInfo.InvariantCulture), exp = expires.ToUnixTimeSeconds(), tag }) + ".sig";
    }
}

/// <summary>A scripted <see cref="ISteamWeb"/>: answers from what a test sets, counts calls, and fails on demand.</summary>
internal sealed class FakeSteamWeb : ISteamWeb
{
    private readonly object _gate = new();
    private readonly List<string> _calls = [];
    private int _renewals;

    public FakeSteamWeb(TimeProvider? time = null) => Time = time ?? TimeProvider.System;

    public TimeProvider Time { get; }

    public List<long> Friends { get; } = [];

    public Dictionary<long, FriendState> States { get; } = [];

    public Dictionary<uint, string> StoreNames { get; } = [];

    public Dictionary<string, byte[]> Avatars { get; } = [];

    public TimeSpan AccessLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Every renewal also returns a new refresh token.</summary>
    public bool RotateRefreshTokens { get; set; }

    /// <summary>Steam refuses every refresh token (revoked, expired).</summary>
    public bool RefuseRefreshToken { get; set; }

    /// <summary>Access tokens Steam refuses; the next renewal makes a new one.</summary>
    public HashSet<string> RefusedAccessTokens { get; } = [];

    /// <summary>Steam refuses the access token of the next call that carries one (once).</summary>
    public bool RefuseNextAccess { get; set; }

    /// <summary>The network is down: every call throws.</summary>
    public bool NetworkDown { get; set; }

    public Queue<QrPoll> Polls { get; } = new();

    public Exception? PollFault { get; set; }

    public QrSession Session { get; set; } = new("client-1", "request-1", "https://s.team/q/1/secret-challenge", TimeSpan.FromSeconds(5));

    public List<string> Revoked { get; } = [];

    public string[] Calls
    {
        get
        {
            lock (_gate)
                return [.. _calls];
        }
    }

    public int Renewals => Volatile.Read(ref _renewals);

    public int Count(string call) => Calls.Count(c => c == call);

    public Task<QrSession> BeginQrAsync(CancellationToken cancellationToken)
    {
        Enter("begin");
        return Task.FromResult(Session);
    }

    public Task<QrPoll> PollQrAsync(QrSession session, CancellationToken cancellationToken)
    {
        Enter("poll");
        if (PollFault != null)
            throw PollFault;
        return Task.FromResult(Polls.Count > 0 ? Polls.Dequeue() : new QrPoll(null, null, false, null));
    }

    public Task<AccessGrant> RenewAsync(string refreshToken, long steamId, CancellationToken cancellationToken)
    {
        Enter("renew");
        if (RefuseRefreshToken)
            throw new SteamRejectedException("refused");
        int n = Interlocked.Increment(ref _renewals);
        var expires = Time.GetUtcNow() + AccessLifetime;
        string? rotated = RotateRefreshTokens ? SteamTestTokens.Make(steamId, expires.AddDays(200), "refresh" + n) : null;
        return Task.FromResult(new AccessGrant(SteamTestTokens.Make(steamId, expires, "access" + n), expires, rotated));
    }

    public Task RevokeAsync(string refreshToken, long steamId, string accessToken, CancellationToken cancellationToken)
    {
        Enter("revoke");
        lock (_gate)
            Revoked.Add(refreshToken);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<long>> GetFriendsAsync(string accessToken, CancellationToken cancellationToken)
    {
        Enter("friends");
        Check(accessToken);
        return Task.FromResult<IReadOnlyList<long>>([.. Friends]);
    }

    public Task<IReadOnlyList<FriendState>> GetStatesAsync(string accessToken, IReadOnlyList<long> steamIds, CancellationToken cancellationToken)
    {
        Enter("states");
        Check(accessToken);
        if (steamIds.Count > 100)
            throw new InvalidOperationException("a batch is at most 100 accounts");
        return Task.FromResult<IReadOnlyList<FriendState>>([.. steamIds.Where(States.ContainsKey).Select(id => States[id])]);
    }

    public Task<string?> GetAppNameAsync(uint appId, CancellationToken cancellationToken)
    {
        Enter("store");
        return Task.FromResult(StoreNames.GetValueOrDefault(appId));
    }

    public Task<byte[]?> GetAvatarAsync(string sha, CancellationToken cancellationToken)
    {
        Enter("avatar");
        return Task.FromResult(Avatars.TryGetValue(sha, out byte[]? bytes) ? bytes : null);
    }

    private void Check(string accessToken)
    {
        if (RefuseNextAccess)
        {
            RefuseNextAccess = false;
            throw new SteamRejectedException("refused");
        }

        if (RefusedAccessTokens.Contains(accessToken))
            throw new SteamRejectedException("refused");
    }

    private void Enter(string call)
    {
        lock (_gate)
            _calls.Add(call);
        if (NetworkDown)
            throw new HttpRequestException("down");
    }
}
