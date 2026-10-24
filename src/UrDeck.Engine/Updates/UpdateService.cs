// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Updates;

/// <summary>Where the update service is.</summary>
public enum UpdateState
{
    /// <summary>Nothing to offer.</summary>
    None,

    /// <summary>Asking the release feed whether a newer version exists.</summary>
    Checking,

    /// <summary>A newer version is being downloaded.</summary>
    Downloading,

    /// <summary>A newer version is downloaded and waits for a restart.</summary>
    Ready,
}

/// <summary>
/// What the update service needs from the installer. The host implements it over Velopack; the engine knows no installer,
/// and the tests use a fake. A failure is an exception, which the service logs; "nothing new" is not one.
/// </summary>
public interface IUpdateBackend
{
    /// <summary>Asks the feed for a version newer than the running one; its version text, or null when there is none.</summary>
    Task<string?> CheckAsync(CancellationToken cancellationToken);

    /// <summary>Downloads the version <see cref="CheckAsync"/> found.</summary>
    Task DownloadAsync(CancellationToken cancellationToken);

    /// <summary>Closes the app, applies the downloaded version and starts the app again.</summary>
    void ApplyAndRestart();

    /// <summary>Applies the downloaded version after this process has exited, without starting the app again.</summary>
    void ApplyAfterExit();
}

/// <summary>
/// The update check of an installed copy (app-updates spec): the first check one minute after <see cref="Start"/>, then
/// one every 24 hours, each one a check and, for a newer version, a background download. While <c>updates</c> is
/// <c>off</c> nothing is requested. A failure is logged as a warning and the next tick tries again; the deck is never
/// interrupted. Whether the app is installed at all is the caller's question: it does not create the service otherwise.
/// </summary>
public sealed class UpdateService : IDisposable
{
    /// <summary>The wait before the first check.</summary>
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(60);

    /// <summary>The time between checks.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly IUpdateBackend _backend;
    private readonly Func<bool> _enabled;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private ITimer? _timer;
    private volatile UpdateState _state;
    private volatile string? _readyVersion;

    /// <param name="backend">The installer's side.</param>
    /// <param name="enabled">Asked at every tick: whether <c>updates</c> is not <c>off</c>. The configuration may change meanwhile.</param>
    /// <param name="time">The clock of the timer; the tests give their own.</param>
    public UpdateService(IUpdateBackend backend, Func<bool> enabled, TimeProvider? time = null)
    {
        _backend = backend;
        _enabled = enabled;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Raised on a thread-pool thread, once per version, when a download has finished.</summary>
    public event Action<string>? UpdateReady;

    public UpdateState State => _state;

    /// <summary>The version waiting for a restart, or null.</summary>
    public string? ReadyVersion => _readyVersion;

    /// <summary>Starts the schedule. A second call does nothing.</summary>
    public void Start()
    {
        if (_timer is not null)
            return;
        _timer = _time.CreateTimer(_ => _ = CheckNowAsync(), null, FirstCheckDelay, CheckInterval);
    }

    /// <summary>
    /// One tick: check and download. Skipped while updates are off, a version is already waiting or a check is running.
    /// Never throws.
    /// </summary>
    public async Task CheckNowAsync()
    {
        if (!await _gate.WaitAsync(0, CancellationToken.None).ConfigureAwait(false))
            return;

        try
        {
            if (!_enabled())
            {
                UrDeckLog.Info("Update check skipped: updates are off");
                return;
            }

            if (_readyVersion is not null)
                return;

            var token = _stop.Token;
            _state = UpdateState.Checking;
            UrDeckLog.Info("Update check: asking the release feed");
            string? version = await _backend.CheckAsync(token).ConfigureAwait(false);
            if (version is null)
            {
                UrDeckLog.Info("Update check: this is the newest version");
                _state = UpdateState.None;
                return;
            }

            UrDeckLog.Info($"Update check: {version} is available; downloading");
            _state = UpdateState.Downloading;
            await _backend.DownloadAsync(token).ConfigureAwait(false);

            _readyVersion = version;
            _state = UpdateState.Ready;
            UrDeckLog.Info($"Update {version} downloaded; it is applied on restart or when the deck quits");
            UpdateReady?.Invoke(version);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            _state = _readyVersion is null ? UpdateState.None : UpdateState.Ready;
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Update check failed, trying again at the next check: {ex.Message}");
            _state = UpdateState.None;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The user chose "Restart to update": applies the ready version and restarts. Does nothing when none is ready.</summary>
    public void ApplyAndRestart()
    {
        if (_readyVersion is not { } version || !_enabled())
            return;
        UrDeckLog.Info($"Applying update {version} and restarting");
        _backend.ApplyAndRestart();
    }

    /// <summary>The deck is quitting: has the ready version applied after exit, without a restart. Does nothing when none is ready or updates are off.</summary>
    public void ApplyOnQuit()
    {
        if (_readyVersion is not { } version || !_enabled())
            return;
        UrDeckLog.Info($"Quitting with update {version} ready; it is applied after exit");
        try
        {
            _backend.ApplyAfterExit();
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Update {version} could not be scheduled for the exit: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _timer?.Dispose();
        _timer = null;
        _stop.Dispose();
        _gate.Dispose();
    }
}
