// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>Everything the engine needs to know about a provider type, read once from its attribute.</summary>
public sealed partial record ProviderDescriptor(
    string Id,
    string Name,
    Type ProviderType,
    int DefaultIntervalMs,
    int MinIntervalMs,
    string AssemblyName)
{
    /// <summary>Builds the instance; the default calls the public parameterless constructor. Lets a host register a built-in provider directly.</summary>
    public Func<IDataProvider>? Factory { get; init; }

    internal IDataProvider CreateInstance() => Factory?.Invoke() ?? (IDataProvider)Activator.CreateInstance(ProviderType)!;

    public static ProviderDescriptor? TryCreate(Type type, out string? rejectReason)
    {
        rejectReason = null;
        if (!type.IsClass || type.IsAbstract || !typeof(IDataProvider).IsAssignableFrom(type))
        {
            rejectReason = "not a concrete IDataProvider";
            return null;
        }

        var meta = type.GetCustomAttribute<DataProviderAttribute>();
        if (meta == null)
        { rejectReason = "missing [DataProvider]"; return null; }

        if (!ValidId().IsMatch(meta.Id))
        { rejectReason = $"provider id '{meta.Id}' must use lowercase letters, digits, '.' and '-'"; return null; }

        if (string.IsNullOrWhiteSpace(meta.Name))
        { rejectReason = "provider name is empty"; return null; }

        if (meta.MinIntervalMs <= 0)
        { rejectReason = $"MinIntervalMs must be positive (was {meta.MinIntervalMs})"; return null; }

        if (meta.DefaultIntervalMs <= 0)
        { rejectReason = $"DefaultIntervalMs must be positive (was {meta.DefaultIntervalMs})"; return null; }

        if (type.GetConstructor(Type.EmptyTypes) == null)
        { rejectReason = "no public parameterless constructor"; return null; }

        return new ProviderDescriptor(
            meta.Id,
            meta.Name,
            type,
            Math.Max(meta.DefaultIntervalMs, meta.MinIntervalMs),
            meta.MinIntervalMs,
            type.Assembly.GetName().Name ?? "?");
    }

    [GeneratedRegex("^[a-z0-9.-]+$")]
    private static partial Regex ValidId();
}

/// <summary>Maps provider ids to their descriptors. Thread-safe.</summary>
public sealed class ProviderRegistry
{
    private readonly ConcurrentDictionary<string, ProviderDescriptor> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers the provider unless its id is taken; <paramref name="existing"/> is then the one that holds it.</summary>
    public bool TryRegister(ProviderDescriptor descriptor, out ProviderDescriptor? existing)
    {
        existing = _providers.GetOrAdd(descriptor.Id, descriptor);
        if (ReferenceEquals(existing, descriptor))
        {
            existing = null;
            return true;
        }

        return false;
    }

    public void Clear() => _providers.Clear();

    public ProviderDescriptor? Get(string providerId) =>
        _providers.TryGetValue(providerId, out var d) ? d : null;

    public IReadOnlyList<string> GetRegisteredProviderIds() => _providers.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

    public IReadOnlyCollection<ProviderDescriptor> Descriptors => _providers.Values.ToList();
}
