// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

public sealed partial class ReadingHub
{
    /// <summary>
    /// The readiness of a provider (provider-readiness spec), worked out now: the provider's availability, then the settings bound
    /// exactly as a start binds them. Starts nothing, samples nothing and uses no network; nothing is cached, so a saved setting or
    /// secret shows at the next call. Null when the provider is unknown.
    /// </summary>
    public ProviderStatus? GetStatus(string providerId)
    {
        ProviderRuntime rt;
        ProviderSettings? raw;
        lock (_gate)
        {
            if (_disposed)
                return null;
            var descriptor = _providers.Get(providerId);
            if (descriptor == null)
                return null;
            rt = GetOrCreateRuntime(descriptor);
            raw = _rawSettings.GetValueOrDefault(descriptor.Id);
        }

        var provider = rt.Descriptor;
        var availability = EnsureInstance(rt, out _) as IProviderAvailability;
        if (availability != null && !AskAvailable(provider, availability))
            return new ProviderStatus(provider.Id, provider.Name, ProviderReadiness.NotAvailable, AskGuidance(provider, availability), []);

        var bound = ProviderSettingsBinder.Bind(provider, raw, _secrets);
        if (bound.Missing.Count > 0)
            return new ProviderStatus(provider.Id, provider.Name, ProviderReadiness.NeedsSetup, availability == null ? null : AskGuidance(provider, availability), [.. bound.Missing.Select(m => m.Label)]);

        return new ProviderStatus(provider.Id, provider.Name, ProviderReadiness.Ready, null, []);
    }

    /// <summary>The readiness of every registered provider, by name.</summary>
    public IReadOnlyList<ProviderStatus> GetStatuses() =>
        [.. _providers.Descriptors.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).Select(d => GetStatus(d.Id)).OfType<ProviderStatus>()];

    // A check that throws is as good as none: the provider is available, and the log says so once.
    private bool AskAvailable(ProviderDescriptor provider, IProviderAvailability availability)
    {
        try
        {
            return availability.IsAvailable;
        }
        catch (Exception ex)
        {
            LogOnce($"Provider '{provider.Id}': its availability check failed ({ex.GetBaseException().Message}); treating it as available");
            return true;
        }
    }

    private SetupGuidance? AskGuidance(ProviderDescriptor provider, IProviderAvailability availability)
    {
        try
        {
            var guidance = availability.Guidance;
            return guidance == null || string.IsNullOrWhiteSpace(guidance.Text) ? null : guidance;
        }
        catch (Exception ex)
        {
            LogOnce($"Provider '{provider.Id}': its setup guidance failed ({ex.GetBaseException().Message}); showing none");
            return null;
        }
    }

    private void LogOnce(string line)
    {
        bool first;
        lock (_gate)
            first = _loggedSettings.Add(line);
        if (first)
            UrDeckLog.Warn(line);
    }
}
