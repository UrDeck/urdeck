// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Settings;

namespace UrDeck.Engine.Settings;

/// <summary>One option of a choice setting.</summary>
/// <param name="Value">The value as written in the file; empty for "the key is absent".</param>
/// <param name="Label">What the user sees.</param>
public sealed record SettingOption(string Value, string Label);

/// <summary>The setting applies only while another setting has one of these values.</summary>
/// <param name="Key">The other setting's key.</param>
/// <param name="Values">The values, as text.</param>
public sealed record ShownWhen(string Key, IReadOnlyList<string> Values);

/// <summary>What an empty value means.</summary>
/// <param name="Text">Words (<c>None</c>, <c>Automatic</c>); null for none.</param>
/// <param name="From">Where the editor works the placeholder out instead.</param>
/// <param name="ReadingSetting">With <see cref="FallbackSource.Reading"/>, the key of the reading setting followed.</param>
public sealed record Fallback(string? Text, FallbackSource From, string? ReadingSetting);

/// <summary>A number setting's limits and unit (also a slider's).</summary>
/// <param name="Min">The smallest value; null for none.</param>
/// <param name="Max">The largest value; null for none.</param>
/// <param name="Step">The step; 0 for the editor's choice.</param>
/// <param name="Unit">A fixed unit.</param>
/// <param name="UnitFromReading">The key of the reading setting whose display unit the value is shown in.</param>
/// <param name="WholeNumber">True for an integer property.</param>
public sealed record NumberSpec(double? Min, double? Max, double Step, string? Unit, string? UnitFromReading, bool WholeNumber);

/// <summary>Which readings a reading setting offers.</summary>
/// <param name="Filter">The filter.</param>
/// <param name="Provider">The provider id; null for all.</param>
public sealed record ReadingSpec(ReadingFilter Filter, string? Provider);

/// <summary>A place setting's source and the properties that receive the coordinates.</summary>
/// <param name="Source">The provider that answers the search.</param>
/// <param name="LatitudeSetting">The latitude's key.</param>
/// <param name="LongitudeSetting">The longitude's key.</param>
public sealed record PlaceSpec(string Source, string? LatitudeSetting, string? LongitudeSetting);

/// <summary>The roles of a list's positions at one widget size.</summary>
/// <param name="Size">The size (<c>4x2</c>).</param>
/// <param name="Roles">One role per position.</param>
public sealed record SizePositions(string Size, IReadOnlyList<string> Roles);

/// <summary>A list setting's positions per size.</summary>
/// <param name="Sizes">One entry per size, in declaration order.</param>
public sealed record PositionsSpec(IReadOnlyList<SizePositions> Sizes)
{
    /// <summary>The roles at <paramref name="size"/>; null when the size has none declared.</summary>
    public IReadOnlyList<string>? RolesAt(string size) => Sizes.FirstOrDefault(s => s.Size == size)?.Roles;
}

/// <summary>One setting as its declaration says it, ready for an editor or the reference.</summary>
public sealed record SettingDescriptor(
    string Key,
    string Kind,
    string Label,
    string Help,
    string? Group,
    int Order,
    bool Required,
    IReadOnlyList<string>? Sizes,
    ShownWhen? When,
    Fallback Fallback,
    bool AllowsNone,
    IReadOnlyList<SettingOption> Options,
    NumberSpec? Number,
    ReadingSpec? Reading,
    string? OwnerReading,
    PlaceSpec? Place,
    LaunchKinds? Launch,
    bool IconAllowsFile,
    string? SecretContext,
    PositionsSpec? Positions,
    SettingSchema? Item,
    object? Default)
{
    /// <summary>The roles in which the setting has no effect (a list item's setting); empty for none.</summary>
    public IReadOnlyList<string> DisabledInRoles { get; init; } = [];

    /// <summary>The sentence that says why it is disabled in those roles.</summary>
    public string? DisabledHelp { get; init; }

    internal PropertyInfo Property { get; init; } = null!;

    /// <summary>Whether this is a secret.</summary>
    public bool IsSecret => Kind == SettingKinds.Secret;

    /// <summary>For a secret, the key of the setting it was issued for.</summary>
    public string? ContextKey => SecretContext;
}

/// <summary>The kind names the engine knows.</summary>
public static class SettingKinds
{
    public const string Toggle = "toggle";
    public const string Choice = "choice";
    public const string Text = "text";
    public const string Number = "number";
    public const string Range = "range";
    public const string Reading = "reading";
    public const string Action = "action";
    public const string Unit = "unit";
    public const string Place = "place";
    public const string LaunchTarget = "launchTarget";
    public const string Icon = "icon";
    public const string Secret = "secret";
    public const string Positions = "positions";
}

/// <summary>
/// The settings a type declares, read once by reflection: a widget's configuration type, a provider's settings type or a list item
/// type. See the settings-metadata spec.
/// </summary>
public sealed class SettingSchema
{
    private SettingSchema(Type type, IReadOnlyList<SettingDescriptor> settings, IReadOnlyList<string> parts)
    {
        Type = type;
        Settings = settings;
        Parts = parts;
    }

    /// <summary>The type that was read.</summary>
    public Type Type { get; }

    /// <summary>The declared settings in display order: group-less first, then by group, order and declaration.</summary>
    public IReadOnlyList<SettingDescriptor> Settings { get; }

    /// <summary>The keys of the properties other settings' controls write (<see cref="SettingPartAttribute"/>).</summary>
    public IReadOnlyList<string> Parts { get; }

    /// <summary>The key a property has in the file.</summary>
    public static string KeyOf(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

    /// <summary>A schema over settings that were declared elsewhere (a provider's), for a form to build from.</summary>
    public static SettingSchema Of(Type type, IReadOnlyList<SettingDescriptor> settings) => new(type, settings, []);

    /// <summary>Reads <paramref name="type"/>; throws <see cref="InvalidOperationException"/> with the reason when a declaration is invalid.</summary>
    /// <param name="type">The type to read.</param>
    /// <param name="stopAt">A base type whose properties are not settings (<c>WidgetConfig</c>, <c>ProviderSettings</c>); null for none.</param>
    public static SettingSchema Read(Type type, Type? stopAt = null)
    {
        if (!TryRead(type, stopAt, out var schema, out string? error))
            throw new InvalidOperationException(error);
        return schema!;
    }

    /// <summary>Reads <paramref name="type"/>; false with the reason when a declaration is invalid.</summary>
    public static bool TryRead(Type type, Type? stopAt, out SettingSchema? schema, out string? error)
    {
        schema = null;
        error = null;
        object? defaults = null;
        try
        {
            defaults = Activator.CreateInstance(type);
        }
        catch (Exception)
        {
            // A type without defaults shows none.
        }

        var entries = new List<(PropertyInfo Property, int Index)>();
        int index = 0;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            index++;
            if (stopAt != null && property.DeclaringType!.IsAssignableFrom(stopAt))
                continue;
            if (property.GetCustomAttribute<JsonExtensionDataAttribute>() != null || property.GetCustomAttribute<JsonIgnoreAttribute>() != null
                || property.SetMethod is not { IsPublic: true } || property.GetIndexParameters().Length > 0)
                continue;
            entries.Add((property, index));
        }

        var parts = new List<string>();
        var built = new List<(SettingDescriptor Descriptor, int Index)>();
        foreach (var (property, i) in entries)
        {
            string key = KeyOf(property);
            if (property.GetCustomAttribute<SettingPartAttribute>() != null)
            {
                parts.Add(key);
                continue;
            }

            if (property.GetCustomAttribute<SettingAttribute>() is not { } setting)
                continue;

            var descriptor = Describe(property, key, setting, defaults, out error);
            if (descriptor == null)
                return false;
            built.Add((descriptor, i));
        }

        var keys = new HashSet<string>(built.Select(b => b.Descriptor.Key).Concat(parts), StringComparer.OrdinalIgnoreCase);
        foreach (var (property, _) in entries)
        {
            if (property.GetCustomAttribute<SettingPartAttribute>() is { } part && !keys.Contains(part.OwnerSetting))
            {
                error = $"'{property.Name}' is a part of the setting '{part.OwnerSetting}', which is not declared on {type.Name}";
                return false;
            }
        }

        foreach (var (descriptor, _) in built)
        {
            error = CheckReferences(type, descriptor, keys, built.Select(b => b.Descriptor.Key).ToHashSet(StringComparer.OrdinalIgnoreCase));
            if (error != null)
                return false;
        }

        // Group-less first, then groups in the order they first appear; within a group by order, then declaration.
        var groupRank = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (descriptor, i) in built.OrderBy(b => b.Index))
        {
            if (descriptor.Group != null && !groupRank.ContainsKey(descriptor.Group))
                groupRank[descriptor.Group] = groupRank.Count + 1;
        }

        var ordered = built
            .OrderBy(b => b.Descriptor.Group == null ? 0 : groupRank[b.Descriptor.Group])
            .ThenBy(b => b.Descriptor.Order)
            .ThenBy(b => b.Index)
            .Select(b => b.Descriptor)
            .ToList();
        schema = new SettingSchema(type, ordered, parts);
        return true;
    }

    private static string? CheckReferences(Type type, SettingDescriptor setting, HashSet<string> allKeys, HashSet<string> declared)
    {
        string? Missing(string? key, string what) =>
            key != null && !allKeys.Contains(key) ? $"setting '{setting.Key}' of {type.Name} names {what} '{key}', which is not declared" : null;
        string? MissingSetting(string? key, string what) =>
            key != null && !declared.Contains(key) ? $"setting '{setting.Key}' of {type.Name} names {what} '{key}', which is not a declared setting" : null;

        return MissingSetting(setting.When?.Key, "the setting it is shown with")
            ?? MissingSetting(setting.Fallback.ReadingSetting, "the reading setting it follows")
            ?? MissingSetting(setting.OwnerReading, "the reading setting")
            ?? MissingSetting(setting.Number?.UnitFromReading, "the reading setting its unit follows")
            ?? Missing(setting.Place?.LatitudeSetting, "the latitude setting")
            ?? Missing(setting.Place?.LongitudeSetting, "the longitude setting");
    }

    private static SettingDescriptor? Describe(PropertyInfo property, string key, SettingAttribute setting, object? defaults, out string? error)
    {
        error = null;
        var kindAttribute = property.GetCustomAttribute<SettingKindAttribute>();
        var options = property.GetCustomAttributes<OptionAttribute>().Select(o => new SettingOption(o.Value, o.Label)).ToList();
        var positions = property.GetCustomAttributes<PositionsAttribute>().ToList();
        bool isSecret = property.GetCustomAttribute<SecretAttribute>() != null;
        var plain = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        NumberSpec? number = null;
        ReadingSpec? reading = null;
        string? owner = null;
        PlaceSpec? place = null;
        LaunchKinds? launch = null;
        bool iconFile = true;
        PositionsSpec? positionsSpec = null;
        SettingSchema? item = null;
        string kind;

        if (isSecret)
        {
            if (plain != typeof(SecretValue))
            {
                error = $"secret '{property.Name}' must be a SecretValue";
                return null;
            }

            kind = SettingKinds.Secret;
        }
        else if (positions.Count > 0)
        {
            kind = SettingKinds.Positions;
            positionsSpec = new PositionsSpec(positions.Select(p => new SizePositions(p.Size, p.Roles)).ToList());
            if (!plain.IsGenericType || plain.GetGenericTypeDefinition() != typeof(List<>))
            {
                error = $"setting '{property.Name}' has [Positions] but is not a List<T>";
                return null;
            }

            if (!TryRead(plain.GetGenericArguments()[0], null, out item, out string? itemError))
            {
                error = $"the items of '{property.Name}': {itemError}";
                return null;
            }
        }
        else if (kindAttribute != null)
        {
            kind = kindAttribute.Kind;
            switch (kindAttribute)
            {
                case NumberAttribute n:
                    number = new NumberSpec(Finite(n.Min), Finite(n.Max), n.Step, n.Unit, n.UnitFromReading, IsWhole(plain));
                    break;
                case SliderAttribute s:
                    number = new NumberSpec(s.Min, s.Max, s.Step, s.Unit, null, IsWhole(plain) || s.Step % 1 == 0);
                    break;
                case ReadingAttribute r:
                    reading = new ReadingSpec(r.Filter, r.Provider);
                    break;
                case ActionAttribute a:
                    owner = a.ReadingSetting;
                    break;
                case UnitAttribute u:
                    owner = u.ReadingSetting;
                    break;
                case PlaceAttribute p:
                    place = new PlaceSpec(p.Source, p.LatitudeSetting, p.LongitudeSetting);
                    break;
                case LaunchTargetAttribute l:
                    launch = l.Allowed;
                    break;
                case IconAttribute i:
                    iconFile = i.AllowFile;
                    break;
            }
        }
        else if (options.Count > 0)
        {
            if (plain == typeof(bool))
            {
                error = $"setting '{property.Name}' is a bool and cannot have [Option]s";
                return null;
            }

            kind = SettingKinds.Choice;
        }
        else if (plain == typeof(bool))
        {
            kind = SettingKinds.Toggle;
        }
        else if (plain == typeof(string))
        {
            kind = SettingKinds.Text;
        }
        else if (IsNumeric(plain))
        {
            kind = SettingKinds.Number;
            number = new NumberSpec(null, null, 0, null, null, IsWhole(plain));
        }
        else if (plain == typeof(JsonElement) || (plain.IsGenericType && plain.GetGenericTypeDefinition() == typeof(List<>)))
        {
            error = $"setting '{property.Name}' of type {plain.Name} needs a kind attribute";
            return null;
        }
        else
        {
            error = $"setting '{property.Name}' has the unsupported type {property.PropertyType.Name}";
            return null;
        }

        var when = property.GetCustomAttribute<ShownWhenAttribute>() is { } w ? new ShownWhen(w.Key, w.Values) : null;
        object? value = isSecret || defaults == null ? null : property.GetValue(defaults);
        return new SettingDescriptor(
            key,
            kind,
            setting.Label,
            setting.Help,
            setting.Group,
            setting.Order,
            setting.Required,
            setting.Sizes,
            when,
            new Fallback(setting.Fallback, setting.FallbackFrom, setting.FallbackReading),
            setting.AllowsNone,
            options,
            number,
            reading,
            owner,
            place,
            launch,
            iconFile,
            property.GetCustomAttribute<SecretAttribute>()?.ContextSetting,
            positionsSpec,
            item,
            value)
        {
            Property = property,
            DisabledInRoles = setting.DisabledInRoles ?? [],
            DisabledHelp = setting.DisabledHelp,
        };
    }

    private static double? Finite(double value) => double.IsNaN(value) ? null : value;

    private static bool IsNumeric(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(float) || type == typeof(decimal);

    private static bool IsWhole(Type type) => type == typeof(int) || type == typeof(long);
}
