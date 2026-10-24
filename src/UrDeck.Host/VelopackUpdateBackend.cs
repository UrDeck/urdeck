// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Startup;
using UrDeck.Engine.Updates;
using Velopack;
using Velopack.Sources;

namespace UrDeck.Host;

/// <summary>
/// The installer's side of the update service (<see cref="IUpdateBackend"/>), over Velopack's <see cref="UpdateManager"/>.
/// The feed is the releases of <c>UrDeck/urdeck</c> on GitHub, read without an account; a copy on a prerelease also takes
/// prereleases, a stable copy only stable ones. <c>URDECK_UPDATE_SOURCE</c> (a folder or an address) replaces the feed to
/// try the path on one PC without publishing a release; it is not for users (see AGENTS.md).
/// </summary>
internal sealed class VelopackUpdateBackend : IUpdateBackend
{
    private const string RepositoryUrl = "https://github.com/UrDeck/urdeck";
    private const string SourceVariable = "URDECK_UPDATE_SOURCE";

    private readonly UpdateManager _manager;
    private UpdateInfo? _found;

    private VelopackUpdateBackend(UpdateManager manager) => _manager = manager;

    /// <summary>
    /// The backend for this copy, or null when the installer did not install it (a build output, a plain publish): such a
    /// copy never checks.
    /// </summary>
    public static VelopackUpdateBackend? TryCreate(string runningVersion)
    {
        try
        {
            string? overrideSource = Environment.GetEnvironmentVariable(SourceVariable);
            bool prerelease = AppVersion.WithoutMetadata(runningVersion).Contains('-', StringComparison.Ordinal);

            var manager = string.IsNullOrWhiteSpace(overrideSource)
                ? new UpdateManager(new GithubSource(RepositoryUrl, null, prerelease))
                : new UpdateManager(overrideSource);
            if (!manager.IsInstalled)
                return null;

            UrDeckLog.Info(string.IsNullOrWhiteSpace(overrideSource)
                ? $"Updates: checking {RepositoryUrl} ({(prerelease ? "stable and prerelease" : "stable")} releases)"
                : $"Updates: checking {overrideSource} ({SourceVariable})");
            return new VelopackUpdateBackend(manager);
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Updates: the installer could not be reached, no update check: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> CheckAsync(CancellationToken cancellationToken)
    {
        _found = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        return _found?.TargetFullRelease.Version.ToString();
    }

    public async Task DownloadAsync(CancellationToken cancellationToken)
    {
        if (_found is null)
            throw new InvalidOperationException("No update was found to download.");
        await _manager.DownloadUpdatesAsync(_found, cancelToken: cancellationToken).ConfigureAwait(false);
    }

    public void ApplyAndRestart()
    {
        if (_found is not null)
            _manager.ApplyUpdatesAndRestart(_found.TargetFullRelease);
    }

    public void ApplyAfterExit()
    {
        if (_found is not null)
            _manager.WaitExitThenApplyUpdates(_found.TargetFullRelease, silent: true, restart: false);
    }
}
