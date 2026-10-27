// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Secrets;

/// <summary>Where a secret's value was found.</summary>
public enum SecretSource
{
    /// <summary>An environment variable.</summary>
    Environment,

    /// <summary>The persistent store (Windows Credential Manager).</summary>
    Stored,

    /// <summary>The configuration file (a secret typed by hand that could not be moved).</summary>
    ConfigFile,
}

/// <summary>A secret's value, where it came from and the context it was saved for.</summary>
/// <param name="Value">The text. Never logged.</param>
/// <param name="Context">The value of the context setting when it was saved (a Home Assistant address); null when none.</param>
/// <param name="Source">Where it was found.</param>
/// <param name="SourceName">The environment variable's name, or "Credential Manager".</param>
public sealed record SecretRecord(string Value, string? Context, SecretSource Source, string SourceName)
{
    public override string ToString() => $"secret from {SourceName}";
}

/// <summary>A secret as the persistent store keeps it.</summary>
public sealed record StoredSecret(string Value, string? Context);

/// <summary>The persistent part of the secret store: Credential Manager, or memory in tests.</summary>
public interface ISecretBackend
{
    /// <summary>The stored secret, or null when there is none.</summary>
    StoredSecret? Read(string providerId, string setting);

    /// <summary>Stores a secret, replacing the old one.</summary>
    void Write(string providerId, string setting, string value, string? context);

    /// <summary>Deletes a secret; false when there was none.</summary>
    bool Delete(string providerId, string setting);

    /// <summary>Deletes every secret this backend holds and returns how many.</summary>
    int DeleteAll();

    /// <summary>The (provider id, setting) of every stored secret.</summary>
    IReadOnlyList<(string ProviderId, string Setting)> List();
}

/// <summary>A secret could not be saved or removed; the message says why in words for the user.</summary>
public sealed class SecretStoreException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Where a provider's secrets are looked up and kept, by (provider id, setting name): the environment first, then the
/// persistent store.
/// </summary>
public interface ISecretStore
{
    /// <summary>Whether a secret can be saved (false where there is no persistent store, only the environment).</summary>
    bool CanStore { get; }

    /// <summary>The secret from the environment, else from the persistent store; null when there is none.</summary>
    SecretRecord? Find(string providerId, string setting);

    /// <summary>The secret from the persistent store only, ignoring the environment; null when there is none.</summary>
    SecretRecord? FindStored(string providerId, string setting);

    /// <summary>Stores a secret with the context it belongs to. Throws <see cref="SecretStoreException"/>.</summary>
    void Save(string providerId, string setting, string value, string? context);

    /// <summary>Deletes the stored secret; false when there was none. Throws <see cref="SecretStoreException"/>.</summary>
    bool Remove(string providerId, string setting);
}
