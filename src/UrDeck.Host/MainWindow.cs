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
using UrDeck.Engine.Deck;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Secrets;
using UrDeck.Engine.Settings;
using UrDeck.Engine.Themes;
using WinRT.Interop;

namespace UrDeck.Host;

/// <summary>
/// The borderless window covering the deck's display. Where it is, and whether it is shown at all, is decided by a
/// <see cref="PlacementController"/>: this class tells it what happened and does what it asks. Its content is an ordered stack of layers composited by the GPU:
/// a background layer (the theme's background colour, and over it the configured shader or picture, see
/// <see cref="BackgroundLayer"/>), and a canvas holding one surface per widget. Later changes add layers (hosted views
/// such as a web view) to the same stack. What the deck shows is decided by its <see cref="DeckSession"/>; this window is
/// the session's presenter (<see cref="IDeckPresenter"/>): it reports its surface and whether it is shown, and draws what
/// the session says.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The session, the background layer and the power watcher are disposed when the window closes (OnClosed).")]
public sealed class MainWindow : Window, ISettingsHost, IDeckPresenter
{
    private readonly ConfigStore _configStore;
    private readonly WidgetPluginLoader _plugins;
    private readonly ThemeStore _themes;
    private readonly ShaderStore _shaders;
    private readonly ProviderSettingsEditor _providerEditor;
    private readonly ProviderSignIn _providerSignIn;
    private readonly string _dataFolder;
    private readonly Engine.Startup.DataFolder _data;
    private Engine.Editor.ReadingDescriptionCache? _readingCache;
    private readonly nint _hwnd;
    private readonly WindowFocus? _focus;
    private readonly DisplayPowerWatcher? _power;
    private readonly WindowsAnimationWatcher? _animations;
    private readonly BackgroundLayer _backgroundLayer;
    private readonly Grid _root = new();
    private readonly Grid _background = new();
    private readonly PatternLayer _pattern = new();
    private readonly Canvas _surface = new();
    private readonly Canvas _overlay = new();
    private readonly PagerController _pager;
    private readonly FrameClock _frameClock;
    private readonly DeckSession _session;
    private readonly Action _saveDisplay;
    private readonly PlacementController _placement;
    private readonly DispatcherQueueTimer _placementTimer;
    private PlacementState _placementState = PlacementState.Starting;
    private string? _placementDisplay;
    private DeckSurface _lastSurface = new(new System.Drawing.Size(1100, 3840), 1);

    /// <summary>Whether this copy starts with Windows, and how to change it: the application's (the tray entry's).</summary>
    internal Func<bool>? AutostartOn { get; set; }

    internal Action<bool>? SetAutostart { get; set; }

    private event Action<string?>? DeckPageChangedEvent;

    private string? _wantedPage;
    private DispatcherQueueTimer? _followTimer;

    /// <summary>The placement state changed (UI thread); the name is the remembered display's, if there is one.</summary>
    internal event Action<PlacementState, string?>? PlacementChanged;

    /// <inheritdoc />
    event Action? ISettingsHost.Changed
    {
        add => SettingsChanged += value;
        remove => SettingsChanged -= value;
    }

    private event Action? SettingsChanged;

    /// <summary><paramref name="saveDisplay"/> remembers the display after it was set or updated (a host edit of the configuration document).</summary>
    internal MainWindow(ConfigStore configStore, WidgetPluginLoader plugins, ThemeStore themes, ShaderStore shaders, Engine.Startup.DataFolder data, Action saveDisplay)
    {
        _configStore = configStore;
        _plugins = plugins;
        _themes = themes;
        _shaders = shaders;
        _data = data;
        _dataFolder = data.Root;
        _backgroundLayer = new BackgroundLayer(_background);
        _saveDisplay = saveDisplay;
        _frameClock = new FrameClock(DispatcherQueue);
        _pager = new PagerController(_root, _surface, _overlay, _frameClock);
        _session = new DeckSession(
            new DeckSessionInputs(_configStore, _themes.Load, _plugins, _plugins.Readings, new DispatcherDeckScheduler(DispatcherQueue), _frameClock,
                WindowsAnimationWatcher.Read()),
            this);
        _pager.Attach(_session.Pager);
        _session.PageChanged += _ => DeckPageChangedEvent?.Invoke(_session.Navigator.Id);
        _hwnd = WindowNative.GetWindowHandle(this);
        _providerEditor = new ProviderSettingsEditor(_configStore, _plugins.Providers, _plugins.Secrets, _plugins.Readings);
        _providerSignIn = new ProviderSignIn(_configStore, _plugins.Providers, _plugins.Secrets, _plugins.Readings);
        _plugins.Readings.ApplySettings(_configStore.Config.Providers);

        Title = "UrDeck";
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);
        // The icon ships with the executable: the application folder, not the data folder.
        string icon = Path.Combine(AppContext.BaseDirectory, "urdeck.ico");
        if (File.Exists(icon))
            AppWindow.SetIcon(icon);

        ApplyBackground();
        _root.Children.Add(_background);
        _root.Children.Add(_pattern);
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
        {
            _power.DisplayStateChanged += _backgroundLayer.SetDisplayOn;
            _power.DisplayStateChanged += _session.SetDisplayOn;
        }

        _animations = WindowsAnimationWatcher.Start(_hwnd);
        if (_animations != null)
        {
            _animations.Changed += _session.SetWindowsAnimations;
        }

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

        // The session has taken the reload (theme, background, motion, pages) and rebuilds the page after this.
        _session.ConfigReloaded += () =>
        {
            SecretStores.MoveFromConfigAndSave(_configStore, _plugins.Providers, _plugins.Secrets);
            _plugins.Readings.ApplySettings(_configStore.Config.Providers);
            ApplyBackground();
            Signal(PlacementSignal.ConfigReloaded);
        };

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

    private void ApplyBackground()
    {
        var c = SKColor.Parse(_session.Theme.Definition.Colors!.Background);
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

    ConfigDocument ISettingsHost.Document => _configStore.Document;

    string ISettingsHost.ConfigPath => _data.ConfigPath;

    string ISettingsHost.LogPath => UrDeckLog.LogPath;

    // The deck's surface while it is shown; the last one it was shown on otherwise (a hidden window keeps a size of its own).
    DeckSurface ISettingsHost.DeckSurface => _session.IsPageHeld ? _lastSurface : CurrentSurface();

    PreviewDeck ISettingsHost.CreatePreview(IDeckPresenter presenter, IFrameSource frames, string? pageId, UrDeckConfig? config)
    {
        var readings = new Engine.Data.PreviewReadings(_plugins.Readings, () => _plugins.Providers.Descriptors);
        Action reload = readings.ReloadSamples;
        // Before the session subscribes: the sample hub takes the new plugins' samples before the session rebuilds its page.
        _plugins.PluginsChanged += reload;
        int start = (config ?? _configStore.Config).Pages.FindIndex(p => p.Id == pageId);
        IDeckConfigSource source = config == null ? _configStore : new StaticConfigSource(config);
        var scheduler = new DispatcherDeckScheduler(DispatcherQueue);
        var session = new DeckSession(
            new DeckSessionInputs(source, _themes.Load, _plugins.WithReadings(readings), readings, scheduler, frames,
                WindowsAnimationWatcher.Read(), StartPage: Math.Max(0, start)),
            presenter);
        return new PreviewDeck(session, readings, () =>
        {
            _plugins.PluginsChanged -= reload;
            // Its timers' handlers lead back to the session: detached, the closed editor's preview can go.
            scheduler.Dispose();
        });
    }

    DeckBackground? ISettingsHost.BuildBackground(BackgroundPlan plan, DeckSurface surface) =>
        plan.BuildPrepared(_shaders, _dataFolder, Math.Max(1, (int)Math.Round(surface.Size.Width * surface.Scale)), Math.Max(1, (int)Math.Round(surface.Size.Height * surface.Scale)));

    Engine.Themes.ThemeStore ISettingsHost.Themes => _themes;

    ShaderStore ISettingsHost.Shaders => _shaders;

    string ISettingsHost.DataFolder => _dataFolder;

    BackgroundStatus ISettingsHost.BackgroundStatus() =>
        BackgroundPlan.Resolve(_session.BackgroundSettings, _session.Theme, _session.Motion.User).Status(_shaders, _dataFolder);

    string? ISettingsHost.BackgroundKey(BackgroundPlan plan) => plan.RequestKey(_shaders, _dataFolder);

    string? ISettingsHost.DeckPageId => _session.Navigator.Id;

    void ISettingsHost.ShowPageOnDeck(string pageId)
    {
        _wantedPage = pageId;
        FollowWantedPage();
    }

    // A page asked for while the deck slides (or before a reload has the page) is asked for again shortly: the last one wins.
    private void FollowWantedPage()
    {
        if (_wantedPage is not { } pageId || _session.Navigator.Id == pageId)
        {
            _wantedPage = null;
            return;
        }

        if (_session.ShowPage(pageId) || _session.Navigator.IndexOf(pageId) < 0)
        {
            _wantedPage = null;
            return;
        }

        _followTimer ??= CreateFollowTimer();
        _followTimer.Stop();
        _followTimer.Start();
    }

    private DispatcherQueueTimer CreateFollowTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.IsRepeating = false;
        timer.Interval = PagerState.SettleTime + TimeSpan.FromMilliseconds(50);
        timer.Tick += (_, _) => FollowWantedPage();
        return timer;
    }

    event Action<string?>? ISettingsHost.DeckPageChanged
    {
        add => DeckPageChangedEvent += value;
        remove => DeckPageChangedEvent -= value;
    }

    DeckState ISettingsHost.DeckState =>
        new(_placementState is not (PlacementState.Absent or PlacementState.Starting), _placementDisplay ?? _configStore.Config.Display?.Name);

    IReadOnlyList<WidgetTypeInfo> ISettingsHost.WidgetTypes =>
        _plugins.Registry.Descriptors
            .Select(d =>
            {
                var name = d.WidgetType.Assembly.GetName();
                string assembly = name.Name ?? "";
                return new WidgetTypeInfo(d, assembly, name.Version?.ToString(3) ?? "", assembly.StartsWith("UrDeck.Widgets.", StringComparison.Ordinal));
            })
            .OrderBy(t => t.Descriptor.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    Engine.Data.ReadingHub ISettingsHost.Readings => _plugins.Readings;

    IReadOnlyCollection<Engine.Data.ProviderDescriptor> ISettingsHost.ProviderDescriptors => _plugins.Providers.Descriptors;

    Engine.Editor.ReadingDescriptionCache ISettingsHost.ReadingCache => _readingCache ??= new Engine.Editor.ReadingDescriptionCache(_data.ReadingCachePath);

    Engine.Plugin.WidgetDescriptor? ISettingsHost.DescribeWidget(string typeId) => _plugins.GetDescriptor(typeId);

    Engine.Plugin.WidgetUnavailable? ISettingsHost.ClassifyWidget(string typeId) => _plugins.Classify(typeId);

    string? ISettingsHost.ReadingLabel(string readingId) => _plugins.Readings.Describe(readingId)?.Label;

    IReadOnlyList<string> ISettingsHost.ProvidersWaitingForSettings => _plugins.Readings.ProvidersWaitingForSettings;

    bool ISettingsHost.StartsWithWindows => AutostartOn?.Invoke() ?? false;

    void ISettingsHost.SetStartWithWindows(bool on) => SetAutostart?.Invoke(on);

    IReadOnlyList<ProviderForm> ISettingsHost.GetProviderForms() => _providerEditor.GetForms();

    ProviderSignIn ISettingsHost.SignIn => _providerSignIn;

    bool ISettingsHost.OpenAddress(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && new Engine.Launch.Launcher().Launch(uri.AbsoluteUri);

    EditorResult ISettingsHost.SaveProvider(string providerId, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> newSecrets) =>
        _providerEditor.Save(providerId, values, newSecrets);

    EditorResult ISettingsHost.RemoveProviderSecret(string providerId, string key) => _providerEditor.RemoveSecret(providerId, key);

    Task<EditorResult> ISettingsHost.TestProviderAsync(string providerId, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> newSecrets, CancellationToken cancellationToken) =>
        _providerEditor.TestAsync(providerId, values, newSecrets, cancellationToken);

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
        _session.SetShown(true);
    }

    private void HideDeck()
    {
        _backgroundLayer.SetVisible(false);
        _session.SetShown(false);
        AppWindow.Hide();
    }

    /// <summary>Drops the page of a hidden deck: its widgets stop and their providers follow after the linger period.</summary>
    private void ReleasePage()
    {
        _session.ReleasePage();
        _backgroundLayer.Release();
        _pattern.Release();
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

        public void RebuildPage(bool force) => window._session.RequestPage(force);

        public void ReleasePage() => window.ReleasePage();

        public void SaveRememberedDisplay(RememberedDisplay display) => window._saveDisplay();

        public void StateChanged(PlacementState state, string? displayName)
        {
            window._placementState = state;
            window._placementDisplay = displayName;
            window.PlacementChanged?.Invoke(state, displayName);
            window.SettingsChanged?.Invoke();
        }
    }

    DeckSurface IDeckPresenter.Surface
    {
        get
        {
            // Asked when the session builds a page, so only while the deck is shown: remember it for the editor's preview.
            var surface = CurrentSurface();
            if (surface.IsUsable)
                _lastSurface = surface;
            return surface;
        }
    }

    private DeckSurface CurrentSurface() => new(new System.Drawing.Size((int)_root.ActualWidth, (int)_root.ActualHeight), Scale);

    IPagerView IDeckPresenter.Pages => _pager;

    void IDeckPresenter.ShowBackground(BackgroundPlan plan, LoadedTheme theme, DeckSurface surface)
    {
        _backgroundLayer.Apply(
            plan.RequestKey(_shaders, _dataFolder),
            first => plan.Build(_shaders, _dataFolder, first),
            surface.Size,
            surface.Scale,
            SKColor.Parse(theme.Definition.Colors!.Background));
        _pattern.Apply(theme, surface.Size, surface.Scale);
    }
    private void OnClosed()
    {
        // SystemEvents are static; unsubscribe or the window leaks.
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _placementTimer.Stop();
        _followTimer?.Stop();
        _pager.Shutdown();
        _session.Dispose();
        _frameClock.Shutdown();
        _power?.Dispose();
        _animations?.Dispose();
        _backgroundLayer.Dispose();
        _pattern.Dispose();
    }
}
