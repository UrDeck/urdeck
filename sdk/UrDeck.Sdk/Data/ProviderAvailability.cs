// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>
/// What a provider tells a user whose provider is not ready: one sentence and, optionally, one web address ("Steam is not
/// installed." and the download page). Shown on the provider's Settings page.
/// </summary>
/// <param name="Text">A short sentence; no secrets.</param>
/// <param name="Url">An <c>https://</c> address that opens in the browser; null for none.</param>
/// <param name="LinkText">What the link says ("Get Steam"); empty shows the address's host.</param>
public sealed record SetupGuidance(string Text, string? Url = null, string LinkText = "");

/// <summary>
/// The availability a provider declares; implemented by <see cref="DataProvider{TSettings}"/>. A provider that does not apply on
/// this PC (a service whose client is not installed) says so, and the host stops offering its readings in the pickers while the
/// Providers section still lists it.
/// </summary>
public interface IProviderAvailability
{
    /// <summary>Whether the provider applies on this PC. Fast, free of side effects and safe to ask repeatedly; no network.</summary>
    bool IsAvailable { get; }

    /// <summary>What to tell a user whose provider is not available; null for nothing to say.</summary>
    SetupGuidance? Guidance { get; }
}
