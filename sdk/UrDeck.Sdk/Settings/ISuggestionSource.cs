// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Text.Json.Nodes;

namespace UrDeck.Sdk.Settings;

/// <summary>
/// Implemented by a provider next to <see cref="Data.IDataProvider"/> to answer what a user types into a picker (a place name, for
/// the weather). Only the provider talks to its service; UrDeck sends the typed text nowhere else.
/// </summary>
public interface ISuggestionSource
{
    /// <summary>The kinds of query the provider answers, for example <c>["place"]</c>.</summary>
    IReadOnlyList<string> SuggestionKinds { get; }

    /// <summary>Answers a query with a short list of suggestions.</summary>
    /// <param name="kind">One of <see cref="SuggestionKinds"/>.</param>
    /// <param name="text">What the user has typed.</param>
    /// <param name="cancellationToken">Cancelled when the engine gives up.</param>
    Task<IReadOnlyList<Suggestion>> SuggestAsync(string kind, string text, CancellationToken cancellationToken);
}

/// <summary>One suggestion.</summary>
/// <param name="Label">The main line.</param>
/// <param name="Detail">A second line (a region, for a place); null for none.</param>
/// <param name="Values">The properties to store when it is chosen, by key (<c>location</c>, <c>latitude</c>, <c>longitude</c>).</param>
public sealed record Suggestion(string Label, string? Detail, IReadOnlyDictionary<string, JsonNode?> Values);
