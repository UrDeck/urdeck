// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text.Json;
using UrDeck.Sdk.Data;

namespace UrDeck.Providers.HomeAssistant;

/// <summary>
/// What an entity is in UrDeck's terms (provider-homeassistant, "Kinds And Units"): its kind from its domain, device class and
/// its own unit (never the installation's unit system), its value in the canonical unit, its label, range, natural action and
/// icon. Shown readings, a listing and the sample all describe an entity with it, so they agree.
/// </summary>
internal static class EntityMapping
{
    internal const string UnavailableInHomeAssistant = "unavailable in Home Assistant";

    private static readonly HashSet<string> OnOffDomains = new(StringComparer.Ordinal)
    {
        "binary_sensor", "switch", "light", "fan", "input_boolean", "automation",
    };

    /// <summary>What an entity's value is and how a reported number becomes the canonical one.</summary>
    private sealed record Shape(ReadingKind Kind, Func<double, double> ToCanonical, string? Unit = null, DepthUnit? Depth = null);

    private static readonly Func<double, double> Same = x => x;

    /// <summary>The description of an entity (<paramref name="attribute"/> null) or of one of its attributes.</summary>
    public static ReadingDescriptor Describe(EntityState entity, string? attribute = null)
    {
        string label = entity.Text("friendly_name") is { Length: > 0 } friendly ? friendly : entity.Id;
        string icon = EntityIcons.For(entity);
        if (attribute != null)
            return DescribeAttribute(entity, attribute, label, icon);

        var shape = ShapeOf(entity);
        var descriptor = new ReadingDescriptor(entity.Id, shape.Kind, label, entity.Id)
        {
            Unit = shape.Unit,
            DepthUnit = shape.Depth,
            Action = EntityIcons.NaturalAction(entity),
            Icon = icon,
        };
        if (shape.Kind == ReadingKind.Percent)
            descriptor = descriptor with { Min = 0, Max = 100 };
        if (shape.Kind is ReadingKind.Number or ReadingKind.Percent or ReadingKind.Temperature or ReadingKind.Speed or ReadingKind.Depth
            && entity.Number("min") is { } min && entity.Number("max") is { } max)
            descriptor = descriptor with { Min = shape.ToCanonical(min), Max = shape.ToCanonical(max) };
        return descriptor;
    }

    /// <summary>
    /// The value of a shown path: a publication, the reason it is unavailable, or null for an entity whose state is
    /// <c>unknown</c> (nothing is published: the reading stays pending, or keeps its last value).
    /// </summary>
    public static ReadingUpdate? Value(EntityState entity, string path, string? attribute, ReadingDescriptor described)
    {
        if (entity.State == "unavailable")
            return ReadingUpdate.Missing(path, UnavailableInHomeAssistant);
        if (attribute != null)
            return AttributeValue(entity, path, attribute, described);
        if (entity.State == "unknown")
            return null;

        string state = entity.State;
        switch (described.Kind)
        {
            case ReadingKind.Text:
                return ReadingUpdate.Of(path, state);
            case ReadingKind.OnOff:
                return state switch
                {
                    "on" => ReadingUpdate.Of(path, true),
                    "off" => ReadingUpdate.Of(path, false),
                    _ => NotFit(path, state, "on or off"),
                };
            case ReadingKind.Time:
                return DateTimeOffset.TryParse(state, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant)
                    ? ReadingUpdate.Of(path, instant)
                    : NotFit(path, state, "a time");
            default:
                if (!double.TryParse(state, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                    return NotFit(path, state, KindText(described.Kind));
                return ReadingUpdate.Of(path, ShapeOf(entity).ToCanonical(number));
        }
    }

    private static ReadingDescriptor DescribeAttribute(EntityState entity, string attribute, string label, string icon)
    {
        string name = $"{entity.Id}/{attribute}";
        string attributeLabel = $"{label} {attribute.Replace('_', ' ')}";
        if (attribute == "brightness")
        {
            // The level a dimmer sets: its natural action takes the level in percent (light.turn_on's brightness_pct).
            return new ReadingDescriptor(name, ReadingKind.Percent, $"{label} brightness", name)
            {
                Min = 0,
                Max = 100,
                Icon = icon,
                Action = entity.Domain == "light" ? $"{entity.Id}/turn_on" : null,
            };
        }

        var kind = entity.Attributes.TryGetValue(attribute, out var value) ? value.ValueKind switch
        {
            JsonValueKind.Number => ReadingKind.Number,
            JsonValueKind.True or JsonValueKind.False => ReadingKind.OnOff,
            _ => ReadingKind.Text,
        } : ReadingKind.Text;
        return new ReadingDescriptor(name, kind, attributeLabel, name) { Icon = icon };
    }

    private static ReadingUpdate AttributeValue(EntityState entity, string path, string attribute, ReadingDescriptor described)
    {
        if (!entity.Attributes.TryGetValue(attribute, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            // A light that is off reports no brightness: it is at nothing.
            if (attribute == "brightness" && entity.State == "off")
                return ReadingUpdate.Of(path, 0.0);
            return ReadingUpdate.Missing(path, $"{entity.Id} has no attribute '{attribute}' in Home Assistant");
        }

        if (attribute == "brightness")
        {
            return EntityState.AsNumber(value) is { } level
                ? ReadingUpdate.Of(path, Math.Clamp(level / 255.0 * 100.0, 0, 100))
                : NotFit(path, value.GetRawText(), "a brightness");
        }

        return described.Kind switch
        {
            ReadingKind.Number => EntityState.AsNumber(value) is { } number ? ReadingUpdate.Of(path, number) : NotFit(path, value.GetRawText(), "a number"),
            ReadingKind.OnOff => value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? ReadingUpdate.Of(path, value.GetBoolean())
                : NotFit(path, value.GetRawText(), "on or off"),
            _ => ReadingUpdate.Of(path, value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText()),
        };
    }

    private static Shape ShapeOf(EntityState entity)
    {
        if (OnOffDomains.Contains(entity.Domain))
            return new Shape(ReadingKind.OnOff, Same);

        string deviceClass = entity.Text("device_class") ?? "";
        string unit = entity.Text("unit_of_measurement") ?? "";
        switch (deviceClass)
        {
            case "temperature" when unit is "°C" or "°F":
                return new Shape(ReadingKind.Temperature, unit == "°F" ? f => (f - 32) * 5 / 9 : Same);
            case "speed" or "wind_speed" when unit is "km/h" or "mph" or "m/s":
                return new Shape(ReadingKind.Speed, unit switch
                {
                    "mph" => x => x * 1.609344,
                    "m/s" => x => x * 3.6,
                    _ => Same,
                });
            case "precipitation" when unit is "mm" or "cm" or "in":
                return unit switch
                {
                    "cm" => new Shape(ReadingKind.Depth, Same, Depth: DepthUnit.Centimetre),
                    "in" => new Shape(ReadingKind.Depth, x => x * 25.4, Depth: DepthUnit.Millimetre),
                    _ => new Shape(ReadingKind.Depth, Same, Depth: DepthUnit.Millimetre),
                };
            case "battery" or "humidity":
                return new Shape(ReadingKind.Percent, Same);
            case "timestamp":
                return new Shape(ReadingKind.Time, Same);
        }

        if (unit == "%")
            return new Shape(ReadingKind.Percent, Same);
        if (unit.Length > 0)
            return new Shape(ReadingKind.Number, Same, Unit: unit);
        return new Shape(ReadingKind.Text, Same);
    }

    private static ReadingUpdate NotFit(string path, string state, string expected) =>
        ReadingUpdate.Missing(path, $"Home Assistant reported '{state}', which is not {expected}");

    private static string KindText(ReadingKind kind) => kind switch
    {
        ReadingKind.Temperature => "a temperature",
        ReadingKind.Speed => "a speed",
        ReadingKind.Depth => "an amount of precipitation",
        ReadingKind.Percent => "a percentage",
        _ => "a number",
    };
}
