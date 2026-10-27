// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>The kind of field a provider setting gets in the settings window and the reference.</summary>
public enum SettingType
{
    Text,
    WholeNumber,
    Number,
    Toggle,
    Secret,
}

/// <summary>One setting a provider declares: what the file calls it and what the user is told about it.</summary>
/// <param name="Key">The key in <c>providers.&lt;id&gt;</c> of <c>urdeck-config.json</c>.</param>
/// <param name="Type">The field kind.</param>
/// <param name="Label">The short name shown beside the field.</param>
/// <param name="Help">One sentence of help.</param>
/// <param name="Required">Whether the provider cannot start without it.</param>
/// <param name="ContextKey">For a secret, the key of the setting it was issued for; null otherwise.</param>
/// <param name="Default">The value of a provider written without the setting; null when there is none.</param>
public sealed record SettingDescriptor(
    string Key,
    SettingType Type,
    string Label,
    string Help,
    bool Required,
    string? ContextKey,
    object? Default)
{
    internal PropertyInfo Property { get; init; } = null!;

    public bool IsSecret => Type == SettingType.Secret;
}

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

    /// <summary>The declared settings, in declaration order: the ones the settings window offers.</summary>
    public IReadOnlyList<SettingDescriptor> Settings { get; }

    /// <summary>Every property the engine binds from the file beyond the base ones, declared as a setting or not.</summary>
    internal IReadOnlyList<PropertyInfo> BoundProperties { get; }

    /// <summary>The secret settings.</summary>
    public IEnumerable<SettingDescriptor> Secrets => Settings.Where(s => s.IsSecret);

    /// <summary>The key a property has in the file.</summary>
    internal static string KeyOf(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

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
        object? defaults = null;
        try
        {
            defaults = Activator.CreateInstance(settingsType);
        }
        catch (Exception)
        {
            // A settings type without defaults shows none.
        }

        var settings = new List<SettingDescriptor>();
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
            if (!TryTypeOf(property.PropertyType, isSecret, out var type))
            {
                error = $"setting '{property.Name}' has the unsupported type {property.PropertyType.Name}";
                return null;
            }

            bound.Add(property);
            if (property.GetCustomAttribute<SettingAttribute>() is not { } setting)
            {
                if (isSecret)
                {
                    error = $"secret '{property.Name}' needs a [Setting] label";
                    return null;
                }

                continue;
            }

            object? value = isSecret ? null : defaults == null ? null : property.GetValue(defaults);
            settings.Add(new SettingDescriptor(
                key,
                type,
                setting.Label,
                setting.Help,
                setting.Required,
                property.GetCustomAttribute<SecretAttribute>()?.ContextSetting,
                value)
            { Property = property });
        }

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

    private static bool TryTypeOf(Type propertyType, bool isSecret, out SettingType type)
    {
        var plain = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        type = default;
        if (isSecret)
        {
            type = SettingType.Secret;
            return plain == typeof(SecretValue);
        }

        if (plain == typeof(string))
            type = SettingType.Text;
        else if (plain == typeof(int) || plain == typeof(long))
            type = SettingType.WholeNumber;
        else if (plain == typeof(double) || plain == typeof(float) || plain == typeof(decimal))
            type = SettingType.Number;
        else if (plain == typeof(bool))
            type = SettingType.Toggle;
        else
            return false;
        return true;
    }
}
