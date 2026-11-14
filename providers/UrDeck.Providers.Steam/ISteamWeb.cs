// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Providers.Steam;

/// <summary>A QR sign-in in progress: what Steam answered to the first call.</summary>
/// <param name="ClientId">Identifies the session in the poll; changes when Steam rotates the challenge.</param>
/// <param name="RequestId">Secret of this sign-in, sent with every poll.</param>
/// <param name="ChallengeUrl">What the QR code encodes. A sign-in secret: drawn locally, never sent anywhere, never logged.</param>
/// <param name="Interval">How long to wait between polls.</param>
internal sealed record QrSession(string ClientId, string RequestId, string ChallengeUrl, TimeSpan Interval);

/// <summary>What one poll of a QR sign-in answered.</summary>
/// <param name="NewClientId">Set when Steam rotated the challenge; the QR code must be drawn again from <paramref name="NewChallengeUrl"/>.</param>
/// <param name="NewChallengeUrl">The rotated challenge.</param>
/// <param name="Scanned">The phone has scanned the code and waits for approval.</param>
/// <param name="Approved">Set when the user approved: the tokens.</param>
internal sealed record QrPoll(string? NewClientId, string? NewChallengeUrl, bool Scanned, QrApproval? Approved);

/// <summary>The result of an approved sign-in.</summary>
internal sealed record QrApproval(string RefreshToken, string AccountName);

/// <summary>A short-lived access token and when it stops working; <paramref name="NewRefreshToken"/> when Steam also rotated the refresh token.</summary>
internal sealed record AccessGrant(string AccessToken, DateTimeOffset ExpiresAt, string? NewRefreshToken = null);

/// <summary>What Steam says about one account: its name, state, game and avatar.</summary>
/// <param name="SteamId">The SteamID64.</param>
/// <param name="Name">The persona name.</param>
/// <param name="PersonaState">0 offline, 1 online, 2 busy, 3 away, 4 snooze, 5 looking to trade, 6 looking to play.</param>
/// <param name="GameId">The game being played; 0 when none.</param>
/// <param name="AvatarSha">The hash of the avatar picture; empty when none.</param>
internal sealed record FriendState(long SteamId, string Name, int PersonaState, ulong GameId, string AvatarSha);

/// <summary>Steam no longer accepts the token it was given (revoked, expired or from another kind of sign-in).</summary>
internal sealed class SteamRejectedException(string message) : Exception(message)
{
}

/// <summary>The user declined the QR sign-in, or Steam dropped it.</summary>
internal sealed class SteamSignInFailedException(string message) : Exception(message)
{
}

/// <summary>
/// Everything the provider asks of Steam's servers: the sign-in, the access token, the friends and, without any token, a game's
/// name and an avatar. The real implementation is <see cref="SteamWebClient"/>; tests use a scripted fake. A network failure
/// throws <see cref="HttpRequestException"/> (or a timeout), a refused token <see cref="SteamRejectedException"/>.
/// </summary>
internal interface ISteamWeb
{
    /// <summary>Starts a QR sign-in as the Steam mobile app's companion (the only kind whose token Steam renews).</summary>
    Task<QrSession> BeginQrAsync(CancellationToken cancellationToken);

    Task<QrPoll> PollQrAsync(QrSession session, CancellationToken cancellationToken);

    /// <summary>A new access token from the refresh token.</summary>
    Task<AccessGrant> RenewAsync(string refreshToken, long steamId, CancellationToken cancellationToken);

    /// <summary>Ends the session the refresh token belongs to.</summary>
    Task RevokeAsync(string refreshToken, long steamId, string accessToken, CancellationToken cancellationToken);

    Task<IReadOnlyList<long>> GetFriendsAsync(string accessToken, CancellationToken cancellationToken);

    /// <summary>The states of up to 100 accounts; an account Steam leaves out is simply missing from the answer.</summary>
    Task<IReadOnlyList<FriendState>> GetStatesAsync(string accessToken, IReadOnlyList<long> steamIds, CancellationToken cancellationToken);

    /// <summary>A game's name from the store, or null when the store does not know it.</summary>
    Task<string?> GetAppNameAsync(uint appId, CancellationToken cancellationToken);

    /// <summary>An avatar picture, or null when there is none.</summary>
    Task<byte[]?> GetAvatarAsync(string sha, CancellationToken cancellationToken);
}
