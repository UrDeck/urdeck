// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>
/// Base class of a provider with settings of its own. The engine binds <c>providers.&lt;id&gt;</c> to
/// <typeparamref name="TSettings"/> and passes the instance to <see cref="Start(IReadingSink, TSettings)"/>; a change to the
/// provider's settings (or its secrets) shuts the provider down and starts a new run with the new ones. A provider that needs no
/// settings of its own derives with <see cref="ProviderSettings"/>.
/// </summary>
public abstract class DataProvider<TSettings> : IDataProvider, IConnectionTest, ISignIn
    where TSettings : ProviderSettings, new()
{
    /// <inheritdoc />
    public abstract IReadOnlyList<ReadingDescriptor> Describe();

    /// <summary>Begins work with the settings the user gave. Keep <paramref name="sink"/> to publish through.</summary>
    public abstract void Start(IReadingSink sink, TSettings settings);

    void IDataProvider.Start(IReadingSink sink, ProviderSettings settings) =>
        Start(sink, settings as TSettings ?? new TSettings());

    /// <inheritdoc />
    public abstract void SetDemand(IReadOnlyCollection<string> paths);

    /// <inheritdoc />
    public abstract Task SampleAsync(CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract void Shutdown();

    /// <summary>
    /// Checks the settings against the service without saving them, for the settings window's Test connection button. Return one
    /// line saying what was found ("Connected to Home Assistant 2026.10") or throw to say what is wrong. Called on a provider
    /// that is not started; the default offers no test (see <see cref="OffersConnectionTest"/>).
    /// </summary>
    public virtual Task<string> ValidateAsync(TSettings settings, CancellationToken cancellationToken) => Task.FromResult("");

    /// <summary>Whether <see cref="ValidateAsync"/> is implemented; the window then shows a Test connection button.</summary>
    public virtual bool OffersConnectionTest => false;

    bool IConnectionTest.Offered => OffersConnectionTest;

    Task<string> IConnectionTest.ValidateAsync(ProviderSettings settings, CancellationToken cancellationToken) =>
        ValidateAsync(settings as TSettings ?? new TSettings(), cancellationToken);

    /// <summary>
    /// Whether the provider offers a sign-in (<see cref="SignInAsync"/>): the user approves it elsewhere (a phone app, a web page)
    /// and the provider receives its own secrets. A provider that offers one must declare at least one secret with
    /// <c>SetBySignIn</c>; the window then shows Connect for it and no text field for those secrets.
    /// </summary>
    public virtual bool OffersSignIn => false;

    /// <summary>One line saying what connecting gives ("Shows your friends and what they play"), beside Connect.</summary>
    public virtual string SignInDescription => "";

    /// <summary>Where the user ends a session this provider could not end at the service ("the Steam app's authorized devices").</summary>
    public virtual string SignOutHint => "";

    /// <summary>
    /// Runs the sign-in. Report what the user has to do through <paramref name="progress"/> (replace the challenge when the service
    /// rotates it), poll the service yourself, and return <see cref="SignInResult.Success"/> with a value for each of your secrets
    /// set by sign-in, or <see cref="SignInResult.Failed"/>. Called on a provider that is not started, with the settings from the
    /// form. Honor <paramref name="cancellationToken"/>: it is cancelled by Cancel, when the window closes and 30 seconds after the
    /// challenge expired; a result that arrives afterwards is dropped. Never put a challenge, a code or a secret in a log line.
    /// </summary>
    public virtual Task<SignInResult> SignInAsync(TSettings settings, ISignInProgress progress, CancellationToken cancellationToken) =>
        Task.FromResult(SignInResult.Failed("This provider has no sign-in."));

    /// <summary>
    /// Ends the session at the service (best effort) when the user disconnects; the stored secrets are in <paramref name="settings"/>.
    /// The host waits 10 seconds, then deletes the secrets whatever happened. Throw when the service could not be told.
    /// </summary>
    public virtual Task SignOutAsync(TSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;

    bool ISignIn.Offered => OffersSignIn;

    string ISignIn.Description => SignInDescription;

    string ISignIn.SignOutHint => SignOutHint;

    Task<SignInResult> ISignIn.SignInAsync(ProviderSettings settings, ISignInProgress progress, CancellationToken cancellationToken) =>
        SignInAsync(settings as TSettings ?? new TSettings(), progress, cancellationToken);

    Task ISignIn.SignOutAsync(ProviderSettings settings, CancellationToken cancellationToken) =>
        SignOutAsync(settings as TSettings ?? new TSettings(), cancellationToken);
}

/// <summary>The optional connection test of a provider; implemented by <see cref="DataProvider{TSettings}"/>.</summary>
public interface IConnectionTest
{
    /// <summary>Whether the provider offers the test.</summary>
    bool Offered { get; }

    /// <summary>Runs the test with unsaved settings; returns a one-line result or throws with the reason it failed.</summary>
    Task<string> ValidateAsync(ProviderSettings settings, CancellationToken cancellationToken);
}
