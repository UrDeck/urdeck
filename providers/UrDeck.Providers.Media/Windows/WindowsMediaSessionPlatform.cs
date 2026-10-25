// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace UrDeck.Providers.Media;

/// <summary>
/// The current Windows media session (System Media Transport Controls), through the Windows Runtime projection that is compiled
/// into this assembly (docs/perf/now-playing.md). The manager is created on the first call and the session's events are
/// subscribed to from a thread-pool thread, as the host's <c>SystemEvents</c> subscription needs.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
internal sealed class WindowsMediaSessionPlatform : IMediaSessionPlatform
{
    private const int MaxThumbnailBytes = 8 * 1024 * 1024;

    private readonly object _gate = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private bool _disposed;

    public event Action<MediaChange>? Changed;

    public async Task<MediaSnapshot?> ReadAsync(CancellationToken cancellationToken)
    {
        var session = await CurrentSessionAsync().ConfigureAwait(false);
        if (session == null)
            return null;

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var controls = playback.Controls;

            // A player that never sent a time leaves the default instant behind.
            DateTimeOffset? at = timeline.LastUpdatedTime.Year > 2000 ? timeline.LastUpdatedTime : null;
            var duration = timeline.EndTime - timeline.StartTime;
            var thumbnail = properties.Thumbnail;
            return new MediaSnapshot(
                session.SourceAppUserModelId ?? "",
                properties.Title ?? "",
                properties.Artist ?? "",
                properties.AlbumTitle ?? "",
                playback.PlaybackStatus switch
                {
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaStatus.Playing,
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaStatus.Paused,
                    _ => MediaStatus.Stopped,
                },
                controls?.IsPreviousEnabled,
                controls?.IsNextEnabled,
                controls?.IsPlayPauseToggleEnabled,
                timeline.Position,
                at,
                duration > TimeSpan.Zero ? duration : TimeSpan.Zero,
                thumbnail == null ? null : token => ReadThumbnailAsync(thumbnail, token));
        }
        catch (COMException)
        {
            // The player closed while it was read: nothing is playing, which is not a fault.
            return null;
        }
    }

    public async Task<bool> SendAsync(MediaCommand command, CancellationToken cancellationToken)
    {
        var session = await CurrentSessionAsync().ConfigureAwait(false);
        if (session == null)
            return false;

        try
        {
            return command switch
            {
                MediaCommand.Previous => await session.TrySkipPreviousAsync(),
                MediaCommand.Next => await session.TrySkipNextAsync(),
                MediaCommand.Play => await session.TryPlayAsync(),
                MediaCommand.Pause => await session.TryPauseAsync(),
                _ => await session.TryTogglePlayPauseAsync(),
            };
        }
        catch (COMException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            Detach();
            if (_manager != null)
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                _manager = null;
            }
        }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference thumbnail, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            ulong size = stream.Size;
            if (size == 0 || size > MaxThumbnailBytes)
                return null;

            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)size);
            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>The manager (created once, with its subscription made off the caller's thread) and its current session.</summary>
    private async Task<GlobalSystemMediaTransportControlsSession?> CurrentSessionAsync()
    {
        GlobalSystemMediaTransportControlsSessionManager? manager;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            manager = _manager;
        }

        if (manager == null)
        {
            manager = await Task.Run(async () =>
            {
                var created = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                created.CurrentSessionChanged += OnCurrentSessionChanged;
                return created;
            }).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed)
                {
                    manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                    return null;
                }

                _manager = manager;
            }
        }

        var session = manager.GetCurrentSession();
        Attach(session);
        return session;
    }

    /// <summary>Follows <paramref name="session"/>: its events are listened to, and the previous session's are not.</summary>
    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        lock (_gate)
        {
            if (_disposed || ReferenceEquals(_session, session))
                return;
            Detach();
            _session = session;
            if (session != null)
            {
                session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            }
        }
    }

    private void Detach()
    {
        if (_session == null)
            return;
        _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        _session = null;
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) =>
        Changed?.Invoke(MediaChange.Session);

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) =>
        Changed?.Invoke(MediaChange.Properties);

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) =>
        Changed?.Invoke(MediaChange.Playback);

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) =>
        Changed?.Invoke(MediaChange.Timeline);
}
