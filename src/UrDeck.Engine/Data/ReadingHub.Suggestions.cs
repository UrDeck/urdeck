// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Settings;

namespace UrDeck.Engine.Data;

/// <summary>What <see cref="ReadingHub.SuggestAsync"/> found: the provider's suggestions, or why there are none.</summary>
/// <param name="Suggestions">The suggestions, possibly none; null when the query failed.</param>
/// <param name="Message">Why the query failed; null when it did not.</param>
public sealed record SuggestionResult(IReadOnlyList<Suggestion>? Suggestions, string? Message)
{
    /// <summary>Whether the provider answered.</summary>
    public bool Succeeded => Suggestions != null;

    internal static SuggestionResult Failed(string message) => new(null, message);
}

// Picker queries (a place name, for the weather) go to the provider that offers them, like a listing of readings does. See the
// settings-metadata spec, "Suggestions From A Provider".
public sealed partial class ReadingHub
{
    /// <summary>How long a suggestion query may take before the answer is a message.</summary>
    internal static readonly TimeSpan SuggestionLimit = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Asks a provider that answers suggestion queries (<see cref="ISuggestionSource"/>) for suggestions of one kind. The provider is
    /// started for it if it is not running, as for a listing, and lingers afterwards. Never throws: a provider that is unknown, does not
    /// answer the kind, cannot start, fails or takes longer than ten seconds gives a <see cref="SuggestionResult"/> with a message.
    /// </summary>
    public async Task<SuggestionResult> SuggestAsync(string providerId, string kind, string text, CancellationToken cancellationToken = default)
    {
        ProviderRuntime rt;
        Task<string?>? ready = null;
        lock (_gate)
        {
            if (_disposed)
                return SuggestionResult.Failed("the host is shutting down");
            var descriptor = _providers.Get(providerId);
            if (descriptor == null)
                return SuggestionResult.Failed($"no data provider '{providerId}' is registered");
            rt = GetOrCreateRuntime(descriptor);
            if (!(rt.Instance != null ? rt.Instance is ISuggestionSource : descriptor.OffersSuggestions))
                return SuggestionResult.Failed($"provider '{providerId}' does not answer suggestion queries");
            if (!rt.Started)
            {
                ready = (rt.StartedSignal ??= ProviderRuntime.NewStartSignal()).Task;
                EnsureLoop(rt);
                TouchLinger(rt);
            }
        }

        if (ready != null)
        {
            if (_manual)
                StartManual(rt);
            string? why = await WaitForStartAsync(ready, SuggestionLimit, cancellationToken);
            if (why != null)
                return SuggestionResult.Failed($"provider '{providerId}': {why}");
        }

        ISuggestionSource? source;
        lock (_gate)
            source = rt.Instance as ISuggestionSource;

        if (source == null || !source.SuggestionKinds.Contains(kind, StringComparer.Ordinal))
            return SuggestionResult.Failed($"provider '{providerId}' does not answer '{kind}' queries");

        IReadOnlyList<Suggestion> answer;
        try
        {
            answer = await Task.Run(() => source.SuggestAsync(kind, text, cancellationToken), CancellationToken.None)
                .WaitAsync(SuggestionLimit, _time, cancellationToken);
        }
        catch (TimeoutException)
        {
            return SuggestionResult.Failed($"provider '{providerId}' did not answer within {SuggestionLimit.TotalSeconds:0.#} s");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return SuggestionResult.Failed("cancelled");
        }
        catch (Exception ex)
        {
            return SuggestionResult.Failed($"provider '{providerId}' could not answer: {ex.GetBaseException().Message}");
        }

        lock (_gate)
        {
            if (!_disposed)
                TouchLinger(rt);
        }

        return new SuggestionResult((answer ?? []).Where(s => s != null).ToList(), null);
    }
}
