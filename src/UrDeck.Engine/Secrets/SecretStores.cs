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
    /// Moves the secrets typed into the configuration (see <see cref="SecretMover"/>) and saves the file when that changed it.
    /// Returns whether the file was changed.
    /// </summary>
    public static bool MoveFromConfigAndSave(ConfigStore config, ProviderRegistry providers, ISecretStore store)
    {
        if (!SecretMover.MoveFromConfig(config.Config, providers.Descriptors, store, config.IsNewerThanHost))
            return false;
        config.Save();
        return true;
    }
}
