// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Input;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Motion;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using MotionLevel = UrDeck.Engine.Motion.MotionLevel;

namespace UrDeck.Engine.Deck;

/// <summary>What the current layout is, for a presenter that hit-tests or draws over it (surface units).</summary>
/// <param name="Surface">The surface the layout was made for.</param>
/// <param name="Chrome">The grid, the indicator's band and the dock's band.</param>
/// <param name="Page">The cards of the page shown.</param>
/// <param name="Dock">The dock's cards; empty without a dock.</param>
public sealed record DeckLayout(DeckSurface Surface, ChromeLayoutResult Chrome, IReadOnlyList<DeckCard> Page, IReadOnlyList<DeckCard> Dock);

/// <summary>
/// What a deck shows, decided without knowing how it is drawn: it reacts to configuration, theme and plugin reloads,
/// resolves the theme and the background plan, decides the motion level, keeps the current page and the pager's state,
/// lays out the page for the presenter's surface, owns the widget slots of the pages in use, and routes changed readings to
/// the slots that declared them. A presenter (<see cref="IDeckPresenter"/>) shows it. Several sessions may live in one
/// process on the same thread: nothing here is static. Deck thread only, except where a member says otherwise.
/// </summary>
public sealed class DeckSession : IDisposable
{
    private readonly DeckSessionInputs _inputs;
    private readonly IDeckPresenter _presenter;
    private readonly IDeckScheduler _scheduler;
    private readonly PageNavigator _navigator;
    private readonly PagerState _pager;
    private readonly MotionGate _gate;
    private readonly object _changedGate = new();
    private HashSet<string> _changedReadings = new(StringComparer.OrdinalIgnoreCase);
    private bool _changedQueued;
    private bool _windowsAnimations;
    private MotionDecision _motion;
    private bool _motionDecided;
    private BackgroundSettings _backgroundSettings;
    private LoadedTheme _loadedTheme;
    private Theme? _theme;
    private Theme? _dockTheme;
    private ResolvedCardStyle? _cardStyle;
    private ResolvedCardStyle? _dockCardStyle;
    private LayoutKey _lastLayout;
    private bool _pageHeld = true;
    private bool _rebuildPending;
    private bool _disposed;

    /// <summary>What a built page depends on: the surface size, its scale, the active theme and the dock's size.</summary>
    private readonly record struct LayoutKey(
        System.Drawing.Size Screen, double Scale, LoadedTheme? Theme, IndicatorMode Mode, int PageCount, int DockCount, bool AnimateBorders);

    /// <summary>
    /// Starts a session: the theme is loaded and the motion decided (both logged), and the session listens for
    /// configuration, plugin and reading changes. Nothing is built until <see cref="RequestPage"/>.
    /// </summary>
    public DeckSession(DeckSessionInputs inputs, IDeckPresenter presenter)
    {
        _inputs = inputs;
        _presenter = presenter;
        _scheduler = inputs.Scheduler;
        _backgroundSettings = BackgroundSettings.Parse(Config.Background);
        _navigator = new PageNavigator(PageKeys(), inputs.StartPage ?? StartPageResolver.Resolve(Config, log: true));
        _pager = new PagerState(_navigator, presenter.Pages, _scheduler);
        _pager.PageChanged += index => PageChanged?.Invoke(index);
        _loadedTheme = LoadTheme();
        _windowsAnimations = inputs.WindowsAnimations;
        _gate = new MotionGate(MotionLevel.Off);
        UpdateMotion();

        inputs.Config.ConfigChanged += OnConfigChanged;
        inputs.Widgets.PluginsChanged += OnPluginsChanged;
        inputs.Readings.ReadingsChanged += OnReadingsChanged;
    }

    /// <summary>
    /// Raised on the deck thread after a configuration reload was applied to the session (theme, background settings,
    /// motion, pages) and before the page is rebuilt for it.
    /// </summary>
    public event Action? ConfigReloaded;

    /// <summary>A page change settled on the page at the index (a swipe, a dot, <see cref="ShowPage(int)"/>).</summary>
    public event Action<int>? PageChanged;

    private UrDeckConfig Config => _inputs.Config.Config;

    /// <summary>The theme the configuration names, as loaded at the last reload.</summary>
    public LoadedTheme Theme => _loadedTheme;

    /// <summary>The configuration's background, as read at the last reload.</summary>
    public BackgroundSettings BackgroundSettings => _backgroundSettings;

    /// <summary>The motion level now (the configuration, Windows' setting, the theme's ceiling).</summary>
    public MotionDecision Motion => _motion;

    /// <summary>Whether continuous animations (moving borders) may run: full motion, the display on, the deck shown.</summary>
    public MotionGate Gate => _gate;

    /// <summary>Which page is shown.</summary>
    public PageNavigator Navigator => _navigator;

    /// <summary>The live pages, the dock and the swipe.</summary>
    public PagerState Pager => _pager;

    /// <summary>True while the deck has no page: hidden, or shown and waiting for <see cref="RequestPage"/>.</summary>
    public bool IsPageHeld => _pageHeld;

    /// <summary>Whether input samples would be followed now (see <see cref="Process"/>).</summary>
    public bool AcceptsInput => _pager.AcceptsInput;

    /// <summary>The current layout; null while nothing is built.</summary>
    public DeckLayout? Layout
    {
        get
        {
            if (_pager.Setup is not { } setup || _pager.Current is not { } page)
                return null;
            return new DeckLayout(new DeckSurface(setup.Screen, setup.Scale), setup.Chrome, page.Cards, _pager.Dock?.Cards ?? []);
        }
    }

    private List<PageKey> PageKeys() => Config.Pages.Select(p => new PageKey(p.Id, p.Name)).ToList();

    private LoadedTheme LoadTheme()
    {
        var loaded = _inputs.LoadTheme(Config.Theme);
        UrDeckLog.Info($"Theme '{loaded.Name}' selected");
        return loaded;
    }

    private void OnConfigChanged(UrDeckConfig _) => _scheduler.Post(() =>
    {
        if (_disposed)
            return;
        // Themes are re-read on every config reload, so editing a theme and saving the config applies it.
        _loadedTheme = LoadTheme();
        _backgroundSettings = BackgroundSettings.Parse(Config.Background);
        UpdateMotion();
        _navigator.Reload(PageKeys());
        ConfigReloaded?.Invoke();
        ScheduleRebuild(force: true);
    });

    private void OnPluginsChanged() => _scheduler.Post(() =>
    {
        if (_disposed)
            return;
        // Drop slots holding old plugin instances now (not at the next idle rebuild) so the old plugin contexts can be
        // collected.
        _pager.Release();
        ScheduleRebuild(force: true);
    });

    /// <summary>Windows' "animation effects" setting changed.</summary>
    public void SetWindowsAnimations(bool on)
    {
        _windowsAnimations = on;
        UpdateMotion();
    }

    /// <summary>The display was turned off or on: moving borders stop and start with it.</summary>
    public void SetDisplayOn(bool on) => _gate.SetDisplayOn(on);

    /// <summary>
    /// The deck was shown or hidden. A hidden deck holds its page back (a rebuild does nothing) until
    /// <see cref="RequestPage"/>; <see cref="ReleasePage"/> drops what it has.
    /// </summary>
    public void SetShown(bool shown)
    {
        if (!shown)
            _pageHeld = true;
        _gate.SetDeckShown(shown);
    }

    /// <summary>The presenter's surface is ready (or changed): build the page for it, soon, coalesced with other requests.</summary>
    public void RequestPage(bool force = false)
    {
        _pageHeld = false;
        ScheduleRebuild(force);
    }

    /// <summary>Drops the page of a hidden deck: its widgets stop and their providers follow after the linger period.</summary>
    public void ReleasePage()
    {
        _pager.Release();
        DisposeThemes();
        _lastLayout = default;
    }

    /// <summary>Feeds one pointer sample (surface units). A session whose presenter has no input never gets any.</summary>
    public void Process(PointerSample sample) => _pager.Process(sample);

    /// <summary>Slides to the page at <paramref name="index"/> with the normal settle; false when it is outside the pages or shown.</summary>
    public bool ShowPage(int index) => _pager.ShowPage(index);

    /// <summary>Slides to the page with the id <paramref name="pageId"/>; false when there is none or it is shown.</summary>
    public bool ShowPage(string pageId)
    {
        int index = _navigator.IndexOf(pageId);
        return index >= 0 && _pager.ShowPage(index);
    }

    /// <summary>
    /// Shows the page with the id <paramref name="pageId"/> at once, without the settle (the preview, which jumps from tab to
    /// tab); false when there is none or it is shown.
    /// </summary>
    public bool JumpToPage(string pageId)
    {
        int index = _navigator.IndexOf(pageId);
        if (index < 0 || index == _navigator.Index)
            return false;
        _navigator.Settle(index);
        ScheduleRebuild(force: true);
        PageChanged?.Invoke(index);
        return true;
    }

    /// <summary>
    /// Decides the motion level again from the configuration, Windows' setting and the theme's ceiling. Logs only when the
    /// effective level changes; a change that matters (the level the borders obey, or the one the background obeys) asks for a
    /// rebuild, which rebuilds the page only when its animated borders change and the background only when its key does.
    /// </summary>
    private void UpdateMotion()
    {
        var before = _motion;
        var decision = MotionPolicy.Decide(Config.Motion, _windowsAnimations, _loadedTheme.Definition.Motion?.Level);
        bool first = !_motionDecided;
        _motion = decision;
        _motionDecided = true;
        if (first || decision.Effective != before.Effective)
            UrDeckLog.Info($"Motion: {decision}");
        _gate.SetLevel(decision.Effective);
        if (!first && (decision.Effective != before.Effective || decision.User != before.User))
            ScheduleRebuild();
    }

    private UrDeck.Sdk.MotionLevel WidgetMotion() => MotionPolicy.ToWidget(_motion.Effective);

    private bool ThemeAnimatesBorders => _loadedTheme.Definition.Card?.BorderAnimate is { Mode: "spin" or "pulse" };

    /// <summary>Coalesces layout rebuilds (resize, scale and placement changes arrive in bursts).</summary>
    public void ScheduleRebuild(bool force = false)
    {
        if (force)
            _lastLayout = default;
        if (_rebuildPending)
            return;
        _rebuildPending = true;
        _scheduler.Post(() =>
        {
            _rebuildPending = false;
            if (!_disposed)
                RebuildLayout();
        }, DeckPriority.Low);
    }

    private void RebuildLayout()
    {
        // A hidden deck has no page, and a deck that was just shown gets one when its presenter asks for it.
        if (_pageHeld)
            return;

        var surface = _presenter.Surface;
        var screen = surface.Size;
        double scale = surface.Scale;

        // Before the layout comparison: an unrelated reload keeps the running background, but a changed one must apply
        // even when the page itself is as it was.
        if (surface.IsUsable)
            _presenter.ShowBackground(BackgroundPlan.Resolve(_backgroundSettings, _loadedTheme, _motion.User), _loadedTheme, surface);

        var config = Config;
        var mode = config.Pager.GetIndicatorMode();
        var key = new LayoutKey(screen, scale, _loadedTheme, mode, config.Pages.Count, config.Dock.Count,
            _motion.Effective == MotionLevel.Full && ThemeAnimatesBorders);
        if (key == _lastLayout)
            return;
        _lastLayout = key;

        // Slots first, so nothing still paints with the old theme's typefaces when they are released.
        _pager.Release();
        DisposeThemes();

        if (config.Pages.Count == 0 || !surface.IsUsable)
            return;

        // Layout is computed in surface units; each surface then renders at the display's physical resolution, so the
        // theme is resolved against the physical size of one grid cell.
        float cellPx = (float)(screen.Width * scale / 4);
        var theme = _theme = ThemeResolver.Resolve(_loadedTheme, cellPx);
        var cardStyle = _cardStyle = ResolvedCardStyle.Resolve(_loadedTheme, cellPx);
        // Whether there is a dock is the configuration's decision alone, so the layout does not depend on the plugins.
        var chrome = ChromeLayout.Compute(screen, mode, config.Pages.Count, _loadedTheme.Definition.Indicator!.BandHeight!.Value,
            config.Dock.Count > 0 ? _loadedTheme.Definition.Dock!.Height!.Value : 0);
        var press = PressStyle.Resolve(_loadedTheme);
        // Below the full level a theme's moving border is drawn still, with the card, exactly as without borderAnimate.
        var borderMotion = key.AnimateBorders ? _gate : null;
        double gap = _loadedTheme.Definition.Card!.Gap!.Value;
        var pageBuild = new PageBuild(theme, cardStyle, press, scale, borderMotion, WidgetMotion);
        UrDeckLog.Info($"Pages: {config.Pages.Count}, showing '{_navigator.Name}'; indicator {chrome.Mode}, {chrome.Rows} grid rows");

        // A dock slot is a small grid cell: a second theme resolved for the slot's size makes its card a miniature.
        Func<PageSlots?> buildDock = () => null;
        int slot = DockLayout.SlotSide(chrome.Dock);
        if (slot > 0)
        {
            var dockTheme = _dockTheme = ThemeResolver.Resolve(_loadedTheme, (float)(slot * scale), compact: true);
            var dockCardStyle = _dockCardStyle = ResolvedCardStyle.Resolve(_loadedTheme, (float)(slot * scale));
            var dockBuild = pageBuild with { Theme = dockTheme, CardStyle = dockCardStyle };
            buildDock = () =>
            {
                var entries = DockLayout.Select(config.Dock, _inputs.Widgets.GetDescriptor);
                var page = new PageConfig { Name = "Dock", Widgets = entries.Select(e => e.Config).ToList() };
                var slots = DockLayout.Compute(chrome.Dock, entries, gap);
                return new PageSlots(page, slots, CardOrder.Dock(slots.Count), dockBuild, _inputs.Widgets, _inputs.Readings, _scheduler, _inputs.Frames, chrome.Dock);
            };
        }

        _pager.Rebuild(new PagerSetup(
            screen,
            scale,
            chrome,
            IndicatorStyle.Resolve(_loadedTheme, cellPx),
            index =>
            {
                var page = config.Pages[index];
                var layout = new GridLayoutManager(screen.Width, screen.Height, gap, chrome.Grid.Height)
                    .RenderWidgetLayout(page.Widgets, screen);
                return new PageSlots(page, layout, CardOrder.Assign(layout), pageBuild, _inputs.Widgets, _inputs.Readings, _scheduler, _inputs.Frames);
            },
            buildDock));
    }

    private void DisposeThemes()
    {
        _theme?.Dispose();
        _theme = null;
        _dockTheme?.Dispose();
        _dockTheme = null;
        _cardStyle?.Dispose();
        _cardStyle = null;
        _dockCardStyle?.Dispose();
        _dockCardStyle = null;
    }

    /// <summary>Runs on a sampling thread: collects the ids and asks the deck thread, once, to look at them.</summary>
    private void OnReadingsChanged(IReadOnlyCollection<string> ids)
    {
        lock (_changedGate)
        {
            _changedReadings.UnionWith(ids);
            if (_changedQueued)
                return;
            _changedQueued = true;
        }

        _scheduler.Post(RepaintChangedReadings);
    }

    private void RepaintChangedReadings()
    {
        HashSet<string> ids;
        lock (_changedGate)
        {
            ids = _changedReadings;
            _changedReadings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _changedQueued = false;
        }

        if (_disposed)
            return;

        // Only the slots that declared a changed reading are asked, each once.
        var asked = new HashSet<WidgetSlot>();
        foreach (var page in _pager.LivePages)
        {
            foreach (string id in ids)
            {
                foreach (var slot in page.SlotsFor(id))
                {
                    if (asked.Add(slot))
                        slot.OnReadingsChanged();
                }
            }
        }
    }

    /// <summary>
    /// Paints every card of the live pages (each at its offset at <paramref name="elapsed"/>, see
    /// <see cref="PagerState.OffsetAt"/>) and then the dock into one canvas in physical pixels, in z-order. Cards only: the
    /// background, the underlay, moving borders and the indicator are the presenter's. For a presenter that draws the deck
    /// on one surface; the window keeps one surface per card.
    /// </summary>
    public void PaintCards(SKCanvas canvas, TimeSpan elapsed)
    {
        if (_pager.Setup is not { } setup)
            return;
        foreach (var page in new[] { _pager.Current, _pager.Neighbor, _pager.Dock })
        {
            if (page == null)
                continue;
            double offset = page == _pager.Dock ? 0 : _pager.OffsetAt(page, elapsed);
            foreach (var card in page.Cards)
                PaintCard(canvas, card, offset, setup.Scale);
        }
    }

    private static void PaintCard(SKCanvas canvas, DeckCard card, double offset, double scale)
    {
        var item = card.Item;
        var size = new System.Drawing.Size(
            Math.Max(1, (int)Math.Round(item.Size.Width * scale)), Math.Max(1, (int)Math.Round(item.Size.Height * scale)));
        int save = canvas.Save();
        canvas.Translate((float)((item.Position.X + offset) * scale), (float)(item.Position.Y * scale));
        canvas.ClipRect(new SKRect(0, 0, size.Width, size.Height));
        card.Slot.Paint(canvas, size);
        canvas.RestoreToCount(save);
    }

    /// <summary>Releases every page and the themes and stops listening; the session does nothing afterwards.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _inputs.Config.ConfigChanged -= OnConfigChanged;
        _inputs.Widgets.PluginsChanged -= OnPluginsChanged;
        _inputs.Readings.ReadingsChanged -= OnReadingsChanged;
        _pager.Release();
        DisposeThemes();
    }
}
