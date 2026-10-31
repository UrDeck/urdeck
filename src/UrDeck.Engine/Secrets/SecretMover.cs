// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Secrets;

/// <summary>
/// Moves a secret that someone typed into <c>urdeck-config.json</c> (following a forum post) into the secret store and removes
/// the key from the file, so a token never stays in the plain-text file that gets pasted into issues.
/// </summary>
public static class SecretMover
{
    /// <summary>
    /// Moves each text value found under a key that a provider declares secret. Returns true when the configuration changed and
    /// must be saved. A configuration newer than the host is never written, so its values stay (used for this run and said so in
    /// the log), and where nothing can be stored they stay too, in plain text.
    /// </summary>
    public static bool MoveFromConfig(UrDeckConfig config, IEnumerable<ProviderDescriptor> providers, ISecretStore store, bool configIsNewerThanHost)
    {
        if (config.Providers == null)
            return false;

        bool changed = false;
        foreach (var descriptor in providers)
        {
            if (!config.Providers.TryGetValue(descriptor.Id, out var settings) || settings.ExtensionData == null)
                continue;
            var schema = descriptor.Schema;

            foreach (var secret in schema.Secrets)
            {
                string? key = settings.ExtensionData.Keys.FirstOrDefault(k => string.Equals(k, secret.Key, StringComparison.OrdinalIgnoreCase));
                if (key == null)
                    continue;
                var element = settings.ExtensionData[key];
                if (element.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(element.GetString()))
                    continue;

                string name = $"{descriptor.Id}/{secret.Key}";
                if (configIsNewerThanHost)
                {
                    UrDeckLog.Warn($"Secret {name}: typed into urdeck-config.json, which is from a newer version and is never written; used for this run, not moved");
                    continue;
                }

                if (!store.CanStore)
                {
                    UrDeckLog.Warn($"Secret {name}: kept in plain text in urdeck-config.json (nothing on this system can store it)");
                    continue;
                }

                try
                {
                    store.Save(descriptor.Id, secret.Key, element.GetString()!, ContextOf(settings, secret));
                }
                catch (SecretStoreException ex)
                {
                    UrDeckLog.Warn($"Secret {name}: not moved out of urdeck-config.json: {ex.Message}");
                    continue;
                }

                settings.ExtensionData.Remove(key);
                changed = true;
                UrDeckLog.Info($"Secret {name}: moved from urdeck-config.json to Credential Manager");
            }
        }

        return changed;
    }

    /// <summary>The value of the secret's context setting in the file (the address a token was issued for).</summary>
    private static string? ContextOf(UrDeck.Sdk.Data.ProviderSettings settings, SettingDescriptor secret)
    {
        if (secret.ContextKey == null || settings.ExtensionData == null)
            return null;
        foreach (var (key, value) in settings.ExtensionData)
        {
            if (string.Equals(key, secret.ContextKey, StringComparison.OrdinalIgnoreCase) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }
}
