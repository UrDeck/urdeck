// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Deck;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Themes;

namespace UrDeck.Host.Editor;

/// <summary>A frame source that never ticks: a picture is one frame.</summary>
internal sealed class NoFrames : IFrameSource
{
    public void Add(IFrameClient client)
    {
    }

    public void Remove(IFrameClient client)
    {
    }
}

/// <summary>
/// Shows a deck session once, into a bitmap: no window, no canvas. The surface is the deck's size at a scale that makes it
/// <see cref="ThemeThumbnails.PixelWidth"/> pixels wide, so the same layout is drawn small (the theme is resolved against the
/// physical size of a cell, so it looks as it does on the deck).
/// </summary>
internal sealed class ThumbnailPresenter(DeckSurface surface, ISettingsHost host) : IDeckPresenter, IPagerView, IDisposable
{
    private readonly List<PageSlots> _pages = [];
    private PageSlots? _dock;
    private DeckBackground? _background;
    private string? _backgroundKey;
    private bool _built;

    public DeckSurface Surface => surface;

    public IPagerView Pages => this;

    /// <summary>Completes when the session has made its page (its cards exist).</summary>
    public TaskCompletionSource Built { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DeckBackground? Background => _background;

    public void ShowBackground(BackgroundPlan plan, LoadedTheme theme, DeckSurface target)
    {
        string key = $"{host.BackgroundKey(plan)}|{target}";
        if (key == _backgroundKey)
            return;
        _backgroundKey = key;
        _background?.Dispose();
        _background = null;
        try
        {
            _background = host.BuildBackground(plan, target);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UrDeckLog.Warn($"Theme picture: the background could not be built, the page is drawn on the theme's colour: {ex.Message}");
        }
    }

    public void PageAdded(PageSlots page, double offset)
    {
        _pages.Add(page);
        Attach(page);
    }

    public void PageRemoved(PageSlots page)
    {
        _pages.Remove(page);
        Detach(page);
    }

    public void DockAdded(PageSlots dock)
    {
        _dock = dock;
        Attach(dock);
    }

    public void IndicatorAdded(IndicatorSetup indicator) => MarkBuilt();

    public void Cleared()
    {
        foreach (var page in _pages)
            Detach(page);
        _pages.Clear();
        if (_dock != null)
            Detach(_dock);
        _dock = null;
    }

    public void SetOffset(PageSlots page, double offset)
    {
    }

    public void Slide(IReadOnlyList<PageMove> moves, TimeSpan duration, Action settled) => settled();

    public void IndicatorFollow(double position)
    {
    }

    public void IndicatorSettle(double position)
    {
    }

    public int IndicatorHitTest(System.Drawing.PointF local, float cellPx) => -1;

    public void Dispose()
    {
        Cleared();
        _background?.Dispose();
        _background = null;
    }

    private void Attach(PageSlots page)
    {
        foreach (var card in page.Cards)
            card.Slot.Show();
        MarkBuilt();
    }

    private static void Detach(PageSlots page)
    {
        foreach (var card in page.Cards)
            card.Slot.Hide();
    }

    private void MarkBuilt()
    {
        if (_built)
            return;
        _built = true;
        Built.TrySetResult();
    }
}

/// <summary>
/// A picture of the user's current page in each installed theme (editor-theme-background D2): a deck session of the page with the
/// theme, the providers' sample readings and a still background, painted once at <see cref="PixelWidth"/> pixels wide and kept
/// as a PNG in <c>cache/theme-previews/</c> under a key of everything it depends on. One at a time, so the editor stays usable; the
/// session is disposed after each. <see cref="Ready"/> says when a picture exists.
/// </summary>
internal sealed class ThemeThumbnails : IDisposable
{
    /// <summary>The width of a picture in pixels (twice the tile's, so it stays sharp at 200 percent).</summary>
    public const int PixelWidth = 440;

    private static readonly TimeSpan BuildTimeout = TimeSpan.FromSeconds(10);

    private readonly ISettingsHost _host;
    private readonly DispatcherQueue _queue;
    private readonly ThumbnailCache _cache;
    private readonly Queue<string> _pending = new();
    private readonly HashSet<string> _queued = new(StringComparer.Ordinal);
    private bool _busy;
    private bool _disposed;
    private string _contentKey = "";

    public ThemeThumbnails(ISettingsHost host, DispatcherQueue queue)
    {
        _host = host;
        _queue = queue;
        _cache = new ThumbnailCache(ThumbnailCache.FolderIn(host.DataFolder));
    }

    /// <summary>A picture is ready: the theme's name and the PNG's file. On the UI thread.</summary>
    public event Action<string, string>? Ready;

    /// <summary>
    /// The picture's file when it is ready (cached for the page, background and theme as they are now); otherwise null and the
    /// picture is made, soon, and <see cref="Ready"/> is raised.
    /// </summary>
    public string? Request(string theme, string? pageId)
    {
        if (_disposed)
            return null;
        string content = ThemePreviewConfig.ContentKey(_host.Document.Root, pageId) + "|" + pageId;
        if (content != _contentKey)
        {
            // The page or the background changed: what waits is stale.
            _contentKey = content;
            _pending.Clear();
            _queued.Clear();
        }

        if (_cache.Find(theme, KeyOf(theme)) is { } cached)
            return cached;

        if (_queued.Add(theme))
            _pending.Enqueue(theme);
        Pump(pageId);
        return null;
    }

    public void Dispose()
    {
        _disposed = true;
        _pending.Clear();
        _queued.Clear();
    }

    private string KeyOf(string theme) =>
        ThumbnailCache.Key(_contentKey, ThemeStamp(theme), SurfaceOf().Size.ToString(), Program.Version);

    private DeckSurface SurfaceOf()
    {
        var deck = _host.DeckSurface;
        return new DeckSurface(deck.Size, PixelWidth / (double)Math.Max(1, deck.Size.Width));
    }

    /// <summary>The user's theme folder's newest file time (an edit of the theme changes the picture); built-in themes change with the version.</summary>
    private string ThemeStamp(string theme)
    {
        try
        {
            string folder = Path.Combine(_host.Themes.ThemesDirectory, theme);
            if (Directory.Exists(folder))
                return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Select(f => File.GetLastWriteTimeUtc(f).Ticks).DefaultIfEmpty().Max().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UrDeckLog.Warn($"Theme picture: the folder of theme '{theme}' could not be read: {ex.Message}");
        }

        return "built-in";
    }

    private void Pump(string? pageId)
    {
        if (_busy || _pending.Count == 0 || _disposed)
            return;
        _busy = true;
        _queue.TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            try
            {
                while (_pending.Count > 0 && !_disposed)
                {
                    string theme = _pending.Dequeue();
                    _queued.Remove(theme);
                    string content = _contentKey;
                    string? file = await RenderAsync(theme, pageId, content);
                    if (file != null && !_disposed && content == _contentKey)
                        Ready?.Invoke(theme, file);
                }
            }
            finally
            {
                _busy = false;
            }
        });
    }

    private async Task<string?> RenderAsync(string theme, string? pageId, string content)
    {
        string key = KeyOf(theme);
        ThumbnailPresenter? presenter = null;
        PreviewDeck? deck = null;
        try
        {
            var config = ThemePreviewConfig.For(_host.Document.Root, theme);
            presenter = new ThumbnailPresenter(SurfaceOf(), _host);
            deck = _host.CreatePreview(presenter, new NoFrames(), pageId, config);
            deck.Session.SetShown(true);
            deck.Session.RequestPage(force: true);
            await presenter.Built.Task.WaitAsync(BuildTimeout);
            // The sample readings arrive on the next turns of the queue.
            await Task.Delay(150);
            if (_disposed || content != _contentKey)
                return null;

            var surface = presenter.Surface;
            int width = PixelWidth;
            int height = Math.Max(1, (int)Math.Round(surface.Size.Height * surface.Scale));
            using var bitmap = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(bitmap))
            using (var painter = new DeckFramePainter())
                painter.PaintCached(canvas, new SKSizeI(width, height), deck.Session, presenter.Background);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            return _cache.Store(theme, key, data.ToArray());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UrDeckLog.Warn($"Theme picture: '{theme}' could not be drawn: {ex.Message}");
            return null;
        }
        finally
        {
            deck?.Dispose();
            presenter?.Dispose();
        }
    }
}
