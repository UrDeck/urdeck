// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UrDeck.Engine.Secrets;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

/// <summary>What the engine found for one secret setting when it prepared a provider's settings.</summary>
/// <param name="ProviderId">The provider.</param>
/// <param name="Setting">The secret's key.</param>
/// <param name="Found">Where the value came from; null when there is none.</param>
/// <param name="ContextNote">A sentence for the log when the secret was saved for another context; null otherwise.</param>
internal sealed record SecretReport(string ProviderId, string Setting, SecretRecord? Found, string? ContextNote)
{
    /// <summary>The log line for a start: where the secret came from or that it is missing. Never the value.</summary>
    public string Line => Found == null
        ? $"Secret {ProviderId}/{Setting}: missing; set it in Settings"
        : Found.Source switch
        {
            SecretSource.Environment => $"Secret {ProviderId}/{Setting}: from {Found.SourceName}",
            SecretSource.Stored => $"Secret {ProviderId}/{Setting}: from {Found.SourceName}",
            _ => $"Secret {ProviderId}/{Setting}: from urdeck-config.json (kept in plain text)",
        };
}

/// <summary>A provider's settings as the engine prepared them for a start.</summary>
/// <param name="Settings">The instance of the provider's settings type.</param>
/// <param name="Missing">The required settings that have no value.</param>
/// <param name="Fingerprint">Changes when anything a restart would change changes; never logged (it covers secrets).</param>
/// <param name="Warnings">Values of the wrong type, one sentence each.</param>
/// <param name="Secrets">One entry per declared secret.</param>
internal sealed record BoundSettings(
    ProviderSettings Settings,
    IReadOnlyList<SettingDescriptor> Missing,
    string Fingerprint,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<SecretReport> Secrets)
{
    /// <summary>What a reading says while <see cref="Missing"/> is not empty.</summary>
    public string MissingReason(ProviderDescriptor descriptor)
    {
        if (descriptor.MissingSettingMessage.Length > 0)
            return descriptor.MissingSettingMessage;
        return $"set {string.Join(", ", Missing.Select(m => m.Label))} for {descriptor.Name} in Settings";
    }
}

/// <summary>Binds <c>providers.&lt;id&gt;</c> of the configuration to a provider's settings type and fills its secrets.</summary>
internal static class ProviderSettingsBinder
{
    public static BoundSettings Bind(ProviderDescriptor descriptor, ProviderSettings? raw, ISecretStore? secrets)
    {
        var schema = descriptor.Schema;
        var typed = (ProviderSettings)Activator.CreateInstance(schema.SettingsType)!;
        var warnings = new List<string>();
        var reports = new List<SecretReport>();
        var missing = new List<SettingDescriptor>();
        var fingerprint = new StringBuilder();

        if (raw != null)
        {
            typed.IntervalMs = raw.IntervalMs;
            typed.ActionTimeoutMs = raw.ActionTimeoutMs;
            typed.ActionWarnMs = raw.ActionWarnMs;
            typed.ExtensionData = raw.ExtensionData == null ? null : new Dictionary<string, JsonElement>(raw.ExtensionData, StringComparer.OrdinalIgnoreCase);
        }

        var bag = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (raw?.ExtensionData != null)
        {
            foreach (var (key, value) in raw.ExtensionData)
                bag[key] = value;
        }

        foreach (var property in schema.BoundProperties)
        {
            string key = ProviderSchema.KeyOf(property);
            var setting = schema.Settings.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
            if (setting is { IsSecret: true } || (setting == null && property.PropertyType == typeof(SecretValue)))
                continue;

            bool present = bag.TryGetValue(key, out var element) && element.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
            if (present && TryConvert(element, property.PropertyType, out object? converted))
                property.SetValue(typed, converted);
            else if (present)
                warnings.Add($"providers.{descriptor.Id}.{key} is not {Expected(property.PropertyType)}; using the default");

            fingerprint.Append(key).Append('=').Append(Convert.ToString(property.GetValue(typed), CultureInfo.InvariantCulture)).Append('\n');
        }

        foreach (var setting in schema.Settings.Where(s => s.IsSecret))
        {
            var record = secrets?.Find(descriptor.Id, setting.Key);
            if (record == null && bag.TryGetValue(setting.Key, out var fromFile)
                && fromFile.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(fromFile.GetString()))
                record = new SecretRecord(fromFile.GetString()!, null, SecretSource.ConfigFile, "urdeck-config.json");

            string? note = null;
            if (record != null)
            {
                setting.Property.SetValue(typed, new SecretValue(record.Value));
                note = ContextNote(descriptor, schema, setting, record, typed);
                fingerprint.Append(setting.Key).Append("=#").Append(Hash(record.Value)).Append('\n');
            }

            reports.Add(new SecretReport(descriptor.Id, setting.Key, record, note));
        }

        foreach (var setting in schema.Settings.Where(s => s.Required))
        {
            if (IsMissing(setting, typed, bag))
                missing.Add(setting);
        }

        return new BoundSettings(typed, missing, fingerprint.ToString(), warnings, reports);
    }

    private static string? ContextNote(ProviderDescriptor descriptor, ProviderSchema schema, SettingDescriptor secret, SecretRecord record, ProviderSettings typed)
    {
        if (secret.ContextKey == null || record.Context == null || record.Source != SecretSource.Stored)
            return null;
        var contextSetting = schema.Settings.FirstOrDefault(s => string.Equals(s.Key, secret.ContextKey, StringComparison.OrdinalIgnoreCase));
        string? current = contextSetting?.Property.GetValue(typed)?.ToString();
        if (string.IsNullOrEmpty(current) || string.Equals(current, record.Context, StringComparison.OrdinalIgnoreCase))
            return null;
        return $"Secret {descriptor.Id}/{secret.Key}: saved for {record.Context}, but {secret.ContextKey} is {current}; "
            + $"set {SecretStore.EnvironmentName(descriptor.Id, secret.Key)} to give this copy its own";
    }

    private static bool IsMissing(SettingDescriptor setting, ProviderSettings typed, Dictionary<string, JsonElement> bag)
    {
        object? value = setting.Property.GetValue(typed);
        return setting.Type switch
        {
            SettingType.Secret => value is not SecretValue { IsEmpty: false },
            SettingType.Text => string.IsNullOrWhiteSpace(value as string),
            _ => !bag.TryGetValue(setting.Key, out var element) || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined,
        };
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Expected(Type type)
    {
        var plain = Nullable.GetUnderlyingType(type) ?? type;
        return plain == typeof(string) ? "text"
            : plain == typeof(bool) ? "on or off"
            : plain == typeof(int) || plain == typeof(long) ? "a whole number"
            : "a number";
    }

    private static bool TryConvert(JsonElement element, Type type, out object? value)
    {
        var plain = Nullable.GetUnderlyingType(type) ?? type;
        value = null;
        try
        {
            if (plain == typeof(string) && element.ValueKind == JsonValueKind.String)
                value = element.GetString();
            else if (plain == typeof(bool) && element.ValueKind is JsonValueKind.True or JsonValueKind.False)
                value = element.GetBoolean();
            else if ((plain == typeof(int) || plain == typeof(long)) && element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out long whole))
                value = plain == typeof(int) ? (object)checked((int)whole) : whole;
            else if ((plain == typeof(double) || plain == typeof(float) || plain == typeof(decimal)) && element.ValueKind == JsonValueKind.Number)
                value = Convert.ChangeType(element.GetDouble(), plain, CultureInfo.InvariantCulture);
            else
                return false;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
