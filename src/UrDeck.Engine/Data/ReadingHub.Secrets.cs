// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Secrets;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

// A running provider renewing its own secret (a rotated refresh token), without a restart.
public sealed partial class ReadingHub
{
    /// <summary>
    /// Stores a new value of one of the provider's own secrets set by sign-in. Refused (and logged once) for any other secret, for a
    /// value Credential Manager would not take, and when the environment variable is the source or there is no store. A stored
    /// renewal is not a settings change: the provider is not restarted by it.
    /// </summary>
    private bool StoreSecretFromProvider(ProviderRuntime rt, int generation, string setting, SecretValue value)
    {
        string id = rt.Descriptor.Id;
        lock (_gate)
        {
            if (_disposed || rt.Generation != generation)
                return false;
        }

        var declared = rt.Descriptor.Schema.Secrets.FirstOrDefault(s => s.SetBySignIn && string.Equals(s.Key, setting, StringComparison.OrdinalIgnoreCase));
        if (declared == null)
            return Refuse($"Provider '{id}' asked to store '{Printable(setting)}', which is not one of its secrets set by sign-in; nothing stored");

        string key = declared.Key;
        if (_secrets == null || !_secrets.CanStore)
            return Refuse($"Secret {id}/{key}: renewed but there is nowhere to keep it; it is kept for this run only");
        if (value == null || value.IsEmpty)
            return Refuse($"Secret {id}/{key}: the provider asked to store an empty value; nothing stored");
        if (_secrets.Find(id, key) is { Source: SecretSource.Environment } fromEnvironment)
            return Refuse($"Secret {id}/{key}: renewed but it comes from {fromEnvironment.SourceName}; the renewed secret is kept for this run only");
        if (Encoding.UTF8.GetByteCount(value.Reveal()) > SecretStore.MaxSecretBytes)
            return Refuse($"Secret {id}/{key}: the renewed value is larger than Credential Manager accepts ({SecretStore.MaxSecretBytes:N0} bytes); nothing stored");

        try
        {
            // The account name of the sign-in stays with the secret.
            _secrets.Save(id, key, value.Reveal(), _secrets.FindStored(id, key)?.Context);
        }
        catch (SecretStoreException ex)
        {
            return Refuse($"Secret {id}/{key}: the renewed value could not be saved ({ex.Message}); it is kept for this run only");
        }

        // The provider already holds the new value, so this is no change that restarts it.
        ProviderSettings? raw;
        lock (_gate)
            raw = _rawSettings.GetValueOrDefault(id);
        var bound = ProviderSettingsBinder.Bind(rt.Descriptor, raw, _secrets);
        lock (_gate)
        {
            if (rt.Generation == generation && rt.Fingerprint != null)
                rt.Fingerprint = bound.Fingerprint;
        }

        UrDeckLog.Info($"Secret {id}/{key}: renewed");
        return true;
    }

    private bool Refuse(string line)
    {
        bool first;
        lock (_gate)
            first = _loggedSettings.Add(line);
        if (first)
            UrDeckLog.Warn(line);
        return false;
    }

    private static string Printable(string text) => text.Length > 64 ? text[..64] : text;
}
