// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Plugin;
using UrDeck.Sdk.Icons;
using UrDeck.Sdk.Launch;

namespace UrDeck.Engine.Icons;

/// <summary>Where icons come from. Every member may block and is called off the UI thread.</summary>
internal interface IIconLoaders : IDisposable
{
    /// <summary>The picture in a local image file; null when it is missing or does not decode.</summary>
    SKBitmap? LoadFile(string path);

    /// <summary>The icon Windows shows for a path, a <c>shell:</c> item or an address; null when it has none.</summary>
    SKBitmap? LoadShell(string parsingName);

    /// <summary>The site's own icon for a web address, from the disk cache or the network; null when it yields none.</summary>
    SKBitmap? LoadSite(Uri address, int wantedSize);
}

/// <summary>
/// The icon cache behind every widget's <see cref="IIconSource"/>. A request never blocks: it records who asked and
/// answers "loading", the icon is loaded on a worker, and the widgets that asked are repainted once when it is known.
/// A decoded icon lives only while a widget that asked for it does. Nothing runs, and no thread or connection exists,
/// until the first request.
/// </summary>
public sealed class IconService : IDisposable
{
    /// <summary>Icons are kept at one of these sizes: the smallest at or above the size asked for.</summary>
    private static readonly int[] Buckets = [64, 128, 256, 512];

    private const int MaxParsedSources = 256;

    private enum EntryState
    {
        Loading,
        Ready,
        None,
    }

    private sealed class Entry((string Source, int Bucket) key)
    {
        public (string Source, int Bucket) Key { get; } = key;
        public EntryState State { get; set; }
        public SKImage? Image { get; set; }
        public HashSet<WidgetServices> Holders { get; } = [];
    }

    private readonly IIconLoaders _loaders;
    private readonly object _gate = new();
    private readonly Dictionary<(string Source, int Bucket), Entry> _entries = [];
    private readonly Dictionary<string, LaunchTarget> _parsed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reported = new(StringComparer.OrdinalIgnoreCase);
    private int _pending;
    private bool _disposed;

    /// <param name="cacheFolder">Where fetched site icons are kept; created on the first fetch.</param>
    /// <param name="time">The clock the cache's age is measured with.</param>
    public IconService(string cacheFolder, TimeProvider? time = null)
        : this(new IconLoaders(cacheFolder, time ?? TimeProvider.System))
    {
    }

    internal IconService(IIconLoaders loaders) => _loaders = loaders;

    /// <summary>True while an icon somebody asked for is still being loaded.</summary>
    public bool AnyPending => Volatile.Read(ref _pending) > 0;

    /// <summary>The number of icons held in memory or being loaded.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
                return _entries.Count;
        }
    }

    internal IconResult Get(WidgetServices holder, string? source, int pixelSize)
    {
        if (string.IsNullOrWhiteSpace(source))
            return IconResult.None;

        Entry entry;
        LaunchTarget target;
        lock (_gate)
        {
            if (_disposed)
                return IconResult.None;

            if (!_parsed.TryGetValue(source, out target))
            {
                if (_parsed.Count >= MaxParsedSources)
                    _parsed.Clear();
                _parsed[source] = target = LaunchTarget.Parse(source);
            }

            if (!target.IsValid)
                return IconResult.None;

            var key = (KeyOf(target), BucketFor(pixelSize));
            if (_entries.TryGetValue(key, out var known))
            {
                known.Holders.Add(holder);
                return known.State switch
                {
                    EntryState.Ready => new IconResult(known.Image, false),
                    EntryState.Loading => IconResult.Loading,
                    _ => IconResult.None,
                };
            }

            entry = new Entry(key);
            entry.Holders.Add(holder);
            _entries[key] = entry;
            Interlocked.Increment(ref _pending);
        }

        Task.Run(() => Load(entry, target));
        return IconResult.Loading;
    }

    /// <summary>Forgets that <paramref name="holder"/> asked for anything; icons nobody else uses are released.</summary>
    internal void Release(WidgetServices holder)
    {
        lock (_gate)
        {
            List<Entry>? unused = null;
            foreach (var entry in _entries.Values)
            {
                if (entry.Holders.Remove(holder) && entry.Holders.Count == 0)
                    (unused ??= []).Add(entry);
            }

            if (unused == null)
                return;
            foreach (var entry in unused)
            {
                // An entry that is still loading is dropped too; its image is disposed when the load finds it gone.
                _entries.Remove(entry.Key);
                entry.Image?.Dispose();
                entry.Image = null;
            }
        }
    }

    private void Load(Entry entry, LaunchTarget target)
    {
        SKImage? image = null;
        try
        {
            image = LoadImage(target, entry.Key.Bucket);
        }
        catch (Exception ex)
        {
            Report(target, $"Icon for '{Describe(target)}' could not be loaded: {ex.Message}");
        }

        WidgetServices[] holders = [];
        lock (_gate)
        {
            bool wanted = !_disposed && _entries.TryGetValue(entry.Key, out var current) && ReferenceEquals(current, entry);
            if (wanted)
            {
                entry.Image = image;
                entry.State = image != null ? EntryState.Ready : EntryState.None;
                holders = [.. entry.Holders];
            }
            else
            {
                image?.Dispose();
            }
        }

        Interlocked.Decrement(ref _pending);
        foreach (var holder in holders)
            holder.RequestRepaint();
    }

    private SKImage? LoadImage(LaunchTarget target, int bucket)
    {
        SKBitmap? bitmap;
        switch (target.Kind)
        {
            case LaunchTargetKind.File when ImageFiles.IsImage(target.Text):
                bitmap = _loaders.LoadFile(target.Text);
                if (bitmap == null)
                    Report(target, $"No icon: image file '{target.Text}' is missing or is not a readable image.");
                break;
            case LaunchTargetKind.Web:
                // The site's own icon, else what Windows shows for the address: the default browser's icon.
                bitmap = _loaders.LoadSite(new Uri(target.Text), bucket) ?? _loaders.LoadShell(target.Text);
                break;
            default:
                bitmap = _loaders.LoadShell(target.Text);
                if (bitmap == null)
                    Report(target, $"No icon: Windows has none for '{target.Text}' (does it exist?).");
                break;
        }

        if (bitmap == null)
            return null;

        using var fitted = IconImages.Fit(bitmap, bucket);
        return SKImage.FromBitmap(fitted);
    }

    /// <summary>Logs a failure once per source and run, however often a page with it is shown.</summary>
    private void Report(LaunchTarget target, string message)
    {
        lock (_gate)
        {
            if (!_reported.Add(target.Text))
                return;
        }

        UrDeckLog.Warn(message);
    }

    /// <summary>A web address may carry a token in its query, so the log names the site only.</summary>
    private static string Describe(LaunchTarget target) =>
        target.Kind == LaunchTargetKind.Web ? target.DisplayName : target.Text;

    private static string KeyOf(LaunchTarget target) => target.Kind switch
    {
        // Paths and shell items do not differ by case; addresses may.
        LaunchTargetKind.File or LaunchTargetKind.Shell => target.Text.ToUpperInvariant(),
        _ => target.Text,
    };

    internal static int BucketFor(int pixelSize)
    {
        foreach (int bucket in Buckets)
        {
            if (bucket >= pixelSize)
                return bucket;
        }

        return Buckets[^1];
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var entry in _entries.Values)
                entry.Image?.Dispose();
            _entries.Clear();
        }

        _loaders.Dispose();
    }
}

/// <summary>The real sources: image files, the Windows shell and the web. Each is created when it is first needed.</summary>
internal sealed class IconLoaders : IIconLoaders
{
    private readonly object _gate = new();
    private ShellIconLoader? _shell;
    private readonly Lazy<IconHttp> _http = new(() => new IconHttp());
    private readonly Lazy<SiteIconCache> _sites;

    public IconLoaders(string cacheFolder, TimeProvider time)
    {
        _sites = new Lazy<SiteIconCache>(() =>
        {
            // The client itself is only created by the first request: a cached icon needs none.
            var finder = new SiteIconFinder(new DeferredIconHttp(() => _http.Value));
            return new SiteIconCache(cacheFolder, finder.FindAsync, time);
        });
    }

    public SKBitmap? LoadFile(string path) => ImageFiles.Load(path);

    public SKBitmap? LoadShell(string parsingName)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        ShellIconLoader shell;
        lock (_gate)
            shell = _shell ??= new ShellIconLoader();
        return shell.Load(parsingName);
    }

    public SKBitmap? LoadSite(Uri address, int wantedSize) => _sites.Value.Load(address, wantedSize);

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            lock (_gate)
                _shell?.Dispose();
        }

        if (_http.IsValueCreated)
            _http.Value.Dispose();
    }
}
