// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>
/// Declares a property of a provider's settings type as a setting the user can change: it gets a field in the settings window
/// and a row in the generated reference. The key in <c>urdeck-config.json</c> is the property's
/// <see cref="System.Text.Json.Serialization.JsonPropertyNameAttribute"/> name, else its name in camelCase. A property without
/// this attribute is bound from the file but not offered in the window. Supported types: text, whole and fractional numbers,
/// on/off and, for a secret, <see cref="SecretValue"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class SettingAttribute : Attribute
{
    public SettingAttribute(string label)
    {
        Label = label;
    }

    /// <summary>The short name shown beside the field.</summary>
    public string Label { get; }

    /// <summary>One sentence under the field and in the reference.</summary>
    public string Help { get; set; } = "";

    /// <summary>
    /// Whether the provider cannot work without it. While a required setting is missing (absent, empty, or a secret with no value)
    /// the provider is not started and its readings say what to set.
    /// </summary>
    public bool Required { get; set; }
}

/// <summary>
/// Marks a <see cref="SecretValue"/> property as a secret (a token, a password). It is never read from nor written to the
/// configuration file and never logged: the engine fills it from the environment variable
/// <c>URDECK_SECRET_&lt;PROVIDER&gt;_&lt;SETTING&gt;</c> or Windows Credential Manager. Use it with <see cref="SettingAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class SecretAttribute : Attribute
{
    /// <summary>
    /// The key of the non-secret setting the secret belongs to (for a token, the address it was issued for). It is saved with the
    /// secret, and a copy configured for another value logs that the secret was saved for the other one.
    /// </summary>
    public string? ContextSetting { get; set; }
}
