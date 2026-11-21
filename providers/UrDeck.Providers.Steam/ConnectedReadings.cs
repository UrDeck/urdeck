// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Steam;

/// <summary>
/// The readings that need the user's Steam Web API key: my state and the friends online and playing. It asks for the friend list
/// every ten minutes and for the states at every sample, and turns the answers into rows. A key Steam refuses ends the calls until
/// the key changes. Nothing is written to the log that names a person, a game, an id or the key. Network failures propagate as
/// <see cref="HttpRequestException"/> (or a timeout) for the provider to report as an interruption.
/// </summary>
internal sealed class ConnectedReadings(
    ISteamWeb web,
    SteamCaches caches,
    Func<uint, string?> installedName,
    Func<long?> localAccount,
    TimeProvider time,
    Action<string> log)
{
    internal const string MeState = "me/state";
    internal const string MeAvatar = "me/avatar";
    internal const string FriendsOnline = "friends/online";
    internal const string FriendsPlaying = "friends/playing";

    internal const string NotConnected = "Connect Steam in Settings";
    internal const string Rejected = "Steam rejected the API key; check it in Settings";
    internal const string NoSteamId = "Enter your SteamID in Settings";
    internal const string FriendsPrivate = "Your Steam friends list is private; make it public to show friends";

    internal static readonly string[] Paths = [MeState, MeAvatar, FriendsOnline, FriendsPlaying];
    private static readonly string[] FriendPaths = [FriendsOnline, FriendsPlaying];

    internal static readonly TimeSpan FriendListEvery = TimeSpan.FromMinutes(10);

    internal const int BatchSize = 100;
    internal const int NameLookupsPerSample = 5;
    internal const int AvatarDownloadsPerSample = 20;

    private static readonly string[] StateNames =
        ["Offline", "Online", "Busy", "Away", "Snooze", "Looking to trade", "Looking to play"];

    private readonly HashSet<uint> _unknownNames = [];
    private string _key = "";
    private long _steamId;
    private IReadOnlyList<long> _friends = [];
    private DateTimeOffset _friendsAt = DateTimeOffset.MinValue;
    private bool _friendsPrivate;
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

    /// <summary>Takes one sample with the Web API key (empty when not set) and the configured SteamID (null: this PC's account).</summary>
    public async Task<IReadOnlyList<ReadingUpdate>> SampleAsync(string apiKey, long? configuredSteamId, CancellationToken cancellationToken)
    {
        if (apiKey.Length == 0)
        {
            Forget();
            return LocalReadings.Unavailable(Paths, NotConnected);
        }

        long? steamId = configuredSteamId ?? localAccount();
        if (steamId is null)
        {
            Forget();
            return LocalReadings.Unavailable(Paths, NoSteamId);
        }

        if (!string.Equals(apiKey, _key, StringComparison.Ordinal) || steamId != _steamId)
        {
            Forget();
            _key = apiKey;
            _steamId = steamId.Value;
        }

        if (_rejected)
            return [];

        try
        {
            if (_friendsAt == DateTimeOffset.MinValue || time.GetUtcNow() - _friendsAt >= FriendListEvery)
            {
                try
                {
                    _friends = await web.GetFriendsAsync(_key, _steamId, cancellationToken);
                    _friendsPrivate = false;
                }
                catch (SteamFriendsPrivateException)
                {
                    _friends = [];
                    _friendsPrivate = true;
                }

                _friendsAt = time.GetUtcNow();
            }

            var ids = new List<long>(_friends.Count + 1) { _steamId };
            ids.AddRange(_friends);
            var states = new List<FriendState>(ids.Count);
            for (int i = 0; i < ids.Count; i += BatchSize)
                states.AddRange(await web.GetStatesAsync(_key, ids.GetRange(i, Math.Min(BatchSize, ids.Count - i)), cancellationToken));

            CheckAccount();
            var updates = await Publish(states, cancellationToken);
            if (_friendsPrivate)
            {
                // My own state is public either way; only the friends are not available.
                var mine = updates.Where(u => !FriendPaths.Contains(u.Path, StringComparer.Ordinal)).ToList();
                mine.AddRange(LocalReadings.Unavailable(FriendPaths, FriendsPrivate));
                return mine;
            }

            return updates;
        }
        catch (SteamRejectedException)
        {
            _rejected = true;
            log("Steam rejected the API key; connected readings stop until the key is changed");
            return LocalReadings.Unavailable(Paths, Rejected);
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
        _key = "";
        _steamId = 0;
        _friends = [];
        _friendsAt = DateTimeOffset.MinValue;
        _friendsPrivate = false;
        _rejected = false;
        _mismatchChecked = false;
        _reportedMissing = -1;
    }

    private void CheckAccount()
    {
        if (_mismatchChecked)
            return;
        _mismatchChecked = true;
        if (localAccount() is { } local && local != _steamId)
            log("The configured Steam account is not the one Steam is signed in to on this PC; friends are the configured account's, my game is this PC's");
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
