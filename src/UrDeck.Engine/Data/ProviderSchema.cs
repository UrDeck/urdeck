// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using System.Text.Json.Serialization;
using UrDeck.Engine.Settings;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;

namespace UrDeck.Engine.Data;

/// <summary>The settings a provider type declares, read once from its settings type by reflection.</summary>
public sealed class ProviderSchema
{
    private static readonly HashSet<string> BaseKeys = new(StringComparer.OrdinalIgnoreCase) { "intervalMs", "actionTimeoutMs", "actionWarnMs" };

    /// <summary>The schema of a provider that declares nothing.</summary>
    public static ProviderSchema Empty { get; } = new(typeof(ProviderSettings), [], []);

    private ProviderSchema(Type settingsType, IReadOnlyList<SettingDescriptor> settings, IReadOnlyList<PropertyInfo> bound)
    {
        SettingsType = settingsType;
        Settings = settings;
        BoundProperties = bound;
    }

    /// <summary>The provider's settings type (<see cref="ProviderSettings"/> itself for a provider that declares none).</summary>
    public Type SettingsType { get; }

    /// <summary>The declared settings, in display order: the ones the settings window offers.</summary>
    public IReadOnlyList<SettingDescriptor> Settings { get; }

    /// <summary>Every property the engine binds from the file beyond the base ones, declared as a setting or not.</summary>
    internal IReadOnlyList<PropertyInfo> BoundProperties { get; }

    /// <summary>The secret settings.</summary>
    public IEnumerable<SettingDescriptor> Secrets => Settings.Where(s => s.IsSecret);

    /// <summary>The key a property has in the file.</summary>
    internal static string KeyOf(PropertyInfo property) => SettingSchema.KeyOf(property);

    /// <summary>The settings type of a provider class: the argument of its <see cref="DataProvider{TSettings}"/> base, else the base type.</summary>
    public static Type FindSettingsType(Type providerType)
    {
        for (var type = providerType; type != null && type != typeof(object); type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DataProvider<>))
                return type.GetGenericArguments()[0];
        }

        return typeof(ProviderSettings);
    }

    /// <summary>Reads the declarations of <paramref name="settingsType"/>. Returns null with the reason when one cannot be supported.</summary>
    public static ProviderSchema? TryRead(Type settingsType, out string? error)
    {
        error = null;
        var bound = new List<PropertyInfo>();
        foreach (var property in settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.DeclaringType == typeof(ProviderSettings) || property.GetCustomAttribute<JsonExtensionDataAttribute>() != null
                || property.GetCustomAttribute<JsonIgnoreAttribute>() != null || property.SetMethod is not { IsPublic: true })
                continue;

            string key = KeyOf(property);
            if (BaseKeys.Contains(key))
            {
                error = $"setting '{property.Name}' uses the key '{key}', which every provider already has";
                return null;
            }

            bool isSecret = property.GetCustomAttribute<SecretAttribute>() != null;
            if (!IsSupported(property.PropertyType, isSecret))
            {
                error = $"setting '{property.Name}' has the unsupported type {property.PropertyType.Name}";
                return null;
            }

            if (isSecret && property.GetCustomAttribute<SettingAttribute>() == null)
            {
                error = $"secret '{property.Name}' needs a [Setting] label";
                return null;
            }

            bound.Add(property);
        }

        if (!SettingSchema.TryRead(settingsType, typeof(ProviderSettings), out var read, out error))
            return null;

        var settings = read!.Settings;
        foreach (var secret in settings.Where(s => s.IsSecret && s.ContextKey != null))
        {
            if (!settings.Any(s => !s.IsSecret && string.Equals(s.Key, secret.ContextKey, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"secret '{secret.Key}' names the context setting '{secret.ContextKey}', which is not a declared setting";
                return null;
            }
        }

        return new ProviderSchema(settingsType, settings, bound);
    }

    private static bool IsSupported(Type propertyType, bool isSecret)
    {
        var plain = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (isSecret)
            return plain == typeof(SecretValue);
        return plain == typeof(string) || plain == typeof(int) || plain == typeof(long) || plain == typeof(double) || plain == typeof(float)
            || plain == typeof(decimal) || plain == typeof(bool);
    }
}
