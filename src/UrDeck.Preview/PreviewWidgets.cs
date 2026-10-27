// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Config;
using UrDeck.Engine.Data;
using UrDeck.Engine.Plugin;
using UrDeck.Providers.HomeAssistant;
using UrDeck.Providers.Machine;
using UrDeck.Providers.Media;
using UrDeck.Providers.Weather;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;
using UrDeck.Sdk.Icons;
using UrDeck.Sdk.Launch;
using UrDeck.Widgets.Clock;
using UrDeck.Widgets.NowPlaying;
using UrDeck.Widgets.Shortcut;
using UrDeck.Widgets.Stats;
using UrDeck.Widgets.Weather;

namespace UrDeck.Preview;

/// <summary>
/// The widget types a preview can draw: the four first-party widgets, registered from their attributes as the plugin loader
/// registers them. A configuration that names another type leaves its cell empty.
/// </summary>
public static class PreviewWidgets
{
    private static readonly Lazy<IReadOnlyList<WidgetDescriptor>> All = new(() =>
        new[] { typeof(ClockWidget), typeof(StatsWidget), typeof(WeatherWidget), typeof(ShortcutWidget), typeof(NowPlayingWidget) }
            .Select(type => WidgetDescriptor.TryCreate(type, out _)!)
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToList());

    /// <summary>The widget types, by id.</summary>
    public static IReadOnlyList<WidgetDescriptor> Descriptors => All.Value;

    public static WidgetDescriptor? Get(string typeId) =>
        All.Value.FirstOrDefault(d => string.Equals(d.Id, typeId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The configuration a widget of the type gets when it is added without settings (<c>DefaultConfig</c>), with its type id
    /// set; its place and size are the caller's.
    /// </summary>
    public static WidgetConfig DefaultConfig(WidgetDescriptor descriptor)
    {
        object widget = Activator.CreateInstance(descriptor.WidgetType)!;
        System.Reflection.PropertyInfo property = typeof(IWidget<>).MakeGenericType(descriptor.ConfigType).GetProperty(nameof(IWidget<WidgetConfig>.DefaultConfig))!;
        var config = (WidgetConfig)property.GetValue(widget)!;
        config.WidgetTypeId = descriptor.Id;
        (widget as IDisposable)?.Dispose();
        return config;
    }

    /// <summary>A widget of the configuration's type, attached to <paramref name="host"/> and configured; null for a type this does not know.</summary>
    internal static IWidget? Create(WidgetConfig config, IWidgetHost host)
    {
        if (Get(config.WidgetTypeId) is not { } descriptor)
            return null;
        var widget = (IWidget)Activator.CreateInstance(descriptor.WidgetType)!;
        widget.Attach(host);
        widget.Configure(config.ToConcrete(descriptor.ConfigType));
        return widget;
    }
}

/// <summary>The first-party data providers, which a preview never creates: each is replaced by the sample its assembly carries.</summary>
public static class PreviewProviders
{
    private static readonly Lazy<IReadOnlyList<(ProviderDescriptor Provider, SampleFile? Sample)>> All = new(() =>
        new[] { typeof(HomeAssistantProvider), typeof(MediaProvider), typeof(SystemProvider), typeof(WeatherProvider) }
            .Select(type => ProviderDescriptor.TryCreate(type, out _)!)
            .Select(provider => (provider, ProviderSamples.Find(provider)))
            .OrderBy(p => p.provider.Id, StringComparer.Ordinal)
            .ToList());

    /// <summary>The first-party providers, by id; the provider types are read, never instantiated.</summary>
    public static IReadOnlyList<ProviderDescriptor> Descriptors => All.Value.Select(p => p.Provider).ToList();

    /// <summary>Whether the provider carries a sample a preview can use.</summary>
    public static bool HasSample(string providerId) => All.Value.Any(p => p.Provider.Id == providerId && p.Sample != null);

    /// <summary>Every first-party provider that has a sample, replaced by it, at the seconds since <paramref name="start"/> by <paramref name="now"/>.</summary>
    internal static ProviderRegistry Registry(Func<DateTime> now, DateTime start)
    {
        var registry = new ProviderRegistry();
        foreach (var (provider, sample) in All.Value)
        {
            if (sample != null)
                registry.TryRegister(ProviderSamples.Replace(provider, sample, now, start), out _);
        }

        return registry;
    }
}

/// <summary>
/// What a widget in a preview is given: the readings of the preview's hub, a launcher that opens nothing, and icons that are
/// always on their way, so a shortcut draws its letter tile on every machine and no network or shell is asked.
/// </summary>
internal sealed class PreviewHost(IReadingSource readings) : IWidgetHost, ILauncher, IIconSource
{
    public IReadingSource Readings { get; } = readings;

    public ILauncher Launcher => this;

    public IIconSource Icons => this;

    public void Log(string message)
    {
    }

    public bool Launch(string target, string? arguments = null) => false;

    public IconResult GetIcon(string source, int pixelSize) => IconResult.Loading;
}

/// <summary>Reads the configuration a preview draws.</summary>
public static class PreviewConfig
{
    /// <summary>The sample page's moment: 13 October 2026, 21:47:32 local time.</summary>
    public static readonly DateTime SampleMoment = new(2026, 10, 13, 21, 47, 32, DateTimeKind.Local);

    /// <summary>The sample page (<c>SamplePage/urdeck-config.json</c>): a clock, two stats cards, a weather card, four shortcuts and a dock.</summary>
    public static UrDeckConfig Sample()
    {
        using var stream = typeof(PreviewConfig).Assembly.GetManifestResourceStream("UrDeck.Preview.sample-page.json")!;
        using var reader = new StreamReader(stream);
        return ConfigStore.Parse(reader.ReadToEnd());
    }

    /// <summary>A configuration file, read and never written. Throws when it cannot be read or is not a configuration.</summary>
    public static UrDeckConfig Load(string path) => ConfigStore.Parse(File.ReadAllText(path));
}
