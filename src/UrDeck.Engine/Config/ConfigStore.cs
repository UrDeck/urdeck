// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;
using UrDeck.Engine.Layout;
using UrDeck.Sdk;
using UrDeck.Sdk.Data;

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

public class UrDeckConfig
{
    /// <summary>The configuration format version; written first, and always the current one on save. Absent in a file means 1.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = ConfigStore.CurrentVersion;

    [JsonPropertyName("pages")]
    public List<PageConfig> Pages { get; set; } = new List<PageConfig>
    {
        new PageConfig { Name = "Default" }
    };

    [JsonPropertyName("activePage")]
    public int ActivePage { get; set; } = 0;

    [JsonPropertyName("pager")]
    public PagerConfig Pager { get; set; } = new PagerConfig();

    /// <summary>
    /// The widgets of the dock, in slot order: widget objects like a page's, whose <c>col</c> and <c>row</c> are ignored.
    /// Empty means no dock.
    /// </summary>
    [JsonPropertyName("dock")]
    public List<WidgetConfig> Dock { get; set; } = new List<WidgetConfig>();

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "default-dark";

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// What the deck shows behind its cards: a shader or an image. Kept raw; <c>BackgroundSettings.Parse</c> reads it
    /// leniently so a wrong shape costs a warning, never the configuration. Absent from a default configuration.
    /// </summary>
    [JsonPropertyName("background")]
    public JsonElement? Background { get; set; }

    /// <summary>
    /// How much the deck may move: <c>full</c>, <c>reduced</c> or <c>off</c>. Absent (the default) follows Windows' "Animation
    /// effects" setting; <see cref="Motion.MotionPolicy"/> reads it and treats an unknown value as absent.
    /// </summary>
    [JsonPropertyName("motion")]
    public string? Motion { get; set; }

    /// <summary>
    /// Whether an installed copy looks for a newer release: <c>auto</c> (the default, and what an absent key means) or
    /// <c>off</c>. <see cref="UpdatesSetting"/> reads it and treats an unknown value as <c>auto</c>.
    /// </summary>
    [JsonPropertyName("updates")]
    public string? Updates { get; set; }

    /// <summary>1-based monitor index, used when <see cref="MonitorName"/> doesn't match anything.</summary>
    [JsonPropertyName("monitor")]
    public int Monitor { get; set; } = 1;

    /// <summary>
    /// Target monitor: "primary", "tallest", "widest", "largest", or a device name such as "DISPLAY1".
    /// </summary>
    [JsonPropertyName("monitorName")]
    public string? MonitorName { get; set; }

    /// <summary>
    /// The display the deck belongs to. Written by the host once the selectors above have chosen one; absent from a
    /// default configuration.
    /// </summary>
    [JsonPropertyName("display")]
    public RememberedDisplay? Display { get; set; }

    /// <summary>Per-provider settings keyed by provider id; absent from a default configuration.</summary>
    [JsonPropertyName("providers")]
    public Dictionary<string, ProviderSettings>? Providers { get; set; }

    [JsonIgnore]
    public PageConfig? CurrentPage =>
        Pages.Count == 0 ? null : Pages[Math.Clamp(ActivePage, 0, Pages.Count - 1)];
}

public sealed class ConfigStore : IDisposable
{
    /// <summary>The configuration format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    private static readonly IReadOnlyList<(int From, Action<JsonObject> Apply)> NoMigrations = Array.Empty<(int, Action<JsonObject>)>();

    private readonly string _configPath;
    private readonly string? _samplePath;
    private readonly bool _create;
    private readonly int _currentVersion;
    private readonly IReadOnlyList<(int From, Action<JsonObject> Apply)> _migrations;
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private DateTime _ignoreChangesUntil;
    private int? _migratedFrom;
    private byte[]? _originalBytes;
    private bool _newerThanHost;
    private bool _newerNoted;

    /// <summary>Raised on a thread-pool thread after the file changed on disk and was reloaded.</summary>
    public event Action<UrDeckConfig>? ConfigChanged;

    /// <param name="configPath">The configuration file.</param>
    /// <param name="samplePath">The sample page that seeds a missing file; without it (or when it is missing) a configuration with one empty page is used.</param>
    /// <param name="createIfMissing">
    /// True (the deck) writes the missing file. False (<c>--snapshot</c>, <c>--demo</c>) reads the sample in memory and writes nothing.
    /// </param>
    public ConfigStore(string configPath, string? samplePath = null, bool createIfMissing = true)
        : this(configPath, samplePath, createIfMissing, CurrentVersion, NoMigrations)
    {
    }

    /// <summary>Tests give their own current version and migrations; version 1 has none.</summary>
    internal ConfigStore(string configPath, string? samplePath, bool createIfMissing, int currentVersion,
        IReadOnlyList<(int From, Action<JsonObject> Apply)> migrations)
    {
        _configPath = configPath;
        _samplePath = samplePath;
        _create = createIfMissing;
        _currentVersion = currentVersion;
        _migrations = migrations;
        Load();
        WatchForConfigChanges();
    }

    public UrDeckConfig Config { get; private set; } = new UrDeckConfig();

    /// <summary>Whether the file's <c>version</c> is newer than this build's: it is read and never written.</summary>
    public bool IsNewerThanHost => _newerThanHost;

    public void Load()
    {
        _migratedFrom = null;
        _originalBytes = null;
        _newerThanHost = false;

        if (!File.Exists(_configPath))
        {
            LoadMissing();
            return;
        }

        byte[] bytes = ReadShared(_configPath);
        string text = new StreamReader(new MemoryStream(bytes), Encoding.UTF8).ReadToEnd();
        Config = ParseVersioned(text, bytes);
    }

    // First run (data-folder spec): the sample page when there is one, otherwise a configuration with one empty page.
    private void LoadMissing()
    {
        bool hasSample = _samplePath != null && File.Exists(_samplePath);
        if (!_create)
        {
            Config = hasSample ? ParseSample() : new UrDeckConfig();
            return;
        }

        string? dir = Path.GetDirectoryName(Path.GetFullPath(_configPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _ignoreChangesUntil = DateTime.UtcNow.AddSeconds(2);
        if (hasSample)
        {
            File.Copy(_samplePath!, _configPath);
            Config = ParseVersioned(File.ReadAllText(_configPath), null);
            UrDeckLog.Info($"Configuration created at {_configPath} from the sample page");
        }
        else
        {
            Config = new UrDeckConfig();
            Save();
            UrDeckLog.Info($"Configuration created at {_configPath} with an empty page");
        }
    }

    private UrDeckConfig ParseSample()
    {
        try
        {
            return ParseVersioned(File.ReadAllText(_samplePath!), null);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            UrDeckLog.Warn($"Could not read the sample page {_samplePath}: {ex.Message}");
            return new UrDeckConfig();
        }
    }

    // The version is read from the JSON before it is bound, so a migration can reshape the file.
    private UrDeckConfig ParseVersioned(string text, byte[]? original)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        if (JsonNode.Parse(text, documentOptions: options) is not JsonObject root)
            return Parse(text, warn: true);

        int version = ReadVersion(root);
        if (version > _currentVersion)
        {
            _newerThanHost = true;
            UrDeckLog.Warn($"{_configPath} has configuration version {version}, newer than this build's {_currentVersion}: reading it as well as possible, never writing it.");
            return Parse(text, warn: true);
        }

        if (version == _currentVersion)
            return Parse(root.ToJsonString(), warn: true);

        int from = version;
        while (version < _currentVersion)
        {
            var step = _migrations.FirstOrDefault(m => m.From == version);
            step.Apply?.Invoke(root);
            version++;
            root["version"] = version;
            UrDeckLog.Info($"Configuration migrated from version {version - 1} to {version}");
        }

        _migratedFrom = from;
        _originalBytes = original;
        return Parse(root.ToJsonString(), warn: true);
    }

    private int ReadVersion(JsonObject root)
    {
        if (!root.TryGetPropertyValue("version", out var node) || node is null)
            return 1;
        if (node is JsonValue value && value.TryGetValue<int>(out int version) && version > 0)
            return version;
        UrDeckLog.Warn($"{_configPath}: version must be a positive whole number; reading it as version 1.");
        root["version"] = 1; // so that it binds
        return 1;
    }

    /// <summary>
    /// Reads a configuration from its text as <see cref="Load"/> does, without a file, a watcher or a write: absent sections
    /// are filled in and a configuration without pages gets one. With <paramref name="warn"/> its unknown values are logged.
    /// Throws <see cref="JsonException"/> on text that is not a configuration.
    /// </summary>
    public static UrDeckConfig Parse(string json, bool warn = false)
    {
        var config = JsonSerializer.Deserialize<UrDeckConfig>(json, UrDeckJson.Options) ?? new UrDeckConfig();
        config.Pager ??= new PagerConfig();
        config.Dock ??= new List<WidgetConfig>();
        config.Pages ??= new List<PageConfig>();
        if (warn)
        {
            config.Pager.GetIndicatorMode(warn: true);
            Motion.MotionPolicy.Parse(config.Motion, warn: true);
            UpdatesSetting.IsEnabled(config.Updates, warn: true);
            Backgrounds.BackgroundSettings.NotePageBackgrounds(config);
        }

        if (config.Pages.Count == 0)
            config.Pages.Add(new PageConfig());
        return config;
    }

    public void Save()
    {
        if (_newerThanHost)
        {
            if (!_newerNoted)
            {
                _newerNoted = true;
                UrDeckLog.Warn($"{_configPath} is from a newer version of UrDeck and is left as it is; nothing is saved.");
            }

            return;
        }

        _ignoreChangesUntil = DateTime.UtcNow.AddSeconds(2);
        if (_migratedFrom is { } from && _originalBytes is { } original)
        {
            string backup = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_configPath))!, $"urdeck-config.v{from}.json");
            File.WriteAllBytes(backup, original);
            UrDeckLog.Info($"Configuration version {from} kept as {backup}");
            _migratedFrom = null;
            _originalBytes = null;
        }

        Config.Version = _currentVersion;
        File.WriteAllText(_configPath, JsonSerializer.Serialize(Config, UrDeckJson.Options));
    }

    private static byte[] ReadShared(string path)
    {
        // Editors often hold the file briefly while saving; retry a few times.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
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
