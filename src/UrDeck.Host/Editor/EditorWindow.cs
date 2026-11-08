// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UrDeck.Engine.Config;
using UrDeck.Engine.Diagnostics;
using UrDeck.Engine.Display;
using UrDeck.Engine.Editor;

namespace UrDeck.Host.Editor;

/// <summary>
/// The editor (the settings window): an ordinary, resizable window titled "UrDeck" that takes focus and has a taskbar button
/// while it is open. A navigation pane holds its sections (Pages, Providers, Display, General) and, in its footer, the deck's
/// status. It talks to the host only through <see cref="ISettingsHost"/>; the application makes one per opening and drops it
/// when it closes, so nothing of it stays behind.
/// </summary>
internal sealed class EditorWindow : Window
{
    private const double WidthDip = 1280;
    private const double HeightDip = 860;
    private const double MinWidthDip = 900;
    private const double MinHeightDip = 600;

    private readonly ISettingsHost _host;
    private readonly PageEditing _editing;
    private readonly NavigationView _nav = new()
    {
        PaneDisplayMode = NavigationViewPaneDisplayMode.Auto,
        ExpandedModeThresholdWidth = 1000,
        CompactModeThresholdWidth = 0,
        IsSettingsVisible = false,
        IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
        OpenPaneLength = 240,
    };
    private readonly NavigationViewItem _pagesItem = Item("Pages", "");
    private readonly NavigationViewItem _providersItem = Item("Providers", "");
    private readonly NavigationViewItem _displayItem = Item("Display", "");
    private readonly NavigationViewItem _generalItem = Item("General", "");
    private readonly InfoBadge _badge = new();
    private readonly PagesSection _pages;
    private readonly ProvidersSection _providers;
    private readonly DisplaySection _display;
    private readonly GeneralSection _general;
    private readonly DeckStatusCard _status;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _badgeTimer;
    private bool _minimised;
    private bool _closed;

    public EditorWindow(ISettingsHost host, string iconPath)
    {
        _host = host;
        _editing = new PageEditing(host.Document, host.DescribeWidget, host.ReadingLabel);
        Title = "UrDeck";
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = true;
        presenter.IsMaximizable = true;
        AppWindow.SetPresenter(presenter);
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            SystemBackdrop = new MicaBackdrop();

        _pages = new PagesSection(host, _editing, DispatcherQueue);
        _providers = new ProvidersSection(host);
        _display = new DisplaySection(host, DispatcherQueue);
        _general = new GeneralSection(host, _editing);
        _status = new DeckStatusCard(host);

        _providersItem.InfoBadge = _badge;
        _nav.MenuItems.Add(_pagesItem);
        _nav.MenuItems.Add(_providersItem);
        _nav.MenuItems.Add(_displayItem);
        _nav.MenuItems.Add(_generalItem);
        _nav.PaneFooter = _status.Root;
        // Named handlers, detached when the window closes: the navigation view holds them from native code, and a handler
        // that leads back to this window kept every closed editor alive (seen in the open-and-close soak, 2026-11-07).
        _nav.SelectionChanged += OnNavSelectionChanged;
        // The status's lines need the open pane; with icons only it is its dot.
        _nav.DisplayModeChanged += OnNavDisplayModeChanged;
        _nav.PaneOpened += OnNavPaneChanged;
        _nav.PaneClosed += OnNavPaneChanged;
        _nav.Loaded += OnNavLoaded;

        var root = new Grid();
        if (SystemBackdrop == null)
            root.Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
        root.Children.Add(_nav);
        var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, e) =>
        {
            e.Handled = true;
            OnEscape();
        };
        root.KeyboardAccelerators.Add(escape);
        root.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        Content = root;

        _badgeTimer = DispatcherQueue.CreateTimer();
        _badgeTimer.IsRepeating = false;
        _badgeTimer.Interval = TimeSpan.FromSeconds(1);
        _badgeTimer.Tick += OnBadgeTick;

        host.Document.Changed += OnDocumentChanged;
        host.Document.Problem += OnProblem;
        host.Document.Written += OnWritten;
        host.Changed += OnHostChanged;
        AppWindow.Changed += OnAppWindowChanged;
        Closed += OnClosed;

        _nav.SelectedItem = _pagesItem;
        UpdateBadge();
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) => Show(args.SelectedItem as NavigationViewItem);

    private void OnNavDisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args) => UpdateStatusMode();

    private void OnNavPaneChanged(NavigationView sender, object args) => UpdateStatusMode();

    private void OnNavLoaded(object sender, RoutedEventArgs e) => UpdateStatusMode();

    private void UpdateStatusMode() =>
        _status.SetCompact(!_nav.IsPaneOpen);


    private static NavigationViewItem Item(string text, string glyph) =>
        new() { Content = text, Icon = new FontIcon { Glyph = glyph } };

    /// <summary>Places the window on a display away from the deck and shows it with focus.</summary>
    public void Open()
    {
        var display = SettingsWindowPlacement.Choose(_display.List, MonitorPlacement.GetCursor());
        Windows.Graphics.RectInt32? placed = null;
        if (display != null)
        {
            // Sized in DIPs of the display it opens on, at most 90 % of its work area; the window is placed in physical pixels.
            var area = DisplayArea.GetFromRect(new Windows.Graphics.RectInt32(display.Bounds.X, display.Bounds.Y, display.Bounds.Width, display.Bounds.Height),
                DisplayAreaFallback.Nearest).WorkArea;
            int width = Math.Min((int)Math.Round(WidthDip * display.Scaling), (int)(area.Width * 0.9));
            int height = Math.Min((int)Math.Round(HeightDip * display.Scaling), (int)(area.Height * 0.9));
            // The minimum first: setting it after the size put the window back to the minimum (seen on the panel, 2026-11-07).
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = (int)Math.Round(MinWidthDip * display.Scaling);
                presenter.PreferredMinimumHeight = (int)Math.Round(MinHeightDip * display.Scaling);
            }

            placed = new Windows.Graphics.RectInt32(area.X + ((area.Width - width) / 2), area.Y + ((area.Height - height) / 2), width, height);
            AppWindow.MoveAndResize(placed.Value);

            UrDeckLog.Info($"Settings window opened on {display.Name}: work area {area.Width}x{area.Height} at {area.X},{area.Y}, scaling {display.Scaling:0.##}, asked {width}x{height}");
        }

        ShowWindow();
        // The first activation of a resizable window gives it a size of its own; the chosen one is applied again after it.
        if (placed is { } rect && (AppWindow.Size.Width != rect.Width || AppWindow.Size.Height != rect.Height))
            AppWindow.MoveAndResize(rect);
        UrDeckLog.Info($"Settings window size after showing: {AppWindow.Size.Width}x{AppWindow.Size.Height} at {AppWindow.Position.X},{AppWindow.Position.Y}");
    }

    /// <summary>The window is already open: restore it if it was minimised, bring it to the front and give it focus.</summary>
    public void BringForward()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        ShowWindow();
    }

    private void ShowWindow()
    {
        Activate();
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (!SetForegroundWindow(hwnd))
            UrDeckLog.Info("Settings window: Windows did not give it the foreground; it is active but may be behind another window");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hwnd);

    private void Show(NavigationViewItem? item)
    {
        if (_closed)
            return;
        _nav.Content = item == _providersItem ? _providers.Root
            : item == _displayItem ? _display.Root
            : item == _generalItem ? _general.Root
            : _pages.Root;
        if (item == _generalItem)
            _general.Refresh();
        UpdateActive();
        UpdateBadge();
    }

    /// <summary>The preview draws only while Pages is shown and the window is not minimised.</summary>
    private void UpdateActive() => _pages.SetActive(!_closed && !_minimised && (ReferenceEquals(_nav.SelectedItem, _pagesItem) || _nav.SelectedItem == null));

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPresenterChange && !args.DidSizeChange)
            return;
        bool minimised = sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized };
        if (minimised == _minimised)
            return;
        _minimised = minimised;
        UpdateActive();
    }

    /// <summary>Escape closes the palette, or the window when no dialog, flyout or text entry has the key.</summary>
    private void OnEscape()
    {
        if (_pages.CloseTransient())
            return;
        var root = Content.XamlRoot;
        if (FocusManager.GetFocusedElement(root) is TextBox or PasswordBox or AutoSuggestBox or NumberBox)
            return;
        if (VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Count > 0)
            return;
        Close();
    }

    private void OnDocumentChanged(ConfigChange change)
    {
        _status.Refresh();
        _general.Refresh();
        _badgeTimer.Stop();
        _badgeTimer.Start();
    }

    private void OnProblem(ConfigProblem problem)
    {
        if (problem.Line == null)
            _status.WriteFailed(problem);
    }

    private void OnWritten() => _status.WriteSucceeded();

    private void OnHostChanged() => _status.Refresh();

    private void OnBadgeTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args) => UpdateBadge();

    private void UpdateBadge()
    {
        if (_closed)
            return;
        int count = _host.ProvidersWaitingForSettings.Count;
        _badge.Value = count;
        _badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_providersItem, count > 0 ? $"Providers, {count} need settings" : "Providers");
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        Closed -= OnClosed;
        _badgeTimer.Stop();
        // A timer holds its handler from native code: an attached one keeps the whole window alive after it closed.
        _badgeTimer.Tick -= OnBadgeTick;
        _host.Document.Changed -= OnDocumentChanged;
        _host.Document.Problem -= OnProblem;
        _host.Document.Written -= OnWritten;
        _host.Changed -= OnHostChanged;
        AppWindow.Changed -= OnAppWindowChanged;
        _pages.Close();
        _providers.Close();
        _display.Close();
        _nav.SelectionChanged -= OnNavSelectionChanged;
        _nav.DisplayModeChanged -= OnNavDisplayModeChanged;
        _nav.PaneOpened -= OnNavPaneChanged;
        _nav.PaneClosed -= OnNavPaneChanged;
        _nav.Loaded -= OnNavLoaded;
        _nav.Content = null;
        _nav.PaneFooter = null;
        _nav.MenuItems.Clear();
        try
        {
            _host.Document.Flush();
        }
        catch (Exception ex)
        {
            UrDeckLog.Warn($"Could not save the configuration: {ex.Message}");
        }

        UrDeckLog.Info("Settings window closed");
    }
}
