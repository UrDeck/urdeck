// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using UrDeck.Sdk.Data;

namespace UrDeck.Sdk.Settings;

/// <summary>Where the editor takes the words it shows for a setting that is empty.</summary>
public enum FallbackSource
{
    /// <summary>The words in <see cref="SettingAttribute.Fallback"/>, if any.</summary>
    None,

    /// <summary>The reading chosen in the setting named by <see cref="SettingAttribute.FallbackReading"/> (its label, unit, range or levels, by this setting's key).</summary>
    Reading,

    /// <summary>What the user's region gives (a temperature unit, for example).</summary>
    Region,

    /// <summary>The theme decides when it draws; <see cref="SettingAttribute.Fallback"/> says so in words.</summary>
    Theme,
}

/// <summary>
/// Declares a property of a widget's configuration type, of a provider's settings type or of a list item type as a setting the user
/// can change: it gets a control in the editor and a row in the generated reference. The key in <c>urdeck-config.json</c> is the
/// property's <see cref="System.Text.Json.Serialization.JsonPropertyNameAttribute"/> name, else its name in camelCase. The kind of
/// control comes from the property's type (on/off is a toggle, text a text box, a number a number box, text with
/// <see cref="OptionAttribute"/>s a choice) or from a <see cref="SettingKindAttribute"/> next to this one. A property without this
/// attribute is read from and written to the file but not offered in the editor.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class SettingAttribute : Attribute
{
    /// <summary>Declares a setting.</summary>
    /// <param name="label">The short name shown beside the control (plain English).</param>
    public SettingAttribute(string label)
    {
        Label = label;
    }

    /// <summary>The short name shown beside the control.</summary>
    public string Label { get; }

    /// <summary>One sentence under the control and in the reference.</summary>
    public string Help { get; set; } = "";

    /// <summary>A small heading the setting is listed under. Settings without a group come first.</summary>
    public string? Group { get; set; }

    /// <summary>Position within the group; settings with the same order keep their declaration order.</summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether the provider cannot work without it. While a required setting is missing (absent, empty, or a secret with no value)
    /// the provider is not started and its readings say what to set.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>The widget sizes (<c>"4x4"</c>) the setting applies at; at another size it is listed as applying only at these. Null: every size.</summary>
    public string[]? Sizes { get; set; }

    /// <summary>The words that say what an empty value means (<c>"None"</c>, <c>"Automatic"</c>, <c>"Off"</c>); shown as the placeholder.</summary>
    public string? Fallback { get; set; }

    /// <summary>Tells the editor to work out the placeholder itself instead of using <see cref="Fallback"/>.</summary>
    public FallbackSource FallbackFrom { get; set; }

    /// <summary>With <see cref="FallbackSource.Reading"/>: the key of the reading setting the empty value follows.</summary>
    public string? FallbackReading { get; set; }

    /// <summary>
    /// Text only: an empty text is meaningful and differs from an absent one (absent uses the fallback, empty shows nothing). The
    /// editor shows a switch for the "nothing" state.
    /// </summary>
    public bool AllowsNone { get; set; }

    /// <summary>List items only: the roles (see <see cref="PositionsAttribute"/>) in which this setting has no effect.</summary>
    public string[]? DisabledInRoles { get; set; }

    /// <summary>With <see cref="DisabledInRoles"/>: the sentence that says why.</summary>
    public string? DisabledHelp { get; set; }
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

/// <summary>The setting applies only while another setting of the same object has one of the given values.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class ShownWhenAttribute : Attribute
{
    /// <summary>Declares the condition.</summary>
    /// <param name="key">The key of the other setting.</param>
    /// <param name="values">The values (as text) that make this setting apply.</param>
    public ShownWhenAttribute(string key, params string[] values)
    {
        Key = key;
        Values = values;
    }

    /// <summary>The key of the other setting.</summary>
    public string Key { get; }

    /// <summary>The values that make this setting apply.</summary>
    public string[] Values { get; }
}

/// <summary>
/// One option of a choice. Repeat it for each option, in display order. An empty <c>value</c> means "the key is absent":
/// the editor removes the key instead of writing it.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
public sealed class OptionAttribute : Attribute
{
    /// <summary>Declares an option.</summary>
    /// <param name="value">The value as written in the file.</param>
    /// <param name="label">What the user sees.</param>
    public OptionAttribute(string value, string label)
    {
        Value = value;
        Label = label;
    }

    /// <summary>The value as written in the file; empty for "absent".</summary>
    public string Value { get; }

    /// <summary>What the user sees.</summary>
    public string Label { get; }
}

/// <summary>
/// Marks a property that another setting's control writes and that is never shown by itself (Weather's latitude and longitude,
/// written by the place picker).
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class SettingPartAttribute : Attribute
{
    /// <summary>Declares the property as a part of another setting.</summary>
    /// <param name="ownerSetting">The key of the setting whose control writes it.</param>
    public SettingPartAttribute(string ownerSetting)
    {
        OwnerSetting = ownerSetting;
    }

    /// <summary>The key of the setting whose control writes it.</summary>
    public string OwnerSetting { get; }
}

/// <summary>
/// On a list setting: how many positions the list has at a widget size and the role of each (words the widget chooses, such as
/// <c>"gauge"</c> and <c>"text"</c>). Repeat it for each size. Items beyond the positions stay in the file.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
public sealed class PositionsAttribute : Attribute
{
    /// <summary>Declares the positions at one size.</summary>
    /// <param name="size">The size, for example <c>"4x2"</c>.</param>
    /// <param name="roles">One role per position, in order.</param>
    public PositionsAttribute(string size, params string[] roles)
    {
        Size = size;
        Roles = roles;
    }

    /// <summary>The widget size, for example <c>"4x2"</c>.</summary>
    public string Size { get; }

    /// <summary>One role per position, in order.</summary>
    public string[] Roles { get; }
}
