// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>Whether a provider can be offered (provider-readiness spec). Derived each time it is asked and never stored.</summary>
public enum ProviderReadiness
{
    /// <summary>Available, and no required setting is missing.</summary>
    Ready,

    /// <summary>Available, and a required setting (a secret set by sign-in counts) has no value.</summary>
    NeedsSetup,

    /// <summary>The provider declared that it does not apply on this PC.</summary>
    NotAvailable,
}

/// <summary>A provider's readiness with what to tell the user.</summary>
/// <param name="ProviderId">The provider.</param>
/// <param name="Name">The provider's name.</param>
/// <param name="Readiness">The state.</param>
/// <param name="Guidance">The provider's own guidance; null when it is ready or declared none.</param>
/// <param name="Missing">The labels of the required settings without a value; empty unless the provider needs setup.</param>
public sealed record ProviderStatus(string ProviderId, string Name, ProviderReadiness Readiness, SetupGuidance? Guidance, IReadOnlyList<string> Missing)
{
    /// <summary>Whether the pickers offer the provider.</summary>
    public bool IsReady => Readiness == ProviderReadiness.Ready;
}
