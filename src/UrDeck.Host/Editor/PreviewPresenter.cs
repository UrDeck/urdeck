// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Deck;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Themes;

namespace UrDeck.Host.Editor;

/// <summary>
/// The editor's preview of a page: a deck session of its own (see <see cref="ISettingsHost.CreatePreview"/>) shown on one
/// <see cref="SKXamlCanvas"/>. The session lays the page out for the deck's surface; the canvas paints it whole (background,
/// cards, dock, indicator; <see cref="DeckFramePainter"/>) scaled to its own size, so the page's layout and text are the deck's.
/// The canvas is a software one, so the background, the pattern and the shadows are painted once into a picture and the
/// background and moving borders are shown still; a frame paints the cards. It repaints when a slot asks (a widget's own
/// animation runs at the frame clock's rate) and not at all while it is not shown (another section, or the window
/// minimised): then the session holds no page.
/// </summary>
internal sealed class PreviewPresenter : IDeckPresenter, IPagerView, IDisposable
{

    private readonly ISettingsHost _host;
    private readonly DispatcherQueue _queue;
    private readonly FrameClock _frames;
    private readonly PreviewDeck _deck;
    private readonly DeckFramePainter _painter = new();
    private readonly List<PageSlots> _pages = [];
    private PageSlots? _dock;
    private DeckBackground? _background;
    private string? _backgroundKey;
    private bool _shown;
    private bool _layoutQueued;
    private bool _disposed;
    private long _paints;

    public PreviewPresenter(ISettingsHost host, DispatcherQueue queue, string? pageId)
    {
        _host = host;
        _queue = queue;
        Canvas = new SKXamlCanvas { IsHitTestVisible = false };
        Canvas.PaintSurface += OnPaintSurface;
        _frames = new FrameClock(queue);
        _deck = host.CreatePreview(this, _frames, pageId);
        _deck.Session.ConfigReloaded += () => Reloaded?.Invoke();
    }

    /// <summary>The page, painted; sized by the owner.</summary>
    public SKXamlCanvas Canvas { get; }

    public DeckSession Session => _deck.Session;

    public Engine.Data.PreviewReadings Readings => _deck.Readings;

    /// <summary>Raised on the UI thread after the session built a page (the overlays move to its layout).</summary>
    public event Action? LayoutChanged;

    /// <summary>Raised after the session took a configuration reload, before it rebuilds the page.</summary>
    public event Action? Reloaded;

    /// <summary>How often the canvas was painted, for the log of the cost measurement.</summary>
    public long Paints => _paints;

    public DeckSurface Surface => _host.DeckSurface;

    public IPagerView Pages => this;

    /// <summary>Shows or stops the preview: a preview not shown holds no page and draws nothing.</summary>
    public void SetShown(bool shown)
    {
        if (_disposed || shown == _shown)
            return;
        _shown = shown;
        if (shown)
        {
            Session.SetShown(true);
            Session.RequestPage(force: true);
        }
        else
        {
            Session.SetShown(false);
            Session.ReleasePage();
            _background?.Dispose();
            _background = null;
            _backgroundKey = null;
            _painter.Dispose();
        }
    }

    /// <summary>Shows the page with the id at once (a tab of the editor).</summary>
    public void ShowPage(string pageId) => Session.JumpToPage(pageId);

    /// <summary>Whether the page drawn now is the page with the id (false while a jump is being built).</summary>
    public bool Shows(string pageId) => Session.Pager.Current?.PageId == pageId && Session.Navigator.Id == pageId;

    /// <summary>Makes sure the page with the id is drawn: jumps to it, or builds it again when the navigator is there and the page is not.</summary>
    public void EnsurePage(string pageId)
    {
        if (Shows(pageId))
            return;
        if (Session.Navigator.Id != pageId)
            Session.JumpToPage(pageId);
        else if (_shown)
            Session.ScheduleRebuild(force: true);
    }

    public void ShowBackground(BackgroundPlan plan, LoadedTheme theme, DeckSurface surface)
    {
        string key = $"{_host.BackgroundKey(plan)}|{surface}";
        if (key == _backgroundKey)
            return;
        _backgroundKey = key;
        _background?.Dispose();
        _background = null;
        try
        {
            _background = _host.BuildBackground(plan, surface);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UrDeckLog.Warn($"Preview: the background could not be built, the page is drawn on the theme's colour: {ex.Message}");
        }

        Canvas.Invalidate();
    }

    // ---------------- IPagerView: the pager says what is alive; the canvas paints it ----------------

    public void PageAdded(PageSlots page, double offset)
    {
        _pages.Add(page);
        Attach(page);
        QueueLayout();
    }

    public void PageRemoved(PageSlots page)
    {
        _pages.Remove(page);
        Detach(page);
        QueueLayout();
    }

    public void DockAdded(PageSlots dock)
    {
        _dock = dock;
        Attach(dock);
        QueueLayout();
    }

    public void IndicatorAdded(IndicatorSetup indicator) => Canvas.Invalidate();

    public void Cleared()
    {
        foreach (var page in _pages)
            Detach(page);
        _pages.Clear();
        if (_dock != null)
            Detach(_dock);
        _dock = null;
        QueueLayout();
    }

    public void SetOffset(PageSlots page, double offset) => Canvas.Invalidate();

    // The preview jumps from page to page; a settle (which only a swipe or ShowPage asks for) ends at once.
    public void Slide(IReadOnlyList<PageMove> moves, TimeSpan duration, Action settled) => _queue.TryEnqueue(() => settled());

    public void IndicatorFollow(double position)
    {
    }

    public void IndicatorSettle(double position) => Canvas.Invalidate();

    public int IndicatorHitTest(System.Drawing.PointF local, float cellPx) => -1;

    private void Attach(PageSlots page)
    {
        foreach (var card in page.Cards)
        {
            card.Slot.PaintRequested += Canvas.Invalidate;
            card.Slot.Show();
        }

        Canvas.Invalidate();
    }

    private void Detach(PageSlots page)
    {
        foreach (var card in page.Cards)
        {
            card.Slot.PaintRequested -= Canvas.Invalidate;
            card.Slot.Hide();
        }

        Canvas.Invalidate();
    }

    private void QueueLayout()
    {
        if (_layoutQueued)
            return;
        _layoutQueued = true;
        _queue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _layoutQueued = false;
            if (!_disposed)
                LayoutChanged?.Invoke();
        });
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var surface = Session.Pager.Setup is { } setup ? new DeckSurface(setup.Screen, setup.Scale) : Surface;
        double surfacePx = surface.Size.Width * surface.Scale;
        if (!_shown || surfacePx <= 0)
        {
            canvas.Clear(SKColors.Transparent);
            return;
        }

        _paints++;
        _painter.PaintCached(canvas, new SKSizeI(e.Info.Width, e.Info.Height), Session, _background);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Canvas.PaintSurface -= OnPaintSurface;
        Cleared();
        _deck.Dispose();
        _frames.Shutdown();
        _background?.Dispose();
        _background = null;
        _painter.Dispose();
    }
}
