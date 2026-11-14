// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Providers.Steam;

/// <summary>
/// Steam: what this PC knows about the account (the game being played, recent games, downloads), always, with no setup and no
/// network, and, after Connect, my state and the friends (through <see cref="ConnectedReadings"/>). It does only what the
/// subscribed readings need: it calls Steam only while a connected reading is subscribed, it watches the registry only while a
/// <c>me/</c> or <c>client/</c> reading is subscribed, reads <c>localconfig.vdf</c> only for <c>games/recent</c> and the manifests
/// only for the download readings, and publishes the local readings from changes, not from the sampling interval.
/// </summary>
[DataProvider("steam", "Steam", DefaultIntervalMs = 60000, MinIntervalMs = 30000)]
public sealed class SteamProvider : DataProvider<SteamSettings>, IDisposable
{
    internal const string RefreshTokenKey = "refreshToken";

    internal static readonly TimeSpan FileDebounce = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan DownloadPoll = TimeSpan.FromSeconds(5);

    private const string ScanInstructions = "Open the Steam app on your phone, choose Steam Guard, scan this code and approve.";

    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly ISteamLocal _local;
    private readonly ISteamWeb _web;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private LocalReadings? _readings;
    private ConnectedReadings? _connected;
    private IReadingSink? _sink;
    private IDisposable? _clientWatch;
    private IDisposable? _fileWatch;
    private ITimer? _debounce;
    private ITimer? _poll;
    private Groups _demand;
    private string _refreshToken = "";
    private bool _connectedDemand;
    private bool _stopped;

    public SteamProvider()
        : this(CreateLocal(), new SteamWebClient(), TimeProvider.System)
    {
    }

    internal SteamProvider(ISteamLocal local, ISteamWeb web, TimeProvider time)
    {
        _local = local;
        _web = web;
        _time = time;
    }

    [Flags]
    private enum Groups
    {
        None = 0,
        Client = 1,
        Recent = 2,
        Downloads = 4,
    }

    public override IReadOnlyList<ReadingDescriptor> Describe() =>
        [.. LocalReadings.Describe(), .. ConnectedReadings.Describe()];

    public override bool OffersSignIn => true;

    public override string SignInDescription => "Shows your friends and what they play";

    public override string SignOutHint => "the authorized devices in the Steam mobile app";

    public override void Start(IReadingSink sink, SteamSettings settings)
    {
        lock (_gate)
        {
            _sink = sink;
            _readings = new LocalReadings(_local, _time, sink.Log);
            _refreshToken = settings.RefreshToken?.Reveal() ?? "";
            string cache = sink.CacheFolder.Length > 0 ? sink.CacheFolder : Path.Combine(Path.GetTempPath(), "UrDeck");
            _connected = new ConnectedReadings(
                _web, new SteamCaches(Path.Combine(cache, "steam")), _readings.GameName, _readings.CurrentAccount, _time, sink.Log, StoreRotated);
        }
    }

    public override void SetDemand(IReadOnlyCollection<string> paths)
    {
        Groups wanted = Groups.None;
        bool connectedWanted = false;
        foreach (string path in paths)
        {
            if (LocalReadings.ClientPaths.Contains(path, StringComparer.Ordinal))
                wanted |= Groups.Client;
            else if (path == LocalReadings.GamesRecent)
                wanted |= Groups.Recent;
            else if (LocalReadings.DownloadPaths.Contains(path, StringComparer.Ordinal))
                wanted |= Groups.Downloads;
            else if (ConnectedReadings.Paths.Contains(path, StringComparer.Ordinal))
                connectedWanted = true;
        }

        Groups added;
        bool connectedAdded;
        lock (_gate)
        {
            if (_stopped || _readings is null)
                return;
            added = wanted & ~_demand;
            _demand = wanted;
            connectedAdded = connectedWanted && !_connectedDemand;
            _connectedDemand = connectedWanted;

            if (wanted.HasFlag(Groups.Client) && _clientWatch is null)
                _clientWatch = _local.Watch(LocalChange.Client, OnChanged);
            else if (!wanted.HasFlag(Groups.Client))
                Release(ref _clientWatch);

            if ((wanted & (Groups.Recent | Groups.Downloads)) != 0 && _fileWatch is null)
                _fileWatch = _local.Watch(LocalChange.Files, OnChanged);
            else if ((wanted & (Groups.Recent | Groups.Downloads)) == 0)
                Release(ref _fileWatch);

            if (!wanted.HasFlag(Groups.Downloads))
                StopPolling();
            if ((wanted & (Groups.Recent | Groups.Downloads)) == 0)
            {
                _debounce?.Dispose();
                _debounce = null;
            }
        }

        if (added != Groups.None)
            Refresh(added);

        // Not connected is known without asking Steam: say so now rather than at the next sample.
        if (connectedAdded && _refreshToken.Length == 0)
            _sink?.PublishBatch(LocalReadings.Unavailable(ConnectedReadings.Paths, ConnectedReadings.NotConnected));
    }

    public override async Task SampleAsync(CancellationToken cancellationToken)
    {
        ConnectedReadings? connected;
        IReadingSink? sink;
        lock (_gate)
        {
            connected = _stopped || !_connectedDemand ? null : _connected;
            sink = _sink;
        }

        if (connected is null || sink is null)
            return;
        try
        {
            var updates = await connected.SampleAsync(_refreshToken, cancellationToken);
            if (updates.Count > 0)
                sink.PublishBatch(updates);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            sink.Interrupted("Steam could not be reached");
        }
    }

    public override async Task<SignInResult> SignInAsync(SteamSettings settings, ISignInProgress progress, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _web.BeginQrAsync(cancellationToken);
            var expires = _time.GetUtcNow() + ChallengeLifetime;
            progress.Show(SignInChallenge.Qr(session.ChallengeUrl, ScanInstructions, expires));
            progress.Status("Waiting for the Steam app");

            bool scannedShown = false;
            int failures = 0;
            while (true)
            {
                await Task.Delay(session.Interval, _time, cancellationToken);
                if (_time.GetUtcNow() > expires)
                    return SignInResult.Failed("The sign-in expired. Connect again.");

                QrPoll poll;
                try
                {
                    poll = await _web.PollQrAsync(session, cancellationToken);
                    failures = 0;
                }
                catch (HttpRequestException) when (++failures < 3)
                {
                    continue; // a dropped request is not a declined sign-in
                }

                if (poll.Approved is { } approved)
                {
                    var secrets = new Dictionary<string, SecretValue> { [RefreshTokenKey] = new SecretValue(approved.RefreshToken) };
                    return SignInResult.Success(approved.AccountName, secrets);
                }

                if (poll.NewClientId is { } clientId && poll.NewChallengeUrl is { } url)
                {
                    session = session with { ClientId = clientId, ChallengeUrl = url };
                    expires = _time.GetUtcNow() + ChallengeLifetime;
                    progress.Show(SignInChallenge.Qr(url, ScanInstructions, expires));
                }

                if (poll.Scanned && !scannedShown)
                {
                    scannedShown = true;
                    progress.Status("Approve the sign-in in the Steam app");
                }
            }
        }
        catch (SteamSignInFailedException)
        {
            return SignInResult.Failed("Steam did not approve the sign-in.");
        }
        catch (HttpRequestException)
        {
            return SignInResult.Failed("Steam could not be reached. Check the connection and try again.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return SignInResult.Failed("The sign-in was cancelled.");
        }
    }

    public override async Task SignOutAsync(SteamSettings settings, CancellationToken cancellationToken)
    {
        string token = settings.RefreshToken?.Reveal() ?? "";
        if (token.Length == 0 || SteamJwt.SteamId(token) is not { } steamId)
            return;
        try
        {
            // The revoke call is signed with an access token, so renew one first.
            var grant = await _web.RenewAsync(token, steamId, cancellationToken);
            await _web.RevokeAsync(token, steamId, grant.AccessToken, cancellationToken);
        }
        catch (SteamRejectedException)
        {
            // Steam no longer accepts this sign-in: there is no session left to end.
        }
    }

    public override void Shutdown()
    {
        lock (_gate)
        {
            if (_stopped)
                return;
            _stopped = true;
            Release(ref _clientWatch);
            Release(ref _fileWatch);
            _debounce?.Dispose();
            _debounce = null;
            StopPolling();
        }

        _local.Dispose();
        (_web as IDisposable)?.Dispose();
    }

    public void Dispose() => Shutdown();

    private void StoreRotated(string refreshToken)
    {
        _refreshToken = refreshToken;
        _sink?.StoreSecret(RefreshTokenKey, new SecretValue(refreshToken));
    }

    private static ISteamLocal CreateLocal() =>
        OperatingSystem.IsWindows() ? new WindowsSteamLocal() : new NoSteamLocal();

    private static void Release(ref IDisposable? watch)
    {
        watch?.Dispose();
        watch = null;
    }

    private void OnChanged(LocalChange change)
    {
        Groups demand;
        lock (_gate)
        {
            if (_stopped)
                return;
            demand = _demand;
        }

        if (change.HasFlag(LocalChange.Client) && demand.HasFlag(Groups.Client))
            Refresh(Groups.Client);

        // The files move in bursts while Steam writes them, and a registry change can follow a download: wait for it to settle.
        if ((demand & (Groups.Recent | Groups.Downloads)) != 0)
            ScheduleFiles();
    }

    private void ScheduleFiles()
    {
        lock (_gate)
        {
            if (_stopped)
                return;
            _debounce?.Dispose();
            _debounce = _time.CreateTimer(_ => RefreshFiles(), null, FileDebounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void RefreshFiles()
    {
        Groups demand;
        lock (_gate)
        {
            demand = _demand;
        }

        Refresh(demand & (Groups.Recent | Groups.Downloads));
    }

    private void Refresh(Groups groups)
    {
        List<ReadingUpdate> updates = [];
        bool downloadsActive = false;
        IReadingSink? sink;
        lock (_gate)
        {
            sink = _sink;
            if (_stopped || _readings is null || sink is null)
                return;
            if (groups.HasFlag(Groups.Client))
                updates.AddRange(_readings.ReadClient());
            if (groups.HasFlag(Groups.Recent))
                updates.AddRange(_readings.ReadRecent());
            if (groups.HasFlag(Groups.Downloads))
            {
                updates.AddRange(_readings.ReadDownloads(out downloadsActive));
                // Steam rewrites a manifest lazily while it downloads, so a running download is read on a timer too.
                if (downloadsActive)
                    _poll ??= _time.CreateTimer(_ => Refresh(Groups.Downloads), null, DownloadPoll, DownloadPoll);
                else
                    StopPolling();
            }
        }

        if (updates.Count > 0)
            sink.PublishBatch(updates);
    }

    private void StopPolling()
    {
        _poll?.Dispose();
        _poll = null;
    }
}
