// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;
using UrDeck.Engine.Diagnostics;

namespace UrDeck.Engine.Icons;

/// <summary>
/// Site icons on disk, so the network is asked rarely: one PNG per web address, kept for <see cref="MaxAge"/>. An older
/// file is still used while it is refreshed in the background, and whenever the refresh fails. An address that yielded
/// no icon is not asked again in this run. If the folder cannot be written, icons are kept in memory for the run.
/// </summary>
/// <param name="folder">The cache folder; created by the first write. Deleting it is always safe.</param>
/// <param name="find">Asks the site (see <see cref="SiteIconFinder.FindAsync"/>).</param>
/// <param name="time">The clock a file's age is measured with.</param>
internal sealed class SiteIconCache(
    string folder,
    Func<Uri, int, CancellationToken, Task<SiteIconResult>> find,
    TimeProvider time)
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    /// <summary>The longer side of a cached icon is at most this many pixels.</summary>
    public const int MaxSide = 512;

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, object> _keyGates = new();
    private readonly HashSet<string> _negative = [];
    private readonly HashSet<string> _refreshed = [];
    private readonly HashSet<string> _announced = [];
    private readonly Dictionary<string, byte[]> _memory = [];
    private bool _folderFailed;

    /// <summary>The background refresh started last, for tests to wait on.</summary>
    internal Task Refreshing { get; private set; } = Task.CompletedTask;

    /// <summary>The file name of an address: a hash, so that nothing in an address can name a path.</summary>
    internal static string KeyOf(Uri address) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(address.AbsoluteUri)));

    internal string PathOf(Uri address) => Path.Combine(folder, KeyOf(address) + ".png");

    /// <summary>The site's icon, or null. Blocks while the site is asked; never call it on the UI thread.</summary>
    public SKBitmap? Load(Uri address, int wantedSize)
    {
        string key = KeyOf(address);
        // Two sizes of one address asked for at once must not both go to the network.
        lock (_keyGates.GetOrAdd(key, _ => new object()))
        {
            lock (_gate)
            {
                if (_negative.Contains(key))
                    return null;
                if (_memory.TryGetValue(key, out byte[]? kept))
                    return ImageFiles.Decode(kept);
            }

            string path = Path.Combine(folder, key + ".png");
            if (ReadCached(path, out DateTime written) is { } cached)
            {
                bool stale = time.GetUtcNow().UtcDateTime - written > MaxAge;
                Announce(key, $"Site icon for {address.Host} from the cache, no request{(stale ? "; older than 30 days, refreshing in the background" : "")}");
                if (stale)
                    Refresh(address, key, path, wantedSize);
                return cached;
            }

            var result = find(address, wantedSize, CancellationToken.None).GetAwaiter().GetResult();
            if (result.Bitmap == null)
            {
                lock (_gate)
                    _negative.Add(key);
                UrDeckLog.Warn($"No site icon for {address.Host}: {result.Detail}. Not asked again until the next start.");
                return null;
            }

            var icon = IconImages.Fit(result.Bitmap, MaxSide);
            Store(key, path, icon);
            UrDeckLog.Info($"Site icon for {address.Host} fetched from {result.Detail}");
            return icon;
        }
    }

    private void Refresh(Uri address, string key, string path, int wantedSize)
    {
        lock (_gate)
        {
            // Once per run: a refresh that failed is not repeated on every page change.
            if (!_refreshed.Add(key))
                return;
        }

        Refreshing = Task.Run(async () =>
        {
            try
            {
                var result = await find(address, wantedSize, CancellationToken.None).ConfigureAwait(false);
                if (result.Bitmap == null)
                {
                    UrDeckLog.Warn($"Site icon for {address.Host} could not be refreshed ({result.Detail}); keeping the cached one.");
                    return;
                }

                using var icon = IconImages.Fit(result.Bitmap, MaxSide);
                Store(key, path, icon);
                UrDeckLog.Info($"Site icon for {address.Host} refreshed from {result.Detail}");
            }
            catch (Exception ex)
            {
                UrDeckLog.Warn($"Site icon for {address.Host} could not be refreshed ({ex.Message}); keeping the cached one.");
            }
        });
    }

    /// <summary>The cached picture and when it was written; null when there is no file or it is not a picture.</summary>
    private static SKBitmap? ReadCached(string path, out DateTime written)
    {
        written = default;
        try
        {
            if (!File.Exists(path))
                return null;
            written = File.GetLastWriteTimeUtc(path);
            return ImageFiles.Decode(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void Store(string key, string path, SKBitmap icon)
    {
        byte[] png;
        using (var image = SKImage.FromBitmap(icon))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            png = data.ToArray();

        try
        {
            Directory.CreateDirectory(folder);
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, png);
            File.Move(temporary, path, overwrite: true);
            // The age is counted on the injected clock, so the stamp comes from it too.
            File.SetLastWriteTimeUtc(path, time.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lock (_gate)
            {
                _memory[key] = png;
                if (_folderFailed)
                    return;
                _folderFailed = true;
            }

            UrDeckLog.Warn($"The icon cache folder {folder} cannot be written ({ex.Message}); site icons are kept in memory for this run.");
        }
    }

    private void Announce(string key, string message)
    {
        lock (_gate)
        {
            if (!_announced.Add(key))
                return;
        }

        UrDeckLog.Info(message);
    }
}
