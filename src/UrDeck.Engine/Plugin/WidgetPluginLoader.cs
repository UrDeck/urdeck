// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using UrDeck.Engine.Data;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Icons;
using UrDeck.Engine.Launch;
using UrDeck.Engine.Secrets;
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
    private Timer? _debounce;
    private readonly List<string> _pluginDirectories = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private string? _loadingFile;
    private readonly object _gate = new();
    private readonly List<LoadedPlugin> _loaded = new();
    private readonly List<string> _pendingDeletes = new();
    private readonly SampleClock? _demo;
    private readonly List<string> _sampled = [];
    private static int StaleCleaned;

    /// <param name="time">The clock of the readings, the launcher's repeat guard and the icon cache's age.</param>
    /// <param name="iconCacheFolder">Where fetched site icons are kept; by default the system's temp folder, so that nothing is written beside the executable.</param>
    /// <param name="demo">
    /// Demo mode (host-shell, "Demo Mode"): every provider whose assembly carries a sample is registered as its sample, read by
    /// this clock, and no site icon is fetched. Null runs the providers.
    /// </param>
    /// <param name="secrets">The secret store of the providers; by default the system's (Credential Manager on Windows, the environment elsewhere).</param>
    public WidgetPluginLoader(TimeProvider? time = null, string? iconCacheFolder = null, SampleClock? demo = null, ISecretStore? secrets = null)
    {
        _demo = demo;
        Secrets = secrets ?? SecretStores.CreateDefault();
        _hub = new ReadingHub(_providers, time, secrets: Secrets);
        _icons = new IconService(iconCacheFolder ?? Path.Combine(Path.GetTempPath(), "urdeck-icons"), time) { SiteIcons = demo == null };
        _launcher = new Launcher(time);
        _registry.CreateServices = () => new WidgetServices(_hub, _launcher, _icons, _hub);
    }

    /// <summary>In demo mode, the ids of the providers that run as their sample; empty otherwise.</summary>
    public IReadOnlyList<string> SampledProviders
    {
        get
        {
            lock (_gate)
                return [.. _sampled.Order(StringComparer.Ordinal)];
        }
    }

    public event Action? PluginsChanged;

    public WidgetRegistry Registry => _registry;

    public ProviderRegistry Providers => _providers;

    /// <summary>Where the providers' secret settings are looked up and kept.</summary>
    public ISecretStore Secrets { get; }

    /// <summary>The readings of every provider; widgets created here are attached to it.</summary>
    public ReadingHub Readings => _hub;

    /// <summary>The icons widgets ask for; every widget created here reaches it through its own services.</summary>
    public IconService Icons => _icons;

    /// <summary>The launch service every widget created here shares.</summary>
    public Launcher Launcher => _launcher;

    public void ScanAndLoadPlugins(string pluginDirectory) => ScanAndLoadPlugins(new[] { pluginDirectory });

    /// <summary>
    /// Loads the plugins of several folders in priority order: when two folders register the same widget type or provider
    /// id, the earlier folder's wins and a warning names the other file. A folder given twice is scanned once. Only the last
    /// folder is created when missing (the user's; the application's exists or has no plugins), and each folder is watched.
    /// </summary>
    public void ScanAndLoadPlugins(IReadOnlyList<string> pluginDirectories)
    {
        _pluginDirectories.Clear();
        foreach (string directory in pluginDirectories)
        {
            string full = Path.GetFullPath(directory);
            if (!_pluginDirectories.Contains(full, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
                _pluginDirectories.Add(full);
        }

        if (_pluginDirectories.Count > 0)
            Directory.CreateDirectory(_pluginDirectories[^1]);
        if (Interlocked.Exchange(ref StaleCleaned, 1) == 0)
            CleanStaleShadowDirs();
        lock (_gate)
            LoadAll();
        if (_demo != null)
        {
            var sampled = SampledProviders;
            UrDeckLog.Info(sampled.Count == 0
                ? "Demo mode: no provider carries sample data; every provider runs as usual"
                : $"Demo mode: sample data for {string.Join(", ", sampled)}");
        }

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
            _sampled.Clear();
            _registry.Clear();
            UnloadAll();
            LoadAll();
        }
    }

    private void LoadAll()
    {
        // Folders in priority order, and in each one by file name, so that which of two plugins claims an id does not depend on the file system.
        foreach (string folder in _pluginDirectories)
        {
            if (Directory.Exists(folder))
                LoadFolder(folder);
        }
    }

    private void LoadFolder(string pluginDirectory)
    {
        foreach (string dllPath in Directory.EnumerateFiles(pluginDirectory, "*.dll").Order(StringComparer.OrdinalIgnoreCase))
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
                    string side = Path.Combine(pluginDirectory, name + ext);
                    if (File.Exists(side))
                        File.Copy(side, Path.Combine(shadowDir, name + ext));
                }

                context = new PluginLoadContext(shadowDll, pluginDirectory);
                _loaded.Add(new LoadedPlugin(context, shadowDir));
                _loadingFile = dllPath;
                LoadAssembly(context.LoadFromAssemblyPath(shadowDll));
            }
            catch (Exception ex)
            {
                UrDeckLog.Warn($"Failed to load plugin {Path.GetFileName(dllPath)}: {ex.Message}");
                // A context created for a failed plugin (e.g. not a .NET assembly) stays tracked and is unloaded on the next reload.
                if (context == null && shadowDir != null)
                    _pendingDeletes.Add(shadowDir);
            }
            finally
            {
                _loadingFile = null;
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

        if (!_registry.TryRegister(descriptor, out var existing))
        {
            UrDeckLog.Warn($"Widget '{descriptor.Id}' from {_loadingFile ?? type.Assembly.GetName().Name} rejected: {existing!.WidgetType.Assembly.GetName().Name} already provides it");
            return false;
        }

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

        // In demo mode the sample takes the provider's place under its own id; the provider itself is never created.
        var sample = _demo != null ? ProviderSamples.Find(descriptor) : null;
        var registered = sample != null ? ProviderSamples.Replace(descriptor, sample, _demo!.Now, _demo.Start) : descriptor;
        if (!_providers.TryRegister(registered, out var existing))
        {
            UrDeckLog.Warn($"Provider '{descriptor.Id}' from {_loadingFile ?? descriptor.AssemblyName} rejected: {existing!.AssemblyName} already provides it");
            return false;
        }

        if (sample != null)
        {
            lock (_gate)
                _sampled.Add(descriptor.Id);
        }

        UrDeckLog.Info($"Registered provider '{descriptor.Id}' ({type.FullName}, default {descriptor.DefaultIntervalMs} ms, minimum {descriptor.MinIntervalMs} ms{(sample != null ? ", as its sample" : "")})");
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

        foreach (string folder in _pluginDirectories.Where(Directory.Exists))
        {
            var watcher = new FileSystemWatcher(folder, "*.dll")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            };
            FileSystemEventHandler onChange = (_, _) => _debounce.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
            watcher.Changed += onChange;
            watcher.Created += onChange;
            watcher.Deleted += onChange;
            watcher.Renamed += (_, _) => _debounce.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    public IReadOnlyList<string> GetRegisteredWidgetTypes() => _registry.GetRegisteredWidgetTypes();

    public WidgetDescriptor? GetDescriptor(string typeId) => _registry.GetDescriptor(typeId);

    public IWidget? CreateWidget(WidgetConfig config) => _registry.CreateWidget(config);

    public IWidget? CreateWidget(WidgetConfig config, out WidgetServices? services) => _registry.CreateWidget(config, out services);

    public void Dispose()
    {
        foreach (var watcher in _watchers)
            watcher.Dispose();
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
