// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Secrets;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Settings;

/// <summary>Where a sign-in stands.</summary>
public enum SignInPhase
{
    /// <summary>Waiting for the user to approve; <see cref="SignInState.Challenge"/> says what to do once the provider reported it.</summary>
    Running,

    /// <summary>Approved: the secrets and the account are stored and the provider restarted.</summary>
    Approved,

    /// <summary>Ended without credentials; <see cref="SignInState.Message"/> says why.</summary>
    Failed,

    /// <summary>The user (or closing the window) cancelled it.</summary>
    Cancelled,
}

/// <summary>What the window shows of a sign-in at one moment.</summary>
/// <param name="Phase">Where it stands.</param>
/// <param name="Challenge">The latest challenge; null until the provider reports one. Never logged.</param>
/// <param name="Status">The provider's latest one-line status; null when none.</param>
/// <param name="Message">The reason when failed; null otherwise.</param>
/// <param name="Account">The account name when approved; null otherwise. Never logged.</param>
public sealed record SignInState(SignInPhase Phase, SignInChallenge? Challenge, string? Status, string? Message, string? Account)
{
    /// <summary>Whether the sign-in has ended.</summary>
    public bool IsFinished => Phase != SignInPhase.Running;
}

/// <summary>One sign-in of one provider: its state, changes and cancellation. Made by <see cref="ProviderSignIn.Start"/>.</summary>
public sealed class SignInSession : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<SignInState> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private SignInState _state = new(SignInPhase.Running, null, null, null, null);
    private ITimer? _watchdog;

    internal SignInSession(string providerId)
    {
        ProviderId = providerId;
    }

    /// <summary>The provider being signed in.</summary>
    public string ProviderId { get; }

    /// <summary>The current state.</summary>
    public SignInState State
    {
        get
        {
            lock (_gate)
                return _state;
        }
    }

    /// <summary>Completes with the final state; never faults.</summary>
    public Task<SignInState> Completion => _done.Task;

    /// <summary>Raised (on any thread) when the state changed, the last time with a finished state.</summary>
    public event Action<SignInState>? Changed;

    internal CancellationToken Token => _cts.Token;

    /// <summary>Releases the cancellation source once the provider is done with it.</summary>
    public void Dispose() => _cts.Dispose();

    /// <summary>Cancels the sign-in: nothing is stored, even if the provider approves later. Does nothing once it has ended.</summary>
    public void Cancel() => Finish(new SignInState(SignInPhase.Cancelled, null, null, null, null), logLine: $"Sign-in {ProviderId}: cancelled");

    internal void Update(Func<SignInState, SignInState> change)
    {
        SignInState next;
        lock (_gate)
        {
            if (_state.IsFinished)
                return;
            next = _state = change(_state);
        }

        Changed?.Invoke(next);
    }

    internal void SetWatchdog(ITimer? timer)
    {
        ITimer? old;
        lock (_gate)
        {
            if (_state.IsFinished)
            {
                timer?.Dispose();
                return;
            }

            old = _watchdog;
            _watchdog = timer;
        }

        old?.Dispose();
    }

    /// <summary>Ends the session with <paramref name="final"/> unless it already ended; returns whether this call ended it.</summary>
    internal bool Finish(SignInState final, string? logLine)
    {
        ITimer? watchdog;
        lock (_gate)
        {
            if (_state.IsFinished)
                return false;
            _state = final;
            watchdog = _watchdog;
            _watchdog = null;
        }

        watchdog?.Dispose();
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // the provider is done with the session
        }
        catch (AggregateException)
        {
            // a callback of the provider threw while cancelling; the session has ended regardless
        }

        if (logLine != null)
            UrDeckLog.Info(logLine);
        Changed?.Invoke(final);
        _done.TrySetResult(final);
        return true;
    }

}

/// <summary>What disconnecting did.</summary>
/// <param name="ServiceTold">Whether the provider ended the session at its service.</param>
/// <param name="Hint">Where the user can remove a session that could not be ended; empty when the service was told.</param>
/// <param name="Error">Why the stored secrets could not be deleted; null when they were.</param>
public sealed record DisconnectResult(bool ServiceTold, string Hint, string? Error)
{
    /// <summary>Whether the stored secrets are gone.</summary>
    public bool Deleted => Error == null;
}

/// <summary>Whether a provider's account is connected, as the Providers section shows it.</summary>
/// <param name="Offered">The provider offers a sign-in.</param>
/// <param name="Description">What connecting gives, one line.</param>
/// <param name="SignOutHint">Where a session that could not be ended is listed at the service.</param>
/// <param name="Connected">A value exists for a secret set by sign-in.</param>
/// <param name="Account">The account name kept with the stored secret; null with none (or from the environment).</param>
/// <param name="EnvironmentName">The variable that supplies the secret when it comes from the environment; null otherwise.</param>
/// <param name="StoredHere">Whether Credential Manager holds a secret set by sign-in (Disconnect can delete it).</param>
public sealed record ConnectionInfo(bool Offered, string Description, string SignOutHint, bool Connected, string? Account, string? EnvironmentName, bool StoredHere = false);

/// <summary>
/// The sign-ins of providers (see the provider-sign-in spec): starts one on a provider instance that is not started, shows what it
/// reports through a <see cref="SignInSession"/>, validates and stores its result in one step and restarts the provider with it;
/// disconnects. Away from any window. At most one sign-in per provider runs; the log never carries a challenge, a code, a secret or
/// an account.
/// </summary>
public sealed class ProviderSignIn
{
    /// <summary>How long after a challenge's expiry the host waits for the provider before it gives up on it.</summary>
    public static readonly TimeSpan ExpiryGrace = TimeSpan.FromSeconds(30);

    /// <summary>How long the provider gets to end the session at its service.</summary>
    public static readonly TimeSpan SignOutLimit = TimeSpan.FromSeconds(10);

    private readonly ConfigStore _config;
    private readonly ProviderRegistry _providers;
    private readonly ISecretStore _secrets;
    private readonly ReadingHub _hub;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<string, SignInSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public ProviderSignIn(ConfigStore config, ProviderRegistry providers, ISecretStore secrets, ReadingHub hub, TimeProvider? time = null)
    {
        _config = config;
        _providers = providers;
        _secrets = secrets;
        _hub = hub;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>What the section shows for the provider's account.</summary>
    public ConnectionInfo GetConnection(string providerId)
    {
        var descriptor = _providers.Get(providerId);
        if (descriptor == null)
            return new ConnectionInfo(false, "", "", false, null, null);

        var (offered, description, hint) = Ask(descriptor);
        string? account = null;
        string? environment = null;
        bool connected = false;
        bool stored = false;
        foreach (var secret in SignInSecrets(descriptor))
        {
            var found = _secrets.Find(descriptor.Id, secret.Key);
            if (found != null)
            {
                connected = true;
                if (found.Source == SecretSource.Environment)
                    environment ??= found.SourceName;
            }

            if (_secrets.FindStored(descriptor.Id, secret.Key) is { } kept)
            {
                stored = true;
                account ??= kept.Context;
            }
        }

        return new ConnectionInfo(offered, description, hint, connected, account, environment, stored);
    }

    /// <summary>
    /// Starts a sign-in with the values of the provider's form (as the connection test takes them) and returns its session at once;
    /// a sign-in already running for the provider is cancelled. The session ends by itself: approved, failed or cancelled.
    /// </summary>
    public SignInSession Start(string providerId, IReadOnlyDictionary<string, string> values)
    {
        var session = new SignInSession(providerId);
        var descriptor = _providers.Get(providerId);
        if (descriptor == null)
        {
            session.Finish(Failed($"There is no provider '{providerId}'."), $"Sign-in {providerId}: failed (no such provider)");
            return session;
        }

        ISignIn? signIn = null;
        IDataProvider? instance = null;
        try
        {
            instance = descriptor.CreateInstance();
            signIn = instance as ISignIn;
        }
        catch (Exception ex)
        {
            session.Finish(Failed(ex.GetBaseException().Message), $"Sign-in {providerId}: failed ({ex.GetBaseException().Message})");
            return session;
        }

        if (signIn is not { Offered: true })
        {
            (instance as IDisposable)?.Dispose();
            session.Finish(Failed($"{descriptor.Name} has no sign-in."), $"Sign-in {providerId}: failed (not offered)");
            return session;
        }

        SignInSession? previous;
        lock (_gate)
        {
            _sessions.TryGetValue(providerId, out previous);
            _sessions[providerId] = session;
        }

        previous?.Cancel();

        ProviderSettings settings;
        try
        {
            var raw = new ProviderSettings { ExtensionData = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase) };
            foreach (var setting in descriptor.Schema.Settings.Where(s => !s.IsSecret))
            {
                if (!ProviderSettingsEditor.TryParseValue(setting, values.GetValueOrDefault(setting.Key) ?? "", out var element, out string? problem))
                    throw new InvalidOperationException(problem);
                if (element is { } value)
                    raw.ExtensionData[setting.Key] = value;
            }

            settings = ProviderSettingsBinder.Bind(descriptor, raw, _secrets).Settings;
        }
        catch (Exception ex)
        {
            session.Finish(Failed(ex.GetBaseException().Message), $"Sign-in {providerId}: failed (settings)");
            (instance as IDisposable)?.Dispose();
            return session;
        }

        UrDeckLog.Info($"Sign-in {providerId}: started");
        var progress = new Progress(this, session);
        Task<SignInResult> work;
        try
        {
            work = Task.Run(() => signIn.SignInAsync(settings, progress, session.Token));
        }
        catch (Exception ex)
        {
            work = Task.FromException<SignInResult>(ex);
        }

        _ = work.ContinueWith(
            task =>
            {
                try
                {
                    Complete(descriptor, session, task);
                }
                finally
                {
                    (instance as IDisposable)?.Dispose();
                    session.Dispose();
                }
            },
            TaskScheduler.Default);
        return session;
    }

    /// <summary>The provider's latest sign-in (running or ended) if it has one; null otherwise.</summary>
    public SignInSession? Current(string providerId)
    {
        lock (_gate)
            return _sessions.GetValueOrDefault(providerId);
    }

    /// <summary>Cancels the sign-in of every provider (the section or the window closed).</summary>
    public void CancelAll()
    {
        List<SignInSession> sessions;
        lock (_gate)
        {
            sessions = _sessions.Values.ToList();
            _sessions.Clear();
        }

        foreach (var session in sessions)
            session.Cancel();
    }

    /// <summary>Cancels the provider's sign-in, if one is running.</summary>
    public void Cancel(string providerId)
    {
        SignInSession? session;
        lock (_gate)
            _sessions.Remove(providerId, out session);
        session?.Cancel();
    }

    /// <summary>
    /// Disconnects: asks the provider to end its session at the service (best effort, <see cref="SignOutLimit"/>), then deletes the
    /// stored secrets whatever happened and restarts the provider without them.
    /// </summary>
    public async Task<DisconnectResult> DisconnectAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var descriptor = _providers.Get(providerId);
        if (descriptor == null)
            return new DisconnectResult(false, "", $"There is no provider '{providerId}'.");

        Cancel(providerId);
        var (_, _, hint) = Ask(descriptor);
        bool told = false;
        bool hasStored = SignInSecrets(descriptor).Any(s => _secrets.FindStored(descriptor.Id, s.Key) != null);
        if (hasStored)
        {
            IDataProvider? instance = null;
            try
            {
                instance = descriptor.CreateInstance();
                if (instance is ISignIn { Offered: true } signIn)
                {
                    var raw = _config.Config.Providers?.GetValueOrDefault(descriptor.Id);
                    var settings = ProviderSettingsBinder.Bind(descriptor, raw, _secrets).Settings;
                    using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    limit.CancelAfter(SignOutLimit);
                    await signIn.SignOutAsync(settings, limit.Token).WaitAsync(SignOutLimit, _time, limit.Token).ConfigureAwait(false);
                    told = true;
                }
            }
            catch (Exception)
            {
                told = false;
            }
            finally
            {
                (instance as IDisposable)?.Dispose();
            }
        }

        string? error = null;
        foreach (var secret in SignInSecrets(descriptor))
        {
            try
            {
                _secrets.Remove(descriptor.Id, secret.Key);
            }
            catch (SecretStoreException ex)
            {
                error ??= ex.Message;
            }
        }

        UrDeckLog.Info($"Sign-out {providerId}: {(told ? "service told" : "service not told")}");
        _hub.SecretsChanged();
        return new DisconnectResult(told, told ? "" : hint, error);
    }

    private void Complete(ProviderDescriptor descriptor, SignInSession session, Task<SignInResult> task)
    {
        string id = descriptor.Id;
        if (session.State.IsFinished)
            return; // cancelled, expired or replaced while the provider went on: its late result is dropped

        if (task.IsCanceled || task.Exception?.GetBaseException() is OperationCanceledException)
        {
            session.Finish(Failed("The sign-in was interrupted."), $"Sign-in {id}: failed (interrupted)");
            return;
        }

        if (task.Exception != null)
        {
            string reason = task.Exception.GetBaseException().Message;
            session.Finish(Failed(reason), $"Sign-in {id}: failed ({reason})");
            return;
        }

        var result = task.Result;
        if (result == null)
        {
            session.Finish(Failed("The provider returned no result."), $"Sign-in {id}: failed (provider returned no result)");
            return;
        }

        if (!result.Approved)
        {
            string reason = string.IsNullOrWhiteSpace(result.Reason) ? "The sign-in failed." : result.Reason;
            session.Finish(Failed(reason), $"Sign-in {id}: failed ({reason})");
            return;
        }

        var declared = SignInSecrets(descriptor).Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stored = new List<string>();
        string? refusal = null;
        string? unexpected = result.Secrets.Keys.FirstOrDefault(k => !declared.Contains(k));
        if (unexpected != null)
            refusal = "The provider returned an unexpected secret";
        else if (result.Secrets.Count == 0 || result.Secrets.Values.Any(v => v == null || v.IsEmpty))
            refusal = "The provider returned no secret";

        if (refusal != null)
        {
            UrDeckLog.Warn($"Sign-in {id}: the provider returned {(unexpected != null ? "a secret it does not declare as set by sign-in" : "no usable secret")}; nothing stored");
            session.Finish(Failed(refusal + "."), $"Sign-in {id}: failed ({refusal})");
            return;
        }

        // Stored together or not at all: a failure, or a cancel that arrives meanwhile, undoes the secrets already written.
        try
        {
            foreach (var (key, value) in result.Secrets)
            {
                string name = descriptor.Schema.Secrets.First(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase)).Key;
                _secrets.Save(id, name, value.Reveal(), string.IsNullOrWhiteSpace(result.Account) ? null : result.Account);
                stored.Add(name);
            }
        }
        catch (SecretStoreException ex)
        {
            Undo();
            session.Finish(Failed(ex.Message), $"Sign-in {id}: failed (the secret could not be stored)");
            return;
        }

        if (!session.Finish(new SignInState(SignInPhase.Approved, null, null, null, result.Account), $"Sign-in {id}: approved"))
        {
            Undo();
            return;
        }

        _hub.SecretsChanged();

        void Undo()
        {
            foreach (string name in stored)
            {
                try
                {
                    _secrets.Remove(id, name);
                }
                catch (SecretStoreException)
                {
                    // nothing more can be done
                }
            }
        }
    }

    private static SignInState Failed(string reason) => new(SignInPhase.Failed, null, null, reason, null);

    private static IEnumerable<UrDeck.Engine.Settings.SettingDescriptor> SignInSecrets(ProviderDescriptor descriptor) =>
        descriptor.Schema.Secrets.Where(s => s.SetBySignIn);

    private static (bool Offered, string Description, string Hint) Ask(ProviderDescriptor descriptor)
    {
        if (!typeof(ISignIn).IsAssignableFrom(descriptor.ProviderType) || !SignInSecrets(descriptor).Any())
            return (false, "", "");
        try
        {
            var instance = descriptor.CreateInstance();
            try
            {
                return instance is ISignIn { Offered: true } signIn ? (true, signIn.Description, signIn.SignOutHint) : (false, "", "");
            }
            finally
            {
                (instance as IDisposable)?.Dispose();
            }
        }
        catch (Exception)
        {
            return (false, "", "");
        }
    }

    private sealed class Progress(ProviderSignIn owner, SignInSession session) : ISignInProgress
    {
        public void Show(SignInChallenge challenge)
        {
            if (challenge == null)
                return;
            session.Update(state => state with { Challenge = challenge, Status = null });

            // The provider gets until the challenge expires plus a grace to return; then the sign-in ends as expired.
            var wait = challenge.ExpiresAt - owner._time.GetUtcNow() + ExpiryGrace;
            if (wait < TimeSpan.Zero)
                wait = TimeSpan.Zero;
            string id = session.ProviderId;
            session.SetWatchdog(owner._time.CreateTimer(
                _ => session.Finish(Failed("The code expired."), $"Sign-in {id}: failed (expired)"),
                null,
                wait,
                Timeout.InfiniteTimeSpan));
        }

        public void Status(string text) =>
            session.Update(state => state with { Status = string.IsNullOrWhiteSpace(text) ? null : text });
    }

    /// <summary>The time left of <paramref name="challenge"/> as "m:ss"; "0:00" once it passed.</summary>
    public static string TimeLeft(SignInChallenge challenge, DateTimeOffset now)
    {
        var left = challenge.ExpiresAt - now;
        if (left < TimeSpan.Zero)
            left = TimeSpan.Zero;
        return string.Create(CultureInfo.InvariantCulture, $"{(int)left.TotalMinutes}:{left.Seconds:00}");
    }
}
