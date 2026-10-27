// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Secrets;

/// <summary>The environment first, then the persistent backend, if there is one.</summary>
public sealed class SecretStore : ISecretStore
{
    /// <summary>What Credential Manager accepts for a credential's blob.</summary>
    public const int MaxSecretBytes = 2560;

    private readonly ISecretBackend? _backend;
    private readonly Func<string, string?> _environment;
    private bool _backendFailureLogged;

    /// <param name="backend">The persistent store; null where there is none (Linux): only the environment is read.</param>
    /// <param name="environment">Reads an environment variable; the process environment by default.</param>
    public SecretStore(ISecretBackend? backend, Func<string, string?>? environment = null)
    {
        _backend = backend;
        _environment = environment ?? Environment.GetEnvironmentVariable;
    }

    public bool CanStore => _backend != null;

    /// <summary>
    /// <c>URDECK_SECRET_&lt;PROVIDER&gt;_&lt;SETTING&gt;</c>: the provider id and the setting name in upper case, with <c>.</c> and
    /// <c>-</c> replaced by <c>_</c>.
    /// </summary>
    public static string EnvironmentName(string providerId, string setting) =>
        "URDECK_SECRET_" + Project(providerId) + "_" + Project(setting);

    private static string Project(string text) => text.ToUpperInvariant().Replace('.', '_').Replace('-', '_');

    public SecretRecord? Find(string providerId, string setting)
    {
        string name = EnvironmentName(providerId, setting);
        string? fromEnvironment = _environment(name);
        if (!string.IsNullOrEmpty(fromEnvironment))
            return new SecretRecord(fromEnvironment, null, SecretSource.Environment, name);
        return FindStored(providerId, setting);
    }

    public SecretRecord? FindStored(string providerId, string setting)
    {
        if (_backend == null)
            return null;
        try
        {
            return _backend.Read(providerId, setting) is { } stored
                ? new SecretRecord(stored.Value, stored.Context, SecretSource.Stored, "Credential Manager")
                : null;
        }
        catch (Exception ex)
        {
            if (!_backendFailureLogged)
            {
                _backendFailureLogged = true;
                UrDeckLog.Warn($"Secrets: Credential Manager cannot be read ({ex.GetBaseException().Message}); only environment variables are used");
            }

            return null;
        }
    }

    public void Save(string providerId, string setting, string value, string? context)
    {
        if (_backend == null)
            throw new SecretStoreException("There is nowhere to keep the secret on this system; set the environment variable " + EnvironmentName(providerId, setting) + " instead.");
        if (string.IsNullOrEmpty(value))
            throw new SecretStoreException("The secret is empty.");
        if (Encoding.UTF8.GetByteCount(value) > MaxSecretBytes)
            throw new SecretStoreException($"The secret is larger than Credential Manager accepts ({MaxSecretBytes:N0} bytes).");
        try
        {
            _backend.Write(providerId, setting, value, string.IsNullOrEmpty(context) ? null : context);
        }
        catch (SecretStoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SecretStoreException($"The secret could not be saved: {ex.GetBaseException().Message}", ex);
        }
    }

    public bool Remove(string providerId, string setting)
    {
        if (_backend == null)
            return false;
        try
        {
            return _backend.Delete(providerId, setting);
        }
        catch (Exception ex)
        {
            throw new SecretStoreException($"The secret could not be removed: {ex.GetBaseException().Message}", ex);
        }
    }
}

/// <summary>A backend in memory, for tests and for hosts that must not touch the system's store.</summary>
public sealed class InMemorySecretBackend : ISecretBackend
{
    private readonly Dictionary<(string, string), StoredSecret> _items = new();
    private readonly object _gate = new();

    public StoredSecret? Read(string providerId, string setting)
    {
        lock (_gate)
            return _items.GetValueOrDefault((providerId.ToLowerInvariant(), setting.ToLowerInvariant()));
    }

    public void Write(string providerId, string setting, string value, string? context)
    {
        lock (_gate)
            _items[(providerId.ToLowerInvariant(), setting.ToLowerInvariant())] = new StoredSecret(value, context);
    }

    public bool Delete(string providerId, string setting)
    {
        lock (_gate)
            return _items.Remove((providerId.ToLowerInvariant(), setting.ToLowerInvariant()));
    }

    public int DeleteAll()
    {
        lock (_gate)
        {
            int count = _items.Count;
            _items.Clear();
            return count;
        }
    }

    public IReadOnlyList<(string ProviderId, string Setting)> List()
    {
        lock (_gate)
            return _items.Keys.Select(k => (k.Item1, k.Item2)).ToList();
    }
}
