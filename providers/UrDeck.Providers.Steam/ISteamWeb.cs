// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Providers.Steam;

/// <summary>What Steam says about one account: its name, state, game and avatar.</summary>
/// <param name="SteamId">The SteamID64.</param>
/// <param name="Name">The persona name.</param>
/// <param name="PersonaState">0 offline, 1 online, 2 busy, 3 away, 4 snooze, 5 looking to trade, 6 looking to play.</param>
/// <param name="GameId">The game being played; 0 when none.</param>
/// <param name="AvatarSha">The hash of the avatar picture; empty when none.</param>
internal sealed record FriendState(long SteamId, string Name, int PersonaState, ulong GameId, string AvatarSha);

/// <summary>Steam refuses the Web API key (wrong, revoked or never activated).</summary>
internal sealed class SteamRejectedException(string message) : Exception(message)
{
}

/// <summary>Steam will not give out the friend list: the profile's friends list is not public.</summary>
internal sealed class SteamFriendsPrivateException(string message) : Exception(message)
{
}

/// <summary>
/// Everything the provider asks of Steam's servers: the friends and their states with the user's own Web API key and, without any
/// key, a game's name and an avatar. The real implementation is <see cref="SteamWebClient"/>; tests use a scripted fake. A network
/// failure throws <see cref="HttpRequestException"/> (or a timeout), a refused key <see cref="SteamRejectedException"/>, a friend
/// list that is not public <see cref="SteamFriendsPrivateException"/>.
/// </summary>
internal interface ISteamWeb
{
    /// <summary>The account's friends (SteamID64s).</summary>
    Task<IReadOnlyList<long>> GetFriendsAsync(string apiKey, long steamId, CancellationToken cancellationToken);

    /// <summary>The states of up to 100 accounts; an account Steam leaves out is simply missing from the answer.</summary>
    Task<IReadOnlyList<FriendState>> GetStatesAsync(string apiKey, IReadOnlyList<long> steamIds, CancellationToken cancellationToken);

    /// <summary>A game's name from the store, or null when the store does not know it.</summary>
    Task<string?> GetAppNameAsync(uint appId, CancellationToken cancellationToken);

    /// <summary>An avatar picture, or null when there is none.</summary>
    Task<byte[]?> GetAvatarAsync(string sha, CancellationToken cancellationToken);
}
