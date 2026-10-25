// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Media;

/// <summary>
/// What the current Windows media session plays, and the commands to control it. It follows the one session Windows calls
/// current, publishes from the session's events and does not poll (<see cref="SampleAsync"/> does nothing, and the
/// configured interval has no effect). Everything that changed with one event is published in one batch, album art included:
/// the art is read first when the track changed, so a widget never draws a new title beside the old cover.
/// </summary>
[DataProvider("media", "Media", DefaultIntervalMs = 60000, MinIntervalMs = 250)]
public sealed class MediaProvider : IDataProvider, IActionProvider, IDisposable
{
    internal const string Status = "status";
    internal const string Title = "title";
    internal const string Artist = "artist";
    internal const string Album = "album";
    internal const string App = "app";
    internal const string Position = "position";
    internal const string PositionAt = "position/at";
    internal const string Duration = "duration";
    internal const string Art = "art";
    internal const string CanPrevious = "can/previous";
    internal const string CanNext = "can/next";
    internal const string CanPlayPause = "can/playpause";

    internal const string NothingPlaying = "nothing is playing";

    private static readonly TimeSpan ThumbnailTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    private readonly IMediaSessionPlatform _platform;
    private readonly ArtStore _art;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<string, ReadingUpdate> _published = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loggedCauses = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();
    private IReadingSink? _sink;
    private ITimer? _retry;
    private MediaChange _pendingChange;
    private bool _pumping;
    private bool _stopped;
    private bool _faulted;
    private string? _trackKey;
    private string _artPath = "";

    public MediaProvider()
        : this(CreatePlatform(), new ArtStore(ArtStore.DefaultRoot), TimeProvider.System)
    {
    }

    internal MediaProvider(IMediaSessionPlatform platform, ArtStore art, TimeProvider time)
    {
        _platform = platform;
        _art = art;
        _time = time;
    }

    public IReadOnlyList<ReadingDescriptor> Describe() =>
    [
        new(Status, ReadingKind.Text, "Status", "Playback status (playing, paused, stopped or none)"),
        new(Title, ReadingKind.Text, "Title", "Track title"),
        new(Artist, ReadingKind.Text, "Artist", "Track artist"),
        new(Album, ReadingKind.Text, "Album", "Track album"),
        new(App, ReadingKind.Text, "Player", "The application playing"),
        new(Position, ReadingKind.Number, "Position", "Position in the track when last reported") { Unit = "s", Min = 0 },
        new(PositionAt, ReadingKind.Time, "Position time", "The instant the position was reported"),
        new(Duration, ReadingKind.Number, "Length", "Length of the track (0 when unknown)") { Unit = "s", Min = 0 },
        new(Art, ReadingKind.Text, "Cover", "Path of the album art file (empty when there is none)"),
        new(CanPrevious, ReadingKind.OnOff, "Can go back", "Whether the player accepts Previous"),
        new(CanNext, ReadingKind.OnOff, "Can skip", "Whether the player accepts Next"),
        new(CanPlayPause, ReadingKind.OnOff, "Can play or pause", "Whether the player accepts Play or Pause"),
    ];

    public IReadOnlyList<ActionDescriptor> DescribeActions() =>
    [
        new("control/previous", "Previous", "Previous track"),
        new("control/next", "Next", "Next track"),
        new("control/play", "Play", "Play"),
        new("control/pause", "Pause", "Pause"),
        new("control/playpause", "Play/Pause", "Play or pause"),
    ];

    public void Start(IReadingSink sink)
    {
        _sink = sink;
        _art.ClearLeftovers();
        _platform.Changed += OnChanged;
        Request(MediaChange.Session | MediaChange.Properties | MediaChange.Playback | MediaChange.Timeline);
    }

    public void SetDemand(IReadOnlyCollection<string> paths)
    {
        // Everything is published whatever is subscribed to: the session's events are the only cost, and they are free to listen to.
    }

    public Task SampleAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Shutdown()
    {
        lock (_gate)
        {
            if (_stopped)
                return;
            _stopped = true;
            _retry?.Dispose();
            _retry = null;
        }

        _platform.Changed -= OnChanged;
        _stop.Cancel();
        _platform.Dispose();
        _art.Dispose();
    }

    public void Dispose()
    {
        Shutdown();
        _stop.Dispose();
    }

    public async Task<ActionResult> InvokeAsync(string path, ReadingValue? argument, CancellationToken cancellationToken)
    {
        MediaCommand command;
        switch (path)
        {
            case "control/previous":
                command = MediaCommand.Previous;
                break;
            case "control/next":
                command = MediaCommand.Next;
                break;
            case "control/play":
                command = MediaCommand.Play;
                break;
            case "control/pause":
                command = MediaCommand.Pause;
                break;
            case "control/playpause":
                command = MediaCommand.PlayPause;
                break;
            default:
                return ActionResult.Failed($"the media provider has no action '{path}'");
        }

        try
        {
            var snapshot = await _platform.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot == null)
                return ActionResult.Failed(NothingPlaying);

            bool? accepted = command switch
            {
                MediaCommand.Previous => snapshot.CanPrevious,
                MediaCommand.Next => snapshot.CanNext,
                MediaCommand.PlayPause => snapshot.CanPlayPause,
                _ => null,
            };
            if (accepted == false)
                return ActionResult.Failed("the player does not accept it");

            return await _platform.SendAsync(command, cancellationToken).ConfigureAwait(false)
                ? ActionResult.Done
                : ActionResult.Failed("the player did not accept it");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ActionResult.Failed($"the media session could not be reached: {ex.GetBaseException().Message}");
        }
    }

    /// <summary>The application user model id as a name to show: the executable's name, or the last part of a package's.</summary>
    internal static string TidyAppId(string appId)
    {
        string id = appId.Trim();
        int bang = id.IndexOf('!', StringComparison.Ordinal);
        if (bang >= 0 && bang + 1 < id.Length)
            id = id[(bang + 1)..];
        if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            id = id[..^4];
        int underscore = id.IndexOf('_', StringComparison.Ordinal);
        if (underscore > 0)
            id = id[..underscore];
        int dot = id.LastIndexOf('.');
        if (dot >= 0 && dot + 1 < id.Length)
            id = id[(dot + 1)..];
        if (id.Length > 0 && id.All(c => !char.IsUpper(c)))
            id = char.ToUpperInvariant(id[0]) + id[1..];
        return id;
    }

    // Without the Windows part the only platform is the unavailable one; the return type stays the seam.
#pragma warning disable CA1859
    private static IMediaSessionPlatform CreatePlatform()
    {
#if URDECK_WINRT
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return new WindowsMediaSessionPlatform();
        return new UnavailableMediaPlatform("the media session needs Windows 10 version 1809 or later");
#else
        return new UnavailableMediaPlatform("the media session is only available on Windows");
#endif
    }
#pragma warning restore CA1859

    private void OnChanged(MediaChange change) => Request(change);

    /// <summary>Asks for a read; reads that arrive while one runs are merged into one more.</summary>
    private void Request(MediaChange change)
    {
        lock (_gate)
        {
            if (_stopped)
                return;
            _pendingChange |= change;
            if (_pumping)
                return;
            _pumping = true;
        }

        Task.Run(PumpAsync);
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            MediaChange change;
            lock (_gate)
            {
                if (_stopped || _pendingChange == 0)
                {
                    _pumping = false;
                    return;
                }

                change = _pendingChange;
                _pendingChange = 0;
            }

            try
            {
                await ReadAndPublishAsync(change).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Fault(ex);
            }
        }
    }

    private async Task ReadAndPublishAsync(MediaChange change)
    {
        var snapshot = await _platform.ReadAsync(_stop.Token).ConfigureAwait(false);
        if (_faulted)
            Recovered();

        if (snapshot == null)
        {
            _trackKey = null;
            _artPath = "";
            Publish(NoSession());
            return;
        }

        string key = $"{snapshot.Title}\n{snapshot.Artist}\n{snapshot.Album}";
        bool trackChanged = !string.Equals(key, _trackKey, StringComparison.Ordinal);
        string art = _artPath;
        if (trackChanged || (change & (MediaChange.Session | MediaChange.Properties)) != 0)
        {
            // Read first: the batch below carries the new title and its cover together.
            string? read = await ReadArtAsync(snapshot).ConfigureAwait(false);
            if (read != null)
                art = read;
            else if (trackChanged)
                art = "";

            bool changedMeanwhile;
            lock (_gate)
                changedMeanwhile = _pendingChange != 0;
            if (changedMeanwhile)
            {
                // Another event arrived while the cover was read: the next pass reads the track again, and nothing half-new is shown.
                _artPath = art;
                _trackKey = null;
                return;
            }
        }

        _trackKey = key;
        _artPath = art;
        Publish(Compose(snapshot, art));
    }

    /// <summary>The cover's file path, "" when the track has none, or null when it could not be read this time.</summary>
    private async Task<string?> ReadArtAsync(MediaSnapshot snapshot)
    {
        if (snapshot.ReadThumbnail == null)
            return "";

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(ThumbnailTimeout);
            byte[]? bytes = await snapshot.ReadThumbnail(timeout.Token).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0)
                return "";
            return _art.Write(bytes) ?? "";
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !_stop.IsCancellationRequested)
        {
            LogOnce($"art:{ex.GetType().Name}", $"The album art could not be read ({ex.GetType().Name}); the card shows none.");
            return null;
        }
    }

    private List<ReadingUpdate> Compose(MediaSnapshot snapshot, string art)
    {
        var now = _time.GetUtcNow();
        var updates = new List<ReadingUpdate>(12)
        {
            ReadingUpdate.Of(Status, snapshot.Status switch
            {
                MediaStatus.Playing => "playing",
                MediaStatus.Paused => "paused",
                _ => "stopped",
            }),
            ReadingUpdate.Of(Title, snapshot.Title),
            ReadingUpdate.Of(Artist, snapshot.Artist),
            ReadingUpdate.Of(Album, snapshot.Album),
            ReadingUpdate.Of(App, TidyAppId(snapshot.AppId)),
            ReadingUpdate.Of(Position, Math.Max(0, snapshot.Position.TotalSeconds)),
            ReadingUpdate.Of(PositionAt, snapshot.PositionAt ?? now),
            ReadingUpdate.Of(Duration, Math.Max(0, snapshot.Duration.TotalSeconds)),
            ReadingUpdate.Of(Art, art),
            Flag(CanPrevious, snapshot.CanPrevious),
            Flag(CanNext, snapshot.CanNext),
            Flag(CanPlayPause, snapshot.CanPlayPause),
        };
        return updates;
    }

    private static ReadingUpdate Flag(string path, bool? value) =>
        value is { } on ? ReadingUpdate.Of(path, on) : ReadingUpdate.Missing(path, "the player does not say");

    private static List<ReadingUpdate> NoSession()
    {
        var updates = new List<ReadingUpdate>(12) { ReadingUpdate.Of(Status, "none") };
        foreach (string path in new[] { Title, Artist, Album, App, Position, PositionAt, Duration, Art, CanPrevious, CanNext, CanPlayPause })
            updates.Add(ReadingUpdate.Missing(path, NothingPlaying));
        return updates;
    }

    /// <summary>Publishes what differs from what was published before, as one batch.</summary>
    private void Publish(List<ReadingUpdate> updates)
    {
        var changed = new List<ReadingUpdate>(updates.Count);
        lock (_gate)
        {
            if (_stopped)
                return;
            foreach (var update in updates)
            {
                if (_published.TryGetValue(update.Path, out var before) && before.Value == update.Value && before.Reason == update.Reason)
                    continue;
                _published[update.Path] = update;
                changed.Add(update);
            }
        }

        if (changed.Count > 0)
            _sink?.PublishBatch(changed);
    }

    private void Fault(Exception ex)
    {
        Exception cause = ex.GetBaseException();
        string reason = $"the media session is not available ({cause.Message})";
        LogOnce($"fault:{cause.GetType().Name}:{cause.Message}", $"The media session is not available: {cause.GetType().Name}: {cause.Message}");
        lock (_gate)
        {
            if (_stopped)
                return;
            _faulted = true;
            _retry ??= _time.CreateTimer(_ => Request(MediaChange.Session | MediaChange.Properties | MediaChange.Playback | MediaChange.Timeline), null, RetryAfter, RetryAfter);
        }

        _trackKey = null;
        _artPath = "";
        var updates = new List<ReadingUpdate>(12);
        foreach (string path in new[] { Status, Title, Artist, Album, App, Position, PositionAt, Duration, Art, CanPrevious, CanNext, CanPlayPause })
            updates.Add(ReadingUpdate.Missing(path, reason));
        Publish(updates);
    }

    private void Recovered()
    {
        lock (_gate)
        {
            _faulted = false;
            _retry?.Dispose();
            _retry = null;
            _loggedCauses.Clear();
        }

        _sink?.Log("The media session is available again.");
    }

    private void LogOnce(string cause, string message)
    {
        lock (_gate)
        {
            if (!_loggedCauses.Add(cause))
                return;
        }

        _sink?.Log(message);
    }
}
