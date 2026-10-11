// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrDeck.Engine.Display;

/// <summary>
/// A connected display, in physical pixels. <paramref name="Path"/> (the monitor's device interface path) and
/// <paramref name="Model"/> (the EDID manufacturer and product code, such as <c>RTD1100</c>) are its identity;
/// <paramref name="Name"/> is for people and <paramref name="DeviceName"/> (<c>\\.\DISPLAY1</c>) changes when displays
/// re-enumerate. A display Windows reports no target for has its device name as path and an empty model.
/// </summary>
public sealed record DisplayInfo(
    string Name, string Model, string Path, string DeviceName, Rectangle Bounds, double Scaling, bool IsPrimary)
{
    public override string ToString() =>
        $"{Name}{(Model.Length > 0 ? $" ({Model})" : "")} {DeviceName} {Bounds.Width}x{Bounds.Height} at {Bounds.X},{Bounds.Y}, " +
        $"{Scaling * 100:0}%{(IsPrimary ? " (primary)" : "")}";
}

/// <summary>
/// The display the deck belongs to, as the configuration's <c>display</c> object. The host writes it; removing it makes
/// the host choose again. Properties the host does not know are kept.
/// </summary>
public class RememberedDisplay
{
    /// <summary>For whoever reads the file or the log. Never matched.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>
    /// The selector the display was chosen with (<c>tallest</c>, <c>display1</c>, <c>index:1</c>). Absent when it was
    /// picked explicitly: such a display is never chosen again from the selectors.
    /// </summary>
    [JsonPropertyName("selector")]
    public string? Selector { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
