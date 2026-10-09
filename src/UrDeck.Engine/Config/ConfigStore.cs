// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json;
using System.Text.Json.Serialization;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Sdk;

namespace UrDeck.Engine.Config;

public class PageConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Default";

    [JsonPropertyName("widgets")]
    public List<WidgetConfig> Widgets { get; set; } = new List<WidgetConfig>();

    [JsonPropertyName("background")]
    public JsonElement? Background { get; set; }
}

public class DockItemConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>Settings for paging between pages. An object so that later pager settings have a home.</summary>
public class PagerConfig
{
    /// <summary><c>always</c>, <c>fade</c>, <c>off</c> or <c>auto</c>; absent means <c>auto</c>.</summary>
    [JsonPropertyName("indicator")]
    public string? Indicator { get; set; }

    /// <summary>The indicator mode; an unknown value is <c>auto</c>, with a warning when <paramref name="warn"/> is set.</summary>
    public IndicatorMode GetIndicatorMode(bool warn = false)
    {
        switch (Indicator?.Trim().ToLowerInvariant())
        {
            case null or "" or "auto":
                return IndicatorMode.Auto;
            case "always":
                return IndicatorMode.Always;
            case "fade":
                return IndicatorMode.Fade;
            case "off":
                return IndicatorMode.Off;
            default:
                if (warn)
                    UrDeckLog.Warn($"pager.indicator '{Indicator}' must be 'always', 'fade', 'off' or 'auto'; using 'auto'.");
                return IndicatorMode.Auto;
        }
    }
}

/// <summary>The user's settings for one data provider. Properties the engine does not know are kept for the provider.</summary>
public class ProviderSettings
{
    /// <summary>Sampling interval in milliseconds; raised to the provider's minimum.</summary>
    [JsonPropertyName("intervalMs")]
    public int? IntervalMs { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class UrDeckConfig
{
    [JsonPropertyName("pages")]
    public List<PageConfig> Pages { get; set; } = new List<PageConfig>
    {
        new PageConfig { Name = "Default" }
    };

    [JsonPropertyName("activePage")]
    public int ActivePage { get; set; } = 0;

    [JsonPropertyName("pager")]
    public PagerConfig Pager { get; set; } = new PagerConfig();

    [JsonPropertyName("dock")]
    public List<DockItemConfig> Dock { get; set; } = new List<DockItemConfig>();

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "default-dark";

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>1-based monitor index, used when <see cref="MonitorName"/> doesn't match anything.</summary>
    [JsonPropertyName("monitor")]
    public int Monitor { get; set; } = 1;

    /// <summary>
    /// Target monitor: "primary", "tallest", "widest", "largest", or a device name such as "DISPLAY1".
    /// </summary>
    [JsonPropertyName("monitorName")]
    public string? MonitorName { get; set; }

    /// <summary>Per-provider settings keyed by provider id; absent from a default configuration.</summary>
    [JsonPropertyName("providers")]
    public Dictionary<string, ProviderSettings>? Providers { get; set; }

    [JsonIgnore]
    public PageConfig? CurrentPage =>
        Pages.Count == 0 ? null : Pages[Math.Clamp(ActivePage, 0, Pages.Count - 1)];
}

public sealed class ConfigStore : IDisposable
{
    private readonly string _configPath;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private DateTime _ignoreChangesUntil;

    /// <summary>Raised on a thread-pool thread after the file changed on disk and was reloaded.</summary>
    public event Action<UrDeckConfig>? ConfigChanged;

    public ConfigStore(string configPath)
    {
        _configPath = configPath;
        Load();
        WatchForConfigChanges();
    }

    public UrDeckConfig Config { get; private set; } = new UrDeckConfig();

    public void Load()
    {
        if (!File.Exists(_configPath))
        {
            Config = new UrDeckConfig();
            Save();
            return;
        }

        string json = ReadShared(_configPath);
        Config = JsonSerializer.Deserialize<UrDeckConfig>(json, UrDeckJson.Options) ?? new UrDeckConfig();
        Config.Pager ??= new PagerConfig();
        Config.Pager.GetIndicatorMode(warn: true);
        if (Config.Pages.Count == 0)
            Config.Pages.Add(new PageConfig());
    }

    public void Save()
    {
        _ignoreChangesUntil = DateTime.UtcNow.AddSeconds(2);
        File.WriteAllText(_configPath, JsonSerializer.Serialize(Config, UrDeckJson.Options));
    }

    private static string ReadShared(string path)
    {
        // Editors often hold the file briefly while saving; retry a few times.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(100);
            }
        }
    }

    private void WatchForConfigChanges()
    {
        string? dir = Path.GetDirectoryName(Path.GetFullPath(_configPath));
        if (string.IsNullOrEmpty(dir))
            return;

        _debounce = new Timer(_ => ReloadFromDisk());
        _watcher = new FileSystemWatcher(dir, Path.GetFileName(_configPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (DateTime.UtcNow < _ignoreChangesUntil)
            return; // our own Save()
        _debounce?.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
    }

    private void ReloadFromDisk()
    {
        try
        {
            Load();
            UrDeckLog.Info($"Config reloaded from {_configPath}");
            ConfigChanged?.Invoke(Config);
        }
        catch (Exception ex)
        {
            // Keep running on the last good config (e.g. a half-typed JSON edit).
            UrDeckLog.Warn($"Config reload failed, keeping previous config: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}
