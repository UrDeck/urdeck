// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>
/// Base class of a provider with settings of its own. The engine binds <c>providers.&lt;id&gt;</c> to
/// <typeparamref name="TSettings"/> and passes the instance to <see cref="Start(IReadingSink, TSettings)"/>; a change to the
/// provider's settings (or its secrets) shuts the provider down and starts a new run with the new ones. A provider that needs no
/// settings of its own derives with <see cref="ProviderSettings"/>.
/// </summary>
public abstract class DataProvider<TSettings> : IDataProvider, IConnectionTest
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
}

/// <summary>The optional connection test of a provider; implemented by <see cref="DataProvider{TSettings}"/>.</summary>
public interface IConnectionTest
{
    /// <summary>Whether the provider offers the test.</summary>
    bool Offered { get; }

    /// <summary>Runs the test with unsaved settings; returns a one-line result or throws with the reason it failed.</summary>
    Task<string> ValidateAsync(ProviderSettings settings, CancellationToken cancellationToken);
}
