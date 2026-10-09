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
        Assert.Equal(9, catalog!.Count);
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
        provider.Start(new Sink());
        provider.SetDemand([]);
        provider.SampleAsync(CancellationToken.None).GetAwaiter().GetResult();
        provider.Shutdown();
        (provider as IDisposable)?.Dispose();
        return new WeakReference(AssemblyLoadContext.GetLoadContext(descriptor.ProviderType.Assembly));
    }
}
