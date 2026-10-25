// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Providers.Media;

/// <summary>How a player says it is doing.</summary>
internal enum MediaStatus
{
    Stopped,
    Playing,
    Paused,
}

/// <summary>What changed in the media session; the provider reads again whatever the platform says.</summary>
[Flags]
internal enum MediaChange
{
    /// <summary>The current session was replaced, or there is none now.</summary>
    Session = 1,

    /// <summary>The title, artist, album or art of the track changed.</summary>
    Properties = 2,

    /// <summary>The playback status or the commands the player accepts changed.</summary>
    Playback = 4,

    /// <summary>The position or the length changed.</summary>
    Timeline = 8,
}

/// <summary>A transport command the provider can send to the current session.</summary>
internal enum MediaCommand
{
    Previous,
    Next,
    Play,
    Pause,
    PlayPause,
}

/// <summary>
/// What the current session reports at one moment. Every field a player may leave out is optional: a flag is null when the
/// player does not say, a text is empty, a duration is 0.
/// </summary>
/// <param name="AppId">The application user model id of the owning application.</param>
/// <param name="Title">The track title; empty when the player reports none.</param>
/// <param name="Artist">The artist; empty when the player reports none.</param>
/// <param name="Album">The album; empty when the player reports none.</param>
/// <param name="Status">Whether the player plays, is paused, or neither.</param>
/// <param name="CanPrevious">Whether the player accepts Previous; null when it does not say.</param>
/// <param name="CanNext">Whether the player accepts Next; null when it does not say.</param>
/// <param name="CanPlayPause">Whether the player accepts Play or Pause; null when it does not say.</param>
/// <param name="Position">The position in the track at <paramref name="PositionAt"/>.</param>
/// <param name="PositionAt">When the player reported <paramref name="Position"/>; null when it never did.</param>
/// <param name="Duration">The length; zero when unknown.</param>
/// <param name="ReadThumbnail">Reads the album art; null when the session offers none. May be slow, so it is called off the UI thread.</param>
internal sealed record MediaSnapshot(
    string AppId,
    string Title,
    string Artist,
    string Album,
    MediaStatus Status,
    bool? CanPrevious,
    bool? CanNext,
    bool? CanPlayPause,
    TimeSpan Position,
    DateTimeOffset? PositionAt,
    TimeSpan Duration,
    Func<CancellationToken, Task<byte[]?>>? ReadThumbnail);

/// <summary>
/// The Windows media session, behind the only interface the provider uses (the System provider's <c>IGpuPlatform</c> is the
/// precedent), so the logic is tested with a fake and builds on Linux.
/// </summary>
internal interface IMediaSessionPlatform : IDisposable
{
    /// <summary>
    /// Raised, on any thread, when the current session, its properties, its playback state or its timeline changed. The provider
    /// reads again; what changed only decides whether the album art is read again.
    /// </summary>
    event Action<MediaChange>? Changed;

    /// <summary>
    /// What the current session reports now, or null when there is none (including a session that closed while it was read).
    /// Throws when the platform is unavailable or the call fails.
    /// </summary>
    Task<MediaSnapshot?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Sends the command to the current session; false when the player did not accept it. Throws when the call fails.</summary>
    Task<bool> SendAsync(MediaCommand command, CancellationToken cancellationToken);
}

/// <summary>The platform of a system without the media session service (not Windows): every call fails with the reason.</summary>
internal sealed class UnavailableMediaPlatform(string reason) : IMediaSessionPlatform
{
    public event Action<MediaChange>? Changed
    {
        add { }
        remove { }
    }

    public Task<MediaSnapshot?> ReadAsync(CancellationToken cancellationToken) => throw new PlatformNotSupportedException(reason);

    public Task<bool> SendAsync(MediaCommand command, CancellationToken cancellationToken) => throw new PlatformNotSupportedException(reason);

    public void Dispose()
    {
    }
}
