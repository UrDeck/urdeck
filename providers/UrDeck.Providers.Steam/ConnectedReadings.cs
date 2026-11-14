// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Steam;

/// <summary>
/// The readings that need a signed-in Steam: my state and the friends online and playing. It keeps the connection alive (a new
/// access token from the refresh token before the old one stops working), asks for the friend list every ten minutes and for the
/// states at every sample, and turns the answers into rows. A refused token ends the calls until the token changes. Nothing is
/// written to the log that names a person, a game or an id. Network failures propagate as <see cref="HttpRequestException"/>
/// (or a timeout) for the provider to report as an interruption.
/// </summary>
internal sealed class ConnectedReadings(
    ISteamWeb web,
    SteamCaches caches,
    Func<uint, string?> installedName,
    Func<long?> localAccount,
    TimeProvider time,
    Action<string> log,
    Action<string>? refreshTokenRotated = null)
{
    internal const string MeState = "me/state";
    internal const string MeAvatar = "me/avatar";
    internal const string FriendsOnline = "friends/online";
    internal const string FriendsPlaying = "friends/playing";

    internal const string NotConnected = "Connect Steam in Settings";
    internal const string Expired = "Steam sign-in expired; connect Steam again in Settings";

    internal static readonly string[] Paths = [MeState, MeAvatar, FriendsOnline, FriendsPlaying];

    internal static readonly TimeSpan FriendListEvery = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(30);

    internal const int BatchSize = 100;
    internal const int NameLookupsPerSample = 5;
    internal const int AvatarDownloadsPerSample = 20;

    private static readonly string[] StateNames =
        ["Offline", "Online", "Busy", "Away", "Snooze", "Looking to trade", "Looking to play"];

    private readonly HashSet<uint> _unknownNames = [];
    private string _token = "";
    private long _steamId;
    private AccessGrant? _access;
    private IReadOnlyList<long> _friends = [];
    private DateTimeOffset _friendsAt = DateTimeOffset.MinValue;
    private bool _rejected;
    private bool _mismatchChecked;
    private int _reportedMissing = -1;

    public static ReadingDescriptor[] Describe() =>
    [
        new(MeState, ReadingKind.Text, "My state", "Your Steam state: Online, Away, Busy, Snooze, Looking to trade, Looking to play or Offline"),
        new(MeAvatar, ReadingKind.Text, "My avatar", "Path of your Steam avatar (empty when there is none)"),
        new(FriendsOnline, ReadingKind.List, "Friends online", "Friends who are not offline, those playing first")
        {
            Unit = "online",
            EmptyText = "No friends online",
            Fields = FriendFields(),
        },
        new(FriendsPlaying, ReadingKind.List, "Friends playing", "Friends who are playing a game")
        {
            Unit = "playing",
            EmptyText = "Nobody is playing",
            Fields = FriendFields(),
        },
    ];

    /// <summary>Takes one sample with the sign-in's refresh token (empty when not connected).</summary>
    public async Task<IReadOnlyList<ReadingUpdate>> SampleAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (refreshToken.Length == 0)
        {
            Forget();
            return LocalReadings.Unavailable(Paths, NotConnected);
        }

        if (!string.Equals(refreshToken, _token, StringComparison.Ordinal))
        {
            Forget();
            _token = refreshToken;
            if (SteamJwt.SteamId(refreshToken) is not { } id)
            {
                _rejected = true;
                return LocalReadings.Unavailable(Paths, Expired);
            }

            _steamId = id;
        }

        if (_rejected)
            return [];

        try
        {
            if (_friendsAt == DateTimeOffset.MinValue || time.GetUtcNow() - _friendsAt >= FriendListEvery)
            {
                _friends = await WithAccess((token, ct) => web.GetFriendsAsync(token, ct), cancellationToken);
                _friendsAt = time.GetUtcNow();
            }

            var ids = new List<long>(_friends.Count + 1) { _steamId };
            ids.AddRange(_friends);
            var states = new List<FriendState>(ids.Count);
            for (int i = 0; i < ids.Count; i += BatchSize)
            {
                var batch = ids.GetRange(i, Math.Min(BatchSize, ids.Count - i));
                states.AddRange(await WithAccess((token, ct) => web.GetStatesAsync(token, batch, ct), cancellationToken));
            }

            CheckAccount();
            return await Publish(states, cancellationToken);
        }
        catch (SteamRejectedException)
        {
            _rejected = true;
            log("Steam rejected the sign-in; connected readings stop until Steam is connected again");
            return LocalReadings.Unavailable(Paths, Expired);
        }
    }

    private static ReadingFields FriendFields() =>
    [
        new ReadingField("name", ReadingKind.Text, "Friend") { Role = FieldRole.Title },
        new ReadingField("game", ReadingKind.Text, "Playing") { Role = FieldRole.Detail },
        new ReadingField("state", ReadingKind.Text, "State") { Role = FieldRole.Status },
        new ReadingField("avatar", ReadingKind.Text, "Avatar") { Role = FieldRole.Image },
        new ReadingField("link", ReadingKind.Text, "Profile") { Role = FieldRole.Link },
    ];

    private void Forget()
    {
        _token = "";
        _steamId = 0;
        _access = null;
        _friends = [];
        _friendsAt = DateTimeOffset.MinValue;
        _rejected = false;
        _mismatchChecked = false;
        _reportedMissing = -1;
    }

    /// <summary>
    /// Runs <paramref name="call"/> with a current access token. A token Steam refuses is renewed once and the call repeated; a
    /// refresh token Steam refuses (or a second refusal) surfaces as <see cref="SteamRejectedException"/>.
    /// </summary>
    private async Task<T> WithAccess<T>(Func<string, CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        string token = await AccessToken(force: false, cancellationToken);
        try
        {
            return await call(token, cancellationToken);
        }
        catch (SteamRejectedException)
        {
            token = await AccessToken(force: true, cancellationToken);
            return await call(token, cancellationToken);
        }
    }

    private async Task<string> AccessToken(bool force, CancellationToken cancellationToken)
    {
        if (!force && _access is { } current && current.ExpiresAt - time.GetUtcNow() > RenewBefore)
            return current.AccessToken;
        _access = await web.RenewAsync(_token, _steamId, cancellationToken);
        if (_access.NewRefreshToken is { Length: > 0 } rotated)
        {
            // Steam handed out a new refresh token: use it from now on and keep it for the next run.
            _token = rotated;
            refreshTokenRotated?.Invoke(rotated);
        }

        return _access.AccessToken;
    }

    private void CheckAccount()
    {
        if (_mismatchChecked)
            return;
        _mismatchChecked = true;
        if (localAccount() is { } local && local != _steamId)
            log("The connected Steam account is not the one Steam is signed in to on this PC; friends are the connected account's, my game is this PC's");
    }

    private async Task<IReadOnlyList<ReadingUpdate>> Publish(List<FriendState> states, CancellationToken cancellationToken)
    {
        var byId = states.ToDictionary(s => s.SteamId, s => s);
        int missing = _friends.Count(id => !byId.ContainsKey(id));
        if (missing != _reportedMissing)
        {
            bool worthSaying = missing > 0 || _reportedMissing > 0;
            _reportedMissing = missing;
            if (worthSaying)
                log($"Steam did not report on {missing.ToString(CultureInfo.InvariantCulture)} friends; they count as offline");
        }

        var online = _friends
            .Where(id => byId.TryGetValue(id, out var s) && s.PersonaState != 0)
            .Select(id => byId[id])
            .OrderByDescending(s => s.GameId != 0)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.SteamId)
            .ToList();

        int lookups = 0;
        int downloads = 0;
        var rows = new Dictionary<long, ReadingRow>();
        foreach (var friend in online)
        {
            string game = "";
            if (friend.GameId != 0)
            {
                (game, bool looked) = await GameName(friend.GameId, lookups < NameLookupsPerSample, cancellationToken);
                if (looked)
                    lookups++;
            }

            string avatar = friend.AvatarSha.Length == 0 ? "" : caches.AvatarPath(friend.AvatarSha);
            if (avatar.Length == 0 && friend.AvatarSha.Length > 0 && downloads < AvatarDownloadsPerSample)
            {
                downloads++;
                avatar = await Avatar(friend.AvatarSha, cancellationToken);
            }

            var fields = new List<KeyValuePair<string, ReadingValue>>
            {
                KeyValuePair.Create("name", (ReadingValue)friend.Name),
                KeyValuePair.Create("state", (ReadingValue)StateName(friend.PersonaState)),
                KeyValuePair.Create("avatar", (ReadingValue)avatar),
                KeyValuePair.Create("link", (ReadingValue)("steam://url/SteamIDPage/" + friend.SteamId.ToString(CultureInfo.InvariantCulture))),
            };
            if (game.Length > 0)
                fields.Add(KeyValuePair.Create("game", (ReadingValue)game));
            rows[friend.SteamId] = new ReadingRow(friend.SteamId.ToString(CultureInfo.InvariantCulture), fields);
        }

        byId.TryGetValue(_steamId, out var self);
        string myAvatar = "";
        if (self is { AvatarSha.Length: > 0 })
        {
            myAvatar = caches.AvatarPath(self.AvatarSha);
            if (myAvatar.Length == 0)
                myAvatar = await Avatar(self.AvatarSha, cancellationToken);
        }

        return
        [
            ReadingUpdate.Of(MeState, StateName(self?.PersonaState ?? 0)),
            ReadingUpdate.Of(MeAvatar, myAvatar),
            ReadingUpdate.Of(FriendsOnline, ReadingValue.FromList(new ReadingList(online.Select(f => rows[f.SteamId])))),
            ReadingUpdate.Of(FriendsPlaying, ReadingValue.FromList(new ReadingList(online.Where(f => f.GameId != 0).Select(f => rows[f.SteamId])))),
        ];
    }

    private static string StateName(int state) => state >= 0 && state < StateNames.Length ? StateNames[state] : "Online";

    /// <summary>The game's name: this PC's, then the cache, then the store (while <paramref name="mayLookUp"/>); empty until known.</summary>
    private async Task<(string Name, bool LookedUp)> GameName(ulong gameId, bool mayLookUp, CancellationToken cancellationToken)
    {
        if (gameId > uint.MaxValue)
            return ("Non-Steam game", false); // a shortcut the friend added to Steam: it has no store page

        uint appId = (uint)gameId;
        if (installedName(appId) is { Length: > 0 } local)
            return (local, false);
        if (caches.Name(appId) is { Length: > 0 } cached)
            return (cached, false);
        if (!mayLookUp || _unknownNames.Contains(appId))
            return ("", false);

        string? name = await web.GetAppNameAsync(appId, cancellationToken);
        if (string.IsNullOrEmpty(name))
        {
            _unknownNames.Add(appId); // not asked again in this run
            return ("", true);
        }

        caches.SetName(appId, name);
        return (name, true);
    }

    private async Task<string> Avatar(string sha, CancellationToken cancellationToken)
    {
        try
        {
            byte[]? bytes = await web.GetAvatarAsync(sha, cancellationToken);
            return bytes is null ? "" : caches.SaveAvatar(sha, bytes);
        }
        catch (HttpRequestException)
        {
            return ""; // a missing picture is not an interruption: the row has none until the next sample
        }
    }
}
