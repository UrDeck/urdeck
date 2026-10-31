// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Providers.HomeAssistant;

/// <summary>
/// An entity's natural action and canonical icon key (provider-homeassistant, "Natural Action And Icon"): the icon by domain and
/// device class, then by a well-known <c>mdi:</c> name, then by the domain alone, and otherwise <c>home/entity</c>.
/// </summary>
internal static class EntityIcons
{
    internal const string Generic = "home/entity";

    /// <summary>The domains whose tap is <c>toggle</c> (see <see cref="ServiceCalls.Resolve"/> for what it calls).</summary>
    private static readonly HashSet<string> ToggleDomains = new(StringComparer.Ordinal)
    {
        "scene", "script", "button", "input_button", "media_player", "lock",
        "light", "switch", "fan", "cover", "input_boolean", "automation", "climate",
    };

    // Prefixes of Material Design icon names that stand for something UrDeck has its own key for.
    private static readonly (string Prefix, string Key)[] WellKnown =
    [
        ("mdi:lightbulb", "home/light"),
        ("mdi:led-strip", "home/light"),
        ("mdi:lamp", "home/light"),
        ("mdi:ceiling-light", "home/light"),
        ("mdi:floor-lamp", "home/light"),
        ("mdi:power-plug", "home/outlet"),
        ("mdi:power-socket", "home/outlet"),
        ("mdi:thermometer", "home/thermometer"),
        ("mdi:water-percent", "home/droplet"),
        ("mdi:flash", "home/bolt"),
        ("mdi:lightning-bolt", "home/bolt"),
        ("mdi:battery", "home/battery"),
        ("mdi:fan", "home/fan"),
        ("mdi:lock", "home/lock"),
        ("mdi:door", "home/door"),
        ("mdi:window", "home/window"),
        ("mdi:motion-sensor", "home/motion"),
        ("mdi:television", "home/tv"),
        ("mdi:speaker", "home/speaker"),
        ("mdi:garage", "home/garage"),
        ("mdi:blinds", "home/blinds"),
    ];

    /// <summary>The entity's natural action path (<c>light.desk/toggle</c>), or null for a display-only entity.</summary>
    public static string? NaturalAction(EntityState entity) =>
        ToggleDomains.Contains(entity.Domain) ? $"{entity.Id}/toggle" : null;

    public static string For(EntityState entity)
    {
        string domain = entity.Domain;
        string deviceClass = entity.Text("device_class") ?? "";
        if (ByClass(domain, deviceClass) is { } specific)
            return specific;

        string icon = entity.Text("icon") ?? "";
        foreach (var (prefix, key) in WellKnown)
        {
            if (icon.StartsWith(prefix, StringComparison.Ordinal))
                return key;
        }

        return ByDomain(domain) ?? Generic;
    }

    private static string? ByClass(string domain, string deviceClass) => (domain, deviceClass) switch
    {
        ("light", _) => "home/light",
        ("fan", _) => "home/fan",
        ("lock", _) => "home/lock",
        ("scene", _) => "home/scene",
        ("script", _) => "home/script",
        ("button" or "input_button", _) => "home/button",
        ("media_player", _) => "media/play",
        ("switch", "outlet") => "home/outlet",
        ("binary_sensor", "door" or "opening") => "home/door",
        ("binary_sensor", "garage_door") => "home/garage",
        ("binary_sensor", "window") => "home/window",
        ("binary_sensor", "motion" or "occupancy" or "presence") => "home/motion",
        ("binary_sensor", "moisture") => "home/leak",
        ("cover", "blind" or "shade" or "shutter" or "curtain" or "awning") => "home/blinds",
        ("cover", "garage") => "home/garage",
        ("cover", "door" or "gate") => "home/door",
        ("cover", "window") => "home/window",
        (_, "temperature") => "home/thermometer",
        (_, "humidity" or "moisture") => "home/droplet",
        (_, "power" or "energy" or "current" or "voltage") => "home/bolt",
        (_, "battery") => "home/battery",
        _ => null,
    };

    private static string? ByDomain(string domain) => domain switch
    {
        "switch" or "input_boolean" => "home/switch",
        "cover" => "home/blinds",
        "automation" => "home/script",
        _ => null,
    };
}
