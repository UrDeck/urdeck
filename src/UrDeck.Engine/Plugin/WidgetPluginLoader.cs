// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Icons;
using UrDeck.Engine.Launch;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Plugin;

public sealed class WidgetPluginLoader : IPluginService, IDisposable
{
    private readonly WidgetRegistry _registry = new();
    private readonly ProviderRegistry _providers = new();
    private readonly ReadingHub _hub;
    private readonly IconService _icons;
    private readonly Launcher _launcher;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private string? _pluginDirectory;
    private readonly object _gate = new();
    private readonly List<LoadedPlugin> _loaded = new();
    private readonly List<string> _pendingDeletes = new();
    private static int StaleCleaned;

    /// <param name="time">The clock of the readings, the launcher's repeat guard and the icon cache's age.</param>
    /// <param name="iconCacheFolder">Where fetched site icons are kept; by default <c>cache/icons</c> next to the executable.</param>
    public WidgetPluginLoader(TimeProvider? time = null, string? iconCacheFolder = null)
    {
        _hub = new ReadingHub(_providers, time);
        _icons = new IconService(iconCacheFolder ?? Path.Combine(AppContext.BaseDirectory, "cache", "icons"), time);
        _launcher = new Launcher(time);
        _registry.CreateServices = () => new WidgetServices(_hub, _launcher, _icons);
    }

    public event Action? PluginsChanged;

    public WidgetRegistry Registry => _registry;

    public ProviderRegistry Providers => _providers;

    /// <summary>The readings of every provider; widgets created here are attached to it.</summary>
    public ReadingHub Readings => _hub;

    /// <summary>The icons widgets ask for; every widget created here reaches it through its own services.</summary>
    public IconService Icons => _icons;

    /// <summary>The launch service every widget created here shares.</summary>
    public Launcher Launcher => _launcher;

    public void ScanAndLoadPlugins(string pluginDirectory)
    {
        _pluginDirectory = Path.GetFullPath(pluginDirectory);
        Directory.CreateDirectory(_pluginDirectory);
        if (Interlocked.Exchange(ref StaleCleaned, 1) == 0)
            CleanStaleShadowDirs();
        lock (_gate)
            LoadAll();
        WatchForChanges();
    }

    /// <summary>Unloads every plugin context, clears the registry and loads the plugins folder afresh.</summary>
    public void ReloadPlugins()
    {
        lock (_gate)
        {
            // Providers go first: they hold plugin types, and the page rebuild that follows resubscribes against the new ones.
            _hub.ReleaseAll();
            _providers.Clear();
            _registry.Clear();
            UnloadAll();
            LoadAll();
        }
    }

    private void LoadAll()
    {
        if (_pluginDirectory == null)
            return;

        // In file name order, so that which of two plugins claims a provider id does not depend on the file system.
        foreach (string dllPath in Directory.EnumerateFiles(_pluginDirectory, "*.dll").Order(StringComparer.OrdinalIgnoreCase))
        {
            string? shadowDir = null;
            PluginLoadContext? context = null;
            try
            {
                // Load from a copy so the original stays unlocked and can be overwritten by a rebuild.
                shadowDir = Path.Combine(ShadowRoot, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(shadowDir);
                string name = Path.GetFileNameWithoutExtension(dllPath);
                string shadowDll = Path.Combine(shadowDir, name + ".dll");
                File.Copy(dllPath, shadowDll);
                foreach (string? ext in new[] { ".pdb", ".deps.json" })
                {
                    string side = Path.Combine(_pluginDirectory, name + ext);
                    if (File.Exists(side))
                        File.Copy(side, Path.Combine(shadowDir, name + ext));
                }

                context = new PluginLoadContext(shadowDll, _pluginDirectory);
                _loaded.Add(new LoadedPlugin(context, shadowDir));
                LoadAssembly(context.LoadFromAssemblyPath(shadowDll));
            }
            catch (Exception ex)
            {
                UrDeckLog.Warn($"Failed to load plugin {Path.GetFileName(dllPath)}: {ex.Message}");
                // A context created for a failed plugin (e.g. not a .NET assembly) stays tracked and is unloaded on the next reload.
                if (context == null && shadowDir != null)
                    _pendingDeletes.Add(shadowDir);
            }
        }
    }

    private void UnloadAll()
    {
        var weakRefs = new List<WeakReference>();
        foreach (var plugin in _loaded)
        {
            weakRefs.Add(new WeakReference(plugin.Context));
            plugin.Context.Unload();
            _pendingDeletes.Add(plugin.ShadowDirectory);
        }
        _loaded.Clear();

        if (weakRefs.Count == 0)
            DeletePending();
        else
            Task.Run(() => AwaitCollection(weakRefs)); // diagnostic only; never blocks the reload
    }

    private void AwaitCollection(List<WeakReference> refs)
    {
        // The host disposes widget views asynchronously after PluginsChanged, and System.Text.Json's
        // emitted-accessor cache holds plugin config members for ~1s and only evicts on its next use,
        // so allow a few seconds and nudge that cache once before declaring a leak.
        bool nudged = false;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (refs.Any(r => r.IsAlive) && stopwatch.Elapsed < TimeSpan.FromSeconds(4))
        {
            if (!nudged && stopwatch.Elapsed > TimeSpan.FromSeconds(1.5))
            {
                NudgeJsonAccessorCache();
                nudged = true;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
        }

        if (!refs.Any(r => r.IsAlive))
            UrDeckLog.Info($"Unloaded plugin context(s) collected after {stopwatch.ElapsedMilliseconds} ms");

        int alive = refs.Count(r => r.IsAlive);
        if (alive > 0)
            UrDeckLog.Warn($"{alive} plugin context(s) were not collected after unload; something still references them.");
        lock (_gate)
            DeletePending();
    }

    private void DeletePending() => _pendingDeletes.RemoveAll(TryDeleteDirectory);

    private sealed class CacheNudge
    {
        public int Value { get; set; }
    }

    /// <summary>Touches System.Text.Json's member-accessor cache so it evicts expired (unloaded plugin) entries.</summary>
    private static void NudgeJsonAccessorCache()
    {
        // A fresh options instance per call is the point: it forces STJ to purge stale (unloaded plugin) accessor entries.
#pragma warning disable CA1869
        var options = new System.Text.Json.JsonSerializerOptions
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };
        System.Text.Json.JsonSerializer.Serialize(new CacheNudge(), options);
#pragma warning restore CA1869
    }

    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ShadowRoot => Path.Combine(Path.GetTempPath(), "urdeck-shadow");

    /// <summary>Removes shadow folders left behind by processes that are no longer running.</summary>
    private static void CleanStaleShadowDirs()
    {
        try
        {
            if (!Directory.Exists(ShadowRoot))
                return;
            foreach (string dir in Directory.EnumerateDirectories(ShadowRoot))
            {
                if (!int.TryParse(Path.GetFileName(dir), out int pid) || pid == Environment.ProcessId)
                    continue;
                if (!IsRunning(pid))
                    TryDeleteDirectory(dir);
            }
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Shadow cleanup failed: {ex.Message}");
        }
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch
        {
            return true; // can't tell; leave it alone
        }
    }

    private sealed record LoadedPlugin(PluginLoadContext Context, string ShadowDirectory);

    /// <summary>Registers every valid widget and provider type in <paramref name="assembly"/>. Public so tests and hosts can register built-ins directly.</summary>
    public void LoadAssembly(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            UrDeckLog.Warn($"Some types in {assembly.GetName().Name} failed to load: {ex.LoaderExceptions.FirstOrDefault()?.Message}");
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        int found = 0;
        bool foreignSdk = false;
        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            bool isWidget = typeof(IWidget).IsAssignableFrom(type);
            bool isProvider = typeof(IDataProvider).IsAssignableFrom(type);
            if (!isWidget && !isProvider)
            {
                // Same interface name but a different assembly copy: the plugin carries or was built against another SDK.
                foreignSdk |= type.GetInterfaces().Any(i => i.FullName == typeof(IWidget).FullName || i.FullName == typeof(IDataProvider).FullName);
                continue;
            }

            if (isWidget && RegisterWidget(type))
                found++;
            if (isProvider && RegisterProvider(type))
                found++;
        }

        if (foreignSdk)
            UrDeckLog.Warn($"{assembly.GetName().Name} has widgets or providers built against a different UrDeck.Sdk than the host's; rebuild it against this version and don't ship UrDeck.Sdk.dll with it.");
        else if (found == 0)
            UrDeckLog.Warn($"No widgets or providers found in {assembly.GetName().Name} (not a plugin, or built against an incompatible UrDeck.Sdk?).");
    }

    private bool RegisterWidget(Type type)
    {
        var descriptor = WidgetDescriptor.TryCreate(type, out string? reason);
        if (descriptor == null)
        {
            UrDeckLog.Warn($"Skipping widget {type.FullName}: {reason}");
            return false;
        }

        _registry.Register(descriptor);
        UrDeckLog.Info($"Registered widget '{descriptor.Id}' ({type.FullName}, {descriptor.Refresh} {descriptor.RefreshInterval})");
        return true;
    }

    private bool RegisterProvider(Type type)
    {
        var descriptor = ProviderDescriptor.TryCreate(type, out string? reason);
        if (descriptor == null)
        {
            UrDeckLog.Warn($"Skipping provider {type.FullName}: {reason}");
            return false;
        }

        if (!_providers.TryRegister(descriptor, out var existing))
        {
            UrDeckLog.Warn($"Provider '{descriptor.Id}' from {descriptor.AssemblyName} rejected: {existing!.AssemblyName} already provides it");
            return false;
        }

        UrDeckLog.Info($"Registered provider '{descriptor.Id}' ({type.FullName}, default {descriptor.DefaultIntervalMs} ms, minimum {descriptor.MinIntervalMs} ms)");
        return true;
    }

    private void WatchForChanges()
    {
        _debounce = new Timer(_ =>
        {
            try
            {
                ReloadPlugins();
                PluginsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                UrDeckLog.Error("Plugin reload failed", ex);
            }
        });

        _watcher = new FileSystemWatcher(_pluginDirectory!, "*.dll")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };
        FileSystemEventHandler onChange = (_, _) => _debounce.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
        _watcher.Changed += onChange;
        _watcher.Created += onChange;
        _watcher.Deleted += onChange;
        _watcher.Renamed += (_, _) => _debounce.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
        _watcher.EnableRaisingEvents = true;
    }

    public IReadOnlyList<string> GetRegisteredWidgetTypes() => _registry.GetRegisteredWidgetTypes();

    public WidgetDescriptor? GetDescriptor(string typeId) => _registry.GetDescriptor(typeId);

    public IWidget? CreateWidget(WidgetConfig config) => _registry.CreateWidget(config);

    public IWidget? CreateWidget(WidgetConfig config, out WidgetServices? services) => _registry.CreateWidget(config, out services);

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
        lock (_gate)
        {
            _hub.Dispose();
            _icons.Dispose();
            _providers.Clear();
            _registry.Clear();
            UnloadAll();
        }
    }
}
