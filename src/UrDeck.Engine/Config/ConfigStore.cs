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
    /// <summary>
    /// The page's identity, unique in the file and never changed by a rename or a move (<c>p-</c> and six base-36
    /// characters, see <see cref="PageIds"/>). The host gives a page without one an id on load.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

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

    /// <summary>
    /// The <see cref="PageConfig.Id"/> of the page shown at startup; absent, or naming no page, means the first page
    /// (<see cref="StartPageResolver"/>). The older <c>activePage</c> index is not read.
    /// </summary>
    [JsonPropertyName("startPage")]
    public string? StartPage { get; set; }

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
}


/// <summary>
/// Loads the configuration file (first run, migration) into a <see cref="ConfigDocument"/> and is the typed view of it for the
/// deck. The document is the only writer; this class never writes except to create a missing file.
/// </summary>
public sealed class ConfigStore : IDisposable, Deck.IDeckConfigSource
{
    /// <summary>The configuration format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The deck reloads this long after the last change, so a burst (dragging a slider) rebuilds it a few times, not for each step.</summary>
    public static readonly TimeSpan CoalesceDelay = TimeSpan.FromMilliseconds(150);

    private static readonly IReadOnlyList<(int From, Action<JsonObject> Apply)> NoMigrations = Array.Empty<(int, Action<JsonObject>)>();

    private readonly string _configPath;
    private readonly string? _samplePath;
    private readonly bool _create;
    private readonly int _currentVersion;
    private readonly IReadOnlyList<(int From, Action<JsonObject> Apply)> _migrations;
    private readonly TimeProvider _time;
    private readonly Action<Action> _post;
    private ITimer? _coalesce;
    private bool _loaded;
    private bool _disposed;

    /// <summary>Raised on the owner thread 150 ms after the last change to the document that matters to the deck (not the host's own edits).</summary>
    public event Action<UrDeckConfig>? ConfigChanged;

    /// <param name="configPath">The configuration file.</param>
    /// <param name="samplePath">The sample page that seeds a missing file; without it (or when it is missing) a configuration with one empty page is used.</param>
    /// <param name="createIfMissing">
    /// True (the deck) writes the missing file and keeps the document writable. False (<c>--snapshot</c>, <c>--demo</c>) reads the
    /// sample in memory, watches nothing and writes nothing: the document is read-only.
    /// </param>
    public ConfigStore(string configPath, string? samplePath = null, bool createIfMissing = true)
        : this(configPath, samplePath, createIfMissing, CurrentVersion, NoMigrations)
    {
    }

    /// <summary>Tests give their own current version, migrations, clock and way of reaching the owner thread; version 1 has no migrations.</summary>
    internal ConfigStore(string configPath, string? samplePath, bool createIfMissing, int currentVersion,
        IReadOnlyList<(int From, Action<JsonObject> Apply)> migrations, TimeProvider? time = null, Action<Action>? post = null)
    {
        _configPath = configPath;
        _samplePath = samplePath;
        _create = createIfMissing;
        _currentVersion = currentVersion;
        _migrations = migrations;
        _time = time ?? TimeProvider.System;
        var context = SynchronizationContext.Current;
        Action<Action>? owner = post ?? (context == null ? null : action => context.Post(_ => action(), null));
        _post = owner ?? (static action => action());
        Document = Load(owner);
        _ = Document.Typed; // a file that cannot be bound fails here, as it always did
        // Pages without an id get one now, as a host edit: written once, never an undo step (page-navigation).
        PageIds.Assign(Document);
        _loaded = true;
        Document.Changed += OnDocumentChanged;
    }

    /// <summary>The configuration as a document: edit it by path, undo, flush.</summary>
    public ConfigDocument Document { get; }

    /// <summary>True when this start created the configuration file (the first run: the sample page or an empty page was written); false when a file existed, and in a read-only document.</summary>
    public bool CreatedOnFirstRun { get; private set; }

    /// <summary>The configuration the deck uses; a new object after every change.</summary>
    public UrDeckConfig Config => Document.Typed;

    /// <summary>Whether the file's <c>version</c> is newer than this build's: it is read and never written.</summary>
    public bool IsNewerThanHost => Document.IsNewerThanHost;

    /// <summary>
    /// Remembers <see cref="Config"/>'s <c>display</c> (set or cleared in place by placement or the settings window) as a host
    /// edit: the document then holds it and writes it. Does nothing in a read-only document.
    /// </summary>
    public void CommitDisplay()
    {
        var display = Config.Display;
        JsonNode? node = display == null ? null : JsonSerializer.SerializeToNode(display, UrDeckJson.Options);
        Document.Edit("Remember the display", e => e.Set("/display", node), EditOrigin.Host);
    }

    private ConfigDocument Load(Action<Action>? post)
    {
        var disk = new DiskConfigFileSystem(_configPath, watch: _create);
        byte[]? bytes = disk.Read();
        ParsedConfig parsed;

        if (bytes == null)
        {
            bool hasSample = _samplePath != null && File.Exists(_samplePath);
            if (!_create)
            {
                disk.Dispose();
                var root = hasSample ? ParseSample() : DefaultRoot();
                return new ConfigDocument(root, readOnly: true, time: _time, post: post, currentVersion: _currentVersion);
            }

            // First run (data-folder spec): the sample page when there is one, otherwise a configuration with one empty page.
            string? dir = Path.GetDirectoryName(Path.GetFullPath(_configPath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            CreatedOnFirstRun = true;
            if (hasSample)
            {
                File.Copy(_samplePath!, _configPath);
                bytes = disk.Read()!;
                parsed = Prepare(ReadText(bytes), null);
                UrDeckLog.Info($"Configuration created at {_configPath} from the sample page");
            }
            else
            {
                parsed = new ParsedConfig(DefaultRoot(), false, null, null);
                bytes = Encoding.UTF8.GetBytes(parsed.Root.ToJsonString(UrDeckJson.Options) + "\n");
                disk.Write(bytes);
                UrDeckLog.Info($"Configuration created at {_configPath} with an empty page");
            }
        }
        else
        {
            parsed = Prepare(ReadText(bytes), bytes);
        }

        if (!_create)
        {
            disk.Dispose();
            return new ConfigDocument(parsed.Root, readOnly: true, time: _time, post: post, currentVersion: _currentVersion, newerThanHost: parsed.NewerThanHost);
        }

        var document = new ConfigDocument(parsed.Root, readOnly: false, disk, Prepare, _time, post, _currentVersion, parsed.NewerThanHost);
        document.NoteFileContent(bytes);
        if (parsed.MigratedFrom is { } from && parsed.Original is { } original)
            document.NoteMigration(from, original);
        return document;
    }

    private static string ReadText(byte[] bytes) => new StreamReader(new MemoryStream(bytes), Encoding.UTF8).ReadToEnd();

    private static JsonObject DefaultRoot() => (JsonObject)JsonSerializer.SerializeToNode(new UrDeckConfig(), UrDeckJson.Options)!;

    private JsonObject ParseSample()
    {
        try
        {
            return Prepare(File.ReadAllText(_samplePath!), null).Root;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            UrDeckLog.Warn($"Could not read the sample page {_samplePath}: {ex.Message}");
            return DefaultRoot();
        }
    }

    // The version is read from the JSON before it is bound, so a migration can reshape the file.
    private ParsedConfig Prepare(string text, byte[]? original)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        if (JsonNode.Parse(text, documentOptions: options) is not JsonObject root)
            throw new JsonException("The configuration must be a JSON object.");

        // A file changed outside whose pages lack ids gets them as part of the new content (a host edit replayed on the
        // undo snapshots would put them on the wrong pages); they are written with the next write.
        if (_loaded && _create)
            PageIds.AssignInPlace(root);

        int version = ReadVersion(root);
        if (version > _currentVersion)
        {
            UrDeckLog.Warn($"{_configPath} has configuration version {version}, newer than this build's {_currentVersion}: reading it as well as possible, never writing it.");
            return new ParsedConfig(root, true, null, null);
        }

        if (version == _currentVersion)
            return new ParsedConfig(root, false, null, null);

        int from = version;
        while (version < _currentVersion)
        {
            var step = _migrations.FirstOrDefault(m => m.From == version);
            step.Apply?.Invoke(root);
            version++;
            root["version"] = version;
            UrDeckLog.Info($"Configuration migrated from version {version - 1} to {version}");
        }

        return new ParsedConfig(root, false, from, original);
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
    /// Reads a configuration from its text without a file, a watcher or a write: absent sections
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

    private void OnDocumentChanged(ConfigChange change)
    {
        if (change.Origin == EditOrigin.Host || _disposed)
            return; // the host's own edits (the remembered display, a moved secret) change nothing the deck shows
        _coalesce ??= _time.CreateTimer(_ => _post(RaiseConfigChanged), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _coalesce.Change(CoalesceDelay, Timeout.InfiniteTimeSpan);
    }

    private void RaiseConfigChanged()
    {
        if (!_disposed)
            ConfigChanged?.Invoke(Config);
    }

    public void Dispose()
    {
        _disposed = true;
        _coalesce?.Dispose();
        Document.Changed -= OnDocumentChanged;
        Document.Dispose();
    }
}
