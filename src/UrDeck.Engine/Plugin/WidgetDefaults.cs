// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UrDeck.Sdk;

namespace UrDeck.Engine.Plugin;

/// <summary>What a widget of a type gets when it is added without settings: its <c>DefaultConfig</c> and its default size.</summary>
public static class WidgetDefaults
{
    /// <summary>The order a new widget's size is chosen in: the first of these the type declares, else its first declared size.</summary>
    public static readonly IReadOnlyList<System.Drawing.Size> SizeOrder =
        [new(2, 2), new(4, 2), new(1, 1), new(2, 1), new(4, 1), new(4, 4)];

    // The placement keys every widget has; a widget's default leaves them to whoever places it.
    private static readonly HashSet<string> PlacementKeys = new(StringComparer.OrdinalIgnoreCase) { "typeId", "col", "row", "width", "height", "parameters", "isVisible" };

    /// <summary>
    /// The configuration a widget of the type gets when it is added without settings (<c>DefaultConfig</c>), with its type id
    /// set; its place and size are the caller's.
    /// </summary>
    public static WidgetConfig Config(WidgetDescriptor descriptor)
    {
        object widget = Activator.CreateInstance(descriptor.WidgetType)!;
        try
        {
            var property = typeof(IWidget<>).MakeGenericType(descriptor.ConfigType).GetProperty(nameof(IWidget<WidgetConfig>.DefaultConfig))!;
            var config = (WidgetConfig)property.GetValue(widget)!;
            config.WidgetTypeId = descriptor.Id;
            return config;
        }
        finally
        {
            (widget as IDisposable)?.Dispose();
        }
    }

    /// <summary>The size a new widget of the type gets (<see cref="SizeOrder"/>).</summary>
    public static System.Drawing.Size Size(WidgetDescriptor descriptor)
    {
        foreach (var size in SizeOrder)
        {
            if (descriptor.SupportedSizes.Contains(size))
                return size;
        }

        return descriptor.SupportedSizes[0];
    }

    /// <summary>
    /// A new widget object for the configuration file: <c>typeId</c>, the place and size given, then the type's default settings
    /// as the catalog writes them (camel case, no nulls).
    /// </summary>
    public static JsonObject Node(WidgetDescriptor descriptor, int col, int row, System.Drawing.Size size)
    {
        // Plugin config types live in collectible contexts: a private resolver keeps System.Text.Json's cache from pinning them.
#pragma warning disable CA1869 // a fresh options instance (and its cache) per call is the point
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };
#pragma warning restore CA1869
        var defaults = JsonSerializer.SerializeToNode(Config(descriptor), descriptor.ConfigType, options)?.AsObject() ?? [];

        var node = new JsonObject
        {
            ["typeId"] = descriptor.Id,
            ["col"] = col,
            ["row"] = row,
            ["width"] = size.Width,
            ["height"] = size.Height,
        };
        foreach (var (key, value) in defaults.ToList())
        {
            if (PlacementKeys.Contains(key))
                continue;
            defaults.Remove(key);
            node[key] = value;
        }

        return node;
    }
}
