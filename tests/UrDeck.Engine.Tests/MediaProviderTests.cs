// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Data;
using UrDeck.Engine.Tests.Support;
using UrDeck.Providers.Media;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The media provider (media-provider spec) over a fake platform: no player, no Windows.</summary>
public sealed class MediaProviderTests : IDisposable
{
    private static readonly byte[] CoverA = [0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2, 3, 4];
    private static readonly byte[] CoverB = [0x89, (byte)'P', (byte)'N', (byte)'G', 9, 8, 7, 6];
    private static readonly byte[] CoverC = [0x89, (byte)'P', (byte)'N', (byte)'G', 5, 5, 5, 5];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "urdeck-media-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakePlatform _platform = new();
    private readonly RecordingSink _sink = new();
    private readonly FakeTime _time = new();
    private readonly MediaProvider _provider;

    public MediaProviderTests() => _provider = new MediaProvider(_platform, new ArtStore(_root), _time);

    public void Dispose()
    {
        _provider.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class RecordingSink : IReadingSink
    {
        private readonly object _gate = new();
        private readonly List<ReadingUpdate[]> _batches = [];
        private readonly List<string> _logged = [];

        public ReadingUpdate[][] Batches
        {
            get
            {
                lock (_gate)
                    return [.. _batches];
            }
        }

        public string[] Logged
        {
            get
            {
                lock (_gate)
                    return [.. _logged];
            }
        }

        public Dictionary<string, ReadingUpdate> Latest
        {
            get
            {
                var latest = new Dictionary<string, ReadingUpdate>(StringComparer.Ordinal);
                foreach (var batch in Batches)
                {
                    foreach (var update in batch)
                        latest[update.Path] = update;
                }

                return latest;
            }
        }

        public void Publish(string path, ReadingValue value) => PublishBatch([ReadingUpdate.Of(path, value)]);

        public void Unavailable(string path, string reason) => PublishBatch([ReadingUpdate.Missing(path, reason)]);

        public void PublishBatch(IReadOnlyList<ReadingUpdate> updates)
        {
            lock (_gate)
                _batches.Add([.. updates]);
        }

        public void Log(string message)
        {
            lock (_gate)
                _logged.Add(message);
        }
    }

    private sealed class FakePlatform : IMediaSessionPlatform
    {
        private int _reads;

        public MediaSnapshot? Snapshot { get; set; }

        public Exception? ReadFault { get; set; }

        public Func<Task>? BeforeRead { get; set; }

        public bool AcceptsCommands { get; set; } = true;

        public Exception? SendFault { get; set; }

        public List<MediaCommand> Sent { get; } = [];

        public bool Disposed { get; private set; }

        public int Reads => Volatile.Read(ref _reads);

        public event Action<MediaChange>? Changed;

        public void Raise(MediaChange change = MediaChange.Properties) => Changed?.Invoke(change);

        public async Task<MediaSnapshot?> ReadAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _reads);
            if (BeforeRead != null)
                await BeforeRead();
            return ReadFault != null ? throw ReadFault : Snapshot;
        }

        public Task<bool> SendAsync(MediaCommand command, CancellationToken cancellationToken)
        {
            if (SendFault != null)
                throw SendFault;
            Sent.Add(command);
            return Task.FromResult(AcceptsCommands);
        }

        public void Dispose() => Disposed = true;
    }

    private static MediaSnapshot Track(
        string title = "Midnight Drive",
        string artist = "The Sample Band",
        string album = "Demo Tracks",
        MediaStatus status = MediaStatus.Playing,
        byte[]? cover = null,
        bool? canPrevious = true,
        bool? canNext = true,
        double position = 83,
        double duration = 215,
        DateTimeOffset? at = null,
        Func<CancellationToken, Task<byte[]?>>? thumbnail = null) =>
        new("Spotify.exe", title, artist, album, status, canPrevious, canNext, true, TimeSpan.FromSeconds(position), at ?? new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero),
            TimeSpan.FromSeconds(duration), thumbnail ?? (cover == null ? null : _ => Task.FromResult<byte[]?>(cover)));

    private void Start() => _provider.Start(_sink, new ProviderSettings());

    private static bool Settled(Func<bool> condition) => HubTestExtensions.WaitFor(condition);

    private ReadingValue? Value(string path) => _sink.Latest.TryGetValue(path, out var update) ? update.Value : null;

    private string? Reason(string path) => _sink.Latest.TryGetValue(path, out var update) ? update.Reason : null;

    private bool HasPublished(string path) => _sink.Latest.ContainsKey(path);

    // Identity

    [Fact]
    public void IsDeclaredAsTheMediaProvider_WithItsReadingsAndActions()
    {
        var descriptor = ProviderDescriptor.TryCreate(typeof(MediaProvider), out string? reason);

        Assert.True(descriptor != null, reason);
        Assert.Equal("media", descriptor.Id);
        Assert.Equal(
            ["status", "title", "artist", "album", "app", "position", "position/at", "duration", "art", "can/previous", "can/next", "can/playpause"],
            _provider.Describe().Select(r => r.Path));
        Assert.Equal(
            ["control/previous", "control/next", "control/play", "control/pause", "control/playpause"],
            _provider.DescribeActions().Select(a => a.Path));
        Assert.All(_provider.DescribeActions(), a => Assert.False(a.TakesArgument));
    }

    [Fact]
    public async Task ASampleDoesNothing_AndNothingIsPublishedBetweenTheSessionsEvents()
    {
        _platform.Snapshot = Track();
        Start();
        Assert.True(Settled(() => HasPublished("status")));
        int batches = _sink.Batches.Length;
        int reads = _platform.Reads;

        await _provider.SampleAsync(CancellationToken.None);
        await _provider.SampleAsync(CancellationToken.None);
        Thread.Sleep(30);

        Assert.Equal(batches, _sink.Batches.Length);
        Assert.Equal(reads, _platform.Reads);
    }

    // Readings

    [Fact]
    public void ATrackThatPlays_PublishesEveryReading_InOneBatch()
    {
        var reported = new DateTimeOffset(2026, 10, 25, 12, 0, 5, TimeSpan.Zero);
        _platform.Snapshot = Track(at: reported, cover: CoverA);

        Start();
        Assert.True(Settled(() => HasPublished("art")));

        Assert.Single(_sink.Batches);
        Assert.Equal("playing", Value("status")!.Value.Text);
        Assert.Equal("Midnight Drive", Value("title")!.Value.Text);
        Assert.Equal("The Sample Band", Value("artist")!.Value.Text);
        Assert.Equal("Demo Tracks", Value("album")!.Value.Text);
        Assert.Equal("Spotify", Value("app")!.Value.Text);
        Assert.Equal(83, Value("position")!.Value.Number);
        Assert.Equal(reported, Value("position/at")!.Value.Time);
        Assert.Equal(215, Value("duration")!.Value.Number);
        Assert.True(Value("can/previous")!.Value.IsOn);
        Assert.True(Value("can/next")!.Value.IsOn);
        Assert.True(Value("can/playpause")!.Value.IsOn);
        Assert.True(File.Exists(Value("art")!.Value.Text));
    }

    [Theory]
    [InlineData("Playing", "playing")]
    [InlineData("Paused", "paused")]
    [InlineData("Stopped", "stopped")]
    public void TheStatus_IsMappedToItsWord(string statusName, string word)
    {
        _platform.Snapshot = Track(status: Enum.Parse<MediaStatus>(statusName));

        Start();

        Assert.True(Settled(() => HasPublished("status")));
        Assert.Equal(word, Value("status")!.Value.Text);
    }

    [Fact]
    public void NoSession_IsStatusNone_AndEveryOtherReadingIsUnavailableWithTheReason()
    {
        _platform.Snapshot = null;

        Start();

        Assert.True(Settled(() => HasPublished("status")));
        Assert.Equal("none", Value("status")!.Value.Text);
        foreach (var d in _provider.Describe().Where(d => d.Path != "status"))
        {
            Assert.Null(Value(d.Path));
            Assert.Equal("nothing is playing", Reason(d.Path));
        }
    }

    [Fact]
    public void AReadingThePlayerDoesNotReport_IsEmptyOrUnavailable_NeverAGuess()
    {
        _platform.Snapshot = Track(album: "", artist: "", canPrevious: null, duration: 0);

        Start();

        Assert.True(Settled(() => HasPublished("status")));
        Assert.Equal("", Value("artist")!.Value.Text);
        Assert.Equal("", Value("album")!.Value.Text);
        Assert.Equal(0, Value("duration")!.Value.Number);
        Assert.Equal("", Value("art")!.Value.Text);
        Assert.Null(Value("can/previous"));
        Assert.Equal("the player does not say", Reason("can/previous"));
    }

    [Fact]
    public void APositionThatWasNeverStamped_IsStampedWithTheMomentItWasRead()
    {
        _platform.Snapshot = Track() with { PositionAt = null };

        Start();

        Assert.True(Settled(() => HasPublished("position/at")));
        Assert.Equal(_time.GetUtcNow(), Value("position/at")!.Value.Time);
    }

    [Fact]
    public void ATrackChange_PublishesTitleArtistAlbumDurationPositionAndArt_InOneBatch()
    {
        _platform.Snapshot = Track(cover: CoverA);
        Start();
        Assert.True(Settled(() => HasPublished("art")));
        string oldArt = Value("art")!.Value.Text;

        _platform.Snapshot = Track(title: "Second Song", album: "Other Album", duration: 180, position: 0, cover: CoverB);
        _platform.Raise(MediaChange.Properties);

        Assert.True(Settled(() => Value("title")?.Text == "Second Song"));
        var change = _sink.Batches[^1];
        Assert.Equal(2, _sink.Batches.Length);
        Assert.Contains(change, u => u.Path == "title");
        Assert.Contains(change, u => u.Path == "album");
        Assert.Contains(change, u => u.Path == "duration");
        Assert.Contains(change, u => u.Path == "position");
        Assert.Contains(change, u => u.Path == "art");
        Assert.NotEqual(oldArt, Value("art")!.Value.Text);
        Assert.DoesNotContain(change, u => u.Path == "artist");
    }

    [Fact]
    public void APlaybackChange_PublishesOnlyWhatChanged_AndReadsNoArtAgain()
    {
        int thumbnailReads = 0;
        Func<CancellationToken, Task<byte[]?>> thumbnail = _ =>
        {
            Interlocked.Increment(ref thumbnailReads);
            return Task.FromResult<byte[]?>(CoverA);
        };
        _platform.Snapshot = Track(thumbnail: thumbnail);
        Start();
        Assert.True(Settled(() => HasPublished("art")));

        _platform.Snapshot = Track(status: MediaStatus.Paused, thumbnail: thumbnail);
        _platform.Raise(MediaChange.Playback);

        Assert.True(Settled(() => Value("status")?.Text == "paused"));
        Assert.Equal(1, thumbnailReads);
        Assert.Equal(["status"], _sink.Batches[^1].Select(u => u.Path));
    }

    // Album art

    [Fact]
    public void TwoTracksWithTheSameCover_ShareOneFileAndOnePath()
    {
        _platform.Snapshot = Track(cover: CoverA);
        Start();
        Assert.True(Settled(() => HasPublished("art")));
        string first = Value("art")!.Value.Text;

        _platform.Snapshot = Track(title: "Next", cover: CoverA);
        _platform.Raise();

        Assert.True(Settled(() => Value("title")?.Text == "Next"));
        Assert.Equal(first, Value("art")!.Value.Text);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(first)!));
    }

    [Fact]
    public void AtMostTheCurrentAndThePreviousCoverAreKept()
    {
        _platform.Snapshot = Track(title: "1", cover: CoverA);
        Start();
        Assert.True(Settled(() => Value("title")?.Text == "1"));
        string first = Value("art")!.Value.Text;
        foreach (var (title, cover) in new[] { ("2", CoverB), ("3", CoverC) })
        {
            _platform.Snapshot = Track(title: title, cover: cover);
            _platform.Raise();
            Assert.True(Settled(() => Value("title")?.Text == title));
        }

        string last = Value("art")!.Value.Text;
        Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(last)!).Length);
        Assert.False(File.Exists(first));
        Assert.True(File.Exists(last));
    }

    [Fact]
    public void ATrackWithoutACover_PublishesAnEmptyArt_SoTheOldCoverIsNeverShownBesideANewTitle()
    {
        _platform.Snapshot = Track(cover: CoverA);
        Start();
        Assert.True(Settled(() => HasPublished("art")));

        _platform.Snapshot = Track(title: "No Cover", cover: null);
        _platform.Raise();

        Assert.True(Settled(() => Value("title")?.Text == "No Cover"));
        Assert.Equal("", Value("art")!.Value.Text);
        var change = _sink.Batches[^1];
        Assert.Contains(change, u => u.Path == "art");
    }

    [Fact]
    public void ACoverThatCannotBeRead_IsLoggedOnce_WithoutTheTrack_AndTheCardShowsNone()
    {
        _platform.Snapshot = Track(thumbnail: _ => throw new InvalidOperationException("boom"));
        Start();
        Assert.True(Settled(() => HasPublished("art")));
        _platform.Snapshot = Track(title: "Second Song", thumbnail: _ => throw new InvalidOperationException("boom"));
        _platform.Raise();
        Assert.True(Settled(() => Value("title")?.Text == "Second Song"));

        Assert.Equal("", Value("art")!.Value.Text);
        string logged = Assert.Single(_sink.Logged);
        Assert.DoesNotContain("Midnight", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("Second", logged, StringComparison.Ordinal);
    }

    [Fact]
    public void StoppingTheProvider_RemovesTheArtFolder_AndDisposesThePlatform()
    {
        _platform.Snapshot = Track(cover: CoverA);
        Start();
        Assert.True(Settled(() => HasPublished("art")));
        string folder = Path.GetDirectoryName(Value("art")!.Value.Text)!;
        Assert.True(Directory.Exists(folder));

        _provider.Shutdown();

        Assert.False(Directory.Exists(folder));
        Assert.True(_platform.Disposed);
    }

    [Fact]
    public void StartingTheProvider_RemovesTheLeftoversOfAnEarlierRun_ButNotOfARunningOne()
    {
        string stale = Path.Combine(_root, "run-2147483000");
        string running = Path.Combine(_root, "run-" + OtherRunningProcessId());
        Directory.CreateDirectory(stale);
        File.WriteAllText(Path.Combine(stale, "old.png"), "x");
        Directory.CreateDirectory(running);

        Start();
        Assert.True(Settled(() => !Directory.Exists(stale)));

        Assert.True(Directory.Exists(running));
    }

    /// <summary>The id of some other process that is running, whose art folder must not be touched.</summary>
    private static int OtherRunningProcessId()
    {
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id > 4 && process.Id != Environment.ProcessId && !process.HasExited)
                        return process.Id;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Not ours to look at.
                }
            }
        }

        throw new InvalidOperationException("no other process to name");
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ".jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, ".png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39 }, ".gif")]
    [InlineData(new byte[] { 0x42, 0x4D, 0x00 }, ".bmp")]
    [InlineData(new byte[] { 0x01, 0x02 }, ".png")]
    public void AnArtFileGetsTheExtensionItsBytesSay(byte[] image, string extension) =>
        Assert.Equal(extension, ArtStore.Extension(image));

    // Faults

    [Fact]
    public void AMissingService_MakesEveryReadingUnavailable_LogsOneLine_AndRecoversWhenItReturns()
    {
        _platform.ReadFault = new PlatformNotSupportedException("the service is not running");
        Start();
        Assert.True(Settled(() => HasPublished("title")));

        Assert.Null(Value("title"));
        Assert.Contains("the service is not running", Reason("title"), StringComparison.Ordinal);
        Assert.Contains("the service is not running", Reason("status"), StringComparison.Ordinal);
        _platform.Raise();
        _platform.Raise();
        Assert.True(Settled(() => _platform.Reads >= 2));
        Assert.Single(_sink.Logged);

        _platform.ReadFault = null;
        _platform.Snapshot = Track();
        _platform.Raise();

        Assert.True(Settled(() => Value("status")?.Text == "playing"));
        Assert.Contains(_sink.Logged, line => line.Contains("available again", StringComparison.Ordinal));
    }

    [Fact]
    public void AFaultIsRetriedOnItsOwn_UntilTheServiceReturns()
    {
        _platform.ReadFault = new InvalidOperationException("hiccup");
        Start();
        Assert.True(Settled(() => HasPublished("title")));
        _platform.ReadFault = null;
        _platform.Snapshot = Track();

        Assert.True(Settled(() => _time.ActiveTimers > 0));
        _time.Advance(TimeSpan.FromSeconds(31));

        Assert.True(Settled(() => Value("status")?.Text == "playing"));
        Assert.Equal(0, _time.ActiveTimers);
    }

    [Fact]
    public void ASessionThatClosesMidRead_IsNothingPlaying_NotAFault()
    {
        _platform.Snapshot = Track();
        Start();
        Assert.True(Settled(() => Value("status")?.Text == "playing"));

        _platform.Snapshot = null;
        _platform.Raise(MediaChange.Session);

        Assert.True(Settled(() => Value("status")?.Text == "none"));
        Assert.Empty(_sink.Logged);
        Assert.Equal("nothing is playing", Reason("title"));
    }

    [Fact]
    public async Task ARapidTrackChange_NeverShowsTheNewTitleWithTheOldCover()
    {
        var release = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _platform.Snapshot = Track(cover: CoverA);
        Start();
        Assert.True(Settled(() => HasPublished("art")));
        string oldArt = Value("art")!.Value.Text;

        // The second track's cover is slow; the third track arrives while it is read.
        _platform.Snapshot = Track(title: "Second", thumbnail: _ => release.Task);
        _platform.Raise();
        Assert.True(Settled(() => _platform.Reads >= 2));
        _platform.Snapshot = Track(title: "Third", cover: CoverC);
        _platform.Raise();
        release.SetResult(CoverB);

        Assert.True(Settled(() => Value("title")?.Text == "Third"));
        foreach (var batch in _sink.Batches)
        {
            var title = batch.FirstOrDefault(u => u.Path == "title");
            var art = batch.FirstOrDefault(u => u.Path == "art");
            if (title.Value?.Text == "Third")
                Assert.NotEqual(oldArt, art.Value?.Text);
            Assert.False(title.Value?.Text == "Second" && art.Value?.Text == oldArt);
        }

        await Task.CompletedTask;
    }

    // Actions

    [Theory]
    [InlineData("control/previous", "Previous")]
    [InlineData("control/next", "Next")]
    [InlineData("control/play", "Play")]
    [InlineData("control/pause", "Pause")]
    [InlineData("control/playpause", "PlayPause")]
    public async Task AnAction_SendsItsCommandToTheSession_AndIsDone(string path, string commandName)
    {
        _platform.Snapshot = Track();
        Start();

        var result = await _provider.InvokeAsync(path, null, CancellationToken.None);

        Assert.True(result.Succeeded, result.ToString());
        Assert.Equal([Enum.Parse<MediaCommand>(commandName)], _platform.Sent);
    }

    [Fact]
    public async Task WithNoSession_AnActionFails_NothingIsPlaying()
    {
        _platform.Snapshot = null;
        Start();

        var result = await _provider.InvokeAsync("control/playpause", null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("nothing is playing", result.Message);
        Assert.Empty(_platform.Sent);
    }

    [Fact]
    public async Task ACommandThePlayerDoesNotAccept_IsRefusedBeforeItIsSent()
    {
        _platform.Snapshot = Track(canPrevious: false);
        Start();

        var result = await _provider.InvokeAsync("control/previous", null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("does not accept", result.Message, StringComparison.Ordinal);
        Assert.Empty(_platform.Sent);
    }

    [Fact]
    public async Task ACommandWhoseFlagIsUnknown_IsSentAnyway()
    {
        _platform.Snapshot = Track(canNext: null);
        Start();

        var result = await _provider.InvokeAsync("control/next", null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal([MediaCommand.Next], _platform.Sent);
    }

    [Fact]
    public async Task APlayerThatRefusesTheCall_FailsTheAction_AndChangesNothingElse()
    {
        _platform.Snapshot = Track();
        _platform.AcceptsCommands = false;
        Start();
        Assert.True(Settled(() => HasPublished("status")));
        int batches = _sink.Batches.Length;

        var result = await _provider.InvokeAsync("control/next", null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(batches, _sink.Batches.Length);
    }

    [Fact]
    public async Task ACallThatThrows_FailsTheAction_WithTheReason()
    {
        _platform.Snapshot = Track();
        _platform.SendFault = new InvalidOperationException("session went away");
        Start();

        var result = await _provider.InvokeAsync("control/next", null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("session went away", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownAction_Fails()
    {
        var result = await _provider.InvokeAsync("control/louder", null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("control/louder", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnActionDuringATrackChange_IsAnsweredFromTheSessionAsItIsNow()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _platform.Snapshot = Track(cover: CoverA);
        Start();
        Assert.True(Settled(() => HasPublished("art")));

        _platform.Snapshot = Track(title: "Second", canNext: false, cover: CoverB);
        _platform.BeforeRead = () => gate.Task;
        _platform.Raise();
        var action = _provider.InvokeAsync("control/next", null, CancellationToken.None);
        gate.SetResult();
        var result = await action;

        Assert.False(result.Succeeded);
        Assert.True(Settled(() => Value("title")?.Text == "Second"));
    }

    // Names

    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("chrome.exe", "Chrome")]
    [InlineData("Brave", "Brave")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "ZuneMusic")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify")]
    [InlineData("", "")]
    public void AnApplicationId_IsTidiedForDisplay(string id, string shown) =>
        Assert.Equal(shown, MediaProvider.TidyAppId(id));
}
