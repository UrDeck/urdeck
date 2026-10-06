// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections.Concurrent;
using System.Reflection;
using UrDeck.Engine.Config;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Plugin;

/// <summary>Everything the host needs to know about a widget type, read once from its attributes.</summary>
public sealed record WidgetDescriptor(
    string Id,
    string Name,
    string Description,
    string Category,
    Type WidgetType,
    Type ConfigType,
    System.Drawing.Size[] SupportedSizes,
    RefreshStrategy Refresh,
    TimeSpan? RefreshInterval)
{
    public static WidgetDescriptor? TryCreate(Type type, out string? rejectReason)
    {
        rejectReason = null;
        if (!type.IsClass || type.IsAbstract || !typeof(IWidget).IsAssignableFrom(type))
        {
            rejectReason = "not a concrete IWidget";
            return null;
        }

        var meta = type.GetCustomAttribute<WidgetAttribute>();
        if (meta == null)
        { rejectReason = "missing [Widget]"; return null; }

        var sizes = type.GetCustomAttributes<WidgetSizeAttribute>()
            .Select(s => new System.Drawing.Size(s.Width, s.Height)).ToArray();
        if (sizes.Length == 0)
        { rejectReason = "missing [WidgetSize]"; return null; }

        var tick = type.GetCustomAttribute<RefreshOnTickAttribute>();
        var adaptive = type.GetCustomAttribute<RefreshAdaptiveAttribute>();
        var onData = type.GetCustomAttribute<RefreshOnDataAttribute>();
        int strategies = new object?[] { tick, adaptive, onData }.Count(a => a != null);
        if (strategies != 1)
        { rejectReason = $"needs exactly one refresh strategy (found {strategies})"; return null; }

        if (type.GetConstructor(Type.EmptyTypes) == null)
        { rejectReason = "no public parameterless constructor"; return null; }

        var configType = meta.ConfigType ?? FindConfigType(type);
        if (configType == null)
        { rejectReason = "cannot determine config type"; return null; }

        RefreshStrategy refresh;
        TimeSpan? interval = null;
        if (tick != null)
        {
            refresh = RefreshStrategy.OnTick;
            interval = ToTimeSpan(tick.Interval, tick.Unit);
        }
        else if (adaptive != null)
        {
            // TODO(adaptive): scale between MinMs and MaxMs based on system load. Uses the fastest rate for now.
            refresh = RefreshStrategy.Adaptive;
            interval = TimeSpan.FromMilliseconds(adaptive.MinMs);
        }
        else
        {
            refresh = RefreshStrategy.OnData;
        }

        return new WidgetDescriptor(
            meta.Id ?? meta.Name,
            meta.Name,
            meta.Description,
            type.GetCustomAttribute<CategoryAttribute>()?.Name ?? meta.Category ?? "General",
            type,
            configType,
            sizes,
            refresh,
            interval);
    }

    private static Type? FindConfigType(Type type) =>
        type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWidget<>))
            ?.GetGenericArguments()[0];

    private static TimeSpan ToTimeSpan(double value, TimeUnit unit) => unit switch
    {
        TimeUnit.Milliseconds => TimeSpan.FromMilliseconds(value),
        TimeUnit.Seconds => TimeSpan.FromSeconds(value),
        TimeUnit.Minutes => TimeSpan.FromMinutes(value),
        TimeUnit.Hours => TimeSpan.FromHours(value),
        _ => throw new ArgumentOutOfRangeException(nameof(unit)),
    };
}

/// <summary>Maps widget type IDs to their descriptors. Thread-safe.</summary>
public class WidgetRegistry
{
    private readonly ConcurrentDictionary<string, WidgetDescriptor> _widgets = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Handed to every widget created here, before it is configured. Null attaches nothing.</summary>
    public IWidgetHost? Host { get; set; }

    public void Register(WidgetDescriptor descriptor) => _widgets[descriptor.Id] = descriptor;

    public void Clear() => _widgets.Clear();

    public IReadOnlyList<string> GetRegisteredWidgetTypes() => _widgets.Keys.OrderBy(k => k).ToList();

    public IReadOnlyCollection<WidgetDescriptor> Descriptors => _widgets.Values.ToList();

    public WidgetDescriptor? GetDescriptor(string typeId) =>
        _widgets.TryGetValue(typeId, out var d) ? d : null;

    public Type? GetWidget(string typeId) => GetDescriptor(typeId)?.WidgetType;

    /// <summary>
    /// Creates a new widget instance configured from <paramref name="config"/>. Each placed widget
    /// gets its own instance. Returns null when the type isn't registered.
    /// </summary>
    public IWidget? CreateWidget(WidgetConfig config)
    {
        var descriptor = GetDescriptor(config.WidgetTypeId);
        if (descriptor == null)
            return null;

        var widget = (IWidget)Activator.CreateInstance(descriptor.WidgetType)!;
        if (Host != null)
            widget.Attach(Host);
        widget.Configure(config.ToConcrete(descriptor.ConfigType));
        return widget;
    }
}
