// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using WinRT.Interop;

namespace UrDeck.Host;

/// <summary>
/// The borderless window covering the deck's display. Where it is, and whether it is shown at all, is decided by a
/// <see cref="PlacementController"/>: this class tells it what happened and does what it asks. Its content is an ordered stack of layers composited by the GPU:
/// a background layer (the theme's background colour, and over it the configured shader or picture, see
/// <see cref="BackgroundLayer"/>), and a canvas holding one surface per widget. Later changes add layers (hosted views
/// such as a web view) to the same stack.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The background layer and the power watcher are disposed when the window closes (OnClosed).")]
public sealed class MainWindow : Window, ISettingsHost
{
    private readonly ConfigStore _configStore;
    private readonly WidgetPluginLoader _plugins;
    private readonly ThemeStore _themes;
    private readonly ShaderStore _shaders;
    private readonly string _appDirectory;
    private readonly nint _hwnd;
    private readonly WindowFocus? _focus;
    private readonly DisplayPowerWatcher? _power;
    private readonly BackgroundLayer _backgroundLayer;
    private BackgroundSettings _backgroundSettings;
    private readonly Grid _root = new();
    private readonly Grid _background = new();
    private readonly Canvas _surface = new();
    private readonly Canvas _overlay = new();
    private readonly PageNavigator _navigator;
    private readonly PagerController _pager;
    private readonly object _changedGate = new();
    private HashSet<string> _changedReadings = new(StringComparer.OrdinalIgnoreCase);
    private bool _changedQueued;
    private readonly FrameClock _frameClock;
    private readonly Action _saveDisplay;
    private readonly PlacementController _placement;
    private readonly DispatcherQueueTimer _placementTimer;
    private bool _pageHeld = true;
    private bool _rebuildPending;
    private LoadedTheme _loadedTheme;
    private Theme? _theme;
    private Theme? _dockTheme;
    private ResolvedCardStyle? _cardStyle;
    private ResolvedCardStyle? _dockCardStyle;
    private LayoutKey _lastLayout;

    /// <summary>What a built page depends on: the window size, the display scaling, the active theme and the dock's size.</summary>
    private readonly record struct LayoutKey(
        System.Drawing.Size Screen, double Scale, LoadedTheme? Theme, IndicatorMode Mode, int PageCount, int DockCount);

    /// <summary>The placement state changed (UI thread); the name is the remembered display's, if there is one.</summary>
    internal event Action<PlacementState, string?>? PlacementChanged;

    /// <inheritdoc />
    event Action? ISettingsHost.Changed
    {
        add => SettingsChanged += value;
        remove => SettingsChanged -= value;
    }

    private event Action? SettingsChanged;

    /// <summary><paramref name="saveDisplay"/> writes the configuration after the remembered display was set or updated.</summary>
    internal MainWindow(ConfigStore configStore, WidgetPluginLoader plugins, ThemeStore themes, ShaderStore shaders, string appDirectory, Action saveDisplay)
    {
        _configStore = configStore;
        _plugins = plugins;
        _themes = themes;
        _shaders = shaders;
        _appDirectory = appDirectory;
        _backgroundLayer = new BackgroundLayer(_background);
        _backgroundSettings = BackgroundSettings.Parse(_configStore.Config.Background);
        _saveDisplay = saveDisplay;
        _frameClock = new FrameClock(DispatcherQueue);
        _navigator = new PageNavigator(PageNames(), _configStore.Config.ActivePage);
        _pager = new PagerController(_root, _surface, _overlay, _frameClock, _navigator);
        _loadedTheme = LoadTheme();
        _hwnd = WindowNative.GetWindowHandle(this);
        _plugins.Readings.ApplySettings(_configStore.Config.Providers);
        _plugins.Readings.ReadingsChanged += OnReadingsChanged;

        Title = "UrDeck";
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);
        string icon = Path.Combine(AppContext.BaseDirectory, "urdeck.ico");
        if (File.Exists(icon))
            AppWindow.SetIcon(icon);

        ApplyBackground();
        _root.Children.Add(_background);
        _root.Children.Add(_surface);
        _root.Children.Add(_overlay);
        if (!WindowFocus.IsEnabled)
        {
            var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
            escape.Invoked += (_, e) =>
            {
                e.Handled = true;
                Close();
            };
            _root.KeyboardAccelerators.Add(escape);
        }
        Content = _root;

        _focus = WindowFocus.Apply(_hwnd);
        WindowCorners.Square(_hwnd);
        _power = DisplayPowerWatcher.Start(_hwnd);
        if (_power != null)
            _power.DisplayStateChanged += _backgroundLayer.SetDisplayOn;

        // One timer drives placement: the controller says after each call when it wants the next one.
        var seam = new PlacementSeam(this);
        _placement = new PlacementController(seam, seam, () => _configStore.Config);
        _placementTimer = DispatcherQueue.CreateTimer();
        _placementTimer.IsRepeating = false;
        _placementTimer.Tick += (_, _) => Arm(_placement.Tick(Now));

        // The scale is only known once the content is in the tree; moving onto a monitor with another scale changes
        // the DPI and the frame. Placement decides what to do about either.
        _root.Loaded += (_, _) => _root.XamlRoot.Changed += (_, _) => Signal(PlacementSignal.WindowChanged);
        _root.SizeChanged += (_, _) => Signal(PlacementSignal.WindowChanged);

        _configStore.ConfigChanged += _ => DispatcherQueue.TryEnqueue(() =>
        {
            // Themes are re-read on every config reload, so editing a theme and saving the config applies it.
            _loadedTheme = LoadTheme();
            _backgroundSettings = BackgroundSettings.Parse(_configStore.Config.Background);
            _navigator.Reload(PageNames());
            _plugins.Readings.ApplySettings(_configStore.Config.Providers);
            ApplyBackground();
            Signal(PlacementSignal.ConfigReloaded);
            ScheduleRebuild(force: true);
        });
        _plugins.PluginsChanged += () => DispatcherQueue.TryEnqueue(() =>
        {
            // Drop views holding old plugin instances now (not at the next idle rebuild) so the old plugin contexts
            // can be collected.
            _pager.Release();
            ScheduleRebuild(force: true);
        });

        // Monitors can come back from sleep/hot-plug in any order and settle their DPI late; placement waits for
        // them and checks its result instead of trusting the first event.
        // Subscribed from the thread pool on purpose: SystemEvents hands events to the synchronization context of the
        // thread that subscribed, and the UI thread's one (set in Program.Main) never delivered them.
        Task.Run(() =>
        {
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            UrDeckLog.Info("Listening for display and power changes");
        });
        Closed += (_, _) => OnClosed();
    }

    private List<string> PageNames() => _configStore.Config.Pages.Select(p => p.Name).ToList();

    private LoadedTheme LoadTheme()
    {
        var loaded = _themes.Load(_configStore.Config.Theme);
        UrDeckLog.Info($"Theme '{loaded.Name}' selected");
        return loaded;
    }

    private void ApplyBackground()
    {
        var c = SKColor.Parse(_loadedTheme.Definition.Colors!.Background);
        var brush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, c.Red, c.Green, c.Blue));
        _root.Background = brush;
        _background.Background = brush;
    }

    private double Scale => _root.XamlRoot?.RasterizationScale ?? 1;

    // These run on the SystemEvents thread, which swallows what a handler throws: say what arrived and what failed.
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        UrDeckLog.Info("Windows: display settings changed");
        Post(PlacementSignal.DisplaysChanged);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        UrDeckLog.Info($"Windows: power mode {e.Mode}");
        if (e.Mode == PowerModes.Resume)
            Post(PlacementSignal.Resumed);
    }

    private void Post(PlacementSignal signal)
    {
        try
        {
            if (!DispatcherQueue.TryEnqueue(() => Signal(signal)))
                UrDeckLog.Warn($"Could not queue the {signal} signal: the UI thread is not accepting work");
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Could not queue the {signal} signal", ex);
        }
    }

    /// <summary>Starts placement: the window is shown if its display is connected, and when it is.</summary>
    internal void Start() => Signal(PlacementSignal.Startup);

    private static TimeSpan Now => TimeSpan.FromMilliseconds(Environment.TickCount64);

    private void Signal(PlacementSignal signal)
    {
        Arm(_placement.Signal(signal, Now));
        // The window's own size and scaling echo every placement; the picker has nothing to learn from them.
        if (signal != PlacementSignal.WindowChanged)
            SettingsChanged?.Invoke();
    }

    DisplayList ISettingsHost.GetDisplayList() => DisplayRows.Build(_configStore.Config, MonitorPlacement.GetDisplays());

    void ISettingsHost.ChooseDisplay(DisplayInfo display)
    {
        if (!DisplayChoice.Choose(_configStore.Config, display))
        {
            UrDeckLog.Info($"Settings: {display.Name} is already the chosen display");
            return;
        }

        UrDeckLog.Info($"Settings: display {display.Name} chosen; it is remembered without a selector");
        _saveDisplay();
        Signal(PlacementSignal.ConfigReloaded);
    }

    void ISettingsHost.ChooseAutomatically()
    {
        if (!DisplayChoice.ChooseAutomatically(_configStore.Config))
        {
            UrDeckLog.Info("Settings: the display is already chosen by the selectors");
            return;
        }

        UrDeckLog.Info("Settings: the remembered display was cleared; the selectors choose again");
        _saveDisplay();
        Signal(PlacementSignal.ConfigReloaded);
    }

    private void Arm(TimeSpan? delay)
    {
        _placementTimer.Stop();
        if (delay is not { } wait)
            return;
        _placementTimer.Interval = wait > TimeSpan.FromMilliseconds(1) ? wait : TimeSpan.FromMilliseconds(1);
        _placementTimer.Start();
    }

    private void ShowDeck()
    {
        // Never with activation: the display may return while a game has the keyboard. The development window is
        // activated, or it would get no Escape.
        if (WindowFocus.IsEnabled)
            AppWindow.Show(false);
        else
            Activate();
        _backgroundLayer.SetVisible(true);
    }

    private void HideDeck()
    {
        _pageHeld = true;
        _backgroundLayer.SetVisible(false);
        AppWindow.Hide();
    }

    /// <summary>Drops the page of a hidden deck: its widgets stop and their providers follow after the linger period.</summary>
    private void ReleasePage()
    {
        _pager.Release();
        _backgroundLayer.Release();
        DisposeThemes();
        _lastLayout = default;
    }

    /// <summary>What the placement controller sees of this window, and what it asks of it.</summary>
    private sealed class PlacementSeam(MainWindow window) : IDisplayEnvironment, IPlacementOwner
    {
        public IReadOnlyList<DisplayInfo> GetDisplays() => MonitorPlacement.GetDisplays();

        public System.Drawing.Rectangle ContentBounds => MonitorPlacement.GetContentBounds(window._hwnd);

        public double WindowScaling => MonitorPlacement.GetWindowScaling(window._hwnd);

        public System.Drawing.SizeF LayoutSize => new((float)window._root.ActualWidth, (float)window._root.ActualHeight);

        public void Cover(System.Drawing.Rectangle bounds, bool nudge) => MonitorPlacement.Cover(window._hwnd, bounds, nudge);

        public void Show() => window.ShowDeck();

        public void Hide() => window.HideDeck();

        public void RebuildPage(bool force)
        {
            window._pageHeld = false;
            window.ScheduleRebuild(force);
        }

        public void ReleasePage() => window.ReleasePage();

        public void SaveRememberedDisplay(RememberedDisplay display) => window._saveDisplay();

        public void StateChanged(PlacementState state, string? displayName)
        {
            window.PlacementChanged?.Invoke(state, displayName);
            window.SettingsChanged?.Invoke();
        }
    }

    /// <summary>Coalesces layout rebuilds (resize, scale and placement changes arrive in bursts).</summary>
    private void ScheduleRebuild(bool force = false)
    {
        if (force)
            _lastLayout = default;
        if (_rebuildPending)
            return;
        _rebuildPending = true;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _rebuildPending = false;
            RebuildLayout();
        });
    }

    private void RebuildLayout()
    {
        // A hidden deck has no page, and a deck that was just shown gets one when placement has settled and asks for it.
        if (_pageHeld)
            return;

        var screen = new System.Drawing.Size((int)_root.ActualWidth, (int)_root.ActualHeight);
        double scale = Scale;

        // Before the layout comparison: an unrelated reload keeps the running background, but a changed one must apply
        // even when the page itself is as it was.
        if (screen.Width >= 16 && screen.Height >= 16)
            ApplyBackgroundLayer(screen, scale);

        var config = _configStore.Config;
        var mode = config.Pager.GetIndicatorMode();
        var key = new LayoutKey(screen, scale, _loadedTheme, mode, config.Pages.Count, config.Dock.Count);
        if (key == _lastLayout)
            return;
        _lastLayout = key;

        // Views first, so nothing still paints with the old theme's typefaces when they are released.
        _pager.Release();
        DisposeThemes();

        if (config.Pages.Count == 0 || screen.Width < 16 || screen.Height < 16)
            return;

        // Layout is computed in DIPs; each surface then renders at the monitor's physical resolution, so the theme
        // is resolved against the physical size of one grid cell.
        float cellPx = (float)(screen.Width * scale / 4);
        _theme = ThemeResolver.Resolve(_loadedTheme, cellPx);
        var cardStyle = _cardStyle = ResolvedCardStyle.Resolve(_loadedTheme, cellPx);
        // Whether there is a dock is the configuration's decision alone, so the layout does not depend on the plugins.
        var chrome = ChromeLayout.Compute(screen, mode, config.Pages.Count, _loadedTheme.Definition.Indicator!.BandHeight!.Value,
            config.Dock.Count > 0 ? _loadedTheme.Definition.Dock!.Height!.Value : 0);
        var theme = _theme;
        var press = PressStyle.Resolve(_loadedTheme);
        double gap = _loadedTheme.Definition.Card!.Gap!.Value;
        UrDeckLog.Info($"Pages: {config.Pages.Count}, showing '{_navigator.Name}'; indicator {chrome.Mode}, {chrome.Rows} grid rows");

        // A dock slot is a small grid cell: a second theme resolved for the slot's size makes its card a miniature.
        Func<PageHost?> buildDock = () => null;
        int slot = DockLayout.SlotSide(chrome.Dock);
        if (slot > 0)
        {
            var dockTheme = _dockTheme = ThemeResolver.Resolve(_loadedTheme, (float)(slot * scale));
            var dockCardStyle = _dockCardStyle = ResolvedCardStyle.Resolve(_loadedTheme, (float)(slot * scale));
            buildDock = () =>
            {
                var entries = DockLayout.Select(config.Dock, _plugins.GetDescriptor);
                var page = new PageConfig { Name = "Dock", Widgets = entries.Select(e => e.Config).ToList() };
                var slots = DockLayout.Compute(chrome.Dock, entries, gap);
                return new PageHost(page, slots, _plugins, dockTheme, dockCardStyle, CardOrder.Dock(slots.Count), press, _frameClock);
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
                return new PageHost(page, layout, _plugins, theme, cardStyle, CardOrder.Assign(layout), press, _frameClock);
            },
            buildDock));
    }

    private void ApplyBackgroundLayer(System.Drawing.Size screen, double scale)
    {
        var settings = _backgroundSettings;
        var theme = _loadedTheme;
        var plan = BackgroundPlan.Resolve(settings, theme);
        _backgroundLayer.Apply(
            plan.RequestKey(_shaders, _appDirectory),
            first => plan.Build(_shaders, _appDirectory, first),
            screen,
            scale,
            SKColor.Parse(theme.Definition.Colors!.Background));
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

    /// <summary>Runs on a sampling thread: collects the ids and asks the UI thread, once, to look at them.</summary>
    private void OnReadingsChanged(IReadOnlyCollection<string> ids)
    {
        lock (_changedGate)
        {
            _changedReadings.UnionWith(ids);
            if (_changedQueued)
                return;
            _changedQueued = true;
        }

        DispatcherQueue.TryEnqueue(RepaintChangedReadings);
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

        // Only the views that declared a changed reading are asked, each once.
        var asked = new HashSet<WidgetView>();
        foreach (var page in _pager.LivePages)
        {
            foreach (string id in ids)
            {
                foreach (var view in page.ViewsFor(id))
                {
                    if (asked.Add(view))
                        view.OnReadingsChanged();
                }
            }
        }
    }

    private void OnClosed()
    {
        // SystemEvents are static; unsubscribe or the window leaks.
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _plugins.Readings.ReadingsChanged -= OnReadingsChanged;
        _placementTimer.Stop();
        _pager.Shutdown();
        _frameClock.Shutdown();
        _power?.Dispose();
        _backgroundLayer.Dispose();
        DisposeThemes();
    }
}
