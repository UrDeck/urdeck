// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using UrDeck.Engine.Plugin;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The weather provider loaded as a plugin, as the host loads it, and unloaded again.</summary>
public sealed class WeatherPluginTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "urdeck-weather-plugin-" + Guid.NewGuid().ToString("N"));
    private readonly WidgetPluginLoader _loader = new();

    public WeatherPluginTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        _loader.Dispose();
        try
        { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best effort */ }
    }

    private sealed class Sink : IReadingSink
    {
        public void Publish(string path, ReadingValue value)
        {
        }

        public void Unavailable(string path, string reason)
        {
        }
    }

    [Fact]
    public void TheWeatherProvider_LoadsAsAPlugin_WithItsCatalogAndNoNetwork()
    {
        File.Copy(typeof(UrDeck.Providers.Weather.WeatherProvider).Assembly.Location, Path.Combine(_dir, "UrDeck.Providers.Weather.dll"));

        _loader.ScanAndLoadPlugins(_dir);

        var descriptor = _loader.Providers.Get("weather");
        Assert.NotNull(descriptor);
        Assert.Equal(900000, descriptor!.DefaultIntervalMs);
        var catalog = _loader.Readings.GetCatalog("weather");
        Assert.Equal(8 + (7 * 9), catalog!.Count);
        Assert.All(catalog, d => Assert.Contains("{location}", d.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void Reload_AfterTheProviderStartedAndStopped_LetsItsContextBeCollected()
    {
        string path = Path.Combine(_dir, "UrDeck.Providers.Weather.dll");
        File.Copy(typeof(UrDeck.Providers.Weather.WeatherProvider).Assembly.Location, path);
        _loader.ScanAndLoadPlugins(_dir);
        WeakReference contextRef = StartAndStopTheProvider();

        File.Delete(path);
        _loader.ReloadPlugins();

        Assert.Null(_loader.Providers.Get("weather"));
        for (int i = 0; i < 50 && contextRef.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
        }

        Assert.False(contextRef.IsAlive, "The weather plugin context is still referenced after a reload (an HttpClient, task or timer is held).");
    }

    // A separate non-inlined method so no local keeps the provider or its context alive in the caller. With nothing
    // subscribed the provider makes no request, so this needs no network; it does create and dispose its HttpClient.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference StartAndStopTheProvider()
    {
        var descriptor = _loader.Providers.Get("weather")!;
        var provider = (IDataProvider)Activator.CreateInstance(descriptor.ProviderType)!;
        provider.Start(new Sink(), new ProviderSettings());
        provider.SetDemand([]);
        provider.SampleAsync(CancellationToken.None).GetAwaiter().GetResult();
        provider.Shutdown();
        (provider as IDisposable)?.Dispose();
        return new WeakReference(AssemblyLoadContext.GetLoadContext(descriptor.ProviderType.Assembly));
    }

    [Fact]
    public void Reload_AfterA4x4WeatherCardWasShownAndDisposed_LetsItsContextBeCollected()
    {
        string path = Path.Combine(_dir, "UrDeck.Widgets.Weather.dll");
        File.Copy(typeof(UrDeck.Widgets.Weather.WeatherWidget).Assembly.Location, path);
        _loader.ScanAndLoadPlugins(_dir);
        WeakReference contextRef = ShowA4x4CardAndDisposeIt();

        File.Delete(path);
        _loader.ReloadPlugins();

        Assert.Empty(_loader.GetRegisteredWidgetTypes());
        // The loader nudges System.Text.Json's accessor cache ~1.5s after unload; allow for that.
        for (int i = 0; i < 50 && contextRef.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
        }

        Assert.False(contextRef.IsAlive, "The weather widget context is still referenced after a reload (a poster image, animation or setting is held).");
    }

    // A separate non-inlined method so no local keeps the widget or its context alive in the caller. The card paints its
    // forecast posters and, with animateForecast, its forecast animations, then is disposed as a removed card is.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference ShowA4x4CardAndDisposeIt()
    {
        var config = new UrDeck.Sdk.WidgetConfig
        {
            WidgetTypeId = "urdeck.widgets.weather",
            Width = 4,
            Height = 4,
            ExtensionData = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["location"] = System.Text.Json.JsonSerializer.SerializeToElement("Zurich"),
                ["days"] = System.Text.Json.JsonSerializer.SerializeToElement(7),
                ["animateForecast"] = System.Text.Json.JsonSerializer.SerializeToElement(true),
                ["motion"] = System.Text.Json.JsonSerializer.SerializeToElement("full"),
            },
        };
        var widget = _loader.CreateWidget(config)!;
        using var bitmap = UrDeck.Engine.Rendering.PageRenderer.RenderToBitmap(
            1100, 1100, new[] { (config, widget) }, new UrDeck.Engine.Themes.ThemeStore("").Load(UrDeck.Engine.Themes.ThemeStore.DefaultName), DateTime.Now);
        WeakReference context = new(AssemblyLoadContext.GetLoadContext(widget.GetType().Assembly));
        (widget as IDisposable)?.Dispose();
        return context;
    }
}
