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
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Layout;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Rendering;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using WinRT.Interop;

namespace UrDeck.Host;

/// <summary>
/// The borderless window covering the target monitor. Its content is an ordered stack of layers composited by the GPU:
/// a background layer filled with the theme's background colour, and a canvas holding one surface per widget.
/// Later changes add layers (an image or video background, hosted views such as a web view) to the same stack.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly ConfigStore _configStore;
    private readonly WidgetPluginLoader _plugins;
    private readonly ThemeStore _themes;
    private readonly nint _hwnd;
    private readonly WindowFocus? _focus;
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
    private readonly List<DispatcherQueueTimer> _pendingRetargets = new();
    private MonitorInfo _target;
    private bool _rebuildPending;
    private bool _nudged;
    private LoadedTheme _loadedTheme;
    private Theme? _theme;
    private Theme? _dockTheme;
    private LayoutKey _lastLayout;

    /// <summary>What a built page depends on: the window size, the display scaling, the active theme and the dock's size.</summary>
    private readonly record struct LayoutKey(
        System.Drawing.Size Screen, double Scale, LoadedTheme? Theme, IndicatorMode Mode, int PageCount, int DockCount);

    internal MainWindow(ConfigStore configStore, WidgetPluginLoader plugins, ThemeStore themes, MonitorInfo target)
    {
        _configStore = configStore;
        _plugins = plugins;
        _themes = themes;
        _target = target;
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
        MonitorPlacement.Cover(_hwnd, _target.Bounds);
        _root.Loaded += (_, _) =>
        {
            // The scale is only known once the content is in the tree; moving onto a monitor with another scale
            // changes the DPI and the frame, so cover again and rebuild.
            _root.XamlRoot.Changed += (_, _) =>
            {
                UrDeckLog.Info($"XAML root changed, scale {_root.XamlRoot.RasterizationScale:0.##}");
                MonitorPlacement.Cover(_hwnd, _target.Bounds);
                ScheduleRebuild();
            };
            LogPlacement();
            ScheduleRebuild();
        };
        _root.SizeChanged += (_, _) => ScheduleRebuild();

        _configStore.ConfigChanged += _ => DispatcherQueue.TryEnqueue(() =>
        {
            // Themes are re-read on every config reload, so editing a theme and saving the config applies it.
            _loadedTheme = LoadTheme();
            _navigator.Reload(PageNames());
            _plugins.Readings.ApplySettings(_configStore.Config.Providers);
            ApplyBackground();
            Retarget("config changed");
            ScheduleRebuild(force: true);
        });
        _plugins.PluginsChanged += () => DispatcherQueue.TryEnqueue(() =>
        {
            // Drop views holding old plugin instances now (not at the next idle rebuild) so the old plugin contexts
            // can be collected.
            _pager.Release();
            ScheduleRebuild(force: true);
        });

        // Monitors can come back from sleep/hot-plug in any order and settle their DPI late, so
        // re-evaluate the target a few times after each change instead of trusting the first event.
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
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

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => ScheduleRetargets("display settings changed", rebuild: false));

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            DispatcherQueue.TryEnqueue(() => ScheduleRetargets("resumed from sleep", rebuild: true));
    }

    private void ScheduleRetargets(string reason, bool rebuild)
    {
        foreach (var t in _pendingRetargets)
            t.Stop();
        _pendingRetargets.Clear();

        Retarget(reason);
        // After a resume the graphics device may have been reset: recreating every surface is the simple recovery.
        if (rebuild)
            ScheduleRebuild(force: true);

        foreach (var delay in new[] { TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(5) })
        {
            var timer = DispatcherQueue.CreateTimer();
            timer.Interval = delay;
            timer.IsRepeating = false;
            timer.Tick += (_, _) =>
            {
                _pendingRetargets.Remove(timer);
                Retarget($"{reason} (+{delay.TotalSeconds:0.#}s)");
                if (rebuild)
                    ScheduleRebuild(force: true);
            };
            _pendingRetargets.Add(timer);
            timer.Start();
        }
    }

    /// <summary>Re-selects the target monitor from config and moves the window if it isn't covering it exactly.</summary>
    private void Retarget(string reason)
    {
        var monitors = MonitorPlacement.GetMonitors();
        if (monitors.Count == 0)
            return;

        var target = MonitorPlacement.Select(_configStore.Config, monitors);
        var actual = MonitorPlacement.GetContentBounds(_hwnd);
        if (target == _target && actual == target.Bounds)
            return;

        UrDeckLog.Info($"Retarget ({reason}): {target}; content area was at {actual.X},{actual.Y} {actual.Width}x{actual.Height}");
        _target = target;
        MonitorPlacement.Cover(_hwnd, target.Bounds);
        LogPlacement();
    }

    private void LogPlacement()
    {
        var actual = MonitorPlacement.GetContentBounds(_hwnd);
        var bounds = _target.Bounds;
        UrDeckLog.Info($"Content area at {actual.X},{actual.Y} {actual.Width}x{actual.Height} physical " +
                      $"(target {bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}), " +
                      $"{_root.ActualWidth:0.#}x{_root.ActualHeight:0.#} DIPs, scale {Scale:0.##}");
        if (actual != bounds)
            UrDeckLog.Warn("Window content area does not match the target monitor.");
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
        var screen = new System.Drawing.Size((int)_root.ActualWidth, (int)_root.ActualHeight);
        double scale = Scale;

        // After a scale change (a monitor unplugged and plugged back in) XAML can keep the content size of the old
        // scale while the window is right. Resize the window by a pixel once to make it lay out again, and build then.
        var content = MonitorPlacement.GetContentBounds(_hwnd);
        bool stale = !content.IsEmpty &&
            (Math.Abs(_root.ActualWidth * scale - content.Width) > 2 || Math.Abs(_root.ActualHeight * scale - content.Height) > 2);
        if (stale && !_nudged)
        {
            _nudged = true;
            UrDeckLog.Info($"Layout {_root.ActualWidth:0.#}x{_root.ActualHeight:0.#} DIPs at scale {scale:0.##} does not match the content area {content.Width}x{content.Height}; resizing the window to refresh it");
            MonitorPlacement.Cover(_hwnd, _target.Bounds, nudge: true);
            ScheduleRebuild();
            return;
        }
        _nudged = stale;

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
            buildDock = () =>
            {
                var entries = DockLayout.Select(config.Dock, _plugins.GetDescriptor);
                var page = new PageConfig { Name = "Dock", Widgets = entries.Select(e => e.Config).ToList() };
                return new PageHost(page, DockLayout.Compute(chrome.Dock, entries, gap), _plugins, dockTheme, press, _frameClock);
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
                return new PageHost(page, layout, _plugins, theme, press, _frameClock);
            },
            buildDock));
    }

    private void DisposeThemes()
    {
        _theme?.Dispose();
        _theme = null;
        _dockTheme?.Dispose();
        _dockTheme = null;
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
        foreach (var t in _pendingRetargets)
            t.Stop();
        _pager.Shutdown();
        _frameClock.Shutdown();
        DisposeThemes();
    }
}
