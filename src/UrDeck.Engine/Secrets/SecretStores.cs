// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Data;

namespace UrDeck.Engine.Secrets;

/// <summary>Makes the secret store of the running system.</summary>
public static class SecretStores
{
    /// <summary>The environment, and on Windows Credential Manager; on any other system the environment only.</summary>
    public static ISecretStore CreateDefault() =>
        new SecretStore(OperatingSystem.IsWindows() ? new CredentialManagerBackend() : null);

    /// <summary>
    /// Moves the secrets typed into the configuration (see <see cref="SecretMover"/>) and removes their keys from the
    /// configuration document as a host edit (not an undo step; the file is written behind it). Returns whether the
    /// document was changed.
    /// </summary>
    public static bool MoveFromConfigAndSave(ConfigStore config, ProviderRegistry providers, ISecretStore store)
    {
        var moved = new List<(string ProviderId, string Key)>();
        if (!SecretMover.MoveFromConfig(config.Config, providers.Descriptors, store, config.IsNewerThanHost, moved) || moved.Count == 0)
            return false;

        return config.Document.Edit("Move secrets out of the file", editor =>
        {
            foreach (var (providerId, key) in moved)
            {
                string? provider = editor.FindKey("/providers", providerId);
                if (provider != null)
                    editor.Remove("/providers/" + JsonPointer.Escape(provider) + "/" + JsonPointer.Escape(key));
            }
        }, EditOrigin.Host);
    }
}
